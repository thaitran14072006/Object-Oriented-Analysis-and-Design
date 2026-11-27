using System.Security.Claims;
using CarRental.Web.Areas.Identity.Data;
using CarRental.Web.Shared.Contracts;
using CarRental.Web.Shared.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;

namespace CarRental.Web.Areas.Identity.Services;

public sealed record LoginReply(string? Ticket = null, string? SessionToken = null, AdminProfile? Admin = null);

public sealed class AuthService(CarRentalDatabase database, OtpService otp, IPasswordHasher<string> passwords,
    TimeProvider time, IConfiguration configuration)
{
    private const string SignInError = "Không thể đăng nhập. Hãy kiểm tra thông tin hoặc thử lại sau.";
    public const string OtpError = "Mã OTP không đúng, đã hết hạn hoặc yêu cầu bị khóa tạm.";

    public async Task<LoginReply> LoginAsync(string? id, string? password, CancellationToken token)
    {
        id = id?.Trim();
        if (!SecurityRules.IsEmployeeId(id) || password is not { Length: > 0 and <= 128 })
            throw new BusinessException("Nhập mã nhân viên và mật khẩu hợp lệ.");

        var result = await database.WriteAsync<LoginReply?>(IdentityData.WriteLock, async work =>
        {
            var now = time.GetUtcNow().UtcDateTime;
            var account = await IdentityData.AccountAsync(work, id!);
            if (account is null || !account.Active || account.LockedUntil > now)
            {
                await IdentityData.AuditAsync(work, now, account?.Id, "DangNhap", "TaiKhoan", account?.Id, false);
                return null;
            }

            var verified = passwords.VerifyHashedPassword(account.Id, account.PasswordHash, password);
            if (verified == PasswordVerificationResult.Failed)
            {
                var attempts = (account.LockedUntil is null ? account.Failures : 0) + 1;
                var lockAccount = attempts >= SecurityRules.AttemptLimit;
                await work.ExecuteAsync("UPDATE dbo.TaiKhoan SET soLanSai=@fails,khoaDen=@lock WHERE maTaiKhoan=@id",
                    ("fails", lockAccount ? 0 : attempts), ("lock", lockAccount ? now + SecurityRules.Lock : null),
                    ("id", account.Id));
                await IdentityData.AuditAsync(work, now, account.Id, "DangNhap", "TaiKhoan", account.Id, false);
                return null;
            }

            if (verified == PasswordVerificationResult.SuccessRehashNeeded)
                await SetHashAsync(work, account.Id, passwords.HashPassword(account.Id, password));
            await ClearPasswordFailuresAsync(work, account.Id);
            return await BeginAsync(work, account, now, token);
        }, token);
        return result ?? throw new BusinessException(SignInError, 401);
    }

    public async Task<LoginReply> ExternalLoginAsync(string provider, string subject, CancellationToken token)
    {
        var result = await database.WriteAsync<LoginReply?>(IdentityData.WriteLock, async work =>
        {
            var now = time.GetUtcNow().UtcDateTime;
            var linked = await work.OneAsync("""
                SELECT maTaiKhoan FROM dbo.DanhTinhLienKet WHERE nhacungcap=@provider AND chuTheNgoai=@subject
                """, row => row.GetString(0), ("provider", provider), ("subject", subject));
            var account = linked is null ? null : await IdentityData.AccountAsync(work, linked);
            if (account is not { Active: true } || account.LockedUntil > now)
            {
                await IdentityData.AuditAsync(work, now, account?.Id, "DangNhapNgoai", "TaiKhoan", account?.Id, false);
                return null;
            }
            return await BeginAsync(work, account, now, token);
        }, token);
        return result ?? throw new BusinessException(SignInError, 401);
    }

    private async Task<LoginReply?> BeginAsync(SqlWork work, Account account, DateTime now, CancellationToken token)
    {
        var profile = await IdentityData.ProfileAsync(work, account);
        if (!profile.Permissions.Contains(AdminAccess.Admin))
        {
            await IdentityData.AuditAsync(work, now, account.Id, "DangNhap", "TaiKhoan", account.Id, false);
            return null;
        }
        if (!account.TwoFactor)
            return await CreateSessionAsync(work, profile, now);
        var channel = account.Channel!;
        return new LoginReply(Ticket: await otp.IssueAsync(work, account, SecurityRules.Login, channel,
            account.Destination(channel), now, token));
    }

    public async Task<LoginReply> VerifyLoginAsync(OtpProof proof, CancellationToken token)
    {
        var ticket = otp.Read(proof.Ticket, SecurityRules.Login);
        var result = await database.WriteAsync<LoginReply?>(IdentityData.WriteLock, async work =>
        {
            var account = await IdentityData.AccountAsync(work, ticket.Account);
            var now = time.GetUtcNow().UtcDateTime;
            if (account is not { Active: true, TwoFactor: true } || account.LockedUntil > now ||
                account.Channel != ticket.Channel || account.Destination(ticket.Channel) != ticket.Destination)
                return null;

            var profile = await IdentityData.ProfileAsync(work, account);
            if (!profile.Permissions.Contains(AdminAccess.Admin))
                return null;
            if (!await otp.CheckAsync(work, ticket, proof.Code, now))
            {
                await IdentityData.AuditAsync(work, now, account.Id, "DangNhapOTP", "TaiKhoan", account.Id, false);
                return null;
            }

            await OtpService.ConsumeAsync(work, ticket, now);
            await ClearPasswordFailuresAsync(work, account.Id);
            return await CreateSessionAsync(work, profile, now);
        }, token);
        return result ?? throw new BusinessException(OtpError, 401);
    }

    public async Task<string> RequestRecoveryAsync(string? identifier, CancellationToken token)
    {
        identifier = identifier?.Trim();
        if (!SecurityRules.IsEmployeeId(identifier) && !SecurityRules.IsEmail(identifier))
            throw new BusinessException("Nhập mã nhân viên hoặc email hợp lệ.");
        try
        {
            return await database.WriteAsync(IdentityData.WriteLock, async work =>
            {
                var account = await IdentityData.RecoveryAccountAsync(work, identifier!);
                if (account is not { Active: true })
                    return otp.DummyRecoveryTicket();

                var channel = account.Channel ?? "EMAIL";
                return await otp.IssueAsync(work, account, SecurityRules.Recovery, channel, account.Destination(channel),
                    time.GetUtcNow().UtcDateTime, token);
            }, token);
        }
        catch (BusinessException ex) when (ex.Status is 429 or 503)
        {
            return otp.DummyRecoveryTicket();
        }
    }

    public async Task<string> ResendAsync(string? encrypted, string purpose, CancellationToken token)
    {
        var ticket = otp.Read(encrypted, purpose);
        if (ticket.Id == 0 && purpose == SecurityRules.Recovery)
            return otp.DummyRecoveryTicket();

        try
        {
            return await database.WriteAsync(IdentityData.WriteLock, async work =>
            {
                var account = await IdentityData.AccountAsync(work, ticket.Account);
                var now = time.GetUtcNow().UtcDateTime;
                if (account is not { Active: true } || account.Destination(ticket.Channel) != ticket.Destination ||
                    (purpose == SecurityRules.Login && (account.LockedUntil > now || account.Channel != ticket.Channel)))
                    throw new BusinessException(OtpError);
                return await otp.IssueAsync(work, account, purpose, ticket.Channel, ticket.Destination, now, token, ticket.Id);
            }, token);
        }
        catch (BusinessException ex) when (purpose == SecurityRules.Recovery && ex.Status is 429 or 503)
        {
            return encrypted!;
        }
    }

    public async Task ResetAsync(OtpProof proof, string? password, string? confirmation, CancellationToken token)
    {
        SecurityRules.ValidatePassword(password, confirmation);
        var ticket = otp.Read(proof.Ticket, SecurityRules.Recovery);
        var changed = await database.WriteAsync(IdentityData.WriteLock, async work =>
        {
            var now = time.GetUtcNow().UtcDateTime;
            var account = ticket.Id == 0 ? null : await IdentityData.AccountAsync(work, ticket.Account);
            if (account is not { Active: true } || account.Destination(ticket.Channel) != ticket.Destination)
                return false;
            var valid = await otp.CheckAsync(work, ticket, proof.Code, now);
            await IdentityData.AuditAsync(work, now, account.Id, "KhoiPhucMatKhau", "TaiKhoan", account.Id, valid);
            if (!valid)
                return false;
            await SetHashAsync(work, account.Id, passwords.HashPassword(account.Id, password!));
            await ClearPasswordFailuresAsync(work, account.Id);
            await RevokeCredentialsAsync(work, account.Id, now);
            return true;
        }, token);
        if (!changed)
            throw new BusinessException(OtpError);
    }

    public static async Task RevokeCredentialsAsync(SqlWork work, string id, DateTime now)
    {
        await work.ExecuteAsync("UPDATE dbo.PhienDangNhap SET thuHoiLuc=@now WHERE maTaiKhoan=@id AND thuHoiLuc IS NULL",
            ("id", id), ("now", now));
        await work.ExecuteAsync("UPDATE dbo.MaXacThuc SET daDungLuc=@now WHERE maTaiKhoan=@id AND daDungLuc IS NULL",
            ("id", id), ("now", now));
    }

    public static Task SetHashAsync(SqlWork work, string id, string hash) =>
        work.ExecuteAsync("UPDATE dbo.TaiKhoan SET matKhauBam=@hash WHERE maTaiKhoan=@id", ("id", id), ("hash", hash));

    private static Task ClearPasswordFailuresAsync(SqlWork work, string id) =>
        work.ExecuteAsync("UPDATE dbo.TaiKhoan SET soLanSai=0,khoaDen=NULL WHERE maTaiKhoan=@id", ("id", id));

    private static async Task<LoginReply> CreateSessionAsync(SqlWork work, AdminProfile profile, DateTime now)
    {
        var token = SecurityRules.NewToken();
        await work.ExecuteAsync("""
            INSERT dbo.PhienDangNhap (maTaiKhoan,tokenBam,taoLuc,hoatDongCuoi) VALUES (@id,@hash,@now,@now)
            """, ("id", profile.Id), ("hash", SecurityRules.TokenHash(token)), ("now", now));
        await IdentityData.AuditAsync(work, now, profile.Id, "DangNhap", "TaiKhoan", profile.Id, true);
        return new LoginReply(SessionToken: token, Admin: profile);
    }

    public Task<AdminProfile?> SessionAsync(string id, string session, CancellationToken token) =>
        database.ReadAsync<AdminProfile?>(async work =>
        {
            var now = time.GetUtcNow().UtcDateTime;
            var account = await IdentityData.AccountAsync(work, id);
            if (account is not { Active: true })
                return null;
            var profile = await IdentityData.ProfileAsync(work, account);
            if (!profile.Permissions.Contains(AdminAccess.Admin))
                return null;
            var touched = await work.ExecuteAsync("""
                UPDATE dbo.PhienDangNhap
                SET hoatDongCuoi=CASE WHEN hoatDongCuoi<@now THEN @now ELSE hoatDongCuoi END
                WHERE maTaiKhoan=@id AND tokenBam=@hash AND thuHoiLuc IS NULL AND hoatDongCuoi>@cutoff
                """, ("now", now), ("cutoff", now - SecurityRules.Idle), ("id", id),
                ("hash", SecurityRules.TokenHash(session)));
            return touched == 1 ? profile : null;
        }, token);

    public Task LogoutAsync(string id, string session, CancellationToken token) =>
        database.WriteAsync(IdentityData.WriteLock, async work =>
        {
            var now = time.GetUtcNow().UtcDateTime;
            await work.ExecuteAsync("""
                UPDATE dbo.PhienDangNhap SET thuHoiLuc=@now WHERE maTaiKhoan=@id AND tokenBam=@hash AND thuHoiLuc IS NULL
                """, ("now", now), ("id", id), ("hash", SecurityRules.TokenHash(session)));
            await IdentityData.AuditAsync(work, now, id, "DangXuat", "TaiKhoan", id, true);
            return true;
        }, token);

    public static ClaimsPrincipal Principal(AdminProfile profile, string token)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, profile.Id), new(ClaimTypes.Name, profile.Name),
            new(AdminAccess.SessionClaim, token) };
        claims.AddRange(profile.Permissions.Select(permission => new Claim(AdminAccess.PermissionClaim, permission)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }

    public async Task BootstrapAsync(CancellationToken token)
    {
        var config = configuration.GetSection("BootstrapAdmin");
        if (config["Password"] is not { Length: > 0 } password)
            return;
        var id = config["Id"]?.Trim();
        var name = config["Name"]?.Trim();
        var email = config["Email"]?.Trim();
        var phone = config["Phone"]?.Trim();
        if (!SecurityRules.IsEmployeeId(id) || name is not { Length: > 0 and <= 100 } ||
            !SecurityRules.IsEmail(email) || !SecurityRules.IsPhone(phone))
            throw new InvalidOperationException("BootstrapAdmin cần mã nhân viên, họ tên, email và điện thoại hợp lệ.");
        SecurityRules.ValidatePassword(password, password);

        await database.WriteAsync(IdentityData.WriteLock, async work =>
        {
            if (await work.ValueAsync<int>("SELECT COUNT(*) FROM dbo.TaiKhoan") != 0)
                return false;
            await work.ExecuteAsync("""
                INSERT dbo.TaiKhoan (maTaiKhoan,maVaiTro,hoTen,email,soDienThoai,matKhauBam,trangThai)
                VALUES (@id,'ADMIN',@name,@email,@phone,@hash,'HoatDong')
                """, ("id", id), ("name", name), ("email", email), ("phone", phone),
                ("hash", passwords.HashPassword(id!, password)));
            return true;
        }, token);
    }
}

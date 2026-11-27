using CarRental.Web.Areas.Identity.Data;
using CarRental.Web.Areas.Identity.Services;
using CarRental.Web.Shared.Data;
using Microsoft.AspNetCore.Identity;

namespace CarRental.Web.Areas.Settings.Services;

public sealed record ProfileInput(string? Name, string? Email, string? Phone, string? Address,
    OtpProof? EmailProof, OtpProof? PhoneProof);
public sealed record AppearanceInput(string? Color, string? Font, string? Layout, int Size);
public sealed record ChangePasswordInput(string? Current, string? NewPassword, string? Confirmation);

public sealed class SettingsService(CarRentalDatabase database, OtpService otp, IPasswordHasher<string> passwords,
    TimeProvider time)
{
    public Task<object> GetAsync(string id, CancellationToken token) => database.ReadAsync<object>(async work =>
    {
        var account = await AccountAsync(work, id);
        var appearance = await work.OneAsync("""
            SELECT mauSac,phongChu,boCuc,kichThuoc FROM dbo.TuyChonGiaoDien WHERE maTaiKhoan=@id
            """, row => new AppearanceInput(row.GetString(0), row.GetString(1), row.GetString(2), row.GetInt32(3)),
            ("id", id));
        var providers = await work.QueryAsync("SELECT nhacungcap FROM dbo.DanhTinhLienKet WHERE maTaiKhoan=@id",
            row => row.GetString(0), ("id", id));
        return new { profile = await IdentityData.ProfileAsync(work, account), appearance, providers };
    }, token);

    public Task<string> ContactOtpAsync(string id, string? channel, string? destination, CancellationToken token)
    {
        destination = destination?.Trim();
        ValidateContact(channel, destination);
        return database.WriteAsync(IdentityData.WriteLock, async work =>
        {
            var account = await AccountAsync(work, id);
            if (account.Destination(channel!) == destination)
                throw new BusinessException("Địa chỉ liên hệ này đã được xác minh.");
            await CheckUniqueContactAsync(work, account.Id, channel == "EMAIL" ? destination! : account.Email,
                channel == "SMS" ? destination! : account.Phone);
            return await otp.IssueAsync(work, account, SecurityRules.ChangeContact, channel!, destination!,
                time.GetUtcNow().UtcDateTime, token);
        }, token);
    }

    public async Task UpdateProfileAsync(string id, ProfileInput input, CancellationToken token)
    {
        var name = input.Name?.Trim();
        var email = input.Email?.Trim();
        var phone = input.Phone?.Trim();
        var address = input.Address?.Trim();
        if (name is not { Length: > 0 and <= 100 } || address is { Length: > 255 })
            throw new BusinessException("Họ tên hoặc địa chỉ không hợp lệ.");
        ValidateContact("EMAIL", email);
        ValidateContact("SMS", phone);

        var changed = await database.WriteAsync(IdentityData.WriteLock, async work =>
        {
            var account = await AccountAsync(work, id);
            await CheckUniqueContactAsync(work, account.Id, email!, phone!);
            var proofs = new List<(OtpTicket Ticket, string? Code)>();
            AddProof("EMAIL", email!, input.EmailProof);
            AddProof("SMS", phone!, input.PhoneProof);
            var now = time.GetUtcNow().UtcDateTime;
            var valid = true;
            foreach (var (ticket, code) in proofs)
                valid &= await otp.CheckAsync(work, ticket, code, now);
            if (!valid)
                return false;
            foreach (var (ticket, _) in proofs)
                await OtpService.ConsumeAsync(work, ticket, now);

            await work.ExecuteAsync("UPDATE dbo.TaiKhoan SET hoTen=@name,email=@email,soDienThoai=@phone,diaChi=@address WHERE maTaiKhoan=@id",
                ("name", name), ("email", email), ("phone", phone), ("address", address), ("id", account.Id));
            if (proofs.Count != 0)
                await work.ExecuteAsync("UPDATE dbo.MaXacThuc SET daDungLuc=@now WHERE maTaiKhoan=@id AND daDungLuc IS NULL",
                    ("now", now), ("id", account.Id));
            await IdentityData.AuditAsync(work, now, id, "SuaHoSo", "TaiKhoan", id, true);
            return true;

            void AddProof(string channel, string destination, OtpProof? proof)
            {
                if (account.Destination(channel) == destination)
                    return;
                var ticket = ReadMatchingProof(proof, SecurityRules.ChangeContact, id, channel, destination);
                proofs.Add((ticket, proof!.Code));
            }
        }, token);
        if (!changed)
            throw new BusinessException(AuthService.OtpError);
    }

    public async Task ChangePasswordAsync(string id, ChangePasswordInput input, CancellationToken token)
    {
        SecurityRules.ValidatePassword(input.NewPassword, input.Confirmation);
        if (input.Current is not { Length: > 0 and <= 128 })
            throw new BusinessException("Nhập mật khẩu hiện tại hợp lệ.");
        if (input.Current == input.NewPassword)
            throw new BusinessException("Mật khẩu mới phải khác mật khẩu hiện tại.");

        var failure = await database.WriteAsync<string?>(IdentityData.WriteLock, async work =>
        {
            var account = await AccountAsync(work, id);
            var now = time.GetUtcNow().UtcDateTime;
            var recentLock = await work.ValueAsync<int>("""
                SELECT COUNT(*) FROM dbo.NhatKyThaoTac
                WHERE maTaiKhoan=@id AND hanhDong='DoiMatKhauKhoa' AND thoiDiem>@cutoff
                """, ("id", id), ("cutoff", now - SecurityRules.Lock));
            const string lockMessage = "Chức năng đổi mật khẩu bị khóa tạm trong 15 phút.";
            if (recentLock != 0)
                return lockMessage;
            if (passwords.VerifyHashedPassword(account.Id, account.PasswordHash, input.Current) == PasswordVerificationResult.Failed)
            {
                var failures = 1 + await work.ValueAsync<int>("""
                    SELECT COUNT(*) FROM dbo.NhatKyThaoTac
                    WHERE maTaiKhoan=@id AND hanhDong='DoiMatKhau' AND ketQua='ThatBai'
                      AND maNhatKy>COALESCE((
                          SELECT MAX(maNhatKy) FROM dbo.NhatKyThaoTac
                          WHERE maTaiKhoan=@id AND (hanhDong='DoiMatKhauKhoa'
                            OR (hanhDong IN ('DoiMatKhau','KhoiPhucMatKhau') AND ketQua='ThanhCong'))
                      ),0)
                    """, ("id", id));
                var locked = failures > SecurityRules.AttemptLimit;
                await IdentityData.AuditAsync(work, now, id, locked ? "DoiMatKhauKhoa" : "DoiMatKhau", "TaiKhoan", id, false);
                return locked ? lockMessage : "Mật khẩu hiện tại không đúng.";
            }

            await AuthService.SetHashAsync(work, account.Id, passwords.HashPassword(account.Id, input.NewPassword!));
            await work.ExecuteAsync("UPDATE dbo.MaXacThuc SET daDungLuc=@now WHERE maTaiKhoan=@id AND daDungLuc IS NULL",
                ("now", now), ("id", id));
            await IdentityData.AuditAsync(work, now, id, "DoiMatKhau", "TaiKhoan", id, true);
            return null;
        }, token);

        if (failure is not null)
            throw new BusinessException(failure);
    }

    public Task<string> TwoFactorOtpAsync(string id, string? channel, CancellationToken token)
    {
        if (channel is not ("EMAIL" or "SMS"))
            throw new BusinessException("Chọn email hoặc SMS.");
        return database.WriteAsync(IdentityData.WriteLock, async work =>
        {
            var account = await AccountAsync(work, id);
            return await otp.IssueAsync(work, account, SecurityRules.EnableTwoFactor, channel,
                account.Destination(channel), time.GetUtcNow().UtcDateTime, token);
        }, token);
    }

    public async Task EnableTwoFactorAsync(string id, OtpProof proof, CancellationToken token)
    {
        var ticket = otp.Read(proof.Ticket, SecurityRules.EnableTwoFactor);
        var saved = await database.WriteAsync(IdentityData.WriteLock, async work =>
        {
            var now = time.GetUtcNow().UtcDateTime;
            var account = await AccountAsync(work, id);
            if (ticket.Account != id || account.Destination(ticket.Channel) != ticket.Destination ||
                !await otp.CheckAsync(work, ticket, proof.Code, now))
                return false;
            await OtpService.ConsumeAsync(work, ticket, now);
            await work.ExecuteAsync("UPDATE dbo.TaiKhoan SET batHaiYeuTo=1,kenhOTP=@channel WHERE maTaiKhoan=@id",
                ("channel", ticket.Channel), ("id", id));
            await IdentityData.AuditAsync(work, now, id, "Bat2FA", "TaiKhoan", id, true);
            return true;
        }, token);

        if (!saved)
            throw new BusinessException(AuthService.OtpError);
    }

    public Task SaveAppearanceAsync(string id, AppearanceInput input, CancellationToken token)
    {
        if (input.Color is not ("teal" or "blue" or "purple") ||
            input.Font is not ("system" or "serif" or "mono") ||
            input.Layout is not ("standard" or "compact") || input.Size is not (90 or 100 or 110 or 120))
            throw new BusinessException("Tùy chọn giao diện không hợp lệ.");
        return database.WriteAsync(IdentityData.WriteLock, async work =>
        {
            await AccountAsync(work, id);
            await work.ExecuteAsync("""
                UPDATE dbo.TuyChonGiaoDien SET mauSac=@color,phongChu=@font,boCuc=@layout,kichThuoc=@size
                WHERE maTaiKhoan=@id;
                IF @@ROWCOUNT=0
                    INSERT dbo.TuyChonGiaoDien (maTaiKhoan,mauSac,phongChu,boCuc,kichThuoc)
                    VALUES (@id,@color,@font,@layout,@size);
                """, ("id", id), ("color", input.Color), ("font", input.Font), ("layout", input.Layout),
                ("size", input.Size));
            await IdentityData.AuditAsync(work, time.GetUtcNow().UtcDateTime, id, "TuyChonGiaoDien", "TaiKhoan", id, true);
            return true;
        }, token);
    }

    public Task EnsureLinkAvailableAsync(string id, string provider, string subject, CancellationToken token) =>
        database.ReadAsync(async work =>
        {
            await AccountAsync(work, id);
            await CheckLinkAsync(work, id, provider, subject);
            return true;
        }, token);

    public Task SaveLinkAsync(string id, string provider, string subject, CancellationToken token) =>
        database.WriteAsync(IdentityData.WriteLock, async work =>
        {
            await AccountAsync(work, id);
            await CheckLinkAsync(work, id, provider, subject);
            var now = time.GetUtcNow().UtcDateTime;
            await work.ExecuteAsync("""
                INSERT dbo.DanhTinhLienKet (maTaiKhoan,nhacungcap,chuTheNgoai,ngayLienKet)
                VALUES (@id,@provider,@subject,@now)
                """, ("id", id), ("provider", provider), ("subject", subject), ("now", now));
            await IdentityData.AuditAsync(work, now, id, "LienKetDanhTinh", "TaiKhoan", id, true);
            return true;
        }, token);

    private static async Task CheckLinkAsync(SqlWork work, string id, string provider, string subject)
    {
        var link = await work.OneAsync("""
            SELECT TOP (1) maTaiKhoan FROM dbo.DanhTinhLienKet
            WHERE nhacungcap=@provider AND (chuTheNgoai=@subject OR maTaiKhoan=@id)
            ORDER BY CASE WHEN chuTheNgoai=@subject THEN 0 ELSE 1 END
            """, row => row.GetString(0), ("id", id), ("provider", provider), ("subject", subject));
        if (link is not null)
            throw new BusinessException(link == id ? "Tài khoản đã liên kết nhà cung cấp này."
                : "Danh tính đã liên kết với tài khoản khác.", 409);
    }

    private OtpTicket ReadMatchingProof(OtpProof? proof, string purpose, string id, string channel, string destination)
    {
        if (proof?.Ticket is null)
            throw new BusinessException("Cần xác minh kênh liên hệ mới trước khi lưu.");
        var ticket = otp.Read(proof.Ticket, purpose);
        if (ticket.Account != id || ticket.Channel != channel || ticket.Destination != destination)
            throw new BusinessException(AuthService.OtpError);
        return ticket;
    }

    private static async Task<Account> AccountAsync(SqlWork work, string id) =>
        await IdentityData.AccountAsync(work, id) is { Active: true } account
            ? account
            : throw new BusinessException("Phiên không còn hợp lệ.", 401);

    private static void ValidateContact(string? channel, string? destination)
    {
        if (!(channel switch { "EMAIL" => SecurityRules.IsEmail(destination), "SMS" => SecurityRules.IsPhone(destination), _ => false }))
            throw new BusinessException("Email hoặc số điện thoại không hợp lệ.");
    }

    private static async Task CheckUniqueContactAsync(SqlWork work, string id, string email, string phone)
    {
        if (await work.ValueAsync<int>("SELECT COUNT(*) FROM dbo.TaiKhoan WHERE maTaiKhoan<>@id AND (email=@email OR soDienThoai=@phone)",
                ("id", id), ("email", email), ("phone", phone)) > 0)
            throw new BusinessException("Email hoặc số điện thoại đã được sử dụng.", 409);
    }
}

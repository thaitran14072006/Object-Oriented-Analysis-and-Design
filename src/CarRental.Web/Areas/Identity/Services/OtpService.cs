using System.Security.Cryptography;
using System.Text.Json;
using CarRental.Web.Areas.Identity.Data;
using CarRental.Web.Shared.Data;
using Microsoft.AspNetCore.DataProtection;

namespace CarRental.Web.Areas.Identity.Services;

public sealed record OtpProof(string? Ticket, string? Code);
public sealed record OtpTicket(long Id, string Account, string Purpose, string Channel, string Destination, string Secret);

public sealed class OtpService(IDataProtectionProvider protection, IOtpDelivery delivery)
{
    private readonly ITimeLimitedDataProtector _protector =
        protection.CreateProtector("RentalPro.Otp.v1").ToTimeLimitedDataProtector();

    private sealed record OtpRow(long Id, string Destination, string Hash, DateTime Created, DateTime Expires,
        DateTime? Used, int Failures);

    public string DummyRecoveryTicket() => Protect(new OtpTicket(0, "", SecurityRules.Recovery, "EMAIL", "",
        SecurityRules.NewToken()));

    private string Protect(OtpTicket ticket) => _protector.Protect(JsonSerializer.Serialize(ticket).PadRight(1536),
        TimeSpan.FromMinutes(25));

    public OtpTicket Read(string? encrypted, string purpose)
    {
        if (encrypted is { Length: > 0 and < 4096 })
        {
            try
            {
                var ticket = JsonSerializer.Deserialize<OtpTicket>(_protector.Unprotect(encrypted));
                if (ticket?.Purpose == purpose)
                    return ticket;
            }
            catch (CryptographicException) { }
            catch (JsonException) { }
        }
        throw new BusinessException("Yêu cầu xác thực không hợp lệ hoặc đã hết hạn.");
    }

    public async Task<string> IssueAsync(SqlWork work, Account account, string purpose, string channel,
        string destination, DateTime now, CancellationToken cancellationToken, long? replacing = null)
    {
        var latest = await LatestAsync(work, account.Id, purpose, channel);
        if (replacing is not null && latest?.Id != replacing)
            throw new BusinessException("Yêu cầu đã được thay thế. Hãy dùng mã mới nhất.", 409);
        if (latest is { Failures: >= SecurityRules.AttemptLimit, Used: { } lockedAt } &&
            lockedAt + SecurityRules.Lock > now)
            throw new BusinessException("Yêu cầu xác thực bị khóa tạm trong 15 phút.", 429);
        if (latest is not null && !SecurityRules.CanResend(latest.Created, now))
            throw new BusinessException("Chờ ít nhất 60 giây trước khi gửi lại mã.", 429);

        var failures = latest is { Used: null } && latest.Expires + SecurityRules.Lock > now ? latest.Failures : 0;
        var secret = SecurityRules.NewToken();
        var code = SecurityRules.NewOtp();
        await work.ExecuteAsync("""
            UPDATE dbo.MaXacThuc SET daDungLuc=@now
            WHERE maTaiKhoan=@id AND mucDich=@purpose AND kenh=@channel AND daDungLuc IS NULL
            """, ("now", now), ("id", account.Id), ("purpose", purpose), ("channel", channel));
        var id = await work.ValueAsync<long>("""
            INSERT dbo.MaXacThuc (maTaiKhoan,mucDich,kenh,dichNhan,maBam,taoLuc,hetHanLuc,soLanSai)
            OUTPUT INSERTED.maXacThuc VALUES (@id,@purpose,@channel,@destination,@hash,@now,@expires,@failures)
            """, ("id", account.Id), ("purpose", purpose), ("channel", channel), ("destination", destination),
            ("hash", SecurityRules.TokenHash(secret + code)), ("now", now), ("expires", now + SecurityRules.OtpLife),
            ("failures", failures));
        await delivery.SendAsync(channel, destination, code, cancellationToken);
        return Protect(new OtpTicket(id, account.Id, purpose, channel, destination, secret));
    }

    public async Task<bool> CheckAsync(SqlWork work, OtpTicket ticket, string? provided, DateTime now)
    {
        var latest = await LatestAsync(work, ticket.Account, ticket.Purpose, ticket.Channel);
        if (latest is null || latest.Id != ticket.Id || latest.Destination != ticket.Destination ||
            !SecurityRules.IsOtpCurrent(latest.Expires, latest.Used, now) ||
            latest.Failures >= SecurityRules.AttemptLimit)
            return false;

        var expected = Convert.FromHexString(latest.Hash);
        if (SecurityRules.IsOtp(provided) && CryptographicOperations.FixedTimeEquals(
                expected, Convert.FromHexString(SecurityRules.TokenHash(ticket.Secret + provided))))
            return true;

        var attempts = latest.Failures + 1;
        await work.ExecuteAsync("""
            UPDATE dbo.MaXacThuc SET soLanSai=@attempts, daDungLuc=@used WHERE maXacThuc=@id
            """, ("attempts", attempts), ("used", attempts >= SecurityRules.AttemptLimit ? now : null),
            ("id", latest.Id));
        if (attempts >= SecurityRules.AttemptLimit && ticket.Purpose == SecurityRules.Login)
            await work.ExecuteAsync("UPDATE dbo.TaiKhoan SET khoaDen=@until, soLanSai=0 WHERE maTaiKhoan=@id",
                ("until", now + SecurityRules.Lock), ("id", ticket.Account));
        return false;
    }

    public static Task ConsumeAsync(SqlWork work, OtpTicket ticket, DateTime now) =>
        work.ExecuteAsync("UPDATE dbo.MaXacThuc SET daDungLuc=@now WHERE maXacThuc=@id",
            ("now", now), ("id", ticket.Id));

    private static Task<OtpRow?> LatestAsync(SqlWork work, string id, string purpose, string channel) =>
        work.OneAsync("""
            SELECT TOP (1) maXacThuc,dichNhan,maBam,taoLuc,hetHanLuc,daDungLuc,soLanSai
            FROM dbo.MaXacThuc WHERE maTaiKhoan=@id AND mucDich=@purpose AND kenh=@channel ORDER BY maXacThuc DESC
            """, row => new OtpRow(row.GetInt64(0), row.GetString(1), row.GetString(2), row.GetDateTime(3),
            row.GetDateTime(4), row.IsDBNull(5) ? null : row.GetDateTime(5), row.GetInt32(6)),
            ("id", id), ("purpose", purpose), ("channel", channel));
}

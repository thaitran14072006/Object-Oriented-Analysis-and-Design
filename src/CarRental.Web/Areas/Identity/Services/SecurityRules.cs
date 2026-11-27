using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;
using CarRental.Web.Shared.Data;

namespace CarRental.Web.Areas.Identity.Services;

public static partial class SecurityRules
{
    public static readonly TimeSpan Idle = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan Lock = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan OtpLife = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan Resend = TimeSpan.FromSeconds(60);
    public const int AttemptLimit = 5;
    public const string Login = "DangNhap";
    public const string Recovery = "KhoiPhuc";
    public const string EnableTwoFactor = "Bat2FA";
    public const string ChangeContact = "DoiLienHe";

    public static bool IsEmployeeId(string? input) => input is not null && EmployeeIdPattern().IsMatch(input);
    public static bool IsPhone(string? input) => input is not null && PhonePattern().IsMatch(input);
    public static bool IsEmail(string? input) => input is { Length: > 0 and <= 254 }
        && input.All(char.IsAscii) && !input.Any(char.IsWhiteSpace) && new EmailAddressAttribute().IsValid(input);
    public static bool IsOtp(string? input) => input is not null && OtpPattern().IsMatch(input);
    public static string NewOtp() => RandomNumberGenerator.GetInt32(1_000_000).ToString("D6", CultureInfo.InvariantCulture);
    public static string NewToken() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
    public static string TokenHash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    public static bool IsIdleExpired(DateTime activity, DateTime now) => activity <= now - Idle;
    public static bool CanResend(DateTime issuedAt, DateTime now) => now - issuedAt >= Resend;
    public static bool IsOtpCurrent(DateTime expiresAt, DateTime? usedAt, DateTime now) =>
        usedAt is null && now < expiresAt;

    public static void ValidatePassword(string? password, string? confirmation)
    {
        if (password is not { Length: >= 8 and <= 128 } ||
            !password.Any(char.IsUpper) || !password.Any(char.IsLower) || !password.Any(char.IsDigit))
            throw new BusinessException("Mật khẩu phải có 8–128 ký tự, gồm chữ hoa, chữ thường và số.");
        if (password != confirmation)
            throw new BusinessException("Mật khẩu xác nhận không khớp.");
    }

    [GeneratedRegex(@"\A[A-Za-z0-9][A-Za-z0-9_-]{0,19}\z")]
    private static partial Regex EmployeeIdPattern();
    [GeneratedRegex(@"\A\+?[0-9]{8,15}\z")]
    private static partial Regex PhonePattern();
    [GeneratedRegex(@"\A[0-9]{6}\z")]
    private static partial Regex OtpPattern();
}

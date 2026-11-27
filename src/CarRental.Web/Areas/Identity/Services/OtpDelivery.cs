using System.Net;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Text;
using CarRental.Web.Shared.Data;

namespace CarRental.Web.Areas.Identity.Services;

public interface IOtpDelivery
{
    Task SendAsync(string channel, string destination, string code, CancellationToken cancellationToken);
}

public sealed class OtpDelivery(IConfiguration configuration, IWebHostEnvironment environment, IHttpClientFactory clients,
    ILogger<OtpDelivery> logger) : IOtpDelivery
{
    public async Task SendAsync(string channel, string destination, string code, CancellationToken cancellationToken)
    {
        var message = $"Mã xác thực RentalPro của bạn là {code}. Mã có hiệu lực trong 5 phút.";
        try
        {
            if (channel == "EMAIL")
                await SendEmailAsync(destination, message, cancellationToken);
            else
                await SendSmsAsync(destination, message, cancellationToken);
        }
        catch (Exception ex) when (ex is SmtpException or HttpRequestException or IOException or FormatException)
        {
            logger.LogWarning("Không gửi được OTP qua {Channel}. Loại lỗi {ErrorType}.", channel, ex.GetType().Name);
            throw new BusinessException("Không gửi được mã xác thực. Hãy thử lại sau.", 503);
        }
    }

    private async Task SendEmailAsync(string destination, string body, CancellationToken cancellationToken)
    {
        var config = configuration.GetSection("Otp:Email");
        if (string.IsNullOrWhiteSpace(config["From"]))
            throw new BusinessException("Kênh email chưa được cấu hình.", 503);
        using var mail = new MailMessage(config["From"]!, destination, "Mã xác thực RentalPro", body);
        using var smtp = new SmtpClient();
        if (environment.IsDevelopment() && !string.IsNullOrWhiteSpace(config["PickupDirectory"]))
        {
            var directory = Path.GetFullPath(config["PickupDirectory"]!);
            Directory.CreateDirectory(directory);
            smtp.DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory;
            smtp.PickupDirectoryLocation = directory;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(config["Host"]))
                throw new BusinessException("Kênh email chưa được cấu hình.", 503);
            smtp.Host = config["Host"]!;
            smtp.Port = config.GetValue("Port", 587);
            smtp.EnableSsl = true;
            if (!string.IsNullOrWhiteSpace(config["Username"]))
                smtp.Credentials = new NetworkCredential(config["Username"], config["Password"]);
        }
        await smtp.SendMailAsync(mail, cancellationToken);
    }

    private async Task SendSmsAsync(string destination, string body, CancellationToken cancellationToken)
    {
        var config = configuration.GetSection("Otp:Sms");
        var sid = config["AccountSid"];
        var token = config["AuthToken"];
        var from = config["From"];
        if (string.IsNullOrWhiteSpace(sid) || string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(from))
            throw new BusinessException("Kênh SMS chưa được cấu hình.", 503);

        if (destination.StartsWith('0'))
            destination = "+84" + destination[1..];
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"https://api.twilio.com/2010-04-01/Accounts/{Uri.EscapeDataString(sid)}/Messages.json");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.ASCII.GetBytes($"{sid}:{token}")));
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["From"] = from, ["To"] = destination, ["Body"] = body
        });
        using var response = await clients.CreateClient("otp").SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}

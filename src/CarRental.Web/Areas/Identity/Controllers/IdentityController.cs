using System.Security.Claims;
using CarRental.Web.Areas.Identity.Services;
using CarRental.Web.Shared.Contracts;
using CarRental.Web.Shared.Data;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarRental.Web.Areas.Identity.Controllers;

[ApiController, Area("Identity"), Route("api/identity")]
public sealed class IdentityController(AuthService auth, IAntiforgery antiforgery, IConfiguration configuration) : Controller
{
    public sealed record LoginInput(string? EmployeeId, string? Password);
    public sealed record RecoveryInput(string? Identifier);
    public sealed record ResetInput(string? Ticket, string? Code, string? Password, string? Confirmation);
    public sealed record ResendInput(string? Ticket, bool Recovery);

    [HttpGet("csrf")]
    public IActionResult Csrf()
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(new { token = antiforgery.GetAndStoreTokens(HttpContext).RequestToken });
    }

    [HttpGet("options")]
    public IActionResult Options() => Ok(new { google = ExternalLogin.IsEnabled(configuration) });

    [HttpGet("me"), Authorize(Policy = AdminAccess.Admin)]
    public IActionResult Me() => Ok(new
    {
        id = User.AccountId(),
        name = User.Identity!.Name,
        permissions = User.FindAll(AdminAccess.PermissionClaim).Select(claim => claim.Value)
    });

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginInput input, CancellationToken token) =>
        await ReplyAsync(await auth.LoginAsync(input.EmployeeId, input.Password, token));

    [HttpPost("login/otp")]
    public async Task<IActionResult> Verify(OtpProof input, CancellationToken token) =>
        await ReplyAsync(await auth.VerifyLoginAsync(input, token));

    [HttpPost("otp/resend")]
    public async Task<IActionResult> Resend(ResendInput input, CancellationToken token) =>
        Ok(new { ticket = await auth.ResendAsync(input.Ticket,
            input.Recovery ? SecurityRules.Recovery : SecurityRules.Login, token) });

    [HttpPost("recovery")]
    public async Task<IActionResult> Recovery(RecoveryInput input, CancellationToken token) =>
        Ok(new { ticket = await auth.RequestRecoveryAsync(input.Identifier, token),
            message = "Nếu tài khoản đủ điều kiện, mã xác thực sẽ được gửi đến kênh đã đăng ký." });

    [HttpPost("recovery/reset")]
    public async Task<IActionResult> Reset(ResetInput input, CancellationToken token)
    {
        await auth.ResetAsync(new OtpProof(input.Ticket, input.Code), input.Password, input.Confirmation, token);
        return NoContent();
    }

    [HttpPost("logout"), Authorize(Policy = AdminAccess.Admin)]
    public async Task<IActionResult> Logout(CancellationToken token)
    {
        await auth.LogoutAsync(User.AccountId(), User.FindFirstValue(AdminAccess.SessionClaim)!, token);
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    [HttpPost("/identity/external")]
    public IActionResult External()
    {
        if (!ExternalLogin.IsEnabled(configuration))
            return NotFound();
        return Challenge(new AuthenticationProperties { RedirectUri = "/login?external=ready" },
            ExternalLogin.Provider);
    }

    [HttpPost("external/finish")]
    public async Task<IActionResult> CompleteExternal(CancellationToken token)
    {
        if (!ExternalLogin.IsEnabled(configuration))
            return NotFound();
        var result = await HttpContext.AuthenticateAsync(ExternalLogin.Scheme);
        await HttpContext.SignOutAsync(ExternalLogin.Scheme);
        if (!result.Succeeded || result.Properties!.Items.ContainsKey(ExternalLogin.AccountProperty) ||
            result.Principal?.FindFirstValue(ClaimTypes.NameIdentifier) is not { } subject)
            throw new BusinessException("Yêu cầu đăng nhập bên thứ ba không hợp lệ.", 401);

        return await ReplyAsync(await auth.ExternalLoginAsync(ExternalLogin.Provider, subject, token));
    }

    private async Task<IActionResult> ReplyAsync(LoginReply result)
    {
        Response.Headers.CacheControl = "no-store";
        if (result.Ticket is not null)
            return Ok(new { ticket = result.Ticket });
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            AuthService.Principal(result.Admin!, result.SessionToken!));
        return Ok(new { admin = result.Admin });
    }
}

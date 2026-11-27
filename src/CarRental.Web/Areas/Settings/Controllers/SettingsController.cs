using System.Security.Claims;
using CarRental.Web.Areas.Identity.Services;
using CarRental.Web.Areas.Settings.Services;
using CarRental.Web.Shared.Contracts;
using CarRental.Web.Shared.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarRental.Web.Areas.Settings.Controllers;

[ApiController, Area("Settings"), Route("api/settings")]
public sealed class SettingsController(SettingsService settings, PermissionService permissions,
    IConfiguration configuration) : Controller
{
    public sealed record ChannelInput(string? Channel, string? Destination);

    [Authorize(Policy = AdminAccess.Settings), HttpGet]
    public async Task<IActionResult> Get(CancellationToken token) => Ok(await settings.GetAsync(User.AccountId(), token));

    [Authorize(Policy = AdminAccess.Settings), HttpPost("profile/otp")]
    public async Task<IActionResult> SendContactCode(ChannelInput input, CancellationToken token) =>
        Ok(new { ticket = await settings.ContactOtpAsync(User.AccountId(), input.Channel, input.Destination, token) });

    [Authorize(Policy = AdminAccess.Settings), HttpPut("profile")]
    public async Task<IActionResult> Profile(ProfileInput input, CancellationToken token)
    {
        await settings.UpdateProfileAsync(User.AccountId(), input, token);
        return NoContent();
    }

    [Authorize(Policy = AdminAccess.Settings), HttpPut("password")]
    public async Task<IActionResult> Password(ChangePasswordInput input, CancellationToken token)
    {
        await settings.ChangePasswordAsync(User.AccountId(), input, token);
        return NoContent();
    }

    [Authorize(Policy = AdminAccess.Settings), HttpPost("2fa/otp")]
    public async Task<IActionResult> SendTwoFactorCode(ChannelInput input, CancellationToken token) =>
        Ok(new { ticket = await settings.TwoFactorOtpAsync(User.AccountId(), input.Channel, token) });

    [Authorize(Policy = AdminAccess.Settings), HttpPut("2fa")]
    public async Task<IActionResult> TwoFactor(OtpProof input, CancellationToken token)
    {
        await settings.EnableTwoFactorAsync(User.AccountId(), input, token);
        return NoContent();
    }

    [Authorize(Policy = AdminAccess.Settings), HttpPut("appearance")]
    public async Task<IActionResult> Appearance(AppearanceInput input, CancellationToken token)
    {
        await settings.SaveAppearanceAsync(User.AccountId(), input, token);
        return NoContent();
    }

    [Authorize(Policy = AdminAccess.Security), HttpGet("permissions")]
    public async Task<IActionResult> Permissions(CancellationToken token) => Ok(await permissions.GetAsync(token));

    [Authorize(Policy = AdminAccess.Security), HttpPut("permissions")]
    public async Task<IActionResult> Permissions(PermissionInput input, CancellationToken token)
    {
        await permissions.ChangeAsync(User.AccountId(), input, token);
        return NoContent();
    }

    [Authorize(Policy = AdminAccess.Settings), HttpPost("/settings/external")]
    public IActionResult Link()
    {
        if (!ExternalLogin.IsEnabled(configuration))
            return NotFound();
        var properties = new AuthenticationProperties { RedirectUri = "/?link=ready#settings" };
        properties.Items[ExternalLogin.AccountProperty] = User.AccountId();
        return Challenge(properties, ExternalLogin.Provider);
    }

    [Authorize(Policy = AdminAccess.Settings), HttpGet("external")]
    public async Task<IActionResult> PreviewLink(CancellationToken token)
    {
        var subject = await LinkSubjectAsync();
        await settings.EnsureLinkAvailableAsync(User.AccountId(), ExternalLogin.Provider, subject, token);
        return Ok(new { provider = ExternalLogin.Provider });
    }

    [Authorize(Policy = AdminAccess.Settings), HttpPost("external")]
    public async Task<IActionResult> ConfirmLink(CancellationToken token)
    {
        var subject = await LinkSubjectAsync();
        await settings.SaveLinkAsync(User.AccountId(), ExternalLogin.Provider, subject, token);
        await HttpContext.SignOutAsync(ExternalLogin.Scheme);
        return NoContent();
    }

    [Authorize(Policy = AdminAccess.Settings), HttpDelete("external")]
    public async Task<IActionResult> CancelLink()
    {
        if (ExternalLogin.IsEnabled(configuration))
            await HttpContext.SignOutAsync(ExternalLogin.Scheme);
        return NoContent();
    }

    private async Task<string> LinkSubjectAsync()
    {
        if (!ExternalLogin.IsEnabled(configuration))
            throw new BusinessException("Nhà cung cấp chưa được cấu hình.", 404);
        var ticket = await HttpContext.AuthenticateAsync(ExternalLogin.Scheme);
        if (!ticket.Succeeded ||
            !ticket.Properties!.Items.TryGetValue(ExternalLogin.AccountProperty, out var account) ||
            account != User.AccountId() || ticket.Principal?.FindFirstValue(ClaimTypes.NameIdentifier) is not { } subject)
            throw new BusinessException("Yêu cầu liên kết không hợp lệ hoặc đã hết hạn.");
        return subject;
    }
}

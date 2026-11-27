using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;

namespace CarRental.Web.Areas.Identity.Services;

public static class ExternalLogin
{
    public const string Provider = "Google";
    public const string Scheme = "RentalPro.External";
    public const string AccountProperty = "rental.linkAccount";

    public static bool IsEnabled(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(configuration["Authentication:Google:ClientId"]) &&
        !string.IsNullOrWhiteSpace(configuration["Authentication:Google:ClientSecret"]);

    public static void Configure(AuthenticationBuilder auth, IConfiguration configuration)
    {
        if (!IsEnabled(configuration))
            return;

        auth.AddCookie(Scheme, options =>
        {
            options.Cookie.Name = "RentalPro.External";
            options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
            options.SlidingExpiration = false;
        });
        auth.AddOAuth(Provider, options =>
        {
            options.SignInScheme = Scheme;
            options.ClientId = configuration["Authentication:Google:ClientId"]!;
            options.ClientSecret = configuration["Authentication:Google:ClientSecret"]!;
            options.CallbackPath = "/signin-google";
            options.AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
            options.TokenEndpoint = "https://oauth2.googleapis.com/token";
            options.UserInformationEndpoint = "https://openidconnect.googleapis.com/v1/userinfo";
            options.Scope.Add("openid");
            options.Scope.Add("profile");
            options.UsePkce = true;
            options.SaveTokens = false;
            options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "sub");
            options.Events = new OAuthEvents
            {
                OnCreatingTicket = async context =>
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);
                    using var response = await context.Backchannel.SendAsync(request, context.HttpContext.RequestAborted);
                    response.EnsureSuccessStatusCode();
                    using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(),
                        cancellationToken: context.HttpContext.RequestAborted);
                    context.RunClaimActions(document.RootElement);
                    if (context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier) is not { Length: > 0 and <= 255 })
                        throw new InvalidOperationException("Không nhận được định danh từ nhà cung cấp.");
                },
                OnRemoteFailure = async context =>
                {
                    var audit = context.HttpContext.RequestServices.GetRequiredService<Shared.Contracts.IAdminAudit>();
                    try
                    {
                        await audit.RecordAsync(null, "XacThucNgoai", "TaiKhoan", null, false,
                            context.HttpContext.RequestAborted);
                    }
                    catch (Shared.Data.DatabaseUnavailableException) { }
                    context.HandleResponse();
                    context.Response.Redirect("/login?external=failed");
                }
            };
        });
    }
}

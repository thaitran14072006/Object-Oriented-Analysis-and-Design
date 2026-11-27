using CarRental.Web.Areas.Customers.Services;
using CarRental.Web.Areas.Identity.Data;
using CarRental.Web.Areas.Identity.Services;
using CarRental.Web.Areas.Settings.Services;
using CarRental.Web.Shared.Contracts;
using CarRental.Web.Shared.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews(options => options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
var secureCookies = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.SecurePolicy = secureCookies;
});
var protection = builder.Services.AddDataProtection();
if (builder.Configuration["DataProtection:KeyDirectory"] is { Length: > 0 } directory)
{
    protection.PersistKeysToFileSystem(new DirectoryInfo(directory));
    if (OperatingSystem.IsWindows())
        protection.ProtectKeysWithDpapi();
}
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<CarRentalDatabase>();
builder.Services.AddScoped<IdentityData>();
builder.Services.AddScoped<IAdminAudit>(services => services.GetRequiredService<IdentityData>());
builder.Services.AddScoped<IPasswordHasher<string>, PasswordHasher<string>>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<OtpService>();
builder.Services.AddScoped<IOtpDelivery, OtpDelivery>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<PermissionService>();
builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<IRentalCustomerDirectory>(services => services.GetRequiredService<CustomerService>());
builder.Services.AddHttpClient("otp", client => client.Timeout = TimeSpan.FromSeconds(10));

var auth = builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.Cookie.Name = "RentalPro.Admin";
    options.Cookie.SecurePolicy = secureCookies;
    options.LoginPath = "/login";
    options.ExpireTimeSpan = SecurityRules.Idle;
    options.SlidingExpiration = true;
    options.Events.OnValidatePrincipal = async context =>
    {
        var principal = context.Principal!;
        var session = principal.FindFirst(AdminAccess.SessionClaim)?.Value;
        var id = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var profile = id is not null && session is not null
            ? await context.HttpContext.RequestServices.GetRequiredService<AuthService>()
                .SessionAsync(id, session, context.HttpContext.RequestAborted)
            : null;
        if (profile is null)
        {
            context.RejectPrincipal();
            return;
        }
        context.ReplacePrincipal(AuthService.Principal(profile, session!));
        context.ShouldRenew = true;
    };
    options.Events.OnRedirectToLogin = context => RespondToCookieRedirect(context, 401);
    options.Events.OnRedirectToAccessDenied = context => RespondToCookieRedirect(context, 403);
});
ExternalLogin.Configure(auth, builder.Configuration);
builder.Services.AddAuthorization(options =>
{
    foreach (var policy in AdminAccess.Policies)
        options.AddPolicy(policy, definition => definition.RequireAuthenticatedUser()
            .RequireClaim(AdminAccess.PermissionClaim, AdminAccess.Admin)
            .RequireClaim(AdminAccess.PermissionClaim, policy));
});

var app = builder.Build();
app.UseExceptionHandler(new ExceptionHandlerOptions
{
    SuppressDiagnosticsCallback = context => context.Exception is BusinessException or DatabaseUnavailableException or SqlException { Number: 208 or 2601 or 2627 },
    ExceptionHandler = async context =>
    {
        var error = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
        var (status, message, details) = error switch
        {
            BusinessException business => (business.Status, business.Message, business.Details),
            DatabaseUnavailableException database => (503, database.Message, null),
            SqlException { Number: 208 } => (503, "Chưa tạo các bảng SQL Server cần thiết.", null),
            SqlException { Number: 2601 or 2627 } => (409, "Thông tin này đã có trong hệ thống.", null),
            _ => (500, "Đã xảy ra lỗi khi xử lý yêu cầu.", null)
        };
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new { message, details });
    }
});
if (!app.Environment.IsDevelopment())
    app.UseHsts();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapControllerRoute("areas", "{area:exists}/{controller=Home}/{action=Index}/{id?}");

if (args is ["--bootstrap-admin"])
{
    if (string.IsNullOrWhiteSpace(app.Configuration["BootstrapAdmin:Password"]))
        throw new InvalidOperationException("Thiết lập BootstrapAdmin:Password trước khi khởi tạo tài khoản.");
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AuthService>().BootstrapAsync(CancellationToken.None);
    return;
}
app.Run();

static Task RespondToCookieRedirect(RedirectContext<CookieAuthenticationOptions> context, int status)
{
    if (!context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    }
    context.Response.StatusCode = status;
    return context.Response.WriteAsJsonAsync(new
    {
        message = status == 401 ? "Phiên đã hết. Vui lòng đăng nhập." : "Bạn không có quyền thực hiện thao tác này."
    });
}

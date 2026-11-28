using CarRental.Web.Areas.Identity.Services;
using CarRental.Web.Areas.Identity.Data;
using CarRental.Web.Areas.Customers.Services;
using CarRental.Web.Areas.Settings.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using CarRental.Web.Areas.Billing.Services;
using CarRental.Web.Shared.Contracts;
using CarRental.Web.Shared.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
var bootstrapOnly = args is ["--bootstrap-admin"];
var builder = WebApplication.CreateBuilder(bootstrapOnly ? Array.Empty<string>() : args);
builder.Services.AddControllersWithViews(o => o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddAntiforgery(o => o.HeaderName = "X-CSRF-TOKEN");
builder.Services.AddDataProtection();
builder.Services.AddHttpClient();
builder.Services.AddScoped<CarRentalDatabase>();
builder.Services.AddScoped<IPasswordHasher<string>, PasswordHasher<string>>();
builder.Services.AddScoped<IOtpDelivery, OtpDelivery>();
builder.Services.AddScoped<OtpService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<IRentalCustomerDirectory>(p => p.GetRequiredService<CustomerService>());
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<PermissionService>();
builder.Services.AddScoped<IAdminAudit, IdentityData>();
builder.Services.AddDbContext<RentalDbContext>(o => o.UseSqlServer(builder.Configuration.GetConnectionString("CarRental") ?? "Server=localhost;Database=CarRental;Integrated Security=True;TrustServerCertificate=True"));
builder.Services.AddScoped<IBillingService, BillingService>();
builder.Services.AddScoped<BillingAudit>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<CarRental.Web.Areas.Fleet.Services.FleetService>();
builder.Services.AddScoped<CarRental.Web.Areas.Maintenance.Services.MaintenanceService>();
builder.Services.AddScoped<CarRental.Web.Areas.Reports.Services.ReportService>();
builder.Services.AddScoped<CarRental.Web.Areas.Reports.Services.PeriodicReportService>();
builder.Services.AddHostedService<CarRental.Web.Areas.Reports.Services.PeriodicReportWorker>();
var authentication = builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o =>
{
    o.ExpireTimeSpan = TimeSpan.FromMinutes(30); o.SlidingExpiration = true;
    o.Events.OnRedirectToLogin = c => { if (c.Request.Path.StartsWithSegments("/api")) c.Response.StatusCode = 401; else c.Response.Redirect("/login"); return Task.CompletedTask; };
    o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
    o.Events.OnValidatePrincipal = async c =>
    {
        var id = c.Principal?.FindFirstValue(ClaimTypes.NameIdentifier); var session = c.Principal?.FindFirstValue(AdminAccess.SessionClaim);
        var profile = id == null || session == null ? null : await c.HttpContext.RequestServices.GetRequiredService<AuthService>().SessionAsync(id, session, c.HttpContext.RequestAborted);
        if (profile == null) c.RejectPrincipal(); else c.ReplacePrincipal(AuthService.Principal(profile, session!));
    };
});
ExternalLogin.Configure(authentication, builder.Configuration);
builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("Pricing.Read", p => p.RequireAuthenticatedUser().RequireAssertion(c => c.User.HasClaim(AdminAccess.PermissionClaim, "Billing.Access") || c.User.HasClaim(AdminAccess.PermissionClaim, "Bookings.Access")));
    foreach (var policy in AdminAccess.Policies) o.AddPolicy(policy, p => p.RequireAuthenticatedUser().RequireClaim(AdminAccess.PermissionClaim, policy));
    o.AddPolicy("BillingAdmin", p => p.RequireAuthenticatedUser().RequireClaim(AdminAccess.PermissionClaim, AdminAccess.Admin));
});
var app = builder.Build();
app.Use(async (context, next) =>
{
    try { await next(); }
    catch (BusinessException e) { context.Response.StatusCode = e.Status; await context.Response.WriteAsJsonAsync(new { message = e.Message, error = e.Message, details = e.Details }); }
    catch (DatabaseUnavailableException e) { context.Response.StatusCode = 503; await context.Response.WriteAsJsonAsync(new { message = e.Message, error = e.Message }); }
    catch (BusinessError e)
    {
        context.Response.StatusCode = 409;
        await context.Response.WriteAsJsonAsync(new { error = e.Message, refund = e.Refund });
    }
    catch (DbUpdateConcurrencyException)
    {
        context.Response.StatusCode = 409;
        await context.Response.WriteAsJsonAsync(new { error = "Dữ liệu đã thay đổi. Hãy tải lại." });
    }
    catch (Microsoft.Data.SqlClient.SqlException e) when (e.Number is 1205 or 51000)
    {
        context.Response.StatusCode = 409;
        await context.Response.WriteAsJsonAsync(new { error = "Giao dịch xung đột. Hãy tải lại và thử lại với cùng mã giao dịch." });
    }
    catch (Microsoft.Data.SqlClient.SqlException e)
    {
        app.Logger.LogError(e, "Không truy cập được dữ liệu.");
        context.Response.StatusCode = 503;
        await context.Response.WriteAsJsonAsync(new { error = "Dữ liệu chưa sẵn sàng. Vui lòng thử lại sau." });
    }
    catch (DbUpdateException)
    {
        context.Response.StatusCode = 409;
        await context.Response.WriteAsJsonAsync(new { error = "Dữ liệu trùng hoặc vi phạm ràng buộc." });
    }
});
app.UseStaticFiles(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
var hasBootstrapPassword = !string.IsNullOrWhiteSpace(builder.Configuration["BootstrapAdmin:Password"]);
if (bootstrapOnly && !hasBootstrapPassword)
    throw new InvalidOperationException("Thiết lập BootstrapAdmin:Password trước khi khởi tạo tài khoản.");

if (hasBootstrapPassword)
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AuthService>().BootstrapAsync(CancellationToken.None);
}
if (!bootstrapOnly)
    app.Run();

public partial class Program { }

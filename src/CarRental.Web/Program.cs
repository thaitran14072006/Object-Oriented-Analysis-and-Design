using CarRental.Web.Areas.Billing.Services;
using CarRental.Web.Shared.Contracts;
using CarRental.Web.Shared.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<RentalDbContext>(o => o.UseSqlServer(builder.Configuration.GetConnectionString("CarRental") ?? "Server=localhost;Database=CarRental;Integrated Security=True;TrustServerCertificate=True"));
builder.Services.AddScoped<IBillingService, BillingService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<CarRental.Web.Areas.Fleet.Services.FleetService>();
builder.Services.AddScoped<CarRental.Web.Areas.Maintenance.Services.MaintenanceService>();
builder.Services.AddScoped<CarRental.Web.Areas.Reports.Services.ReportService>();
builder.Services.AddScoped<CarRental.Web.Areas.Reports.Services.PeriodicReportService>();
builder.Services.AddHostedService<CarRental.Web.Areas.Reports.Services.PeriodicReportWorker>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o => { o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; }; o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; }; });
builder.Services.AddAuthorization(o => o.AddPolicy("BillingAdmin", p => p.RequireAuthenticatedUser().RequireClaim(System.Security.Claims.ClaimTypes.NameIdentifier).RequireRole("Admin")));
var app = builder.Build();
app.Use(async (context, next) =>
{
    try { await next(); }
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
app.UseStaticFiles(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers(); app.Run();
public partial class Program { }

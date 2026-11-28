using System.Reflection;
using System.Security.Claims;
using CarRental.Web.Areas.Fleet.Controllers;
using CarRental.Web.Areas.Maintenance.Controllers;
using CarRental.Web.Areas.Reports.Controllers;
using CarRental.Web.Shared.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

public static class ModuleAccessChecks
{
    public static async Task Run()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddAuthorization(options =>
        {
            foreach (var permission in AdminAccess.Policies)
                options.AddPolicy(permission, policy => policy.RequireAuthenticatedUser()
                    .RequireClaim(AdminAccess.PermissionClaim, permission));
        });
        await using var provider = services.BuildServiceProvider();
        var authorization = provider.GetRequiredService<IAuthorizationService>();
        ClaimsPrincipal User(bool authenticated, params string[] permissions) => new(new ClaimsIdentity(
            permissions.Select(p => new Claim(AdminAccess.PermissionClaim, p)), authenticated ? "Test" : null));
        void Check(bool ok, string name)
        {
            if (!ok) throw new Exception(name);
            Console.WriteLine("PASS " + name);
        }
        foreach (var (controller, permission) in new[]
        {
            (typeof(FleetController), "Fleet.Access"),
            (typeof(MaintenanceController), "Maintenance.Access"),
            (typeof(ReportsController), "Reports.Access")
        })
        {
            var policy = controller.GetCustomAttribute<AuthorizeAttribute>()?.Policy;
            Check(policy == permission, controller.Name + " dùng quyền riêng");
            Check(!(await authorization.AuthorizeAsync(User(false, permission), null, policy!)).Succeeded,
                permission + " chặn người chưa đăng nhập");
            Check(!(await authorization.AuthorizeAsync(User(true, AdminAccess.Admin), null, policy!)).Succeeded,
                permission + " không chấp nhận chỉ quyền quản trị chung");
            Check(!(await authorization.AuthorizeAsync(User(true, "Billing.Access"), null, policy!)).Succeeded,
                permission + " không chấp nhận quyền thanh toán");
            Check((await authorization.AuthorizeAsync(User(true, AdminAccess.Admin, permission), null, policy!)).Succeeded,
                permission + " cho phép người được cấp quyền");
            Check(!controller.IsDefined(typeof(AllowAnonymousAttribute)) && !controller.GetMethods()
                .Any(method => method.IsDefined(typeof(AllowAnonymousAttribute))), controller.Name + " không bỏ qua xác thực");
        }
    }
}

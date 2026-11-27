using CarRental.Web.Shared.Contracts;
using Microsoft.AspNetCore.Mvc.Filters;
namespace CarRental.Web.Areas.Billing.Services;
// Only action and entity identifiers are recorded; request contents and credentials never enter this log.
public class BillingAudit(IAdminAudit audit) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (HttpMethods.IsGet(context.HttpContext.Request.Method)) { await next(); return; }
        var result = await next();
        var admin = context.HttpContext.User.AccountId();
        var action = context.ActionDescriptor.RouteValues["action"] ?? "Write";
        var entity = context.ActionDescriptor.RouteValues["controller"] ?? "Billing";
        var id = context.RouteData.Values["id"]?.ToString();
        await audit.RecordAsync(admin, action, entity, id, result.Exception == null, CancellationToken.None);
    }
}

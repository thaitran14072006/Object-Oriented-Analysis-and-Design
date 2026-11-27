using System.Security.Claims;

namespace CarRental.Web.Shared.Contracts;

public static class AdminAccess
{
    public const string PermissionClaim = "rental.permission";
    public const string SessionClaim = "rental.session";

    public const string Admin = "Admin.Access";
    public const string Security = "Admin.Security";
    public const string CustomersRead = "Customers.Read";
    public const string CustomersEdit = "Customers.Edit";
    public const string CustomersRate = "Customers.Rate";
    public const string Settings = "Settings.Personal";

    public static readonly string[] Policies =
    [
        Admin, Security, CustomersRead, CustomersEdit, CustomersRate, Settings,
        "Fleet.Access", "Bookings.Access", "RentalSchedule.Access", "Billing.Access",
        "Reports.Access", "Maintenance.Access", "Promotions.Access", "Reviews.Access"
    ];

    public static string AccountId(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("A validated admin session is required.");
}

public interface IAdminAudit
{
    Task RecordAsync(string? adminId, string action, string entityType, string? entityId,
        bool succeeded, CancellationToken cancellationToken);
}

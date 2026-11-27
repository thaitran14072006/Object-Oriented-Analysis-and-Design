namespace CarRental.Web.Areas.Bookings.Data;

public sealed class Booking
{
    public string Id { get; set; } = ""; public string Vehicle { get; set; } = ""; public string Customer { get; set; } = ""; public string RateId { get; set; } = "";
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public decimal UnitPrice { get; set; }
    public string Unit { get; set; } = "";
    public string Source { get; set; } = ""; public string Status { get; set; } = ""; public DateTime? ActualStart { get; set; }
    public DateTime? ActualEnd { get; set; }
    public long Version { get; set; }
}
public record BookingInput(string Vehicle, string Customer, string RateId, DateTime Start, DateTime End, string Source, string? PromotionCode = null);
public record ChangeState(long Version, string Action);
public record ScheduleInput(long Version, long? InvoiceVersion, DateTime Start, DateTime End, string Reason, CarRental.Web.Shared.Contracts.FeeInput[]? Fees = null);
public sealed class History { public long Id { get; set; } public DateTime OldStart { get; set; } public DateTime OldEnd { get; set; } public DateTime NewStart { get; set; } public DateTime NewEnd { get; set; } public string Reason { get; set; } = ""; public string Admin { get; set; } = ""; public DateTime Created { get; set; } }
public static class BookingRules
{
    public static bool Overlaps(DateTime a, DateTime b, DateTime c, DateTime d) => a < d && c < b;
    public static string? Next(string status, string action) => (status, action) switch { ("Pending", "confirm") => "Confirmed", ("Pending", "reject") => "Rejected", ("Pending" or "Confirmed", "cancel") => "Cancelled", ("Confirmed", "handover") => "HandedOver", ("HandedOver", "return") => "Returned", _ => null };
}

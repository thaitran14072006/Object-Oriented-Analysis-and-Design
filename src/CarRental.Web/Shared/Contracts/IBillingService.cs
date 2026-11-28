namespace CarRental.Web.Shared.Contracts;

public record QuoteRequest(string RateId, DateTime Start, DateTime End, FeeInput[] Fees, string? PromotionCode, string? Group = null, string? Season = null);
public record FeeInput(string Id, decimal Quantity);
public record InvoiceRequest(string Booking, QuoteRequest Quote, decimal? ExpectedTotal = null);
public record PaymentRequest(long Version, decimal Amount, string Kind, string Method, string RequestId, string? Reference = null, DateTime? At = null);
public record AdjustmentRequest(long Version, DateTime Start, DateTime End, FeeInput[]? Fees);
public record Cost(decimal Rent, decimal Fees, decimal Discount) { public decimal Total => Math.Max(0, Rent + Fees - Discount); }
public record BookingBalance(Cost Cost,decimal Paid,string? PromotionCode) { public decimal Remaining => Cost.Total-Paid; }
public interface IBillingService
{
    Task<Cost> Quote(QuoteRequest request);
    Task<BookingBalance> Balance(string bookingId);
    Task<BookingBalance> Preview(InvoiceRequest request);
 Task<object> PayBooking(string bookingId,PaymentRequest request,string admin);
 Task CheckUnbilled(string bookingId);
    Task<object> Apply(string bookingId, QuoteRequest request);
    Task<object> Create(InvoiceRequest request, string admin);
    Task<object> Pay(string id, PaymentRequest request, string admin);
    Task<object> Adjust(string id, AdjustmentRequest request);
}

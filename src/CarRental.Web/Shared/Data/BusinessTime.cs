namespace CarRental.Web.Shared.Data;

public static class BusinessTime
{
    public static TimeZoneInfo Zone => TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
    public static DateOnly Date(DateTime utc) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone));
}

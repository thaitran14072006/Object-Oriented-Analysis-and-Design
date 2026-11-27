namespace CarRental.Web.Areas.Reports.Data;

public sealed class PeriodicReport
{
    public long Id { get; set; }
    public string Kind { get; set; } = "HIEU_QUA_KHUYEN_MAI";
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public DateTime Created { get; set; }
    public string Path { get; set; } = "";
    public string Status { get; set; } = "ThatBai";
}

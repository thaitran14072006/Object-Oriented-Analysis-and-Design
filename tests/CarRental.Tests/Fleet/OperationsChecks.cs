using System.IO.Compression;
using System.Xml.Linq;
using CarRental.Web.Areas.Billing.Services;
using CarRental.Web.Areas.Fleet.Data;
using CarRental.Web.Areas.Fleet.Services;
using CarRental.Web.Areas.Maintenance.Services;
using CarRental.Web.Areas.Reports.Services;
using CarRental.Web.Shared.Data;

public static class OperationsChecks
{
    public static void Run()
    {
        void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }
        var input = new VehicleInput("XE1", "SEDAN", "Xe thử", "Toyota", "51A-12345", "Đen", 2024, 5, "Tự động", null, "SanSang", []);
        FleetService.Validate(input);
        Reject(() => FleetService.Validate(input with { Year = (short)(DateTime.Today.Year + 1) }), "chặn năm sản xuất tương lai");
        Reject(() => FleetService.Validate(input with { Images = ["javascript:alert(1)"] }), "chặn đường dẫn ảnh không hợp lệ");
        Reject(() => FleetService.Validate(input with { Seats = 0 }), "chặn số chỗ không hợp lệ");
        Reject(() => MaintenanceService.EnsureSafe(false), "chặn hoàn tất khi an toàn chưa đạt");
        var (from, to) = ReportService.Range(new(2026, 11, 1), new(2026, 11, 1));
        Check(from == new DateTime(2026, 10, 31, 17, 0, 0, DateTimeKind.Utc) && to - from == TimeSpan.FromDays(1), "báo cáo bao gồm đủ ngày theo giờ Việt Nam");
        Reject(() => ReportService.Range(new(2026, 11, 2), new(2026, 11, 1)), "chặn kỳ báo cáo đảo ngày");
        using var zip = new ZipArchive(new MemoryStream(SpreadsheetExport.Create(["Tên", "Số tiền"], [new object?[] { "=1+1", 12.5m }])));
        var sheet = XDocument.Load(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open());
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        Check(!sheet.Descendants(ns + "f").Any() && sheet.Descendants(ns + "t").Any(x => x.Value == "=1+1"), "Excel giữ dữ liệu nhập dưới dạng văn bản");
        Check(sheet.Descendants(ns + "v").Single().Value == "12.5", "Excel giữ giá trị số độc lập định dạng máy");
    }

    public static void Reject(Action action, string name)
    {
        try { action(); throw new Exception("Expected rejection: " + name); }
        catch (BusinessError) { Console.WriteLine("PASS " + name); }
    }
}

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
        var normalized = FleetService.Normalize(input with { Name = "  Toyota   Vios  ", Plate = " 51a-\t12345 ", Parking = "  Bãi   A  " });
        Check(normalized.Name == "Toyota Vios" && normalized.Plate == "51A-12345" && normalized.Parking == "Bãi A", "chuẩn hóa tên xe, biển số và vị trí đỗ");
        Check(FleetService.Normalize(input with { Parking = " \t " }).Parking == null, "vị trí đỗ để trống được lưu null");
        Reject(() => FleetService.Validate(input with { Name = "   " }), "chặn tên xe chỉ có khoảng trắng");
        Reject(() => FleetService.Validate(input with { Plate = "51A/@123" }), "chặn ký tự không hợp lệ trong biển số");
        Reject(() => FleetService.Validate(input with { Transmission = "ABC" }), "chặn hộp số ngoài danh sách");
        Reject(() => FleetService.Validate(input with { Parking = new string('A', 256) }), "chặn vị trí đỗ vượt 255 ký tự");
        Reject(() => FleetService.Validate(input with { Parking = "Bãi D" }), "chặn bãi ngoài danh sách");
        foreach (var parking in new[] { "Bãi A", "Bãi B", "Bãi C" })
        {
            FleetService.Validate(input with { Parking = parking });
            Check(true, "chấp nhận " + parking);
        }
        FleetService.Validate(input with { Transmission = "Số sàn" });
        Check(true, "chấp nhận hộp số sàn");
        Reject(() => FleetService.Validate(input with { Year = (short)(DateTime.Today.Year + 1) }), "chặn năm sản xuất tương lai");
        Reject(() => FleetService.Validate(input with { Year = 1999 }), "chặn năm sản xuất trước 2000");
        FleetService.Validate(input with { Year = 2000 });
        Check(true, "chấp nhận năm sản xuất 2000");
        FleetService.Validate(input with { Year = (short)DateTime.Today.Year });
        Check(true, "chấp nhận năm sản xuất hiện tại");
        Reject(() => FleetService.Validate(input with { Images = ["javascript:alert(1)"] }), "chặn đường dẫn ảnh không hợp lệ");
        Reject(() => FleetService.Validate(input with { Seats = 0 }), "chặn số chỗ không hợp lệ");
        foreach (short seats in new short[] { 1, 3, 17, 100 })
            Reject(() => FleetService.Validate(input with { Seats = seats }), "chặn số chỗ " + seats);
        foreach (short seats in new short[] { 4, 9, 16 })
        {
            FleetService.Validate(input with { Seats = seats });
            Check(true, "chấp nhận số chỗ " + seats);
        }
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

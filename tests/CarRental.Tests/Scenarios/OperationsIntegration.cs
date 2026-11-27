using System.Text.RegularExpressions;
using CarRental.Web.Areas.Billing.Services;
using CarRental.Web.Areas.Bookings.Data;
using CarRental.Web.Areas.Bookings.Services;
using CarRental.Web.Areas.Fleet.Data;
using CarRental.Web.Areas.Fleet.Services;
using CarRental.Web.Areas.Maintenance.Data;
using CarRental.Web.Areas.Maintenance.Services;
using CarRental.Web.Areas.Reports.Data;
using CarRental.Web.Areas.Reports.Services;
using CarRental.Web.Shared.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

public static class OperationsIntegration
{
    public static async Task Run()
    {
        var name = "OperationsTests_" + Guid.NewGuid().ToString("N");
        var master = Environment.GetEnvironmentVariable("CARRENTAL_TEST_MASTER")
            ?? "Server=localhost;Database=master;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=5";
        var builder = new SqlConnectionStringBuilder(master) { InitialCatalog = name };
        var cs = builder.ConnectionString;
        var folder = Path.Combine(AppContext.BaseDirectory, "operations", name);
        var environment = new TestEnvironment { ContentRootPath = folder };
        var clock = new TestClock(DateTimeOffset.UtcNow);
        RentalDbContext Db() => new(new DbContextOptionsBuilder<RentalDbContext>().UseSqlServer(cs).Options);
        async Task Sql(string connection, string sql)
        {
            await using var c = new SqlConnection(connection); await c.OpenAsync();
            foreach (var batch in Regex.Split(sql, @"^GO\s*$", RegexOptions.Multiline))
            { if (string.IsNullOrWhiteSpace(batch)) continue; using var command = new SqlCommand(batch, c); await command.ExecuteNonQueryAsync(); }
        }
        async Task Reject(Func<Task> action, string label)
        {
            try { await action(); throw new Exception("Expected rejection: " + label); }
            catch (BusinessError) { Console.WriteLine("PASS " + label); }
        }
        void Check(bool ok, string label) { if (!ok) throw new Exception(label); Console.WriteLine("PASS " + label); }
        await Sql(master, $"CREATE DATABASE [{name}]");
        try
        {
            await Sql(cs, "CREATE TABLE TaiKhoan(maTaiKhoan varchar(20) PRIMARY KEY); INSERT TaiKhoan VALUES('ADMIN'); CREATE TABLE NhomKhachHang(maNhom varchar(20) PRIMARY KEY);");
            foreach (var module in new[] { "02_fleet", "04_pricing", "05_bookings", "06_billing", "07_maintenance", "08_promotions", "09_reviews", "10_reports" })
                await Sql(cs, await File.ReadAllTextAsync($"database/migrations/{module}/001_schema.sql"));
            await Sql(cs, "INSERT LoaiXe VALUES('SEDAN',N'Sedan','B');");
            var input = new VehicleInput("XE1", "SEDAN", "Xe thử", "Toyota", "51A-12345", "Đen", 2024, 5, "Tự động", "Bãi A", "SanSang", ["https://example.com/car.png"]);
            await using (var db = Db())
            {
                var fleet = new FleetService(db); await fleet.Save(input, true);
                Check(await db.Set<VehicleImage>().CountAsync() == 1, "lưu hồ sơ xe và ảnh");
            }
            await using (var db = Db()) { await new FleetService(db).Save(input with { Name = "Xe cập nhật", Images = [] }, false); Check(!await db.Set<VehicleImage>().AnyAsync(), "sửa hồ sơ và danh sách ảnh"); }
            await using (var db = Db()) { Check(await new FleetService(db).Filter("51A", null, "SEDAN").CountAsync() == 1, "lọc xe theo biển số và loại"); }
            await using (var db = Db()) { await Reject(() => new FleetService(db).Save(input with { Id = "XE2" }, true), "chặn trùng biển số"); }
            await using (var db = Db()) { await new FleetService(db).Save(input with { Id = "TEMP", Plate = "TEMP" }, true); Check(!await new FleetService(db).Delete("TEMP"), "xóa vật lý xe chưa có lịch sử"); }

            var now = clock.GetUtcNow().UtcDateTime;
            var future = new MaintenanceInput("XE1", "DinhKy", now.AddHours(2), now.AddHours(3), "Bảo dưỡng định kỳ");
            string maintenanceId;
            await using (var db = Db()) { maintenanceId = await new MaintenanceService(db, clock).Create(future, "ADMIN"); Check((await db.Set<Vehicle>().SingleAsync()).Status == "SanSang", "lập lịch tương lai không đổi tình trạng xe"); }
            await using (var db = Db()) { await Reject(() => new MaintenanceService(db, clock).Create(future with { Start = now.AddHours(2.5) }, "ADMIN"), "chặn hai lịch bảo trì trùng"); }
            await using (var db = Db()) { await Reject(async () => { await using var tx = await db.Database.BeginTransactionAsync(); await new CarRental.Web.Shared.Contracts.VehicleAvailability(db).Ensure("XE1", future.Start, future.End); }, "đặt xe kiểm tra lịch bảo trì"); }
            await using (var db = Db()) { var availability = new CarRental.Web.Shared.Contracts.VehicleAvailability(db); await availability.Ensure("XE1", future.End, future.End.AddHours(1)); Console.WriteLine("PASS lịch tiếp giáp không trùng"); }
            clock.Advance(TimeSpan.FromHours(2));
            await using (var db = Db()) { await new MaintenanceService(db, clock).Change(maintenanceId, "start", "ADMIN"); Check((await db.Set<Vehicle>().SingleAsync()).Status == "DangBaoTri", "bắt đầu bảo trì cập nhật xe"); }
            await using (var db = Db()) { await Reject(() => new MaintenanceService(db, clock).Change(maintenanceId, "complete", "ADMIN"), "không hoàn tất khi chưa kiểm tra an toàn"); }
            await using (var db = Db()) { var s = new MaintenanceService(db, clock); await s.Change(maintenanceId, "progress", "ADMIN", progress: new("Đã thay dầu")); }
            var costDate = BusinessTime.Date(clock.GetUtcNow().UtcDateTime);
            await using (var db = Db()) { await new MaintenanceService(db, clock).Change(maintenanceId, "cost", "ADMIN", cost: new("Dầu máy", "Xưởng A", 250000, costDate)); }
            await using (var db = Db()) { await new MaintenanceService(db, clock).Change(maintenanceId, "safety", "ADMIN", safety: new("Kỹ thuật viên", true, null)); }
            await using (var db = Db()) { await new MaintenanceService(db, clock).Change(maintenanceId, "safety", "ADMIN", safety: new("Kỹ thuật viên", false, "Cần kiểm tra lại")); }
            await using (var db = Db()) { await Reject(() => new MaintenanceService(db, clock).Change(maintenanceId, "complete", "ADMIN"), "lấy kết quả an toàn mới nhất thay vì lần từng đạt"); }
            await using (var db = Db()) { await new MaintenanceService(db, clock).Change(maintenanceId, "safety", "ADMIN", safety: new("Kỹ thuật viên", true, null)); }
            await using (var db = Db()) { await new MaintenanceService(db, clock).Change(maintenanceId, "complete", "ADMIN"); Check((await db.Set<Vehicle>().SingleAsync()).Status == "SanSang", "hoàn tất trả xe về sẵn sàng"); }
            await using (var db = Db()) { await Reject(() => new MaintenanceService(db, clock).Change(maintenanceId, "cost", "ADMIN", cost: new("Thêm", "Xưởng A", 1, costDate)), "không sửa chi phí phiếu đã hoàn tất"); }

            await Sql(cs, $"""
                INSERT MucGiaThue(maMucGia,maLoaiXe,donViTinh,hinhThuc,muaVu,donGia,batDau,ketThuc,conSuDung)
                  VALUES('R1','SEDAN','NGAY','NGAN_HAN','MAC_DINH',100,'2020-01-01','2099-01-01',1);
                """);
            await using (var db = Db())
            {
                var s = new BookingService(db, new BillingService(db));
                await s.Create(new("XE1", "CUSTOMER", "R1", now.AddDays(10), now.AddDays(11), "TrucTiep"), "ADMIN");
            }
            await using (var db = Db()) { await Reject(() => new MaintenanceService(db, clock).Create(new("XE1", "SuaChua", now.AddDays(10), now.AddDays(11), "Sửa xe"), "ADMIN"), "bảo trì kiểm tra lịch thuê"); }
            await using (var db = Db()) { await Reject(() => new FleetService(db).Delete("XE1"), "không xóa xe còn đơn đang giữ lịch"); }

            await using (var db = Db()) { await new FleetService(db).Save(input with { Id = "RACE", Plate = "RACE" }, true); }
            var raceStart = now.AddDays(20);
            async Task<bool> Race(bool maintenance)
            {
                await using var db = Db();
                try
                {
                    if (maintenance) await new MaintenanceService(db, clock).Create(new("RACE", "DinhKy", raceStart, raceStart.AddHours(2), "Kiểm tra"), "ADMIN");
                    else await new BookingService(db, new BillingService(db)).Create(new("RACE", "CUSTOMER", "R1", raceStart, raceStart.AddHours(2), "TrucTiep"), "ADMIN");
                    return true;
                }
                catch (BusinessError) { return false; }
                catch (SqlException ex) when (ex.Number == 1205) { return false; }
            }
            Check((await Task.WhenAll(Race(true), Race(false))).Count(x => x) == 1, "đặt xe và bảo trì đồng thời chỉ một bên giữ lịch");

            await using (var db = Db()) { await new FleetService(db).Save(input with { Id = "HANDOVER", Plate = "HANDOVER" }, true); }
            Booking handover;
            await using (var db = Db()) { handover = await new BookingService(db, new BillingService(db)).Create(new("HANDOVER", "CUSTOMER", "R1", DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddHours(1), "TrucTiep"), "ADMIN"); }
            await using (var db = Db()) { handover = await new BookingService(db, new BillingService(db)).State(handover.Id, new(handover.Version, "confirm")); }
            await using (var db = Db()) { handover = await new BookingService(db, new BillingService(db)).State(handover.Id, new(handover.Version, "handover")); Check((await db.Set<Vehicle>().SingleAsync(x => x.Id == "HANDOVER")).Status == "DangChoThue", "giao xe cập nhật tình trạng đội xe"); }
            await using (var db = Db()) { await Reject(() => new MaintenanceService(db, TimeProvider.System).Create(new("HANDOVER", "SuaChua", DateTime.UtcNow.AddMinutes(1), DateTime.UtcNow.AddHours(2), "Sửa xe"), "ADMIN"), "không nhận bảo trì khi xe chưa trả"); }
            await using (var db = Db()) { await new BookingService(db, new BillingService(db)).State(handover.Id, new(handover.Version, "return")); Check((await db.Set<Vehicle>().SingleAsync(x => x.Id == "HANDOVER")).Status == "SanSang", "nhận trả cập nhật tình trạng đội xe"); }

            await Sql(cs, "UPDATE DonThue SET trangThai='Cancelled' WHERE nhanThucTe IS NULL;");
            var from = DateOnly.FromDateTime(now).AddDays(-1); var to = from.AddDays(4);
            await using (var db = Db())
            {
                var reports = new ReportService(db, clock);
                foreach (var type in new[] { "usage", "revenue", "fleet", "maintenance", "quality", "promotions", "channels" })
                { var result = await reports.Build(type, from, to); Check(result != null, "truy vấn báo cáo " + type); }
                var maintenance = await reports.Build("maintenance", from, to);
                Check(maintenance.Rows.Single().Amount == 250000, "báo cáo chi phí khớp dữ liệu gốc");
                Check(!(await reports.Build("revenue", from, to)).Rows.Any(), "kỳ không có giao dịch trả kết quả rỗng");
            }
            await Sql(cs, """
                INSERT DonThue(maDon,maXe,maKhachHang,maMucGia,nhanDuKien,traDuKien,donGiaApDung,donViApDung,nguon,trangThai,maAdmin,nhanThucTe,traThucTe)
                  VALUES('DREPORT','XE1','CUSTOMER','R1','2026-11-01','2026-11-02',100,'NGAY','DienThoai','Returned','ADMIN','2026-11-01','2026-11-02');
                INSERT GiaoDichThanhToan VALUES('T1','DREPORT','ADMIN','DAT_COC','TIEN_MAT',30,NULL,'2026-10-31T17:00:00');
                INSERT GiaoDichThanhToan VALUES('T2','DREPORT','ADMIN','THANH_TOAN','TIEN_MAT',70,NULL,'2026-11-01T16:59:59');
                INSERT GiaoDichThanhToan VALUES('T3','DREPORT','ADMIN','THANH_TOAN','TIEN_MAT',10,NULL,'2026-11-01T17:00:00');
                INSERT KhuyenMai(maKhuyenMai,tenChuongTrinh,hinhThucGiam,mucGiam,batDau,ketThuc,trangThai) VALUES('KM1',N'Chương trình thử','SO_TIEN',10,'2026-01-01','2027-01-01','DangApDung');
                INSERT ApDungKhuyenMai VALUES('DREPORT','KM1','SO_TIEN',10,10,'2026-11-01');
                INSERT HoaDon VALUES('H1','DREPORT','ADMIN','2026-11-02',100,0,10,90,0);
                INSERT DanhGiaDichVu(maDon,vehicleScore,serviceScore,feedback,created,admin) VALUES('DREPORT',4,5,N'Tốt','2026-11-02','ADMIN');
                """);
            await using (var db = Db())
            {
                var s = new ReportService(db, clock);
                var revenue = await s.Build("revenue", new(2026, 11, 1), new(2026, 11, 1));
                Check(revenue.Rows.Single().Amount == 100 && revenue.Rows.Single().Secondary == 30, "doanh thu đúng hai biên ngày và phân biệt đặt cọc");
                Check((await s.Build("promotions", new(2026, 11, 1), new(2026, 11, 3))).Rows.Single().Secondary == 90, "khuyến mãi đối chiếu hóa đơn thực tế");
                Check((await s.Build("quality", new(2026, 11, 1), new(2026, 11, 3))).Rows.Single().Secondary == 5, "điểm dịch vụ lấy từ đánh giá nguồn");
                var usage = (await s.Build("usage", new(2026, 11, 1), new(2026, 11, 2))).Rows.Single(x => x.Label.StartsWith("XE1 -"));
                Check(usage.Amount == 24 && usage.Rate == 50, "mức sử dụng tính từ thời gian giao trả thực tế");
            }
            clock.Advance(TimeSpan.FromDays(40));
            async Task Generate() { await using var db = Db(); await new PeriodicReportService(db, new ReportService(db, clock), environment, clock).Generate(new(2026, 11, 1), new(2026, 11, 30)); }
            await Task.WhenAll(Generate(), Generate());
            await using (var db = Db())
            {
                Check(await db.Set<PeriodicReport>().CountAsync() == 1, "hai tác vụ cùng kỳ chỉ tạo một báo cáo");
                var s = new PeriodicReportService(db, new ReportService(db, clock), environment, clock);
                var record = await db.Set<PeriodicReport>().SingleAsync();
                Check((await s.Download(record.Id)).Bytes.Length > 0, "tải được báo cáo định kỳ");
                File.Delete(Path.Combine(folder, "App_Data", "Reports", record.Path));
            }
            await Generate();
            await using (var db = Db()) { Check(await db.Set<PeriodicReport>().CountAsync() == 1, "tạo lại tệp bị thiếu giữ nguyên bản ghi kỳ"); }
            await using (var db = Db())
            {
                var report = await db.Set<PeriodicReport>().SingleAsync(); report.Status = "ThatBai"; await db.SaveChangesAsync();
            }
            await Generate();
            await using (var db = Db()) { Check((await db.Set<PeriodicReport>().SingleAsync()).Status == "ThanhCong", "thử lại kỳ báo cáo thất bại"); }
            await using (var db = Db()) { Check(await new FleetService(db).Delete("XE1"), "xe có lịch sử được lưu trữ"); Check(!await db.Set<VehicleImage>().AnyAsync(x => x.VehicleId == "XE1") && await db.Set<MaintenanceRecord>().AnyAsync(x => x.VehicleId == "XE1"), "giữ lịch sử bảo trì khi lưu trữ xe"); }
            Console.WriteLine("Operations integration complete");
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await Sql(master, $"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]");
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan elapsed) => now += elapsed;
    }
    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Tests";
        public string EnvironmentName { get; set; } = "Testing";
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

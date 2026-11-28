using System.Data;
using CarRental.Web.Areas.Fleet.Data;
using CarRental.Web.Areas.Maintenance.Data;
using CarRental.Web.Areas.Billing.Services;
using CarRental.Web.Shared.Contracts;
using CarRental.Web.Shared.Data;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Web.Areas.Maintenance.Services;

public sealed class MaintenanceService(RentalDbContext db, TimeProvider clock)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<object> Vehicles() => await db.Set<Vehicle>().AsNoTracking()
        .Where(x => !x.Archived && x.Status != "NgungKhaiThac")
        .OrderBy(x => x.Id).Select(x => new { x.Id, x.Name, x.Plate, x.Status }).ToListAsync();

    public async Task<object> List(string? q, string? status, string? vehicle)
    {
        var rows = from record in db.Set<MaintenanceRecord>().AsNoTracking()
                   join car in db.Set<Vehicle>() on record.VehicleId equals car.Id
                   where (q == null || record.Id.Contains(q) || car.Name.Contains(q) || car.Plate.Contains(q) || record.Content.Contains(q))
                     && (status == null || record.Status == status) && (vehicle == null || record.VehicleId == vehicle)
                   orderby record.Start descending, record.Id
                   select new { record.Id, record.VehicleId, VehicleName = car.Name, car.Plate, record.Kind,
                       record.Start, record.End, record.ActualStart, record.ActualEnd, record.Content, record.Status,
                       Cost = db.Set<MaintenanceCost>().Where(x => x.MaintenanceId == record.Id).Sum(x => (decimal?)x.Amount) ?? 0 };
        return await rows.ToListAsync();
    }

    public async Task<object> Detail(string id)
    {
        var record = await db.Set<MaintenanceRecord>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new BusinessError("Không tìm thấy lịch bảo trì.");
        var progress = await db.Set<MaintenanceProgress>().AsNoTracking().Where(x => x.MaintenanceId == id).OrderByDescending(x => x.Created).ThenByDescending(x => x.Id).ToListAsync();
        var costs = await db.Set<MaintenanceCost>().AsNoTracking().Where(x => x.MaintenanceId == id).OrderByDescending(x => x.Date).ThenByDescending(x => x.Id).ToListAsync();
        var safety = await db.Set<SafetyCheck>().AsNoTracking().Where(x => x.MaintenanceId == id).OrderByDescending(x => x.Created).ThenByDescending(x => x.Id).ToListAsync();
        return new { record, progress, costs, safety };
    }

    public async Task<string> Create(MaintenanceInput input, string admin)
    {
        BillingService.Require(input.Start >= Now.AddMinutes(-1) && input.End > input.Start, "Chọn lịch từ thời điểm hiện tại, kết thúc sau bắt đầu.");
        BillingService.Require(input.Kind is "DinhKy" or "SuaChua" or "NangCap" && !string.IsNullOrWhiteSpace(input.Content), "Thông tin bảo trì không hợp lệ.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var availability = new VehicleAvailability(db);
        await availability.Lock(input.VehicleId);
        await availability.Ensure(input.VehicleId, input.Start, input.End);
        var id = "BT" + Guid.NewGuid().ToString("N")[..18];
        db.Add(new MaintenanceRecord { Id = id, VehicleId = input.VehicleId, Creator = admin, Kind = input.Kind,
            Start = input.Start, End = input.End, Content = input.Content.Trim() });
        await db.SaveChangesAsync(); await transaction.CommitAsync();
        return id;
    }

    public async Task Change(string id, string action, string admin, ProgressInput? progress = null, MaintenanceCostInput? cost = null, SafetyInput? safety = null)
    {
        var vehicleId = await db.Set<MaintenanceRecord>().Where(x => x.Id == id).Select(x => x.VehicleId).SingleOrDefaultAsync()
            ?? throw new BusinessError("Không tìm thấy lịch bảo trì.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var availability = new VehicleAvailability(db);
        await availability.Lock(vehicleId);
        var record = await db.Set<MaintenanceRecord>().SingleAsync(x => x.Id == id);
        var vehicle = await db.Set<Vehicle>().SingleAsync(x => x.Id == vehicleId);
        BillingService.Require(record.Status != "HoanTat", "Phiếu đã hoàn tất, không thể chỉnh sửa.");
        var now = Now;
        if (action == "start")
        {
            BillingService.Require(record.Status == "ChuaThucHien" && now >= record.Start && now < record.End, "Chưa đến lịch, đã hết lịch hoặc phiếu đã bắt đầu.");
            BillingService.Require(vehicle.Status == "SanSang" && !vehicle.Archived, "Xe chưa sẵn sàng để bảo trì.");
            await availability.Ensure(vehicleId, now, record.End, id, handingOver: true);
            record.Status = "DangThucHien"; record.ActualStart = now; vehicle.Status = "DangBaoTri";
        }
        else
        {
            BillingService.Require(record.Status == "DangThucHien", "Cần bắt đầu bảo trì trước khi ghi nhận.");
            switch (action)
            {
                case "progress":
                    BillingService.Require(!string.IsNullOrWhiteSpace(progress?.Content), "Nhập nội dung tiến độ.");
                    db.Add(new MaintenanceProgress { MaintenanceId = id, Recorder = admin, Content = progress!.Content.Trim(), Created = now });
                    break;
                case "cost":
                    BillingService.Require(cost != null && cost.Amount >= 0 && !string.IsNullOrWhiteSpace(cost.Item) && !string.IsNullOrWhiteSpace(cost.Provider)
                        && cost.Date <= BusinessTime.Date(now) && cost.Date >= BusinessTime.Date(record.ActualStart!.Value), "Chi phí hoặc ngày ghi nhận không hợp lệ.");
                    db.Add(new MaintenanceCost { MaintenanceId = id, Item = cost!.Item.Trim(), Provider = cost.Provider.Trim(), Amount = cost.Amount, Date = cost.Date });
                    break;
                case "safety":
                    BillingService.Require(!string.IsNullOrWhiteSpace(safety?.Inspector), "Nhập tên người kiểm tra.");
                    db.Add(new SafetyCheck { MaintenanceId = id, Recorder = admin, Inspector = safety!.Inspector.Trim(), Passed = safety.Passed, Note = safety.Note, Created = now });
                    break;
                case "complete":
                    var latest = await db.Set<SafetyCheck>().Where(x => x.MaintenanceId == id).OrderByDescending(x => x.Created).ThenByDescending(x => x.Id).FirstOrDefaultAsync();
                    EnsureSafe(latest?.Passed == true);
                    BillingService.Require(vehicle.Status == "DangBaoTri", "Tình trạng xe đã thay đổi, cần kiểm tra lại.");
                    record.Status = "HoanTat"; record.ActualEnd = now; vehicle.Status = "SanSang";
                    break;
                default: throw new BusinessError("Thao tác không hợp lệ.");
            }
        }
        await db.SaveChangesAsync(); await transaction.CommitAsync();
    }

    public static void EnsureSafe(bool latestPassed) => BillingService.Require(latestPassed, "Lần kiểm tra an toàn mới nhất phải đạt trước khi hoàn tất.");
}

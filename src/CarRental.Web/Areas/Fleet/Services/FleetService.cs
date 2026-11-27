using System.Data;
using CarRental.Web.Areas.Fleet.Data;
using CarRental.Web.Areas.Maintenance.Data;
using CarRental.Web.Areas.Billing.Services;
using CarRental.Web.Shared.Contracts;
using CarRental.Web.Shared.Data;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Web.Areas.Fleet.Services;

public sealed class FleetService(RentalDbContext db)
{
    public IQueryable<Vehicle> Filter(string? query, string? status, string? type, bool archived = false)
    {
        var rows = db.Set<Vehicle>().AsNoTracking().Where(x => x.Archived == archived);
        if (!string.IsNullOrWhiteSpace(query))
        {
            query = query.Trim();
            rows = rows.Where(x => x.Id.Contains(query) || x.Name.Contains(query) || x.Plate.Contains(query)
                || db.Set<VehicleType>().Any(t => t.Id == x.TypeId && t.Name.Contains(query)));
        }
        if (!string.IsNullOrWhiteSpace(status)) rows = rows.Where(x => x.Status == status);
        if (!string.IsNullOrWhiteSpace(type)) rows = rows.Where(x => x.TypeId == type);
        return rows.OrderBy(x => x.Id);
    }

    public Task<List<VehicleType>> Types() => db.Set<VehicleType>().AsNoTracking().OrderBy(x => x.Name).ToListAsync();

    public async Task<object> Detail(string id)
    {
        var vehicle = await db.Set<Vehicle>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new BusinessError("Không tìm thấy xe.");
        var images = await db.Set<VehicleImage>().AsNoTracking().Where(x => x.VehicleId == id).OrderBy(x => x.Order).ToListAsync();
        return new { vehicle, images };
    }

    public static void Validate(VehicleInput input)
    {
        BillingService.Require(input.Year >= 1900 && input.Year <= DateTime.Today.Year, "Năm sản xuất không hợp lệ.");
        BillingService.Require(input.Seats is > 0 and <= 100 && input.Status is "SanSang" or "NgungKhaiThac", "Thông tin xe không hợp lệ.");
        BillingService.Require(new[] { input.Name, input.TypeId, input.Brand, input.Plate, input.Color, input.Transmission }.All(x => !string.IsNullOrWhiteSpace(x)), "Nhập đủ thông tin xe.");
        BillingService.Require(input.Images != null && input.Images.Length <= 12, "Mỗi xe có tối đa 12 ảnh.");
        foreach (var url in input.Images!)
            BillingService.Require(url != null && url.Length <= 500 && Uri.TryCreate(url, UriKind.Absolute, out var uri)
                && uri.Scheme is "https" or "http", "Đường dẫn ảnh phải là địa chỉ HTTP hoặc HTTPS hợp lệ.");
    }

    public async Task Save(VehicleInput input, bool creating)
    {
        Validate(input);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await new VehicleAvailability(db).Lock(input.Id);
        var vehicle = await db.Set<Vehicle>().SingleOrDefaultAsync(x => x.Id == input.Id);
        BillingService.Require(creating ? vehicle == null : vehicle != null, creating ? "Mã xe đã tồn tại." : "Không tìm thấy xe.");
        if (vehicle != null)
        {
            BillingService.Require(!vehicle.Archived && vehicle.Status is "SanSang" or "NgungKhaiThac", "Không thể sửa xe đang thuê, bảo trì hoặc đã lưu trữ.");
            if (input.Status == "NgungKhaiThac") await EnsureNoActiveWork(input.Id);
        }
        BillingService.Require(await db.Set<VehicleType>().AnyAsync(x => x.Id == input.TypeId), "Loại xe không tồn tại.");
        var plate = input.Plate.Trim().ToUpperInvariant().Replace(" ", "");
        BillingService.Require(!await db.Set<Vehicle>().AnyAsync(x => x.Id != input.Id && x.Plate == plate), "Biển số đã tồn tại.");
        if (vehicle == null) { vehicle = new Vehicle { Id = input.Id }; db.Add(vehicle); }
        vehicle.TypeId = input.TypeId; vehicle.Name = input.Name.Trim(); vehicle.Brand = input.Brand.Trim();
        vehicle.Plate = plate; vehicle.Color = input.Color.Trim(); vehicle.Year = input.Year; vehicle.Seats = input.Seats;
        vehicle.Transmission = input.Transmission.Trim(); vehicle.Status = input.Status; vehicle.Parking = input.Parking?.Trim();
        var images = await db.Set<VehicleImage>().Where(x => x.VehicleId == input.Id).ToListAsync();
        db.RemoveRange(images);
        db.AddRange(input.Images.Select((url, index) => new VehicleImage { VehicleId = input.Id, Url = url, Order = index }));
        await db.SaveChangesAsync(); await transaction.CommitAsync();
    }

    private async Task EnsureNoActiveWork(string id)
    {
        var active = await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM DonThue WHERE maXe={id} AND trangThai IN ('Pending','Confirmed','HandedOver')").SingleAsync();
        BillingService.Require(active == 0 && !await db.Set<MaintenanceRecord>().AnyAsync(x => x.VehicleId == id && x.Status != "HoanTat"), "Xe còn đơn thuê hoặc lịch bảo trì chưa kết thúc.");
    }

    public async Task<bool> Delete(string id)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await new VehicleAvailability(db).Lock(id);
        var vehicle = await db.Set<Vehicle>().SingleOrDefaultAsync(x => x.Id == id && !x.Archived) ?? throw new BusinessError("Không tìm thấy xe.");
        BillingService.Require(vehicle.Status is "SanSang" or "NgungKhaiThac", "Xe đang được sử dụng.");
        await EnsureNoActiveWork(id);
        var bookings = await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM DonThue WHERE maXe={id}").SingleAsync();
        var archived = bookings > 0 || await db.Set<MaintenanceRecord>().AnyAsync(x => x.VehicleId == id);
        if (archived) { vehicle.Archived = true; vehicle.Status = "NgungKhaiThac"; }
        else
        {
            db.RemoveRange(await db.Set<VehicleImage>().Where(x => x.VehicleId == id).ToListAsync());
            db.Remove(vehicle);
        }
        await db.SaveChangesAsync(); await transaction.CommitAsync();
        return archived;
    }
}

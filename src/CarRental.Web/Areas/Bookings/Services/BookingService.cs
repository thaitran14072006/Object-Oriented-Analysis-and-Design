using System.Data;
using CarRental.Web.Areas.Bookings.Data;
using CarRental.Web.Areas.Billing.Services;
using CarRental.Web.Shared.Data;
using CarRental.Web.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
namespace CarRental.Web.Areas.Bookings.Services;

public sealed class BookingService(RentalDbContext db, IBillingService billing)
{
    static void Require(bool ok, string message) => BillingService.Require(ok, message);
    const string Columns = "maDon AS Id,maXe AS Vehicle,maKhachHang AS Customer,maMucGia AS RateId,nhanDuKien AS Start,traDuKien AS [End],donGiaApDung AS UnitPrice,donViApDung AS Unit,nguon AS Source,trangThai AS Status,nhanThucTe AS ActualStart,traThucTe AS ActualEnd,phienBan AS Version";
    IQueryable<Booking> Rows() => db.Database.SqlQueryRaw<Booking>("SELECT " + Columns + " FROM DonThue");
    public async Task<List<Booking>> List() => await Rows().OrderBy(x => x.Start).ToListAsync();
    public async Task<Booking> Get(string id) => await Rows().SingleOrDefaultAsync(x => x.Id == id) ?? throw new BusinessError("Không tìm thấy đơn.");
    // Đặt xe và bảo trì dùng chung khóa theo xe.
    Task Lock(string vehicle) => new VehicleAvailability(db).Lock(vehicle);
    async Task Available(string vehicle, DateTime start, DateTime end, string exclude = "")
    {
        Require(start < end, "Khoảng thuê không hợp lệ.");
        Require(!await Rows().AnyAsync(x => x.Vehicle == vehicle && x.Id != exclude && (x.Status == "Pending" || x.Status == "Confirmed" || x.Status == "HandedOver") && x.Start < end && start < x.End), "Xe đã có lịch thuê trùng.");
        await new VehicleAvailability(db).Ensure(vehicle, start, end, excludeBooking: exclude);
    }
    public async Task<Booking> Create(BookingInput input, string admin)
    {
        Require(!string.IsNullOrWhiteSpace(input.Vehicle) && !string.IsNullOrWhiteSpace(input.Customer) && input.Source is "TrucTiep" or "DienThoai" or "LienKet", "Thông tin đơn không hợp lệ.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable); await Lock(input.Vehicle); await Available(input.Vehicle, input.Start, input.End);
        var rate = await db.Rates.FindAsync(input.RateId) ?? throw new BusinessError("Không tìm thấy mức giá."); await billing.Quote(new(input.RateId, input.Start, input.End, [], null)); var vehicle = await db.Set<CarRental.Web.Areas.Fleet.Data.Vehicle>().AsNoTracking().SingleAsync(x => x.Id == input.Vehicle); Require(vehicle.TypeId == rate.Vehicle, "Mức giá không thuộc loại xe đã chọn.");
        var id = "D" + Guid.NewGuid().ToString("N")[..19];
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT DonThue(maDon,maXe,maKhachHang,maMucGia,nhanDuKien,traDuKien,donGiaApDung,donViApDung,nguon,trangThai,maAdmin) VALUES ({id},{input.Vehicle},{input.Customer},{input.RateId},{input.Start},{input.End},{rate.Amount},{rate.Unit},{input.Source},'Pending',{admin})"); if (!string.IsNullOrWhiteSpace(input.PromotionCode)) await billing.Apply(id, new(input.RateId, input.Start, input.End, [], input.PromotionCode)); await tx.CommitAsync(); return await Get(id);
    }
    public async Task<Booking> State(string id, ChangeState input)
    {
        var old = await Get(id); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable); await Lock(old.Vehicle); var b = await Get(id); Require(b.Version == input.Version, "Phiên bản đơn đã thay đổi.");
        var next = BookingRules.Next(b.Status, input.Action) ?? throw new BusinessError("Chuyển trạng thái không hợp lệ."); var now = DateTime.UtcNow;
        if (next == "Cancelled" || next == "Rejected") Require(!await db.Payments.AnyAsync(x => x.Booking == id), "Đơn đã thu tiền, cần xử lý hoàn tiền trước.");
        if (next == "Confirmed" || next == "HandedOver") await Available(b.Vehicle, b.Start, b.End, id);
        if (next == "HandedOver") { Require(now >= b.Start && now < b.End, "Chưa đến hoặc đã hết lịch giao xe."); await new VehicleAvailability(db).Ensure(b.Vehicle, now, b.End, excludeBooking: id, handingOver: true); Require(!await Rows().AnyAsync(x => x.Vehicle == b.Vehicle && x.Id != id && x.Status == "HandedOver" && x.ActualEnd == null), "Xe chưa được trả thực tế."); }
        DateTime? actualStart = next == "HandedOver" ? now : b.ActualStart; DateTime? actualEnd = next == "Returned" ? now : b.ActualEnd;
        var n = await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE DonThue SET trangThai={next},nhanThucTe={actualStart},traThucTe={actualEnd},phienBan=phienBan+1 WHERE maDon={id} AND phienBan={input.Version}"); Require(n == 1, "Phiên bản đơn đã thay đổi.");
        if (next == "HandedOver") { var changed = await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Xe SET tinhTrang='DangChoThue' WHERE maXe={b.Vehicle} AND tinhTrang='SanSang' AND daXoa=0"); Require(changed == 1, "Xe chưa sẵn sàng để bàn giao."); }
        if (next == "Returned") await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Xe SET tinhTrang='SanSang' WHERE maXe={b.Vehicle} AND tinhTrang='DangChoThue'");
        await tx.CommitAsync(); return await Get(id);
    }
    public async Task<List<History>> History(string id) => await db.Database.SqlQuery<History>($"SELECT Id,oldStart,oldEnd,newStart,newEnd,reason,admin,created FROM LichSuDieuChinh WHERE maDon={id}").OrderByDescending(x => x.Created).ToListAsync();
    public async Task<Booking> Reschedule(string id, ScheduleInput input, string admin)
    {
        var old = await Get(id); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable); await Lock(old.Vehicle); var b = await Get(id);
        Require(b.Version == input.Version && b.Status is "Pending" or "Confirmed" or "HandedOver", "Đơn không thể điều chỉnh hoặc phiên bản cũ."); Require(!string.IsNullOrWhiteSpace(input.Reason) && input.Reason.Length <= 500, "Cần lý do điều chỉnh.");
        if (b.Status == "HandedOver") Require(input.Start == b.Start && input.End > b.End, "Xe đang thuê chỉ được gia hạn."); await Available(b.Vehicle, input.Start, input.End, id);
        var n = await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE DonThue SET nhanDuKien={input.Start},traDuKien={input.End},phienBan=phienBan+1 WHERE maDon={id} AND phienBan={input.Version}"); Require(n == 1, "Phiên bản cũ.");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT LichSuDieuChinh(maDon,oldStart,oldEnd,newStart,newEnd,reason,admin,created) VALUES ({id},{b.Start},{b.End},{input.Start},{input.End},{input.Reason},{admin},{DateTime.UtcNow})");
        var invoice = await db.Invoices.AsNoTracking().SingleOrDefaultAsync(x => x.Booking == id); if (invoice != null) { Require(input.InvoiceVersion.HasValue, "Cần phiên bản hóa đơn."); await billing.Adjust(invoice.Id, new(input.InvoiceVersion!.Value, input.Start, input.End, input.Fees)); } else { await billing.CheckUnbilled(id); }
        await tx.CommitAsync(); return await Get(id);
    }
}

using CarRental.Web.Areas.Fleet.Data;
using CarRental.Web.Areas.Maintenance.Data;
using CarRental.Web.Areas.Billing.Services;
using CarRental.Web.Shared.Data;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Web.Shared.Contracts;

public sealed class VehicleAvailability(RentalDbContext db)
{
    public Task Lock(string vehicle)
    {
        var key = "CarRental:Vehicle:" + vehicle.ToUpperInvariant();
        return db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r=sp_getapplock @Resource={key},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 51000,'Vehicle lock failed',1;");
    }

    public async Task Ensure(string vehicle, DateTime start, DateTime end, string excludeMaintenance = "", string excludeBooking = "", bool handingOver = false)
    {
        BillingService.Require(start < end, "Khoảng thời gian không hợp lệ.");
        var car = await db.Set<Vehicle>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == vehicle);
        BillingService.Require(car != null && !car.Archived && car.Status != "NgungKhaiThac", "Xe không còn được khai thác.");
        var now = DateTime.UtcNow;
        var conflicts = await db.Set<MaintenanceRecord>().AnyAsync(x => x.VehicleId == vehicle && x.Id != excludeMaintenance
            && x.Status != "HoanTat" && ((x.Start < end && start < x.End)
                || (x.Status == "DangThucHien" && (handingOver || x.End <= now))));
        BillingService.Require(!conflicts, "Xe có lịch bảo trì trùng hoặc đang bảo trì.");
        var bookings = await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS [Value] FROM DonThue WHERE maXe={vehicle} AND maDon<>{excludeBooking}
              AND trangThai IN ('Pending','Confirmed','HandedOver')
              AND ((nhanDuKien<{end} AND {start}<traDuKien)
                OR (trangThai='HandedOver' AND traThucTe IS NULL AND ({handingOver}=1 OR traDuKien<={DateTime.UtcNow})))
            """).SingleAsync();
        BillingService.Require(bookings == 0, "Xe đã có lịch thuê trùng hoặc chưa được trả.");
    }
}

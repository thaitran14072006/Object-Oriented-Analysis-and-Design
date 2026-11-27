using CarRental.Web.Areas.Fleet.Data;
using CarRental.Web.Shared.Data;
using CarRental.Web.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
namespace CarRental.Web.Areas.Billing.Services;

public record PricedVehicle(string VehicleId, string Name, string Plate, string RateId, string Unit, decimal UnitPrice, decimal Rent);
public class PricingSearch(RentalDbContext db)
{
    public async Task<List<PricedVehicle>> Find(DateTime start, DateTime end, decimal min, decimal max, string unit = "NGAY", string form = "NGAN_HAN", string season = "MAC_DINH")
    {
        BillingService.Require(end > start && min >= 0 && max >= min && unit is "GIO" or "NGAY", "Khoảng ngày hoặc giá không hợp lệ.");
        var rows = await (from v in db.Set<Vehicle>().AsNoTracking() join r in db.Rates.AsNoTracking() on v.TypeId equals r.Vehicle where !v.Archived && v.Status != "NgungKhaiThac" && r.Active && r.Start <= start && start < r.End && r.Unit == unit && r.Form == form && r.Season == season && r.Amount >= min && r.Amount <= max select new { v.Id, v.Name, v.Plate, Rate = r.Id, r.Unit, r.Amount }).ToListAsync();
        var result = new List<PricedVehicle>();
        foreach (var r in rows) { try { await new VehicleAvailability(db).Ensure(r.Id, start, end); } catch (BusinessError) { continue; } result.Add(new(r.Id, r.Name, r.Plate, r.Rate, r.Unit, r.Amount, BillingService.Rental(r.Amount, r.Unit, start, end))); }
        return result.OrderBy(x => x.UnitPrice).ThenBy(x => x.VehicleId).ToList();
    }
}

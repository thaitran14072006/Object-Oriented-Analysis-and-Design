using CarRental.Web.Areas.Billing.Data;
using CarRental.Web.Areas.Billing.Services;
using CarRental.Web.Shared.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace CarRental.Web.Areas.Promotions.Controllers;

[ServiceFilter(typeof(CarRental.Web.Areas.Billing.Services.BillingAudit))]
[ApiController, Route("api/promotions"), Authorize(Policy = "Promotions.Access")]
public class PromotionsController(RentalDbContext db) : ControllerBase
{
    [HttpGet] public async Task<object> List() => await db.Promotions.AsNoTracking().ToListAsync();
    [HttpPost] public Task<object> Create(Promotion p) => Save(p, false);
    [HttpPut("{id}")] public Task<object> Edit(string id, Promotion p) { BillingService.Require(id == p.Id, "Mã không khớp."); return Save(p, true); }
    async Task<object> Save(Promotion p, bool edit)
    {
        p.Id = p.Id.Trim().ToUpperInvariant(); BillingService.Require(p.Id.Length is > 0 and <= 30 && !string.IsNullOrWhiteSpace(p.Name) && p.End > p.Start && p.Value > 0 && (p.Kind == "SO_TIEN" || p.Kind == "PHAN_TRAM" && p.Value <= 100) && p.Status is "SapApDung" or "DangApDung" or "TamNgung" or "DaKetThuc", "Thông tin khuyến mãi không hợp lệ.");
        if (p.Status == "DaKetThuc") p.ActualEnd = DateTime.UtcNow; if (edit) { var old = await db.Promotions.FindAsync(p.Id) ?? throw new BusinessError("Không tìm thấy khuyến mãi."); BillingService.Require(old.Status != "DaKetThuc", "Chương trình đã kết thúc."); BillingService.Require(!await db.Conditions.AnyAsync(x => x.PromotionId == p.Id && x.Kind == "THOI_DIEM" && (x.Start < p.Start || x.End > p.End)), "Thời hạn không bao phủ điều kiện."); db.Entry(old).CurrentValues.SetValues(p); } else db.Add(p); if (p.Status == "DaKetThuc") p.ActualEnd = DateTime.UtcNow; await db.SaveChangesAsync(); return p;
    }
    [HttpDelete("{id}")] public async Task<object> Delete(string id) { var p = await db.Promotions.FindAsync(id) ?? throw new BusinessError("Không tìm thấy."); BillingService.Require(p.Status == "SapApDung" && !await db.Applied.AnyAsync(x => x.PromotionId == id), "Chỉ xóa chương trình chưa áp dụng."); db.Conditions.RemoveRange(await db.Conditions.Where(x => x.PromotionId == id).ToListAsync()); db.Remove(p); await db.SaveChangesAsync(); return p; }
    [HttpGet("{id}/conditions")] public async Task<object> Conditions(string id) => await db.Conditions.AsNoTracking().Where(x => x.PromotionId == id).ToListAsync();
    [HttpPut("{id}/conditions")] public async Task<object> Conditions(string id, PromotionCondition[] rules) { await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable); var p = await db.Promotions.FindAsync(id) ?? throw new BusinessError("Không tìm thấy."); BillingService.Require(p.Status != "DaKetThuc", "Chương trình đã kết thúc."); foreach (var r in rules) { BillingService.Require(r.Kind == "NHOM_KHACH" ? !string.IsNullOrWhiteSpace(r.Group) && r.Season == null && r.Start == null && r.End == null : r.Kind == "MUA_VU" ? !string.IsNullOrWhiteSpace(r.Season) && r.Group == null && r.Start == null && r.End == null : r.Kind == "THOI_DIEM" && r.Group == null && r.Season == null && r.Start >= p.Start && r.End <= p.End && r.End > r.Start, "Điều kiện không hợp lệ."); r.Id = 0; r.PromotionId = id; } db.Conditions.RemoveRange(await db.Conditions.Where(x => x.PromotionId == id).ToListAsync()); db.AddRange(rules); await db.SaveChangesAsync(); await tx.CommitAsync(); return rules; }
    [HttpGet("applied")] public async Task<object> Applied() => await db.Applied.AsNoTracking().ToListAsync();
}

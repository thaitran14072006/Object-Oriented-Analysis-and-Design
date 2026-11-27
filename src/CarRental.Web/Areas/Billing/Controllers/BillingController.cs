using CarRental.Web.Areas.Billing.Data;
using CarRental.Web.Areas.Billing.Services;
using CarRental.Web.Shared.Contracts;
using CarRental.Web.Shared.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace CarRental.Web.Areas.Billing.Controllers;

[ServiceFilter(typeof(CarRental.Web.Areas.Billing.Services.BillingAudit))]
[ApiController, Route("api/billing"), Authorize(Policy = "Billing.Access")]
public class BillingController(RentalDbContext db, IBillingService billing) : ControllerBase
{
    string Admin => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
    [HttpGet("rates")] public async Task<object> Rates(decimal? min, decimal? max) => await db.Rates.AsNoTracking().Where(x => (min == null || x.Amount >= min) && (max == null || x.Amount <= max)).ToListAsync();
    [HttpPost("rates")] public Task<object> AddRate(Rate r) => SaveRate(r, false);
    [HttpPut("rates/{id}")] public Task<object> EditRate(string id, Rate r) { BillingService.Require(id == r.Id, "Mã không khớp."); return SaveRate(r, true); }
    async Task<object> SaveRate(Rate r, bool edit) { BillingService.Require(r.Id.Length is > 0 and <= 20 && !string.IsNullOrWhiteSpace(r.Vehicle) && !string.IsNullOrWhiteSpace(r.Season) && r.Amount > 0 && r.End > r.Start && r.Unit is "GIO" or "NGAY" && r.Form is "NGAN_HAN" or "DAI_HAN", "Thông tin mức giá không hợp lệ."); await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable); BillingService.Require(!await db.Rates.AnyAsync(x => x.Id != r.Id && x.Active && r.Active && x.Vehicle == r.Vehicle && x.Unit == r.Unit && x.Form == r.Form && x.Season == r.Season && x.Start < r.End && r.Start < x.End), "Trùng khoảng hiệu lực mức giá."); if (edit) { var old = await db.Rates.FindAsync(r.Id) ?? throw new BusinessError("Không tìm thấy mức giá."); db.Entry(old).CurrentValues.SetValues(r); } else db.Add(r); await db.SaveChangesAsync(); await tx.CommitAsync(); return r; }
    [HttpDelete("rates/{id}")] public async Task<object> DeleteRate(string id) { await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);var r = await db.Rates.FindAsync(id) ?? throw new BusinessError("Không tìm thấy."); BillingService.Require(!await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM DonThue WHERE maMucGia={id} AND trangThai NOT IN ('Returned','Cancelled','Rejected','DaTraXe','DaHuy','DaTuChoi')").AnyAsync(x=>x>0),"Không thể xóa mức giá đang áp dụng cho đơn chưa hoàn tất.");if (await db.Bookings.AnyAsync(x => x.RateId == id)) r.Active = false; else db.Remove(r); await db.SaveChangesAsync(); await tx.CommitAsync(); return r; }
    [HttpGet("surcharges")] public async Task<object> Fees() => await db.Surcharges.AsNoTracking().ToListAsync();
    [HttpPost("surcharges")] public async Task<object> AddFee(Surcharge r) { ValidateFee(r); db.Add(r); await db.SaveChangesAsync(); return r; }
    static void ValidateFee(Surcharge r) => BillingService.Require(r.Id.Length is > 0 and <= 20 && !string.IsNullOrWhiteSpace(r.Name) && r.Amount >= 0 && r.Calculation is "CO_DINH" or "THEO_GIO_TRE", "Phụ phí không hợp lệ.");
    [HttpPut("surcharges/{id}")] public async Task<object> EditFee(string id, Surcharge r) { ValidateFee(r); BillingService.Require(id == r.Id, "Mã không khớp."); var old = await db.Surcharges.FindAsync(id) ?? throw new BusinessError("Không tìm thấy."); db.Entry(old).CurrentValues.SetValues(r); await db.SaveChangesAsync(); return old; }
    [HttpDelete("surcharges/{id}")] public async Task<object> DeleteFee(string id) { await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);var r = await db.Surcharges.FindAsync(id) ?? throw new BusinessError("Không tìm thấy."); BillingService.Require(!await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM PhuPhiDonThue f LEFT JOIN HoaDon h ON h.maDon=f.maDon WHERE f.maPhuPhi={id} AND (h.maHoaDon IS NULL OR COALESCE((SELECT SUM(t.soTien) FROM GiaoDichThanhToan t WHERE t.maDon=f.maDon),0)<h.tongTien)").AnyAsync(x=>x>0),"Không thể xóa phụ phí của đơn chưa thanh toán đủ.");if (await db.InvoiceFees.AnyAsync(x => x.SurchargeId == id)) r.Active = false; else db.Remove(r); await db.SaveChangesAsync(); await tx.CommitAsync(); return r; }
    [HttpPost("bookings/{id}/payments")] public Task<object> PayBooking(string id,PaymentRequest r)=>billing.PayBooking(id,r,Admin);
    [HttpPost("quote")] public Task<Cost> Quote(QuoteRequest r) => billing.Quote(r);
    [HttpGet("invoices")] public async Task<object> Invoices() => await db.Invoices.AsNoTracking().Select(i => new { i.Id, i.Booking, i.Rent, i.Fees, i.Discount, i.Total, i.Version, Paid = db.Payments.Where(p => p.Booking == i.Booking).Sum(p => (decimal?)p.Amount) ?? 0 }).ToListAsync();
    [HttpPost("bookings/{id}/promotion")] public Task<object> Apply(string id, QuoteRequest r) => billing.Apply(id, r);
    public class InvoiceParty {public string Customer {get;set;}="";public string Vehicle {get;set;}="";public string Plate {get;set;}="";}
    [HttpGet("invoices/{id}")] public async Task<object> Detail(string id){var i=await db.Invoices.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id)??throw new BusinessError("Không tìm thấy hóa đơn.");var b=await db.Bookings.AsNoTracking().SingleAsync(x=>x.Id==i.Booking);var party=await db.Database.SqlQuery<InvoiceParty>($"SELECT c.hoTen AS Customer,x.tenXe AS Vehicle,x.bienSo AS Plate FROM DonThue d JOIN KhachHang c ON c.maKhachHang=d.maKhachHang JOIN Xe x ON x.maXe=d.maXe WHERE d.maDon={i.Booking}").SingleAsync();var paid=await db.Payments.Where(x=>x.Booking==i.Booking).SumAsync(x=>(decimal?)x.Amount)??0;return new {invoice=i,booking=b,party,fees=await db.InvoiceFees.AsNoTracking().Where(x=>x.Booking==i.Booking).ToListAsync(),paid,remaining=i.Total-paid};}
    [HttpPost("invoices")] public Task<object> Create(InvoiceRequest r) => billing.Create(r, Admin);
    [HttpPost("invoices/{id}/payments")] public Task<object> Pay(string id, PaymentRequest r) => billing.Pay(id, r, Admin);
    [HttpPost("invoices/{id}/adjust")] public Task<object> Adjust(string id, AdjustmentRequest r) => billing.Adjust(id, r);
    [HttpGet("payments")] public async Task<object> Payments() => await db.Payments.AsNoTracking().ToListAsync();
    [HttpGet("complaints")] public async Task<object> Complaints() => await db.Complaints.AsNoTracking().ToListAsync();
    public record ComplaintInput(string InvoiceId, string Channel, string Item, string Content);
    [HttpPost("complaints")] public async Task<object> Complain(ComplaintInput r) { BillingService.Require(r.Channel is "TrucTiep" or "DienThoai" && !string.IsNullOrWhiteSpace(r.Content) && !string.IsNullOrWhiteSpace(r.Item), "Thiếu thông tin phản ánh."); BillingService.Require(await db.Invoices.AnyAsync(x => x.Id == r.InvoiceId), "Không tìm thấy hóa đơn."); var c = new Complaint { InvoiceId = r.InvoiceId, Channel = r.Channel, Item = r.Item, Content = r.Content, Receiver = Admin, Created = DateTime.UtcNow }; db.Add(c); await db.SaveChangesAsync(); return c; }
    public record ResolutionInput(string Resolution, AdjustmentRequest? Adjustment);
    [HttpPost("complaints/{id:long}/resolve")] public async Task<object> Resolve(long id, ResolutionInput r) { BillingService.Require(!string.IsNullOrWhiteSpace(r.Resolution), "Nhập nội dung giải quyết."); await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable); var c = await db.Complaints.FindAsync(id) ?? throw new BusinessError("Không tìm thấy phản ánh."); BillingService.Require(c.Status == "ChoXuLy", "Phản ánh đã xử lý."); if (r.Adjustment != null) await billing.Adjust(c.InvoiceId, r.Adjustment); c.Status = "DaXuLy"; c.Resolution = r.Resolution; c.Resolver = Admin; c.Resolved = DateTime.UtcNow; await db.SaveChangesAsync(); await tx.CommitAsync(); return c; }
}

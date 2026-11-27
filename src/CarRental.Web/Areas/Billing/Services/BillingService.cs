using System.Data;
using CarRental.Web.Shared.Contracts;
using CarRental.Web.Shared.Data;
using CarRental.Web.Areas.Billing.Data;
using Microsoft.EntityFrameworkCore;
namespace CarRental.Web.Areas.Billing.Services;

public class BusinessError(string message, decimal? refund = null) : Exception(message) { public decimal? Refund => refund; }
public class BillingService(RentalDbContext db) : IBillingService
{
    public static void Require(bool condition, string message) { if (!condition) throw new BusinessError(message); }
    public static decimal Rental(decimal price, string unit, DateTime start, DateTime end)
    {
        Require(end > start, "Giờ trả phải sau giờ thuê."); Require(price > 0 && (unit == "GIO" || unit == "NGAY"), "Đơn giá không hợp lệ.");
        return decimal.Round(price * (decimal)Math.Ceiling((end - start).TotalHours / (unit == "NGAY" ? 24 : 1)), 2);
    }
    async Task<decimal> Fees(FeeInput[] inputs, string? booking = null) { var ids = inputs.Select(x => x.Id).ToArray(); Require(inputs.All(x => x.Quantity >= 0 && decimal.Round(x.Quantity, 2) == x.Quantity), "Số lượng phụ phí không hợp lệ."); Require(ids.Distinct().Count() == ids.Length, "Phụ phí bị lặp."); var fees = await db.Surcharges.Where(x => ids.Contains(x.Id) && x.Active).ToListAsync(); Require(fees.Count == ids.Length, "Phụ phí không còn hiệu lực."); var snapshots = booking == null ? new List<InvoiceFee>() : await db.InvoiceFees.AsNoTracking().Where(x => x.Booking == booking).ToListAsync(); return fees.Sum(x => decimal.Round((snapshots.FirstOrDefault(y => y.SurchargeId == x.Id)?.UnitPrice ?? x.Amount) * inputs.Single(y => y.Id == x.Id).Quantity, 2)); }
    async Task<(Cost Cost, Rate Rate, Promotion? Promo)> Calculate(QuoteRequest q, BookingCost? booking = null)
    {
        var rate = await db.Rates.FindAsync(q.RateId) ?? throw new BusinessError("Không tìm thấy mức giá."); Require(booking != null || rate.Active && q.Start >= rate.Start && q.Start < rate.End, "Mức giá hết hiệu lực tại giờ nhận xe.");
        var rent = Rental(booking?.UnitPrice ?? rate.Amount, booking?.Unit ?? rate.Unit, q.Start, q.End); var fees = await Fees(q.Fees); Promotion? promo = null; decimal discount = 0;
        if (!string.IsNullOrWhiteSpace(q.PromotionCode))
        {
            var code = q.PromotionCode.Trim().ToUpperInvariant(); promo = await db.Promotions.SingleOrDefaultAsync(x => x.Id == code) ?? throw new BusinessError("Mã khuyến mãi không tồn tại."); Require(CarRental.Web.Areas.Promotions.PromotionRules.State(promo!,DateTime.UtcNow) == "DangApDung" && q.Start >= promo.Start && q.Start < promo.End && DateTime.UtcNow >= promo.Start && DateTime.UtcNow < promo.End, "Khuyến mãi chưa hoặc hết hiệu lực."); var rules = await db.Conditions.Where(x => x.PromotionId == promo.Id).ToListAsync(); string? group = q.Group;
            if (booking != null && rules.Any(x => x.Kind == "NHOM_KHACH")) group = await db.Database.SqlQuery<string>($"SELECT c.maNhom AS [Value] FROM DonThue d JOIN KhachHang c ON c.maKhachHang=d.maKhachHang WHERE d.maDon={booking.Id}").SingleOrDefaultAsync(); Require(rules.All(x => x.Kind == "NHOM_KHACH" ? x.Group == group : x.Kind == "MUA_VU" ? x.Season == rate.Season : x.Kind == "THOI_DIEM" && q.Start >= x.Start && q.Start < x.End), "Đơn chưa đáp ứng điều kiện ưu đãi."); discount = promo.Kind == "PHAN_TRAM" ? decimal.Round(rent * promo.Value / 100, 2) : promo.Value; discount = Math.Min(rent, discount);
        }
        return (new Cost(rent, fees, discount), rate, promo);
    }
    public async Task<Cost> Quote(QuoteRequest q) => (await Calculate(q)).Cost;
    public async Task<object> Apply(string bookingId, QuoteRequest q)
    {
        var owns = db.Database.CurrentTransaction == null; await using var tx = owns ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable) : null;
        await LockBooking(bookingId);Require(!await db.Invoices.AnyAsync(x => x.Booking == bookingId), "Đơn đã có hóa đơn."); var b = await db.Bookings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == bookingId) ?? throw new BusinessError("Không tìm thấy đơn."); Require(b.RateId == q.RateId && b.Start == q.Start && b.End == q.End, "Báo giá không khớp đơn."); var c = await Calculate(q, b); Require(c.Promo != null, "Chưa chọn mã ưu đãi."); Require(!await db.Applied.AnyAsync(x => x.Booking == bookingId), "Đơn đã áp dụng ưu đãi."); var a = new AppliedPromotion { Booking = bookingId, PromotionId = c.Promo!.Id, Kind = c.Promo.Kind, Value = c.Promo.Value, Discount = c.Cost.Discount, Applied = DateTime.UtcNow }; db.Add(a); await db.SaveChangesAsync(); await CheckUnbilled(bookingId); if (tx != null) await tx.CommitAsync(); return a;
    }
    public async Task<object> Create(InvoiceRequest r, string admin)
    {
        Require(!string.IsNullOrWhiteSpace(r.Booking) && r.Booking.Length <= 20, "Mã đơn không hợp lệ.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable); await LockBooking(r.Booking);var booking = await db.Bookings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == r.Booking) ?? throw new BusinessError("Đơn thuê chưa tồn tại."); var c = await Calculate(r.Quote with { PromotionCode = null }, booking); var applied = await db.Applied.SingleOrDefaultAsync(x => x.Booking == r.Booking); var cost = c.Cost with { Discount = applied?.Discount ?? 0 }; Require(string.IsNullOrWhiteSpace(r.Quote.PromotionCode) || applied?.PromotionId == r.Quote.PromotionCode, "Áp dụng mã cho đơn trước khi lập hóa đơn.");
        var alreadyPaid=await db.Payments.Where(x=>x.Booking==r.Booking).SumAsync(x=>(decimal?)x.Amount)??0;Require(cost.Total>=alreadyPaid,"Tổng hóa đơn thấp hơn khoản đã thu; cần hoàn tiền trước.");Require(!await db.Invoices.AnyAsync(x => x.Booking == r.Booking), "Đơn đã có hóa đơn.");
        Require(booking.RateId == r.Quote.RateId && booking.Start == r.Quote.Start && booking.End == r.Quote.End, "Thông tin báo giá không khớp đơn thuê."); var i = new Invoice { Booking = r.Booking, Creator = admin, Rent = cost.Rent, Fees = cost.Fees, Discount = cost.Discount, Total = cost.Total }; db.Add(i); await db.SaveChangesAsync(); await SaveFees(i.Booking, r.Quote.Fees);
        await db.SaveChangesAsync(); await tx.CommitAsync(); return i;
    }
    async Task SaveFees(string booking, FeeInput[] inputs) { var ids = inputs.Select(x => x.Id).ToArray(); foreach (var f in await db.Surcharges.Where(x => ids.Contains(x.Id)).ToListAsync()) { var qty = inputs.Single(x => x.Id == f.Id).Quantity; var snapshot = await db.InvoiceFees.AsNoTracking().FirstOrDefaultAsync(x => x.Booking == booking && x.SurchargeId == f.Id); var price = snapshot?.UnitPrice ?? f.Amount; db.Add(new InvoiceFee { Booking = booking, SurchargeId = f.Id, Name = snapshot?.Name ?? f.Name, Calculation = snapshot?.Calculation ?? f.Calculation, Quantity = qty, UnitPrice = price, Amount = decimal.Round(price * qty, 2) }); } }
    static DateTime PaymentTime(DateTime? at) {var value=at??DateTime.UtcNow;Require(value<=DateTime.UtcNow.AddMinutes(1),"Ngày thanh toán không được ở tương lai.");return value;}
    async Task LockBooking(string id) {await db.Bookings.FromSqlInterpolated($"SELECT * FROM DonThue WITH(UPDLOCK,HOLDLOCK) WHERE maDon={id}").AsNoTracking().SingleOrDefaultAsync();}
    async Task<Cost> BookingTotal(string id) {var b=await db.Bookings.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id)??throw new BusinessError("Không tìm thấy đơn.");var fees=await db.InvoiceFees.Where(x=>x.Booking==id).SumAsync(x=>(decimal?)x.Amount)??0;var discount=await db.Applied.Where(x=>x.Booking==id).Select(x=>(decimal?)x.Discount).SingleOrDefaultAsync()??0;return new(Rental(b.UnitPrice,b.Unit,b.Start,b.End),fees,discount);}
    public async Task CheckUnbilled(string id){var cost=await BookingTotal(id);var paid=await db.Payments.Where(x=>x.Booking==id).SumAsync(x=>(decimal?)x.Amount)??0;if(paid>cost.Total)throw new BusinessError("Cần hoàn tiền trước khi chốt điều chỉnh.",paid-cost.Total);}
    public async Task<object> PayBooking(string id,PaymentRequest r,string admin){
      var owns=db.Database.CurrentTransaction==null;await using var tx=owns?await db.Database.BeginTransactionAsync(IsolationLevel.Serializable):null;
      await LockBooking(id);var invoice=await db.Invoices.AsNoTracking().SingleOrDefaultAsync(x=>x.Booking==id);Require(invoice==null,"Đơn đã có hóa đơn. Hãy ghi thu theo hóa đơn.");
      var version=await db.Database.SqlQuery<long>($"SELECT phienBan AS [Value] FROM DonThue WHERE maDon={id}").SingleAsync();
      Require(r.Amount>0&&decimal.Round(r.Amount,2)==r.Amount&&r.Kind is "DAT_COC" or "THANH_TOAN"&&r.Method is "TIEN_MAT" or "CHUYEN_KHOAN" or "KHAC"&&!string.IsNullOrWhiteSpace(r.RequestId)&&r.RequestId.Length<=30,"Thông tin khoản thu không hợp lệ.");
      var prior=await db.Payments.SingleOrDefaultAsync(x=>x.Id==r.RequestId);if(prior!=null){Require(prior.Booking==id&&prior.Amount==r.Amount&&prior.Kind==r.Kind&&prior.Method==r.Method,"Mã giao dịch đã sử dụng.");return prior;}
      Require(version==r.Version,"Đơn đã thay đổi. Hãy tải lại.");var cost=await BookingTotal(id);var paid=await db.Payments.Where(x=>x.Booking==id).SumAsync(x=>(decimal?)x.Amount)??0;Require(paid+r.Amount<=cost.Total,"Số thu vượt số còn phải thu.");
      var p=new Payment{Id=r.RequestId,Booking=id,Recorder=admin,Amount=r.Amount,Kind=r.Kind,Method=r.Method,Reference=r.Reference,Created=PaymentTime(r.At)};db.Add(p);await db.SaveChangesAsync();if(tx!=null)await tx.CommitAsync();return p;
    }
    public async Task<object> Pay(string id, PaymentRequest r, string admin)
    {
        Require(r.Amount > 0 && decimal.Round(r.Amount, 2) == r.Amount, "Số tiền phải dương và tối đa hai chữ số thập phân."); Require(r.Kind is "DAT_COC" or "THANH_TOAN", "Loại giao dịch không hợp lệ."); Require(r.Method is "TIEN_MAT" or "CHUYEN_KHOAN" or "KHAC", "Phương thức không hợp lệ."); Require(!string.IsNullOrWhiteSpace(r.RequestId) && r.RequestId.Length <= 30, "Thiếu mã yêu cầu.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var i = await db.Invoices.FromSqlInterpolated($"SELECT * FROM HoaDon WITH (UPDLOCK,HOLDLOCK) WHERE maHoaDon={id}").SingleOrDefaultAsync() ?? throw new BusinessError("Không tìm thấy hóa đơn.");
        var prior = await db.Payments.SingleOrDefaultAsync(x => x.Id == r.RequestId); if (prior != null) { Require(prior.Booking == (db.Invoices.Single(x => x.Id == id).Booking) && prior.Amount == r.Amount && prior.Kind == r.Kind && prior.Method == r.Method, "Mã yêu cầu đã dùng cho giao dịch khác."); return prior; }
        Require(i.Version == r.Version, "Hóa đơn đã thay đổi. Hãy tải lại."); var paid = await db.Payments.Where(x => x.Booking == i.Booking).SumAsync(x => (decimal?)x.Amount) ?? 0;
        Require(paid + r.Amount <= i.Total, "Số thu vượt số còn phải thu."); var p = new Payment { Booking = i.Booking, Recorder = admin, Id = r.RequestId, Reference = r.Reference, Amount = r.Amount, Kind = r.Kind, Method = r.Method, Created = PaymentTime(r.At) }; db.Add(p); i.Version++; await db.SaveChangesAsync(); await tx.CommitAsync(); return p;
    }
    public async Task<object> Adjust(string id, AdjustmentRequest r)
    {
        // Reuse an outer booking transaction so schedule, history and invoice commit together.
        var owns = db.Database.CurrentTransaction == null; await using var tx = owns ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable) : null;
        var i = await db.Invoices.FromSqlInterpolated($"SELECT * FROM HoaDon WITH (UPDLOCK,HOLDLOCK) WHERE maHoaDon={id}").SingleOrDefaultAsync() ?? throw new BusinessError("Không tìm thấy hóa đơn."); Require(i.Version == r.Version, "Hóa đơn đã thay đổi. Hãy tải lại."); var booking = await db.Bookings.AsNoTracking().SingleAsync(x => x.Id == i.Booking); Require(booking.Start == r.Start && booking.End == r.End, "Đổi lịch phải được Bookings lưu cùng giao dịch trước khi tính lại hóa đơn."); var rent = Rental(booking.UnitPrice, booking.Unit, r.Start, r.End); var fees = r.Fees == null ? i.Fees : await Fees(r.Fees, i.Booking); var total = Math.Max(0, rent + fees - i.Discount); var paid = await db.Payments.Where(x => x.Booking == i.Booking).SumAsync(x => (decimal?)x.Amount) ?? 0;
        if (total < paid) throw new BusinessError("Cần xử lý hoàn tiền trước khi chốt điều chỉnh.", paid - total);
        i.Rent = rent; i.Fees = fees; i.Total = total; i.Version++; if (r.Fees != null) { db.InvoiceFees.RemoveRange(await db.InvoiceFees.Where(x => x.Booking == i.Booking).ToListAsync()); await SaveFees(i.Booking, r.Fees); }
        await db.SaveChangesAsync(); if (tx != null) await tx.CommitAsync(); return i;
    }
}

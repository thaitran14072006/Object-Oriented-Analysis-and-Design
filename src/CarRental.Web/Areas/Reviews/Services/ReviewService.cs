using System.Data;
using CarRental.Web.Areas.Billing.Services;
using CarRental.Web.Areas.Bookings.Services;
using CarRental.Web.Shared.Data;
using Microsoft.EntityFrameworkCore;
namespace CarRental.Web.Areas.Reviews.Services;
public record ReviewInput(string Booking,int VehicleScore,int ServiceScore,string Feedback);
public record ReviewDecision(string Improvement);
public sealed class Review {public long Id{get;set;} public string Booking{get;set;}="";public int VehicleScore{get;set;}public int ServiceScore{get;set;}public string Feedback{get;set;}="";public string? Improvement{get;set;}public DateTime Created{get;set;}public DateTime? Reviewed{get;set;}}
public sealed class ReviewService(RentalDbContext db,BookingService bookings){
 public Task<List<Review>> List()=>db.Database.SqlQueryRaw<Review>("SELECT Id,maDon AS Booking,vehicleScore,serviceScore,feedback,improvement,created,reviewed FROM DanhGiaDichVu").ToListAsync();
 public async Task Create(ReviewInput r,string admin){
 BillingService.Require(r.VehicleScore is >=1 and <=5&&r.ServiceScore is >=1 and <=5&&!string.IsNullOrWhiteSpace(r.Feedback)&&r.Feedback.Length<=1000,"Đánh giá không hợp lệ.");
 await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable); var b=await bookings.Get(r.Booking); BillingService.Require(b.Status=="Returned"&&b.ActualEnd.HasValue,"Chỉ đánh giá đơn đã trả xe.");
 await db.Database.ExecuteSqlInterpolatedAsync($"INSERT DanhGiaDichVu(maDon,vehicleScore,serviceScore,feedback,created,admin) VALUES ({r.Booking},{r.VehicleScore},{r.ServiceScore},{r.Feedback},{DateTime.UtcNow},{admin})");await tx.CommitAsync();}
 public async Task Resolve(long id,ReviewDecision r){BillingService.Require(!string.IsNullOrWhiteSpace(r.Improvement)&&r.Improvement.Length<=1000,"Cần hướng cải thiện."); var n=await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE DanhGiaDichVu SET improvement={r.Improvement},reviewed={DateTime.UtcNow} WHERE Id={id}");BillingService.Require(n==1,"Không tìm thấy đánh giá.");}
}

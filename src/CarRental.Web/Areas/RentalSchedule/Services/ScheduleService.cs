using CarRental.Web.Areas.Bookings.Services;
using CarRental.Web.Areas.Bookings.Data;
namespace CarRental.Web.Areas.RentalSchedule.Services;
public sealed class ScheduleService(BookingService bookings){
 public Task<List<Booking>> List()=>bookings.List();
 public Task<List<History>> History(string id)=>bookings.History(id);
 public Task<Booking> Adjust(string id,ScheduleInput input,string admin)=>bookings.Reschedule(id,input,admin);
}

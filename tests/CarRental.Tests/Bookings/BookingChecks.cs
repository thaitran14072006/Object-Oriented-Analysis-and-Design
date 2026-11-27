using CarRental.Web.Areas.Bookings.Data;
public static class BookingChecks {
 public static void Run(){var a=DateTime.UtcNow;void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}Check(!BookingRules.Overlaps(a,a.AddHours(1),a.AddHours(1),a.AddHours(2)),"Lịch tiếp giáp");Check(BookingRules.Overlaps(a,a.AddHours(2),a.AddHours(1),a.AddHours(3)),"Trùng lịch");Check(BookingRules.Next("Pending","handover")==null,"Chặn giao đơn chưa xác nhận");Check(BookingRules.Next("HandedOver","return")=="Returned","Nhận trả");Check(BookingRules.Next("Returned","cancel")==null,"Không hủy đơn đã trả");}
}

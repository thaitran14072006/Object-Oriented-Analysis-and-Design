using CarRental.Web.Shared.Data;
using CarRental.Web.Areas.Billing.Services;
using CarRental.Web.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
var start = new DateTime(2026, 11, 27, 0, 0, 0, DateTimeKind.Utc);
void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }
Check(BillingService.Rental(100, "NGAY", start, start.AddHours(24)) == 100, "24 giờ tính một ngày");
Check(BillingService.Rental(100, "NGAY", start, start.AddHours(24).AddMinutes(1)) == 200, "làm tròn ngày lẻ");
Check(BillingService.Rental(50, "GIO", start, start.AddMinutes(61)) == 100, "làm tròn giờ lẻ");
Check(new Cost(100, 20, 150).Total == 0, "tổng tối thiểu bằng không");
try { BillingService.Rental(100, "NGAY", start, start); throw new Exception("Expected rejection"); } catch (BusinessError) { Console.WriteLine("PASS chặn khoảng thuê rỗng"); }
using var db = new RentalDbContext(new DbContextOptionsBuilder<RentalDbContext>().UseSqlServer("Server=localhost;Database=CarRental;Integrated Security=True;TrustServerCertificate=True").Options);
var sql = db.Database.GenerateCreateScript();
if (args.Length > 0 && args[0] == "sql") { File.WriteAllText("database/migrations/billing-schema.generated.sql", sql); Console.WriteLine("Generated SQL schema"); }
Check(db.Model.FindEntityType(typeof(CarRental.Web.Areas.Billing.Data.Invoice))!.FindProperty("Version")!.IsConcurrencyToken, "hóa đơn có kiểm soát phiên bản");
Check(!sql.Contains("CREATE TABLE [DonThue]"), "không tạo lại bảng của Bookings");
if (args.Contains("integration")) await Integration.Run();
BookingChecks.Run();
OperationsChecks.Run();
if (args.Contains("operations")) await OperationsIntegration.Run();
if (args.Contains("billing-flow")) await BillingFlowIntegration.Run(args.Contains("preview"));

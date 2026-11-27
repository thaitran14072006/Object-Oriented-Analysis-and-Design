using CarRental.Web.Shared.Data;
using CarRental.Web.Shared.Contracts;
using CarRental.Web.Areas.Billing.Data;
using CarRental.Web.Areas.Billing.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
public static class Integration
{
    public static async Task Run()
    {
        var name = "NhuTests_" + Guid.NewGuid().ToString("N"); var master = "Server=(localdb)\\MSSQLLocalDB;Database=master;Integrated Security=True;TrustServerCertificate=True";
        async Task Sql(string connection, string sql) { await using var c = new SqlConnection(connection); await c.OpenAsync(); foreach (var batch in Regex.Split(sql, @"^GO\s*$", RegexOptions.Multiline)) { if (!string.IsNullOrWhiteSpace(batch)) { await using var cmd = new SqlCommand(batch, c); await cmd.ExecuteNonQueryAsync(); } } }
        await Sql(master, $"CREATE DATABASE [{name}]"); var cs = master.Replace("Database=master", "Database=" + name);
        RentalDbContext Db() => new(new DbContextOptionsBuilder<RentalDbContext>().UseSqlServer(cs).Options);
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); Console.WriteLine("PASS " + message); }
        async Task Reject(Func<Task> f, string message) { try { await f(); throw new Exception("Expected rejection: " + message); } catch (BusinessError) { Console.WriteLine("PASS " + message); } }
        try
        {
            await Sql(cs, "CREATE TABLE TaiKhoan(maTaiKhoan varchar(20) PRIMARY KEY); INSERT TaiKhoan VALUES ('ADMIN'); CREATE TABLE LoaiXe(maLoaiXe varchar(20) PRIMARY KEY); INSERT LoaiXe VALUES ('SEDAN'); CREATE TABLE NhomKhachHang(maNhom varchar(20) PRIMARY KEY); CREATE TABLE DonThue(maDon varchar(20) PRIMARY KEY,maMucGia varchar(20) NOT NULL,nhanDuKien datetime2 NOT NULL,traDuKien datetime2 NOT NULL,donGiaApDung decimal(18,2) NOT NULL,donViApDung varchar(10) NOT NULL);");
            foreach (var dir in new[] { "04_pricing", "06_billing", "08_promotions" }) await Sql(cs, await File.ReadAllTextAsync($"database/migrations/{dir}/001_schema.sql"));
            var start = DateTime.UtcNow.AddHours(1); await using (var db = Db())
            {
                db.Add(new Rate { Id = "R1", Vehicle = "SEDAN", Amount = 100, Start = start.AddDays(-1), End = start.AddDays(10) }); db.Add(new Promotion { Id = "KM1", Name = "Test", Status = "DangApDung", Kind = "PHAN_TRAM", Value = 10, Start = start.AddDays(-1), End = start.AddDays(10) }); await db.SaveChangesAsync();
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT DonThue VALUES ('D1','R1',{start},{start.AddDays(2)},100,'NGAY')");
            }
            var quote = new QuoteRequest("R1", start, start.AddDays(2), [], "KM1"); string invoiceId;
            await using (var db = Db()) { var s = new BillingService(db); await s.Apply("D1", quote); }
            await using (var db = Db()) { var rate = await db.Rates.SingleAsync(); rate.Amount = 999; var promo = await db.Promotions.SingleAsync(); promo.Value = 90; promo.Status = "TamNgung"; await db.SaveChangesAsync(); }
            await using (var db = Db()) { await new BillingService(db).Create(new InvoiceRequest("D1", quote), "ADMIN"); var i = await db.Invoices.SingleAsync(); invoiceId = i.Id; Check(i.Rent == 200 && i.Discount == 20 && i.Total == 180, "giữ đơn giá và ưu đãi chụp khi danh mục thay đổi"); }
            await using (var db = Db()) { await new BillingService(db).Pay(invoiceId, new(0, 50, "DAT_COC", "TIEN_MAT", "T1"), "ADMIN"); }
            await using (var db = Db()) { await new BillingService(db).Pay(invoiceId, new(0, 50, "DAT_COC", "TIEN_MAT", "T1"), "ADMIN"); Check(await db.Payments.CountAsync() == 1, "gửi lại giao dịch không thu hai lần"); }
            await using (var db = Db()) { await Reject(async () => { await new BillingService(db).Pay(invoiceId, new(1, 131, "THANH_TOAN", "TIEN_MAT", "T2"), "ADMIN"); }, "chặn thu vượt số còn phải thu"); }
            async Task<bool> Race(string key) { await using var db = Db(); try { await new BillingService(db).Pay(invoiceId, new(1, 100, "THANH_TOAN", "CHUYEN_KHOAN", key), "ADMIN"); return true; } catch (Exception e) when (e is BusinessError or DbUpdateException or SqlException) { return false; } }
            var results = await Task.WhenAll(Race("RACE1"), Race("RACE2")); Check(results.Count(x => x) == 1, "hai khoản thu đồng thời chỉ một yêu cầu được ghi");
            await using (var db = Db()) { var s = new BillingService(db); await Reject(async () => { await s.Pay(invoiceId, new(1, 1, "THANH_TOAN", "TIEN_MAT", "OLD"), "ADMIN"); }, "từ chối phiên bản hóa đơn cũ"); }
            await using (var db = Db()) { await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable); await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE DonThue SET traDuKien={start.AddDays(1)} WHERE maDon='D1'"); try { await new BillingService(db).Adjust(invoiceId, new(2, start, start.AddDays(1), [])); throw new Exception("Expected refund"); } catch (BusinessError e) { Check(e.Refund == 70, "hiển thị chính xác tiền cần hoàn và dừng điều chỉnh"); } await tx.RollbackAsync(); }
            await using (var db = Db()) { Check((await db.Invoices.SingleAsync()).Total == 180 && (await db.Bookings.SingleAsync()).End == start.AddDays(2), "hoàn tác đồng thời lịch và hóa đơn khi cần hoàn tiền"); }
            Console.WriteLine("SQL Server integration complete");
        }
        finally { SqlConnection.ClearAllPools(); await Sql(master, $"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]"); }
    }
}

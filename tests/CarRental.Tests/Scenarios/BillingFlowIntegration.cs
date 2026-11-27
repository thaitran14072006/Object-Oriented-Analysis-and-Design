using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;
using CarRental.Web.Areas.Fleet.Data;
using CarRental.Web.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
public static class BillingFlowIntegration {
 public static async Task Run(){
 var name="BillingFlow_"+Guid.NewGuid().ToString("N");var master="Server=(localdb)\\MSSQLLocalDB;Database=master;Integrated Security=True;TrustServerCertificate=True";var cs=new SqlConnectionStringBuilder(master){InitialCatalog=name}.ConnectionString;
 async Task Sql(string connection,string sql){await using var c=new SqlConnection(connection);await c.OpenAsync();foreach(var b in Regex.Split(sql,@"^GO\s*$",RegexOptions.Multiline)){if(string.IsNullOrWhiteSpace(b))continue;await using var cmd=new SqlCommand(b,c);await cmd.ExecuteNonQueryAsync();}}
 void Check(bool ok,string label){if(!ok)throw new Exception(label);Console.WriteLine("PASS "+label);}
 await Sql(master,$"CREATE DATABASE [{name}]");Process? process=null;
 try {
 foreach(var dir in Directory.GetDirectories("database/migrations").Order())foreach(var file in Directory.GetFiles(dir,"*.sql").Order())await Sql(cs,await File.ReadAllTextAsync(file));
 var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();var port=((IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();
 var info=new ProcessStartInfo("dotnet"){CreateNoWindow=true,UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=Path.GetFullPath("src/CarRental.Web")};
 info.ArgumentList.Add(Path.GetFullPath("src/CarRental.Web/bin/Debug/net10.0/CarRental.Web.dll"));info.ArgumentList.Add("--urls");info.ArgumentList.Add($"http://127.0.0.1:{port}");
 info.Environment["ConnectionStrings__CarRental"]=cs;info.Environment["BootstrapAdmin__Id"]="ADMIN";info.Environment["BootstrapAdmin__Name"]="Test Admin";info.Environment["BootstrapAdmin__Password"]="TestAdmin123!";info.Environment["BootstrapAdmin__Email"]="admin@example.com";info.Environment["BootstrapAdmin__Phone"]="0901234567";info.Environment["ASPNETCORE_ENVIRONMENT"]="Production";
 var logs=new System.Text.StringBuilder();process=Process.Start(info)!;process.OutputDataReceived+=(_,e)=>{lock(logs)logs.AppendLine(e.Data);};process.ErrorDataReceived+=(_,e)=>{lock(logs)logs.AppendLine(e.Data);};process.BeginOutputReadLine();process.BeginErrorReadLine();
 using var client=new HttpClient(new HttpClientHandler{CookieContainer=new CookieContainer(),AllowAutoRedirect=false}){BaseAddress=new Uri($"http://127.0.0.1:{port}"),Timeout=TimeSpan.FromSeconds(10)};
 bool ready=false;for(int k=0;k<60;k++){if(process.HasExited)throw new Exception(logs.ToString());try{using var r=await client.GetAsync("/api/identity/csrf");if(r.IsSuccessStatusCode){ready=true;break;}}catch(HttpRequestException){}await Task.Delay(250);}Check(ready,"ứng dụng khởi động với dịch vụ Identity mới");
 async Task<JsonElement> Send(string path,object body){var token=await client.GetFromJsonAsync<JsonElement>("/api/identity/csrf");using var req=new HttpRequestMessage(HttpMethod.Post,path){Content=JsonContent.Create(body)};req.Headers.Add("X-CSRF-TOKEN",token.GetProperty("token").GetString());using var r=await client.SendAsync(req);var text=await r.Content.ReadAsStringAsync();if(!r.IsSuccessStatusCode)throw new Exception($"{path} {(int)r.StatusCode}: {text}");return string.IsNullOrWhiteSpace(text)?default:JsonSerializer.Deserialize<JsonElement>(text);}
 Check((await client.GetAsync("/api/billing/invoices")).StatusCode==HttpStatusCode.Unauthorized,"API hóa đơn yêu cầu đăng nhập");
 await Send("/api/identity/login",new {employeeId="ADMIN",password="TestAdmin123!"});Check((await client.GetAsync("/api/billing/invoices")).IsSuccessStatusCode,"cookie Identity được Billing chấp nhận");
 using(var noToken=await client.PostAsJsonAsync("/api/billing/quote",new {}))Check(noToken.StatusCode==HttpStatusCode.BadRequest,"chặn yêu cầu ghi thiếu CSRF");
 var start=DateTime.UtcNow.AddDays(2);var end=start.AddDays(2);
 await using(var db=new RentalDbContext(new DbContextOptionsBuilder<RentalDbContext>().UseSqlServer(cs).Options)){
 db.Add(new VehicleType{Id="SEDAN",Name="Sedan",LicenseClass="B"});db.Add(new Vehicle{Id="XE1",TypeId="SEDAN",Name="Xe thử",Brand="Toyota",Plate="TEST01",Color="Đen",Year=2024,Seats=5,Transmission="Tự động"});await db.SaveChangesAsync();}
 await Sql(cs,"INSERT KhachHang(maKhachHang,maNhom,hoTen,soDienThoai,cccd,diaChi,soGPLX,hangGPLX,hetHanGPLX,thongTinXacMinh) VALUES ('KH1','THUONG',N'Khách thử','0901111111','012345678901',N'TP HCM','GPLX01','B','2035-01-01',N'Đã đối chiếu');");
 await Send("/api/billing/rates",new{id="R1",vehicle="SEDAN",amount=100,start=start.AddDays(-3),end=end.AddDays(30),unit="NGAY",form="NGAN_HAN",season="MAC_DINH"});
 await Send("/api/promotions",new{id="KM1",name="Ưu đãi thử",kind="PHAN_TRAM",value=10,start=start.AddDays(-3),end=end.AddDays(30),status="DangApDung"});
 await Sql(cs,"INSERT DieuKienKhuyenMai(maKhuyenMai,loaiDieuKien,maNhom) VALUES ('KM1','NHOM_KHACH','THUONG');");
 var search=await client.GetFromJsonAsync<JsonElement>($"/api/billing/vehicles?start={Uri.EscapeDataString(start.ToString("O"))}&end={Uri.EscapeDataString(end.ToString("O"))}&min=50&max=150");Check(search.GetArrayLength()==1,"tìm xe thật theo đơn giá và lịch trống");
 var booking=await Send("/api/bookings",new{vehicle="XE1",customer="KH1",rateId="R1",start,end,source="TrucTiep",promotionCode="KM1"});var bookingId=booking.GetProperty("id").GetString()!;
 await using(var db=new RentalDbContext(new DbContextOptionsBuilder<RentalDbContext>().UseSqlServer(cs).Options)){Check((await db.Applied.SingleAsync()).Discount==20,"tạo đơn và áp dụng ưu đãi theo nhóm khách thật cùng giao dịch");}
  await Sql(cs,"INSERT KhuyenMai(maKhuyenMai,tenChuongTrinh,hinhThucGiam,mucGiam,batDau,ketThuc,trangThai) SELECT 'VIP',N'VIP','SO_TIEN',50,batDau,ketThuc,trangThai FROM KhuyenMai WHERE maKhuyenMai='KM1'; INSERT DieuKienKhuyenMai(maKhuyenMai,loaiDieuKien,maNhom) VALUES ('VIP','NHOM_KHACH','THANTHIET');");
 var csrf=await client.GetFromJsonAsync<JsonElement>("/api/identity/csrf");using(var req=new HttpRequestMessage(HttpMethod.Post,"/api/bookings")){req.Headers.Add("X-CSRF-TOKEN",csrf.GetProperty("token").GetString());req.Content=JsonContent.Create(new{vehicle="XE1",customer="KH1",rateId="R1",start=end.AddDays(1),end=end.AddDays(2),source="TrucTiep",promotionCode="VIP",group="THANTHIET"});using var denied=await client.SendAsync(req);Check(denied.StatusCode==HttpStatusCode.Conflict,"không thể giả mạo nhóm khách để nhận ưu đãi");}
 await using(var db=new RentalDbContext(new DbContextOptionsBuilder<RentalDbContext>().UseSqlServer(cs).Options)){Check(await db.Bookings.CountAsync()==1,"mã không hợp lệ hoàn tác đơn mới cùng giao dịch");}
 var invoice=await Send("/api/billing/invoices",new {booking=bookingId,quote=new {rateId="R1",start,end,fees=Array.Empty<object>(),promotionCode="KM1"}});var invoiceId=invoice.GetProperty("id").GetString();Check(invoice.GetProperty("total").GetDecimal()==180,"lập hóa đơn dùng ưu đãi của đơn");
 await Send($"/api/billing/invoices/{invoiceId}/payments",new{version=0,amount=50,kind="DAT_COC",method="TIEN_MAT",requestId="FLOWPAY"});
 await Send($"/api/rental-schedule/{bookingId}/adjust",new{version=0,invoiceVersion=1,start,end=end.AddDays(1),reason="Gia hạn kiểm thử"});
 var list=await client.GetFromJsonAsync<JsonElement>("/api/billing/invoices");Check(list[0].GetProperty("total").GetDecimal()==280&&list[0].GetProperty("paid").GetDecimal()==50,"gia hạn đơn có hóa đơn giữ giảm chốt và khoản đã thu");
 await Sql(cs,"INSERT QuyenCaNhan VALUES ('ADMIN','Billing.Access',0);");Check((await client.GetAsync("/api/billing/invoices")).StatusCode==HttpStatusCode.Forbidden,"quyền cá nhân mới được áp dụng ngay cho phiên hiện tại");
 await Sql(cs,"DELETE QuyenCaNhan WHERE maTaiKhoan='ADMIN'; UPDATE PhienDangNhap SET thuHoiLuc=SYSUTCDATETIME();");Check((await client.GetAsync("/api/billing/invoices")).StatusCode==HttpStatusCode.Unauthorized,"thu hồi phiên Identity chặn Billing ngay");
 await using(var c=new SqlConnection(cs)){await c.OpenAsync();using var cmd=new SqlCommand("SELECT COUNT(*) FROM NhatKyThaoTac WHERE loaiDoiTuong='Billing' AND hanhDong='Pay'",c);Check(Convert.ToInt32(await cmd.ExecuteScalarAsync())==1,"thanh toán có nhật ký dùng chung");}
 }finally{if(process is {HasExited:false}){process.Kill(true);await process.WaitForExitAsync();}process?.Dispose();SqlConnection.ClearAllPools();await Sql(master,$"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]");}
 }
}



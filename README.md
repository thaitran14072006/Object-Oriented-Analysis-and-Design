# Object-Oriented-Analysis-and-Design

## Phần mềm quản lý thuê xe

Ứng dụng quản trị nội bộ cho doanh nghiệp cho thuê xe, dựa trên báo cáo OOAD S6 Nhóm 02 lần 04. Ứng dụng sử dụng ASP.NET Core MVC trên .NET 10 và SQL Server.

## Chạy RentalPro

Dùng .NET SDK 10, Node.js 22.12 trở lên và SQL Server. Từ thư mục gốc của repo, tạo cơ sở dữ liệu và chạy các script của mô-đun tài khoản, cài đặt và khách hàng theo thứ tự.

```powershell
sqlcmd -S '.\SQLEXPRESS' -E -C -b -Q "IF DB_ID(N'CarRental') IS NULL CREATE DATABASE [CarRental]"
sqlcmd -S '.\SQLEXPRESS' -E -C -b -f 65001 -d CarRental -i database/migrations/01_identity/01_identity.sql
sqlcmd -S '.\SQLEXPRESS' -E -C -b -f 65001 -d CarRental -i database/migrations/03_customers/03_customers.sql

$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ConnectionStrings__CarRental = 'Server=.\SQLEXPRESS;Database=CarRental;Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
$env:BootstrapAdmin__Id = 'ADMIN01'
$env:BootstrapAdmin__Name = 'Quản trị RentalPro'
$env:BootstrapAdmin__Email = Read-Host 'Email quản trị đã xác minh'
$env:BootstrapAdmin__Phone = Read-Host 'Số điện thoại quản trị đã xác minh'
$env:BootstrapAdmin__Password = [Net.NetworkCredential]::new('', (Read-Host 'Mật khẩu Admin' -AsSecureString)).Password

dotnet run --project src/CarRental.Web -- --bootstrap-admin
Remove-Item Env:BootstrapAdmin__Password
npm --prefix src/CarRental.Web/Frontend install
npm --prefix src/CarRental.Web/Frontend run build
dotnet run --project src/CarRental.Web --urls http://localhost:5080
```

Mở [http://localhost:5080](http://localhost:5080) và đăng nhập bằng mã Admin vừa tạo. Bước bootstrap áp dụng khi bảng tài khoản còn trống. Chạy hai script SQL một lần trên cơ sở dữ liệu mới. Dùng HTTPS cùng chứng chỉ tin cậy cho môi trường triển khai. Khi cần dùng thư mục riêng cho khóa bảo vệ phiên, đặt DataProtection__KeyDirectory tới thư mục có quyền ghi của tài khoản chạy ứng dụng.

### Kênh nhận mã và đăng nhập Google

| Chức năng | Biến môi trường |
| --- | --- |
| Gửi qua email SMTP | `Otp__Email__From`, `Otp__Email__Host`, `Otp__Email__Port`, `Otp__Email__Username`, `Otp__Email__Password` |
| Gửi qua SMS Twilio | `Otp__Sms__AccountSid`, `Otp__Sms__AuthToken`, `Otp__Sms__From` |
| Đăng nhập và liên kết Google | `Authentication__Google__ClientId`, `Authentication__Google__ClientSecret` |

Khi chạy thử bằng môi trường `Development`, đặt `Otp__Email__From` và `Otp__Email__PickupDirectory` để nhận email trong một thư mục riêng ngoài repo. Đăng ký URL callback Google với đường dẫn `/signin-google` trên đúng origin của ứng dụng. Đăng nhập qua Google dùng cặp nhà cung cấp và định danh Google đã liên kết trong Cài đặt.

Khách hàng đọc lịch sử trực tiếp từ `dbo.DonThue` khi script Bookings được triển khai. Bookings dùng `IRentalCustomerDirectory` để lấy nhóm khách, tình trạng xác minh giấy tờ, hạng và hạn GPLX, cùng tín nhiệm gần nhất. Các Area dùng chính sách trong `AdminAccess` và dịch vụ `IAdminAudit`.

### Chạy kiểm thử

```powershell
dotnet build CarRental.slnx
dotnet run --project tests/CarRental.Tests
# Tùy chọn tích hợp SQL Server. Kết nối thử cần quyền tạo database tạm.
dotnet run --project tests/CarRental.Tests -- --sql
```

Khi chạy `--sql`, đặt `CARRENTAL_SQL_TEST_CONNECTION` cho SQL Server thử. Chương trình tạo và dọn cơ sở dữ liệu kiểm thử có tên riêng, tách khỏi cơ sở dữ liệu ứng dụng.

### Cấu trúc repository

| Đường dẫn | Nội dung |
| --- | --- |
| `src/CarRental.Web/Frontend/` | Mã React, màn hình và CSS của giao diện quản trị. |
| `src/CarRental.Web/Areas/` | Màn hình, xử lý nghiệp vụ và truy cập dữ liệu theo mô-đun. |
| `src/CarRental.Web/Shared/Contracts/` | Hợp đồng dùng chung giữa các mô-đun. |
| `src/CarRental.Web/Shared/Data/` | Kết nối SQL Server và giao dịch dùng chung. |
| `src/CarRental.Web/Views/Shared/`, `wwwroot/` trong dự án web | Bố cục, thành phần Razor và tài nguyên tĩnh dùng chung. |
| `database/migrations/` | Script SQL Server chia theo mô-đun, áp dụng theo thứ tự tiền tố 01–10. |
| `database/seeds/` | Dữ liệu mẫu phục vụ phát triển và chạy thử. |
| `tests/CarRental.Tests/` | Kiểm thử theo mô-đun và kịch bản tích hợp trong `Scenarios/`. |
| `docs/analysis/`, `docs/deployment/` | Báo cáo, sơ đồ phân tích thiết kế và hướng dẫn triển khai. |

### Đối chiếu với báo cáo

| Use case | Thư mục trong `Areas/` |
| --- | --- |
| UC1, UC1.1, UC1.2 | `Identity/` |
| UC2 | `Fleet/` |
| UC3 | `Bookings/` |
| UC4 | `Billing/` |
| UC5 | `RentalSchedule/` |
| UC6 | `Reports/` |
| UC7 | `Customers/` |
| UC8 | `Maintenance/` |
| UC9 | `Settings/` |
| UC10.1–UC10.3 | `Promotions/` |
| UC10.4.1–UC10.4.2 | `Reviews/` |

Mỗi mô-đun đặt controller và view trong `Controllers/` và `Views/`, đặt xử lý nghiệp vụ trong `Services/`. Các mô-đun sở hữu bảng đặt mô hình và ánh xạ EF Core trong `Data/`. `Settings/` dùng dữ liệu từ `Identity/`. `RentalSchedule/` dùng dữ liệu từ `Bookings/` và gọi xử lý tính lại chi phí của `Billing/`.

Script SQL là nguồn quản lý cấu trúc cơ sở dữ liệu theo tên và ràng buộc bảng ở mục 1.6 của báo cáo. Giữ tệp `.gitkeep` ở thư mục rỗng cho đến khi có mã nguồn, kiểm thử hoặc tài liệu tương ứng.

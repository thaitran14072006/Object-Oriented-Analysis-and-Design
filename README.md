# Object-Oriented-Analysis-and-Design

## Phần mềm quản lý thuê xe

Ứng dụng quản trị nội bộ cho doanh nghiệp cho thuê xe, dựa trên báo cáo OOAD S6 Nhóm 02 lần 04. Phương án triển khai sử dụng ASP.NET Core MVC trên .NET 10, EF Core và SQL Server.

## Chạy giao diện RentalPro

Dùng .NET SDK 10 và Node.js 22.12 trở lên. Từ thư mục gốc của repository, chạy:

```powershell
npm --prefix src/CarRental.Web/Frontend install
npm --prefix src/CarRental.Web/Frontend run build
dotnet run --project src/CarRental.Web --urls http://localhost:5080
```

Mở [http://localhost:5080](http://localhost:5080). Khi sửa giao diện, có thể chạy `npm --prefix src/CarRental.Web/Frontend run dev` và mở địa chỉ Vite hiển thị trong terminal. Chạy lại `npm run build` trong thư mục `Frontend` để cập nhật giao diện do ASP.NET Core phục vụ hoặc trước khi chạy `dotnet publish`.

Giao diện React được chuyển từ Figma Make. Mỗi màn hình nằm trong `src/CarRental.Web/Frontend/src/screens/`. Điều hướng, xem chi tiết, tìm kiếm và lọc hợp đồng sử dụng dữ liệu mẫu. Đăng nhập, ghi dữ liệu và các thao tác cần cơ sở dữ liệu sẽ được nối với nghiệp vụ theo từng mô-đun.

### Cấu trúc repository

| Đường dẫn | Nội dung |
| --- | --- |
| `src/CarRental.Web/Frontend/` | Mã React, màn hình và CSS của giao diện quản trị. |
| `src/CarRental.Web/Areas/` | Màn hình, xử lý nghiệp vụ và truy cập dữ liệu theo mô-đun. |
| `src/CarRental.Web/Shared/Contracts/` | Hợp đồng dùng chung giữa các mô-đun. |
| `src/CarRental.Web/Shared/Data/` | DbContext, cấu hình kết nối và giao dịch dùng chung. |
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

Script SQL là nguồn quản lý cấu trúc cơ sở dữ liệu. Ánh xạ EF Core theo tên và ràng buộc bảng ở mục 1.6 của báo cáo. Giữ tệp `.gitkeep` ở thư mục rỗng cho đến khi có mã nguồn, kiểm thử hoặc tài liệu tương ứng.

### Phần giá thuê, thanh toán và khuyến mãi

Phần của Phạm Quỳnh Như có API SQL Server, giao diện ghi dữ liệu và kiểm thử giao dịch thực tế. Xem [hướng dẫn tích hợp và chạy kiểm thử](docs/deployment/billing-promotions.md). Cấu hình `ConnectionStrings__CarRental` và chạy các script SQL theo thứ tự mô-đun. Các API cần cookie Admin từ phần Identity; dữ liệu khách, loại xe và đơn thuê do mô-đun tương ứng cung cấp. Những màn hình còn lại vẫn sử dụng dữ liệu mẫu như mô tả ở trên.

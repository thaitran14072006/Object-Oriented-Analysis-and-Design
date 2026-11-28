# Chạy RentalPro bằng Docker Compose

Cấu hình này dành cho phát triển và demo trên một máy. Cổng web được gắn vào loopback và SQL Server chạy trong mạng nội bộ Compose. Docker bao gồm Node.js, .NET và SQL Server Developer.

## Khởi tạo

Trong PowerShell 7.2 trở lên, mở thư mục repo rồi chạy.

```powershell
.\scripts\Start-Docker.ps1 -InitializeAdmin -DemoData
```

- `-InitializeAdmin` hỏi email, số điện thoại và mật khẩu nếu bảng tài khoản còn trống. Admin mặc định mang mã `ADMIN01`. Docker Compose chạy tác vụ tạo Admin trong container tạm rồi xóa container đó khi hoàn tất.
- `-DemoData` nạp dữ liệu mẫu gồm loại xe, xe, mức giá, khách hàng và mã khuyến mãi. Các script seed sử dụng mã cố định và giữ các bản ghi đã có.
- Mở [http://localhost:5080/login](http://localhost:5080/login). Sau khi đăng nhập, kiểm tra luồng tạo đơn, đặt cọc và lập hóa đơn. Mục **Báo cáo & thống kê** đọc dữ liệu hệ thống.

Chạy lại bằng `.\scripts\Start-Docker.ps1`. Tiến trình `db-init` tạo database `CarRental` nếu cần, áp dụng SQL 01–10 trên schema rỗng trong một giao dịch và kiểm tra đủ bảng. `Exited (0)` của tiến trình này là trạng thái bình thường. Khi database đã có bảng, tiến trình kiểm tra sự tồn tại của các bảng và giữ dữ liệu hiện có. Áp dụng các thay đổi cột hoặc ràng buộc bằng script nâng cấp phù hợp khi schema đã có dữ liệu.

## Cổng và lưu dữ liệu

Nếu cổng `5080` đang được dùng, chọn cổng khác trước khi chạy lại script.

```powershell
$env:RENTALPRO_WEB_PORT = '5081'
.\scripts\Start-Docker.ps1
```

| Volume | Dữ liệu |
| --- | --- |
| `sql_data` | Tệp SQL Server, tài khoản và dữ liệu nghiệp vụ |
| `app_data` | Tệp báo cáo định kỳ trong `App_Data` |
| `web_keys` | Khóa bảo vệ cookie phiên |

Giữ tệp `.env` cùng volume SQL. Dừng bằng `docker compose down` để giữ các volume. Cờ `-v` xóa volume, bao gồm dữ liệu SQL và tệp báo cáo.

## Kiểm tra lỗi

```powershell
docker compose ps -a
docker compose logs --tail 100 sqlserver db-init web
```

| Triệu chứng | Cách kiểm tra |
| --- | --- |
| Terminal báo lỗi kết nối Docker Engine | Mở Docker Desktop, chờ trạng thái đang chạy, rồi xác nhận `docker info --format '{{.OSType}}'` trả về `linux`. |
| `db-init` thoát với mã khác 0 | Xem lỗi SQL của `db-init`. Nếu báo thiếu bảng trên database đã có dữ liệu, kiểm tra schema trước khi áp dụng các script còn thiếu. |
| Web không đăng nhập được | Xác nhận `db-init` thành công và chạy lại với `-InitializeAdmin` khi chưa tạo tài khoản. |
| Cần OTP hoặc đăng nhập Google | Bổ sung thiết lập `Otp__Email__*`, `Otp__Sms__*` hoặc `Authentication__Google__*` vào cấu hình web theo nhà cung cấp. |

Với máy chủ dùng chung, triển khai sau HTTPS, dùng đăng nhập SQL riêng có quyền phù hợp và cấp thông tin xác thực từ cơ chế quản lý secret của máy chủ.

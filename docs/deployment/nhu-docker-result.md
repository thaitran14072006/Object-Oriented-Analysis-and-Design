# Kết quả kiểm thử Docker phần Như

Ngày kiểm tra 28/11/2026. Phiên bản mã nguồn 968e5c6, gồm cấu hình Docker của Thái tại eecd8ff và b3275e7. Kiểm thử trên Docker Desktop với Linux containers, dùng Compose project riêng rentalpro-nhu-check và web loopback cổng 5087. Dữ liệu và Admin đều là dữ liệu thử, tách khỏi các project Docker khác.

## Kết quả

| Hạng mục | Kết quả quan sát |
|---|---|
| Dockerfile | Build React bằng Node 24, publish .NET 10 Release thành công |
| SQL Server container | Healthy; SQL không được công bố ra cổng máy chủ |
| Schema | db-init kết thúc mã 0; xác minh đủ 33 bảng |
| Khởi tạo lại | Chạy lại db-init thành công, không tạo lại hoặc xóa dữ liệu |
| Web container | Trang /login trả HTTP 200 |
| Admin bootstrap | Tạo tài khoản thử qua web --bootstrap-admin bằng mật khẩu ngẫu nhiên |
| Đăng nhập | Cookie, CSRF và phiên SQL hoạt động giữa các container |
| Dữ liệu mẫu | Nạp loại xe và nhu-demo.sql thành công |
| Tìm xe theo giá | Trả xe thử đúng khoảng 400.000–600.000 đồng/ngày |
| Ưu đãi và cọc | Tạo đơn có NHU10, ghi cọc trước hóa đơn 300.000 đồng |
| Xem trước | Tổng 950.000 đồng; đã thu 300.000; còn thu 650.000 |
| Lập hóa đơn | Tổng khớp bước xem trước; gửi lại mã cọc không ghi thêm khoản thu |
| Gia hạn | Tổng mới 1.450.000; giữ giảm chốt 100.000 và cọc 300.000; còn thu 1.150.000 |
| Thanh toán | Thu nốt 1.150.000, số còn thu bằng 0; thu thêm 1 đồng bị chặn HTTP 409 |
| Phản ánh | Tiếp nhận và giải quyết phản ánh giữ nguyên hóa đơn thành công |
| Báo cáo | Báo cáo doanh thu đọc được tiền cọc và thanh toán theo ngày Việt Nam trên Linux |
| Đăng xuất | HTTP 204; API hóa đơn sau đăng xuất trả 401 |
| Restart web | Số hóa đơn trước/sau restart bằng nhau; chạy lại luồng đăng nhập và thanh toán thành công |

Các trường hợp trên đã được thực thi qua HTTP vào web container thật, sử dụng SQL Server trong Compose; không dùng LocalDB thay thế. Có sửa một kỳ vọng trong script kiểm thử: endpoint logout trả 204 đúng thiết kế, không phải 200. Không phát hiện lỗi ứng dụng phải sửa trong đợt kiểm tra Docker này.

## Chạy lại

Dùng cơ sở dữ liệu demo, nạp seed và có tài khoản Admin trước. Script tạo xe có mã ngẫu nhiên cùng đơn, hóa đơn và giao dịch thử; không xóa lịch sử, không dùng cho dữ liệu kinh doanh thật.

```powershell
.\scripts\Test-NhuDocker.ps1 -BaseUrl http://localhost:5080 -Credential (Get-Credential)
```

Chạy trên cổng khác thì thay BaseUrl tương ứng. Script chỉ chấp nhận địa chỉ loopback, không in mật khẩu hoặc cookie. Kết quả PASS hiển thị tại terminal; gặp số tiền hoặc HTTP không đúng sẽ dừng với lỗi.

## Phạm vi kết luận

Đã hoàn tất mục kiểm thử bản Docker cho Billing/Promotions và các điểm tích hợp đặt xe, lịch, Identity và báo cáo cần cho phần Như. Kết quả không thay thế nghiệm thu tất cả chức năng của nhóm, cấu hình gửi OTP/Google, kiểm thử tải hay sao lưu/phục hồi SQL. Bản thử riêng được dừng sau khi kiểm tra; các volume được giữ để tránh xóa dữ liệu. Docker Desktop và project khác đang chạy được giữ nguyên.

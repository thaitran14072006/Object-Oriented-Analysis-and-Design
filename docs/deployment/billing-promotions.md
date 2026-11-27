# Phần triển khai của Phạm Quỳnh Như

Phạm vi: UC4 và UC10.1–UC10.3, theo bảng phân công và báo cáo S6 Nhóm 02 lần 04 đã sửa. Mã nằm trong Areas/Billing, Areas/Promotions; giao diện React là Payments và Promotions. Không có thay đổi dữ liệu nghiệp vụ ngoài cơ sở dữ liệu thử tự tạo.

## Chạy và kiểm tra

Cần .NET 10 SDK trở lên, Node 22.12 trở lên và SQL Server. Đặt biến môi trường `ConnectionStrings__CarRental` theo máy chạy. Không đưa mật khẩu vào Git.

```powershell
dotnet restore tests/CarRental.Tests
npm --prefix src/CarRental.Web/Frontend ci
npm --prefix src/CarRental.Web/Frontend run build
dotnet run --project src/CarRental.Web --urls http://localhost:5080
dotnet run --project tests/CarRental.Tests
dotnet run --project tests/CarRental.Tests -- integration
```

Lệnh cuối dùng `(localdb)\MSSQLLocalDB`, tạo cơ sở dữ liệu `NhuTests_<GUID>`, chạy các script SQL thật trên bảng nền thử, sau đó xóa riêng cơ sở dữ liệu đó trong `finally`. Không dùng cơ sở dữ liệu nhóm làm nơi kiểm thử. Kiểm thử gồm làm tròn giờ/ngày, tổng tối thiểu 0, chụp giá và giảm giá, gửi lại thanh toán, thu vượt, hai khoản thu đồng thời, phiên bản cũ, khoản cần hoàn và hoàn tác lịch cùng hóa đơn.

## SQL và dữ liệu

Chạy `001_schema.sql` trong 04_pricing, 06_billing, 08_promotions đúng thứ tự chung 01–10. Các bảng nền TaiKhoan, LoaiXe, NhomKhachHang, DonThue phải tồn tại theo RDM trước khi chạy script phụ thuộc. Script áp dụng một lần trên cơ sở dữ liệu mới, không tự xóa dữ liệu. Không gọi EnsureCreated trên cơ sở dữ liệu nhóm.

Chín bảng của Như dùng tên cột và kiểu khóa của RDM. DonThue chỉ có ánh xạ một phần để đọc dữ liệu chi phí, bị loại khỏi EF migrations. Đơn giá dùng snapshot `donGiaApDung`, `donViApDung` của DonThue. PhuPhiDonThue chụp tên, cách tính, số lượng và đơn giá. Điều chỉnh không truyền `fees` sẽ giữ nguyên phụ phí đã ghi; truyền mảng sẽ thay danh sách, giữ đơn giá chụp cho hạng mục đã có. Giá thuê tham khảo lấy tại thời điểm nhận trong khoảng [batDau, ketThuc). Giờ/ngày lẻ làm tròn lên; mốc thời gian đầu vào lưu UTC trong datetime2.

## Hợp đồng tích hợp

- Thái: cookie authentication hiện dùng scheme Cookies, role Admin và claim NameIdentifier bằng maTaiKhoan. Các API từ chối 401/403 khi chưa đáp ứng. Chưa có màn hình đăng nhập của Thái trong repository; không có tài khoản hoặc đường đăng nhập bỏ qua xác thực. Khi ghép quyền chi tiết, thay policy BillingAdmin bằng cơ chế vai trò/quyền cá nhân của Identity. Cần ghép nhật ký dùng chung ở bước này.
- Nam: lưu DonThue trước; gọi `POST /api/billing/bookings/{maDon}/promotion` với QuoteRequest để kiểm tra và chụp ưu đãi trước hóa đơn. Dữ liệu nhóm khách phải lấy từ Customers; mùa vụ và mã mức giá từ dữ liệu đơn. Mỗi đơn một ưu đãi, bị chặn thay đổi sau hóa đơn. Báo giá `POST /api/billing/quote` chỉ đọc, không giữ giá.
- Lập hóa đơn `POST /api/billing/invoices` nhận `{booking, quote}`; đơn giá và giảm giá lấy từ snapshot, không lấy lại giá hiện hành. `GET /api/billing/invoices` trả phiên bản, tổng và đã thu.
- Ghi thu `POST /api/billing/invoices/{id}/payments` nhận version, amount, kind, method, requestId và reference tùy chọn. requestId là maGiaoDich duy nhất tối đa 30 ký tự. Gửi lại cùng mã và nội dung trả giao dịch cũ. DAT_COC và THANH_TOAN cùng trừ số còn thu. Khóa UPDLOCK/HOLDLOCK hóa đơn cùng giao dịch Serializable tránh thu vượt khi đồng thời.
- Đổi lịch: dùng cùng scoped RentalDbContext và giao dịch Serializable, kiểm tra phienBan của DonThue tại Bookings, lưu đơn và lịch sử rồi gọi IBillingService.Adjust trước Commit. Adjust kiểm tra phiên bản HoaDon, chỉ chấp nhận ngày trùng với DonThue đã lưu trong giao dịch, giữ giá/giảm đã chốt. Gặp BusinessError phải rollback toàn bộ. API độc lập không được đổi ngày trước đơn. Nếu tổng mới thấp hơn đã thu, lỗi 409 chứa refund; không tự tạo hoàn tiền.
- Mai: GET /api/billing/payments, /api/billing/invoices, /api/promotions/applied trả dữ liệu gốc để tổng hợp. Không tạo bảng doanh thu sao chép.

## Màn hình và các điểm cần ghép nhóm

Màn hình có thêm/sửa/ngừng giá và phụ phí, lọc khoảng giá, báo giá, áp dụng mã trước hóa đơn, lập hóa đơn kèm tối đa ba hạng mục phụ phí, ghi đặt cọc/thanh toán, tiếp nhận và giải quyết phản ánh giữ nguyên hoặc điều chỉnh chi phí, quản lý chương trình và điều kiện nhóm/mùa vụ/thời điểm. API nhận danh sách phụ phí không giới hạn ba dòng; số lượng nhập là số lần hoặc giờ trễ, được làm tròn tiền hai chữ số. Biểu mẫu lưu điều kiện sẽ thay bộ điều kiện; API cho phép lưu đồng thời nhiều điều kiện với phép AND.

Tra khoảng giá hiện lọc mức giá qua GET rates?min=&max=; ghép thông tin xe thực tế qua Fleet khi có mô-đun này. Các endpoint yêu cầu dữ liệu nền và cookie Identity; giao diện hiển thị lỗi đăng nhập rõ ràng khi chưa ghép.
Repository chưa có mô-đun nền của các thành viên, vì vậy chưa thể chạy demo trọn vẹn đăng nhập → đặt xe → thanh toán trên cơ sở dữ liệu nhóm. Bộ tích hợp dùng bảng nền tối thiểu để xác minh riêng SQL và nghiệp vụ của Như.


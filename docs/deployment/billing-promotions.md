# Tích hợp giá thuê thanh toán và khuyến mãi

Phần của Phạm Quỳnh Như gồm UC4 và UC10.1–UC10.3, ghép với Identity/Customers của Thái, Bookings/RentalSchedule của Nam và Fleet/Reports của Mai trên main sau commit cfcebb9.

## Chạy ứng dụng

Dùng .NET 10 trở lên, Node 22.12 trở lên và SQL Server. Tạo cơ sở dữ liệu mới rồi chạy các tệp SQL trong database/migrations theo thứ tự 01–10. Đặt ConnectionStrings__CarRental bằng kết nối của máy chạy, dùng cùng giá trị cho Identity và EF Core. Không chạy lại các script CREATE TABLE trên cơ sở dữ liệu đã có.

Lần đầu, cấu hình BootstrapAdmin__Id, BootstrapAdmin__Name, BootstrapAdmin__Email, BootstrapAdmin__Phone và BootstrapAdmin__Password bằng dữ liệu quản trị của bạn. Ứng dụng chỉ tạo tài khoản khi TaiKhoan còn rỗng; sau khi tạo, bỏ cấu hình mật khẩu bootstrap. Không có tài khoản hoặc mật khẩu mặc định. Email/SMS OTP và Google cần cấu hình riêng theo dịch vụ của Thái.

```powershell
dotnet restore tests/CarRental.Tests
npm --prefix src/CarRental.Web/Frontend ci
npm --prefix src/CarRental.Web/Frontend run build
dotnet run --project src/CarRental.Web --urls http://localhost:5080
```

Mở /login, đăng nhập, vào màn hình Đặt xe hoặc Thanh toán. Các API dùng cookie Identity, kiểm tra phiên phía máy chủ và quyền cá nhân hiện hành. Billing.Access và Promotions.Access là hai quyền độc lập; tìm xe theo giá chấp nhận Billing.Access hoặc Bookings.Access. Các mô-đun cũ còn dùng policy BillingAdmin tương thích Admin.Access; quyền chi tiết của từng mô-đun đó cần được nhóm chuyển đổi riêng. Mọi yêu cầu ghi gửi mã CSRF lấy từ /api/identity/csrf; frontend chung, Billing và Fleet đã hỗ trợ.

## Luồng sử dụng

1. Tạo loại xe và xe, bảng giá, hồ sơ khách và chương trình ưu đãi.
2. Ở Đặt xe, tìm xe theo khoảng đơn giá, lịch, đơn vị, hình thức và mùa vụ. Kết quả loại xe lưu trữ, trùng lịch thuê, bảo trì và xe chưa trả. Lịch được kiểm tra lại khi tạo đơn.
3. Tạo đơn kèm mã ưu đãi tùy chọn. Máy chủ xác định nhóm khách từ KhachHang và mùa vụ từ MucGiaThue, không tin nhóm/mùa do trình duyệt gửi. Đơn và ưu đãi cùng thành công hoặc cùng hoàn tác.
4. Vào Thanh toán, chọn đơn từ danh sách. Lịch, đơn giá và hóa đơn hiện tại được lấy tự động. Chọn phụ phí và số lần/giờ trễ để lập hóa đơn. Nếu đơn chưa có ưu đãi, có thể áp dụng tại đây trước khi lập hóa đơn.
5. Ghi đặt cọc hoặc thanh toán ngay trong đơn đã chọn; phiên bản và mã giao dịch được cấp tự động. Mã giao dịch giữ nguyên khi thử lại lỗi, đổi sau khi thành công. Tổng đã thu, còn thu cập nhật sau thao tác.
6. Đổi lịch tại Lịch thuê – trả xe. Có thể giữ nguyên phụ phí hoặc thay danh sách bằng hạng mục đã nhập; biểu mẫu hiện hỗ trợ một hạng mục, API hỗ trợ mảng nhiều hạng mục. Để tránh xóa phí cũ ngoài ý muốn, chỉ chọn thay danh sách khi đã xác định đủ khoản phí cần giữ.
7. Phản ánh hóa đơn có thể giải quyết giữ nguyên hoặc điều chỉnh chi phí; thao tác Billing/Promotions được ghi nhật ký qua IAdminAudit, không lưu nội dung yêu cầu hay thông tin xác thực.

## Hợp đồng dữ liệu

- GET /api/billing/vehicles: start, end, min, max, unit, form, season. Khoảng giá là đơn giá theo đơn vị được chọn; kết quả kèm tiền thuê dự kiến. Đây là tra cứu, không giữ lịch.
- IBillingService.Apply dùng lại giao dịch hiện có của Bookings, chụp hinhThucChup, mucGiamChup và giaTriGiam trước hóa đơn. Mỗi đơn một mã; không thay ưu đãi sau hóa đơn.
- IBillingService.Create lấy snapshot donGiaApDung, donViApDung từ DonThue và giá trị giảm đã chụp, không lấy lại giá danh mục mới.
- IBillingService.Adjust dùng cùng scoped RentalDbContext và giao dịch Serializable với DonThue, LichSuDieuChinh, PhuPhiDonThue, HoaDon. Nam kiểm tra phiên bản đơn, Billing kiểm tra phiên bản hóa đơn. Chỉ tính lại sau khi lịch đơn đã lưu trong giao dịch.
- Điều chỉnh fees=null giữ phí đã chốt; truyền mảng thay danh sách, giữ tên/cách tính/đơn giá chụp cho hạng mục cũ. Giờ/ngày lẻ làm tròn lên, tiền làm tròn hai chữ số. Thời gian lưu UTC trong datetime2.
- DAT_COC và THANH_TOAN cùng tính vào đã thu. UPDLOCK/HOLDLOCK trên hóa đơn và Serializable chặn thu vượt. Gửi lại cùng requestId và nội dung trả giao dịch cũ.
- Nếu tổng điều chỉnh thấp hơn đã thu: HTTP 409 có refund; không tự tạo giao dịch hoàn tiền, toàn bộ đổi lịch được rollback.
- Reports đọc giao dịch, hóa đơn và ApDungKhuyenMai trực tiếp; không tạo bảng doanh thu sao chép.

## Kiểm thử

```powershell
$env:CARRENTAL_TEST_MASTER='Server=(localdb)\MSSQLLocalDB;Database=master;Integrated Security=True;TrustServerCertificate=True'
dotnet run --project tests/CarRental.Tests -- integration operations billing-flow
```

integration kiểm tra thu đồng thời, thu vượt, gửi lại, snapshot và hoàn tiền. operations kiểm tra Fleet/Maintenance/Reports và đối soát dữ liệu. billing-flow tạo cơ sở dữ liệu thử từ đủ 10 nhóm script và chạy máy chủ HTTP riêng: bootstrap, đăng nhập thật, cookie/CSRF, tìm xe theo giá, nhóm ưu đãi thật, hoàn tác mã sai, lập hóa đơn, đặt cọc, gia hạn, thay quyền cá nhân, thu hồi phiên và nhật ký. Tất cả chỉ dùng cơ sở dữ liệu có tên GUID và được xóa trong finally; máy chủ thử được dừng khi hoàn tất.

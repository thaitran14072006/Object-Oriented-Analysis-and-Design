# Bản bổ sung Nam cho repository Object-Oriented-Analysis-and-Design
Nguồn đối chiếu: main, tree e8b26fbbcfe51c1085cbb0aa45d0a1cd7e8ef5e2; README, Program.cs, RentalDbContext, IBillingService, BillingService.Adjust, SQL Billing, React screens và chương trình test. Đây là ZIP overlay, không phải toàn bộ repo. Không push GitHub.

## Ghép và chạy
1. Clone hoặc tải ZIP repo chính xác, giải nén. Sao lưu Bookings.tsx và Schedule.tsx; chép các thư mục src, database, docs, tests của gói này vào thư mục gốc repo, ghi đè hai screen trên.
2. Thêm `BookingChecks.Run();` vào cuối tests/CarRental.Tests/Program.cs để chạy kiểm thử bổ sung. Không thay Program.cs gốc.
3. Chạy SQL theo thứ tự 01–10 khi các bảng phụ thuộc có sẵn; riêng 05 cần TaiKhoan và MucGiaThue, 09 cần DonThue. Cấu hình ConnectionStrings__CarRental theo README gốc.
4. npm --prefix src/CarRental.Web/Frontend install
5. npm --prefix src/CarRental.Web/Frontend run build
6. dotnet run --project src/CarRental.Web --urls http://localhost:5080
7. dotnet run --project tests/CarRental.Tests
8. Mở localhost:5080/#bookings và /#schedule. Đánh giá nằm cuối màn hình đặt xe để không sửa Promotions/App của nhóm. API yêu cầu cookie Admin với claim ID như policy BillingAdmin đang có. Identity chưa có thì API trả 401, không tắt xác thực để demo.

## Giới hạn và nghiệm thu
- Chưa có báo cáo lần 04/RDM chính thức; các cột ngoài BookingCost là đề xuất, cần thống nhất. Không có FK khách/xe do các bảng và khóa chưa được xác minh. Chưa kiểm tra giấy phép, tín nhiệm, xe đủ điều kiện, hạng xe/giá và bảo trì. Không dùng dữ liệu thật trước khi nối các kiểm tra này. Khóa SQL theo CarRental:Vehicle:<MA XE HOA> phải dùng chung với Mai; hiện chỉ có phía Nam. Cần chuyển lỗi sp_getapplock 51000 thành HTTP 409 ở middleware chung.
- SQL dùng EF Core SqlQuery cho DTO, không ánh xạ thêm entity vào DonThue vì Billing đã ánh xạ BookingCost; chưa đáp ứng đầy đủ yêu cầu mô hình EF trong RDM. Chưa thay DbContext chung.
- Chưa sửa hồ sơ đơn, chưa áp dụng khuyến mãi lúc tạo, chưa có giao diện lựa chọn phụ phí khi đổi lịch, chưa hỗ trợ hủy đơn đã thu tiền an toàn. Cần tích hợp các điểm này trước nghiệm thu.
- API đổi lịch gọi Billing.Adjust với cùng DbContext và transaction, có kiểm soát phiên bản và hoàn tác nếu lỗi. Chưa kiểm thử thực tế đồng thời/hoàn tiền. Các API ghi cần kiểm tra chính sách quyền riêng và CSRF trước triển khai production. Chưa ghi nhật ký thao tác chung.
- Không có .NET SDK, SQL Server hay node_modules trong môi trường tạo gói; không tuyên bố build/test thành công. Test mới chỉ là kiểm tra quy tắc thuần; cần test tích hợp SQL với hai kết nối, bảo trì, hoàn tiền, đánh giá trước trả và quyền Admin.

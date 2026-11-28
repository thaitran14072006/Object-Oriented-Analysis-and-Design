# Nghiệm thu phần Phạm Quỳnh Như

Đối chiếu ngày 27/11/2026 theo báo cáo S6_Nhom02_Lan04_Da_sua và bảng phân công. Phạm vi 18 UC chi tiết của Như là UC4 và UC10.1–UC10.3. Bảng dưới phân biệt chức năng đã có với bằng chứng kiểm thử; không thay chữ ký duyệt của nhóm.

| UC | Chức năng hiện tại | Kiểm chứng |
|---|---|---|
| UC4.1.1 | Thêm mức giá theo loại xe, giờ/ngày, hình thức và mùa vụ; chặn chồng hiệu lực | Luồng HTTP tạo giá; SQL CHECK và FK |
| UC4.1.2 | Chọn giá và sửa với dữ liệu được nạp sẵn; đơn cũ giữ snapshot | Kiểm thử integration giữ giá sau sửa danh mục |
| UC4.1.3 | Chặn xóa khi còn đơn chưa hoàn tất; bản ghi có lịch sử ngừng sử dụng theo mục 1.6.4 | HTTP DELETE trả 409 với đơn đang dùng |
| UC4.2.1 | Thêm phụ phí, cách tính, mức phí và mô tả | HTTP tạo phí; SQL CHECK, UNIQUE tên |
| UC4.2.2 | Chọn phí và sửa; dòng phí đã chụp giữ giá cũ | Luồng điều chỉnh dùng đơn giá snapshot; cần thành viên khác kiểm tra lại thao tác sửa form |
| UC4.2.3 | Chặn xóa phí của đơn chưa thu đủ; giữ lịch sử khi ngừng dùng | HTTP DELETE trả 409 với hóa đơn chưa đủ tiền |
| UC4.3 | Tìm xe theo cận đơn giá, đơn vị và thời gian | HTTP trả xe đúng giá; availability loại xung đột |
| UC4.4 | Ghi cọc trước hóa đơn hoặc thu theo hóa đơn; chọn ngày thu; chống thu vượt và gửi lại | SQL đồng thời; HTTP cọc trước hóa đơn, quá số phải thu và ngày thu |
| UC4.5 | Lập hóa đơn; chi tiết khách, xe, lịch, giá, phí, giảm, đã thu, còn thu | HTTP và quan sát giao diện hóa đơn có dòng phí, khách/xe và khoản đã cọc |
| UC4.6.1 | Tiếp nhận phản ánh theo hóa đơn, kênh và khoản bị phản ánh | HTTP tạo phản ánh |
| UC4.6.2 | Giải quyết giữ nguyên hoặc điều chỉnh cùng giao dịch | HTTP chặn nội dung trống và chuyển DaXuLy; kiểm thử integration về hoàn tiền/rollback khi điều chỉnh |
| UC10.1.1 | Tìm mã/tên, lọc trạng thái, số lượt, xem điều kiện và đơn đã dùng | HTTP tìm/lọc; quan sát giao diện danh sách và chi tiết |
| UC10.1.2 | Sửa chương trình chưa kết thúc, giữ mức chụp của đơn cũ | Revision kiểm tra đồng thời; integration giữ snapshot ưu đãi |
| UC10.1.3 | Tạm ngừng riêng, chỉ nhận trạng thái đang áp dụng | HTTP pause; revision cũ bị từ chối |
| UC10.1.4 | Kết thúc riêng, chốt thời điểm và giữ lịch sử | HTTP end và chặn resume sau kết thúc |
| UC10.1.5 | Áp dụng lại chương trình tạm ngừng còn hiệu lực | HTTP pause/resume; chặn trạng thái không hợp lệ |
| UC10.2 | Tạo ưu đãi, tự xác định trạng thái theo thời hạn; nhóm/mùa/thời điểm | HTTP tạo chương trình tương lai về SapApDung; kiểm tra điều kiện tại API |
| UC10.3 | Áp dụng mã trước hóa đơn, nhóm khách lấy từ CSDL, chụp giảm thực tế | HTTP chặn giả nhóm khách, rollback đơn có mã sai; integration giữ giảm sau sửa danh mục |

## Kết quả xác minh

- Backend biên dịch thành công. Frontend qua TypeScript và Vite production build.
- Lệnh `dotnet run --project tests/CarRental.Tests -- integration operations billing-flow` đã hoàn tất: nghiệp vụ tiền, giao dịch đồng thời, Fleet/Maintenance/Reports và luồng HTTP có Identity/CSRF.
- SQL seed demo đã được chạy hai lần trong cơ sở dữ liệu thử, không nhân bản bản ghi.
- Đăng nhập qua trình duyệt với tài khoản thử dùng một lần; mở Thanh toán, chọn đơn và xem hóa đơn có khách, xe, phụ phí, giảm giá, đã thu, còn thu. Mở Khuyến mãi, xem danh sách, số lượt, bộ điều kiện và các đơn đã dùng. Phiên demo đã dừng, cơ sở dữ liệu thử đã xóa.
- Tài khoản, mật khẩu và cookie thử không được dùng làm thông tin đăng nhập triển khai.

## Duyệt trước khi nộp

Một thành viên khác chạy lại kịch bản dưới đây, kiểm tra thao tác sửa giá/phí và các điều kiện mùa vụ/thời điểm trên giao diện, xác nhận kết quả rồi mới đánh dấu nghiệm thu nhóm. Màn hình tổng quan còn là dữ liệu mẫu ngoài phần Như. Các mô-đun dùng policy BillingAdmin cũ còn cần người phụ trách chuyển sang quyền chi tiết riêng. Không có nghiệp vụ hoàn tiền tự động vì báo cáo chưa đặc tả.

## Dữ liệu và kịch bản demo

Trên cơ sở dữ liệu demo đã chạy đủ migrations, chạy `database/seeds/nhu-demo.sql`. Script chỉ thêm bản ghi có mã riêng, không sửa dữ liệu trùng mã và không tạo tài khoản. Đăng nhập bằng tài khoản Admin cấu hình theo hướng dẫn triển khai.

| Dữ liệu | Mã hoặc giá trị |
|---|---|
| Khách demo | NHU_KH01, nhóm THUONG |
| Xe demo | NHU_XE01, biển DEMO-NHU-01 |
| Mức giá | NHU_GIA01, 500.000 đồng/ngày |
| Mùa vụ và hình thức | MAC_DINH, NGAN_HAN |
| Ưu đãi | NHU10, giảm 10% tiền thuê |
| Phí giao xe | NHU_GIAOXE, 50.000 đồng/lần |

1. Chọn nhận xe ngày mai, trả sau đúng 48 giờ, cùng phút; tìm xe ở khoảng 400.000–600.000 đồng/ngày. Xe NHU_XE01 hiện khi không có lịch trùng.
2. Tạo đơn với khách/xe/giá trên và mã NHU10. Tiền thuê 1.000.000 đồng; giảm chốt 100.000 đồng.
3. Vào Thanh toán, chọn đơn, ghi cọc 300.000 đồng trước hóa đơn. Kiểm tra khoản thu đã hiện.
4. Chọn một lần phí NHU_GIAOXE rồi lập hóa đơn. Tổng 950.000 đồng; đã thu 300.000 đồng; còn thu 650.000 đồng. Mở chi tiết đối chiếu tên khách, biển số, lịch, dòng phí và giảm.
5. Sang Lịch thuê – trả, gia hạn thêm đúng 24 giờ, nhập phiên bản đơn và hóa đơn hiện tại; để trống phí để giữ phí cũ. Tổng mới 1.450.000 đồng; giảm vẫn 100.000 đồng; còn thu 1.150.000 đồng.
6. Thu nốt 1.150.000 đồng. Hóa đơn còn thu 0. Thử ghi thêm 1 đồng phải bị chặn. Có thể mở Báo cáo doanh thu trong đúng kỳ để đối chiếu tổng cọc và thanh toán 1.450.000 đồng.
7. Ghi một phản ánh, xử lý với nội dung giải thích và giữ nguyên hóa đơn. Kiểm tra trạng thái Đã xử lý.
8. Mở NHU10, tạm ngừng, áp dụng lại rồi kết thúc. Chương trình kết thúc không nhận áp dụng lại; số lượt và giảm đã chốt trên đơn vẫn còn.

Nếu demo lại sau khi chương trình đã kết thúc, tạo mã mới; không tái sử dụng mã đã kết thúc. Nếu xe đã có đơn từ lần trước, dùng khung ngày khác hoặc một xe demo mới. Không xóa lịch sử để làm lại demo.

## Bổ sung xác minh ngày 28 tháng 11 năm 2026

Sau hai commit Docker của Thái, hoàn thiện bước Xem trước → Xác nhận cho UC4.5 và số tiền còn thu trước hóa đơn cho UC4.4. Bộ kiểm thử HTTP đã bổ sung kiểm tra không có hóa đơn sau xem trước, từ chối expectedTotal sai, gửi lại cọc không thu trùng sau khi đơn đã có hóa đơn và giữ đơn giá phí cũ khi danh mục đã ngừng/thay giá. Trong kịch bản demo bước 4, chọn Xem trước, đối chiếu 950.000 tổng / 300.000 đã thu / 650.000 còn thu, sau đó mới Xác nhận lập hóa đơn.

Kiểm thử ứng dụng vẫn chạy trên cơ sở dữ liệu tạm LocalDB. Docker Engine chưa hoạt động lúc kiểm tra; kết quả này không được dùng để tuyên bố đã nghiệm thu bản container.

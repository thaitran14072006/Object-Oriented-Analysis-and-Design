# Phần Nam – đối chiếu sơ bộ
UC3: tạo, tìm, xem, xác nhận, từ chối, hủy, giao, trả, hiển thị quá hạn. Sửa thông tin ngoài đổi lịch còn thiếu.
UC5: danh sách, lịch sử, điều chỉnh và gia hạn; cập nhật hóa đơn qua IBillingService.Adjust trong transaction chung. Chưa có lịch tuần động và chọn phụ phí.
UC10.4: ghi điểm xe/dịch vụ sau trả và lưu hướng cải thiện.

```mermaid
classDiagram
BookingsController --> BookingService
ScheduleController --> ScheduleService
ScheduleService --> BookingService
BookingService --> RentalDbContext
BookingService --> IBillingService
ReviewsController --> ReviewService
ReviewService --> BookingService
ReviewService --> RentalDbContext
```
```mermaid
sequenceDiagram
Admin->>ScheduleController: Điều chỉnh + phiên bản
ScheduleController->>BookingService: Reschedule
BookingService->>RentalDbContext: BEGIN + khóa xe + kiểm tra
BookingService->>RentalDbContext: UPDATE đơn + INSERT lịch sử
BookingService->>IBillingService: Adjust
alt Thành công
BookingService->>RentalDbContext: COMMIT
else Phiên bản sai hoặc cần hoàn
BookingService->>RentalDbContext: ROLLBACK khi dispose
end
```

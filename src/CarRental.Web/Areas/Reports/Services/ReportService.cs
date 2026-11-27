using CarRental.Web.Areas.Billing.Services;
using CarRental.Web.Shared.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Web.Areas.Reports.Services;

public sealed class ReportRow
{
    public string Label { get; set; } = "";
    public int Count { get; set; }
    public decimal Amount { get; set; }
    public decimal Secondary { get; set; }
    public decimal Rate { get; set; }
}
public sealed record ReportResult(string Title, string[] Headers, List<ReportRow> Rows);

public sealed class ReportService(RentalDbContext db, TimeProvider clock)
{
    public static TimeZoneInfo BusinessTimeZone => BusinessTime.Zone;
    public static (DateTime Start, DateTime End) Range(DateOnly from, DateOnly to)
    {
        BillingService.Require(from.Year >= 1900 && to >= from && to < DateOnly.MaxValue && to.DayNumber - from.DayNumber <= 3660, "Khoảng ngày không hợp lệ hoặc vượt quá 10 năm.");
        return (TimeZoneInfo.ConvertTimeToUtc(from.ToDateTime(TimeOnly.MinValue), BusinessTimeZone),
            TimeZoneInfo.ConvertTimeToUtc(to.AddDays(1).ToDateTime(TimeOnly.MinValue), BusinessTimeZone));
    }

    public async Task<ReportResult> Build(string type, DateOnly from, DateOnly to)
    {
        var (start, end) = Range(from, to);
        var (title, headers, source) = type switch
        {
            "revenue" => ("Doanh thu thực thu", new[] { "Ngày", "Giao dịch", "Tổng thực thu", "Đặt cọc", "Thanh toán" }, """
                SELECT CONVERT(varchar(10),DATEADD(hour,7,thoiDiem),23) Label, COUNT(*) [Count],
                    SUM(soTien) Amount, SUM(CASE WHEN loaiKhoan='DAT_COC' THEN soTien ELSE 0 END) Secondary,
                    SUM(CASE WHEN loaiKhoan='THANH_TOAN' THEN soTien ELSE 0 END) Rate
                FROM GiaoDichThanhToan WHERE thoiDiem>=@start AND thoiDiem<@end
                GROUP BY CONVERT(varchar(10),DATEADD(hour,7,thoiDiem),23)
                """),
            "usage" => ("Mức sử dụng xe", new[] { "Xe", "Lượt thuê", "Giờ sử dụng", "Giờ trong kỳ đã qua", "Tỷ lệ (%)" }, """
                SELECT CONCAT(x.maXe,' - ',x.tenXe) Label,COUNT(d.maDon) [Count],
                    CAST(COALESCE(SUM(DATEDIFF_BIG(second,
                        CASE WHEN d.nhanThucTe<@start THEN @start ELSE d.nhanThucTe END,
                        CASE WHEN COALESCE(d.traThucTe,@now)>@cutoff THEN @cutoff ELSE COALESCE(d.traThucTe,@now) END)),0)/3600.0 AS decimal(18,2)) Amount,
                    CAST(CASE WHEN @cutoff>@start THEN DATEDIFF_BIG(second,@start,@cutoff)/3600.0 ELSE 0 END AS decimal(18,2)) Secondary,
                    CAST(COALESCE(SUM(DATEDIFF_BIG(second,
                        CASE WHEN d.nhanThucTe<@start THEN @start ELSE d.nhanThucTe END,
                        CASE WHEN COALESCE(d.traThucTe,@now)>@cutoff THEN @cutoff ELSE COALESCE(d.traThucTe,@now) END))*100.0/
                        NULLIF(CASE WHEN @cutoff>@start THEN DATEDIFF_BIG(second,@start,@cutoff) ELSE 0 END,0),0) AS decimal(18,2)) Rate
                FROM Xe x LEFT JOIN DonThue d ON d.maXe=x.maXe AND d.nhanThucTe IS NOT NULL
                    AND d.nhanThucTe<@cutoff AND COALESCE(d.traThucTe,@now)>@start
                GROUP BY x.maXe,x.tenXe
                """),
            "fleet" => ("Tình trạng xe hiện tại", new[] { "Tình trạng", "Số xe", "", "", "" }, """
                SELECT tinhTrang Label,COUNT(*) [Count],CAST(0 AS decimal(18,2)) Amount,
                    CAST(0 AS decimal(18,2)) Secondary,CAST(0 AS decimal(18,2)) Rate FROM Xe WHERE daXoa=0 GROUP BY tinhTrang
                """),
            "maintenance" => ("Chi phí bảo trì", new[] { "Xe", "Phiếu có chi phí", "Tổng chi phí", "", "" }, """
                SELECT CONCAT(x.maXe,' - ',x.tenXe) Label,COUNT(DISTINCT b.maBaoTri) [Count],SUM(c.soTien) Amount,
                    CAST(0 AS decimal(18,2)) Secondary,CAST(0 AS decimal(18,2)) Rate
                FROM ChiPhiBaoTri c JOIN LichBaoTri b ON b.maBaoTri=c.maBaoTri JOIN Xe x ON x.maXe=b.maXe
                WHERE c.ngayGhiNhan>=@fromDate AND c.ngayGhiNhan<=@toDate GROUP BY x.maXe,x.tenXe
                """),
            "quality" => ("Chất lượng xe và dịch vụ", new[] { "Xe", "Đánh giá", "Điểm xe", "Điểm dịch vụ", "" }, """
                SELECT CONCAT(x.maXe,' - ',x.tenXe) Label,COUNT(*) [Count],
                    CAST(AVG(CAST(g.vehicleScore AS decimal(18,2))) AS decimal(18,2)) Amount,
                    CAST(AVG(CAST(g.serviceScore AS decimal(18,2))) AS decimal(18,2)) Secondary,CAST(0 AS decimal(18,2)) Rate
                FROM DanhGiaDichVu g JOIN DonThue d ON d.maDon=g.maDon JOIN Xe x ON x.maXe=d.maXe
                WHERE g.created>=@start AND g.created<@end GROUP BY x.maXe,x.tenXe
                """),
            "promotions" => ("Hiệu quả khuyến mãi", new[] { "Chương trình", "Lượt áp dụng", "Giảm thực tế", "Tổng hóa đơn liên quan", "" }, """
                SELECT CONCAT(k.maKhuyenMai,' - ',k.tenChuongTrinh) Label,COUNT(*) [Count],SUM(a.giaTriGiam) Amount,
                    SUM(COALESCE(h.tongTien,0)) Secondary,CAST(0 AS decimal(18,2)) Rate
                FROM ApDungKhuyenMai a JOIN KhuyenMai k ON k.maKhuyenMai=a.maKhuyenMai
                    JOIN DonThue d ON d.maDon=a.maDon LEFT JOIN HoaDon h ON h.maDon=a.maDon
                WHERE a.apDungLuc>=@start AND a.apDungLuc<@end AND d.trangThai NOT IN ('Cancelled','Rejected')
                GROUP BY k.maKhuyenMai,k.tenChuongTrinh
                """),
            "channels" => ("Kênh đặt xe", new[] { "Kênh", "Số đơn", "Tổng hóa đơn", "Đơn hủy/từ chối", "" }, """
                SELECT nguon Label,COUNT(*) [Count],SUM(CASE WHEN d.trangThai IN ('Cancelled','Rejected') THEN 0 ELSE COALESCE(h.tongTien,0) END) Amount,
                    CAST(SUM(CASE WHEN d.trangThai IN ('Cancelled','Rejected') THEN 1 ELSE 0 END) AS decimal(18,2)) Secondary,CAST(0 AS decimal(18,2)) Rate
                FROM DonThue d LEFT JOIN HoaDon h ON h.maDon=d.maDon
                WHERE d.nhanDuKien>=@start AND d.nhanDuKien<@end GROUP BY nguon
                """),
            _ => throw new BusinessError("Loại báo cáo không hợp lệ.")
        };
        var now = clock.GetUtcNow().UtcDateTime;
        var rows = await db.Database.SqlQueryRaw<ReportRow>(source,
            new SqlParameter("@start", start), new SqlParameter("@end", end),
            new SqlParameter("@now", now), new SqlParameter("@cutoff", end < now ? end : now),
            new SqlParameter("@fromDate", from.ToDateTime(TimeOnly.MinValue)), new SqlParameter("@toDate", to.ToDateTime(TimeOnly.MinValue))).OrderBy(x => x.Label).ToListAsync();
        return new ReportResult(title, headers, rows);
    }

    public static byte[] Export(ReportResult report)
    {
        var included = report.Headers.Select((header, index) => (header, index)).Where(x => x.header != "").ToArray();
        return SpreadsheetExport.Create(included.Select(x => x.header).ToArray(), report.Rows.Select(row =>
        {
            object?[] values = [row.Label, row.Count, row.Amount, row.Secondary, row.Rate];
            return included.Select(x => values[x.index]).ToArray();
        }));
    }
}

using CarRental.Web.Areas.Billing.Data;
using Microsoft.EntityFrameworkCore;
namespace CarRental.Web.Shared.Data;

public class RentalDbContext(DbContextOptions<RentalDbContext> options) : DbContext(options)
{
    public DbSet<Rate> Rates => Set<Rate>(); public DbSet<Surcharge> Surcharges => Set<Surcharge>(); public DbSet<Invoice> Invoices => Set<Invoice>(); public DbSet<InvoiceFee> InvoiceFees => Set<InvoiceFee>(); public DbSet<Payment> Payments => Set<Payment>(); public DbSet<Complaint> Complaints => Set<Complaint>(); public DbSet<Promotion> Promotions => Set<Promotion>(); public DbSet<PromotionCondition> Conditions => Set<PromotionCondition>(); public DbSet<AppliedPromotion> Applied => Set<AppliedPromotion>(); public DbSet<BookingCost> Bookings => Set<BookingCost>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        FleetMappings.Configure(b);
        Map<Rate>(b, "MucGiaThue", "Id:maMucGia Vehicle:maLoaiXe Unit:donViTinh Form:hinhThuc Season:muaVu Active:conSuDung Amount:donGia Start:batDau End:ketThuc");
        Map<Surcharge>(b, "PhuPhi", "Id:maPhuPhi Name:tenPhuPhi Calculation:cachTinh Amount:mucPhi Description:moTa Active:conSuDung");
        Map<Invoice>(b, "HoaDon", "Id:maHoaDon Booking:maDon Creator:maNguoiLap Created:lapLuc Rent:tienThue Fees:tongPhuPhi Discount:tienGiam Total:tongTien Version:phienBan");
        Map<InvoiceFee>(b, "PhuPhiDonThue", "Id:maKhoanPhi Booking:maDon SurchargeId:maPhuPhi Name:tenApDung Calculation:cachTinhApDung Quantity:soLuong UnitPrice:donGiaApDung Amount:thanhTien Created:ghiNhanLuc");
        Map<Payment>(b, "GiaoDichThanhToan", "Id:maGiaoDich Booking:maDon Recorder:maNguoiGhiNhan Kind:loaiKhoan Amount:soTien Method:phuongThuc Created:thoiDiem Reference:maThamChieu");
        Map<Complaint>(b, "PhanAnhHoaDon", "Id:maPhanAnh InvoiceId:maHoaDon Receiver:maNguoiTiepNhan Resolver:maNguoiXuLy Channel:kenh Item:khoanBiPhanAnh Content:noiDung Created:taoLuc Status:trangThai Resolution:cachGiaiQuyet Resolved:xuLyLuc");
        Map<Promotion>(b, "KhuyenMai", "Id:maKhuyenMai Name:tenChuongTrinh Kind:hinhThucGiam Value:mucGiam Start:batDau End:ketThuc Status:trangThai ActualEnd:ketThucThucTe Description:moTa");
        Map<PromotionCondition>(b, "DieuKienKhuyenMai", "Id:maDieuKien PromotionId:maKhuyenMai Kind:loaiDieuKien Group:maNhom Season:giaTriMuaVu Start:tuLuc End:denLuc");
        Map<AppliedPromotion>(b, "ApDungKhuyenMai", "Booking:maDon PromotionId:maKhuyenMai Kind:hinhThucChup Value:mucGiamChup Discount:giaTriGiam Applied:apDungLuc"); b.Entity<AppliedPromotion>().HasKey(x => x.Booking);
        Map<BookingCost>(b, "DonThue", "Id:maDon RateId:maMucGia Start:nhanDuKien End:traDuKien UnitPrice:donGiaApDung Unit:donViApDung"); b.Entity<BookingCost>().ToTable("DonThue", t => t.ExcludeFromMigrations());
        b.Entity<InvoiceFee>().Property(x => x.Quantity).HasPrecision(10, 2);
        b.Entity<Invoice>().Property(x => x.Version).IsConcurrencyToken(); b.Entity<Invoice>().HasIndex(x => x.Booking).IsUnique(); b.Entity<Surcharge>().HasIndex(x => x.Name).IsUnique();
        b.Entity<InvoiceFee>().HasOne<Surcharge>().WithMany().HasForeignKey(x => x.SurchargeId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Complaint>().HasOne<Invoice>().WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<PromotionCondition>().HasOne<Promotion>().WithMany().HasForeignKey(x => x.PromotionId).OnDelete(DeleteBehavior.Restrict); b.Entity<AppliedPromotion>().HasOne<Promotion>().WithMany().HasForeignKey(x => x.PromotionId).OnDelete(DeleteBehavior.Restrict);
    }
    static void Map<T>(ModelBuilder b, string table, string columns) where T : class
    {
        var e = b.Entity<T>(); e.ToTable(table); foreach (var pair in columns.Split(' '))
        {
            var p = pair.Split(':'); var prop = e.Property(p[0]).HasColumnName(p[1]); var type = typeof(T).GetProperty(p[0])!.PropertyType; if (type == typeof(decimal) || type == typeof(decimal?)) prop.HasPrecision(18, 2); if (type == typeof(string))
            {
                var col = p[1]; var len = col.StartsWith("ma") ? 20 : 1000; var unicode = !col.StartsWith("ma");
                if (col == "maKhuyenMai" || col == "maGiaoDich") len = 30;
                if (col == "maThamChieu") len = 100;
                if (col is "donViTinh" or "donViApDung") { len = 10; unicode = false; }
                if (col is "hinhThuc" or "cachTinh" or "cachTinhApDung" or "phuongThuc" or "kenh" or "loaiDieuKien" or "trangThai") { len = 20; unicode = false; }
                if (col is "hinhThucGiam" or "hinhThucChup" or "loaiKhoan") { len = 15; unicode = false; }
                if (col is "muaVu" or "giaTriMuaVu") len = 80;
                if (col is "tenPhuPhi" or "tenApDung") len = 100;
                if (col == "tenChuongTrinh") len = 150;
                if (col == "khoanBiPhanAnh") len = 255;
                if (col == "moTa" && typeof(T) == typeof(Surcharge)) len = 500;
                prop.HasMaxLength(len).IsUnicode(unicode);
            }
        }
    }
}



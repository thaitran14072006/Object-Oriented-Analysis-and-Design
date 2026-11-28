using CarRental.Web.Areas.Fleet.Data;
using CarRental.Web.Areas.Maintenance.Data;
using CarRental.Web.Areas.Reports.Data;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Web.Shared.Data;

public static class FleetMappings
{
    public static void Configure(ModelBuilder b)
    {
        Map<VehicleType>(b, "LoaiXe", "Id:maLoaiXe Name:tenLoaiXe LicenseClass:hangGPLXYeuCau");
        Map<Vehicle>(b, "Xe", "Id:maXe TypeId:maLoaiXe Name:tenXe Brand:hangXe Plate:bienSo Color:mauSac Year:namSanXuat Seats:soChoNgoi Transmission:loaiHopSo Status:tinhTrang Parking:viTriDo Archived:daXoa");
        Map<VehicleImage>(b, "HinhAnhXe", "Id:maHinhAnh VehicleId:maXe Url:duongDan Order:thuTu");
        Map<MaintenanceRecord>(b, "LichBaoTri", "Id:maBaoTri VehicleId:maXe Creator:maNguoiLap Kind:loaiBaoTri Start:batDauDuKien End:ketThucDuKien ActualStart:batDauThucTe ActualEnd:ketThucThucTe Content:noiDung Status:trangThai");
        Map<MaintenanceProgress>(b, "TienDoBaoTri", "Id:maTienDo MaintenanceId:maBaoTri Recorder:maNguoiCapNhat Content:noiDung Created:thoiDiem");
        Map<MaintenanceCost>(b, "ChiPhiBaoTri", "Id:maChiPhi MaintenanceId:maBaoTri Item:hangMuc Provider:donViThucHien Amount:soTien Date:ngayGhiNhan");
        Map<SafetyCheck>(b, "KiemTraAnToan", "Id:maKiemTra MaintenanceId:maBaoTri Recorder:maNguoiGhiNhan Inspector:nguoiKiemTra Created:thoiDiem Passed:dat Note:ghiChu");
        Map<PeriodicReport>(b, "BaoCaoDinhKy", "Id:maBaoCao Kind:loaiBaoCao From:tuNgay To:denNgay Created:taoLuc Path:duongDanTep Status:trangThai");
        b.Entity<VehicleType>().Property(x => x.Name).HasMaxLength(80);
        b.Entity<Vehicle>().Property(x => x.Name).HasMaxLength(100);
        b.Entity<Vehicle>().Property(x => x.Brand).HasMaxLength(80);
        b.Entity<Vehicle>().Property(x => x.Color).HasMaxLength(40);
        b.Entity<Vehicle>().Property(x => x.Plate).HasMaxLength(20).IsUnicode(false);
        b.Entity<Vehicle>().Property(x => x.Transmission).HasMaxLength(30);
        b.Entity<Vehicle>().Property(x => x.Status).HasMaxLength(25).IsUnicode(false).IsConcurrencyToken();
        b.Entity<Vehicle>().Property(x => x.Parking).HasMaxLength(255);
        b.Entity<Vehicle>().HasIndex(x => x.Plate).IsUnique();
        b.Entity<VehicleType>().HasIndex(x => x.Name).IsUnique();
        b.Entity<Vehicle>().HasOne<VehicleType>().WithMany().HasForeignKey(x => x.TypeId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<VehicleImage>().HasOne<Vehicle>().WithMany().HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<VehicleImage>().Property(x => x.Url).HasMaxLength(500);
        b.Entity<MaintenanceRecord>().HasOne<Vehicle>().WithMany().HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<MaintenanceRecord>().Property(x => x.Status).IsConcurrencyToken();
        b.Entity<MaintenanceProgress>().HasOne<MaintenanceRecord>().WithMany().HasForeignKey(x => x.MaintenanceId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<MaintenanceCost>().HasOne<MaintenanceRecord>().WithMany().HasForeignKey(x => x.MaintenanceId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<SafetyCheck>().HasOne<MaintenanceRecord>().WithMany().HasForeignKey(x => x.MaintenanceId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<MaintenanceCost>().Property(x => x.Amount).HasPrecision(18, 2);
        b.Entity<MaintenanceCost>().Property(x => x.Item).HasMaxLength(255);
        b.Entity<MaintenanceCost>().Property(x => x.Provider).HasMaxLength(255);
        b.Entity<SafetyCheck>().Property(x => x.Inspector).HasMaxLength(100);
        b.Entity<PeriodicReport>().Property(x => x.Kind).HasMaxLength(40);
        b.Entity<PeriodicReport>().Property(x => x.Path).HasMaxLength(500);
        b.Entity<PeriodicReport>().HasIndex(x => new { x.Kind, x.From, x.To }).IsUnique();
    }

    private static void Map<T>(ModelBuilder b, string table, string columns) where T : class
    {
        var entity = b.Entity<T>().ToTable(table);
        foreach (var pair in columns.Split(' '))
        {
            var names = pair.Split(':');
            var property = entity.Property(names[0]).HasColumnName(names[1]);
            if (typeof(T).GetProperty(names[0])!.PropertyType != typeof(string)) continue;
            var identifier = names[1].StartsWith("ma") && names[1].Length > 2 && char.IsUpper(names[1][2]);
            property.HasMaxLength(identifier ? 20 : 1000).IsUnicode(!identifier);
            if (names[0] is "Status" or "Kind" or "LicenseClass") property.HasMaxLength(20).IsUnicode(false);
        }
    }
}

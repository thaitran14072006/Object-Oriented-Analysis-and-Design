-- Run after migrations 01-10 on a development/demo database only.
-- No credentials or orders are created. Existing records with these IDs are left intact.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
DECLARE @start datetime2=DATEADD(day,-1,SYSUTCDATETIME()), @finish datetime2=DATEADD(day,60,SYSUTCDATETIME());
IF NOT EXISTS(SELECT 1 FROM LoaiXe WHERE maLoaiXe='NHU_SEDAN')
 INSERT LoaiXe(maLoaiXe,tenLoaiXe,hangGPLXYeuCau) VALUES('NHU_SEDAN',N'Sedan demo Như','B');
IF NOT EXISTS(SELECT 1 FROM Xe WHERE maXe='NHU_XE01')
 INSERT Xe(maXe,maLoaiXe,tenXe,hangXe,bienSo,mauSac,namSanXuat,soChoNgoi,loaiHopSo,tinhTrang,viTriDo,daXoa)
 VALUES('NHU_XE01','NHU_SEDAN',N'Toyota Vios demo','Toyota','DEMO-NHU-01',N'Trắng',2024,5,N'Tự động','SanSang',N'Bãi demo',0);
IF NOT EXISTS(SELECT 1 FROM MucGiaThue WHERE maMucGia='NHU_GIA01')
 INSERT MucGiaThue(maMucGia,maLoaiXe,donViTinh,hinhThuc,muaVu,donGia,batDau,ketThuc,conSuDung)
 VALUES('NHU_GIA01','NHU_SEDAN','NGAY','NGAN_HAN','MAC_DINH',500000,@start,@finish,1);
IF NOT EXISTS(SELECT 1 FROM PhuPhi WHERE maPhuPhi='NHU_GIAOXE')
 INSERT PhuPhi(maPhuPhi,tenPhuPhi,cachTinh,mucPhi,moTa,conSuDung) VALUES('NHU_GIAOXE',N'Giao xe demo Như','CO_DINH',50000,N'Phí giao một lần',1);
IF NOT EXISTS(SELECT 1 FROM KhachHang WHERE maKhachHang='NHU_KH01')
 INSERT KhachHang(maKhachHang,maNhom,hoTen,soDienThoai,cccd,diaChi,soGPLX,hangGPLX,hetHanGPLX,thongTinXacMinh)
 VALUES('NHU_KH01','THUONG',N'Khách demo Như','0900000001','DEMO-NHU-CCCD',N'Dữ liệu giả lập','DEMO-NHU-GPLX','B','2035-12-31',N'Hồ sơ demo đã đối chiếu');
IF NOT EXISTS(SELECT 1 FROM KhuyenMai WHERE maKhuyenMai='NHU10')
BEGIN
 INSERT KhuyenMai(maKhuyenMai,tenChuongTrinh,hinhThucGiam,mucGiam,batDau,ketThuc,trangThai,moTa)
 VALUES('NHU10',N'Ưu đãi demo Như','PHAN_TRAM',10,@start,@finish,'DangApDung',N'Giảm 10 phần trăm tiền thuê cho khách thường');
 INSERT DieuKienKhuyenMai(maKhuyenMai,loaiDieuKien,maNhom) VALUES('NHU10','NHOM_KHACH','THUONG');
END;
COMMIT;

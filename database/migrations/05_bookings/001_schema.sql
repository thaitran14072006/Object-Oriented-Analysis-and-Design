-- Requires preceding modules. Proposed columns pending official RDM review.
CREATE TABLE DonThue (
 maDon varchar(20) PRIMARY KEY, maXe varchar(20) NOT NULL, maKhachHang varchar(20) NOT NULL,
 maMucGia varchar(20) NOT NULL REFERENCES MucGiaThue(maMucGia),
 nhanDuKien datetime2 NOT NULL, traDuKien datetime2 NOT NULL,
 donGiaApDung decimal(18,2) NOT NULL, donViApDung varchar(10) NOT NULL,
 nguon varchar(20) NOT NULL, trangThai varchar(20) NOT NULL,
 nhanThucTe datetime2 NULL, traThucTe datetime2 NULL, phienBan bigint NOT NULL DEFAULT 0,
 maAdmin varchar(20) NOT NULL REFERENCES TaiKhoan(maTaiKhoan),
 CONSTRAINT CK_DonThue CHECK(nhanDuKien<traDuKien AND donGiaApDung>0 AND donViApDung IN ('GIO','NGAY') AND phienBan>=0 AND nguon IN ('TrucTiep','DienThoai','LienKet') AND trangThai IN ('Pending','Confirmed','Rejected','Cancelled','HandedOver','Returned') AND (traThucTe IS NULL OR (nhanThucTe IS NOT NULL AND traThucTe>=nhanThucTe)))
);
CREATE INDEX IX_DonThue_Xe_Lich ON DonThue(maXe,nhanDuKien,traDuKien);
CREATE TABLE LichSuDieuChinh (
 Id bigint IDENTITY PRIMARY KEY, maDon varchar(20) NOT NULL REFERENCES DonThue(maDon),
 oldStart datetime2 NOT NULL, oldEnd datetime2 NOT NULL, newStart datetime2 NOT NULL, newEnd datetime2 NOT NULL,
 reason nvarchar(500) NOT NULL, admin varchar(20) NOT NULL REFERENCES TaiKhoan(maTaiKhoan), created datetime2 NOT NULL
);
GO

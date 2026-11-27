CREATE TABLE [GiaoDichThanhToan] (
    [maGiaoDich] varchar(30) NOT NULL,
    [maDon] varchar(20) NOT NULL,
    [maNguoiGhiNhan] varchar(20) NOT NULL,
    [loaiKhoan] varchar(15) NOT NULL,
    [phuongThuc] varchar(20) NOT NULL,
    [soTien] decimal(18,2) NOT NULL,
    [maThamChieu] varchar(100) NULL,
    [thoiDiem] datetime2 NOT NULL,
    CONSTRAINT [PK_GiaoDichThanhToan] PRIMARY KEY ([maGiaoDich])
);
GO

CREATE TABLE [HoaDon] (
    [maHoaDon] varchar(20) NOT NULL,
    [maDon] varchar(20) NOT NULL,
    [maNguoiLap] varchar(20) NOT NULL,
    [lapLuc] datetime2 NOT NULL,
    [tienThue] decimal(18,2) NOT NULL,
    [tongPhuPhi] decimal(18,2) NOT NULL,
    [tienGiam] decimal(18,2) NOT NULL,
    [tongTien] decimal(18,2) NOT NULL,
    [phienBan] bigint NOT NULL,
    CONSTRAINT [PK_HoaDon] PRIMARY KEY ([maHoaDon])
);
GO

CREATE TABLE [PhanAnhHoaDon] (
    [maPhanAnh] bigint NOT NULL IDENTITY,
    [maHoaDon] varchar(20) NOT NULL,
    [maNguoiTiepNhan] varchar(20) NOT NULL,
    [maNguoiXuLy] varchar(20) NULL,
    [kenh] varchar(20) NOT NULL,
    [khoanBiPhanAnh] nvarchar(255) NOT NULL,
    [noiDung] nvarchar(1000) NOT NULL,
    [trangThai] varchar(20) NOT NULL,
    [cachGiaiQuyet] nvarchar(1000) NULL,
    [taoLuc] datetime2 NOT NULL,
    [xuLyLuc] datetime2 NULL,
    CONSTRAINT [PK_PhanAnhHoaDon] PRIMARY KEY ([maPhanAnh]),
    CONSTRAINT [FK_PhanAnhHoaDon_HoaDon_maHoaDon] FOREIGN KEY ([maHoaDon]) REFERENCES [HoaDon] ([maHoaDon]) ON DELETE NO ACTION
);
GO

CREATE TABLE [PhuPhiDonThue] (
    [maKhoanPhi] bigint NOT NULL IDENTITY,
    [maDon] varchar(20) NOT NULL,
    [maPhuPhi] varchar(20) NOT NULL,
    [tenApDung] nvarchar(100) NOT NULL,
    [cachTinhApDung] varchar(20) NOT NULL,
    [soLuong] decimal(10,2) NOT NULL,
    [donGiaApDung] decimal(18,2) NOT NULL,
    [thanhTien] decimal(18,2) NOT NULL,
    [ghiNhanLuc] datetime2 NOT NULL,
    CONSTRAINT [PK_PhuPhiDonThue] PRIMARY KEY ([maKhoanPhi]),
    CONSTRAINT [FK_PhuPhiDonThue_PhuPhi_maPhuPhi] FOREIGN KEY ([maPhuPhi]) REFERENCES [PhuPhi] ([maPhuPhi]) ON DELETE NO ACTION
);
GO

CREATE UNIQUE INDEX [IX_HoaDon_maDon] ON [HoaDon] ([maDon]);
GO

CREATE INDEX [IX_PhanAnhHoaDon_maHoaDon] ON [PhanAnhHoaDon] ([maHoaDon]);
GO

CREATE INDEX [IX_PhuPhiDonThue_maPhuPhi] ON [PhuPhiDonThue] ([maPhuPhi]);
GO
ALTER TABLE HoaDon ADD CONSTRAINT FK_HoaDon_Don FOREIGN KEY(maDon) REFERENCES DonThue(maDon), CONSTRAINT FK_HoaDon_Admin FOREIGN KEY(maNguoiLap) REFERENCES TaiKhoan(maTaiKhoan), CONSTRAINT CK_HoaDon CHECK(tienThue>=0 AND tongPhuPhi>=0 AND tienGiam>=0 AND tongTien>=0 AND tongTien=CASE WHEN tienThue+tongPhuPhi-tienGiam<0 THEN 0 ELSE tienThue+tongPhuPhi-tienGiam END AND phienBan>=0);
ALTER TABLE GiaoDichThanhToan ADD CONSTRAINT FK_GiaoDich_Don FOREIGN KEY(maDon) REFERENCES DonThue(maDon), CONSTRAINT FK_GiaoDich_Admin FOREIGN KEY(maNguoiGhiNhan) REFERENCES TaiKhoan(maTaiKhoan), CONSTRAINT CK_GiaoDich CHECK(soTien>0 AND loaiKhoan IN ('DAT_COC','THANH_TOAN') AND phuongThuc IN ('TIEN_MAT','CHUYEN_KHOAN','KHAC'));
ALTER TABLE PhuPhiDonThue ADD CONSTRAINT FK_Phi_Don FOREIGN KEY(maDon) REFERENCES DonThue(maDon), CONSTRAINT CK_Phi CHECK(soLuong>=0 AND donGiaApDung>=0 AND thanhTien=ROUND(soLuong*donGiaApDung,2) AND cachTinhApDung IN ('CO_DINH','THEO_GIO_TRE'));
ALTER TABLE PhanAnhHoaDon ADD CONSTRAINT FK_PhanAnh_Nhan FOREIGN KEY(maNguoiTiepNhan) REFERENCES TaiKhoan(maTaiKhoan), CONSTRAINT FK_PhanAnh_XuLy FOREIGN KEY(maNguoiXuLy) REFERENCES TaiKhoan(maTaiKhoan), CONSTRAINT CK_PhanAnh CHECK(kenh IN ('TrucTiep','DienThoai') AND ((trangThai='ChoXuLy' AND xuLyLuc IS NULL AND maNguoiXuLy IS NULL) OR (trangThai='DaXuLy' AND xuLyLuc IS NOT NULL AND maNguoiXuLy IS NOT NULL AND LEN(LTRIM(RTRIM(cachGiaiQuyet)))>0)));
CREATE INDEX IX_ThanhToan_Don ON GiaoDichThanhToan(maDon) INCLUDE(soTien);
CREATE INDEX IX_Phi_Don ON PhuPhiDonThue(maDon);
GO
ALTER TABLE HoaDon ADD CONSTRAINT DF_HoaDon_Version DEFAULT 0 FOR phienBan;
GO

CREATE TABLE [KhuyenMai] (
    [maKhuyenMai] varchar(30) NOT NULL,
    [tenChuongTrinh] nvarchar(150) NOT NULL,
    [trangThai] varchar(20) NOT NULL,
    [batDau] datetime2 NOT NULL,
    [ketThuc] datetime2 NOT NULL,
    [ketThucThucTe] datetime2 NULL,
    [moTa] nvarchar(1000) NULL,
    [hinhThucGiam] varchar(15) NOT NULL,
    [mucGiam] decimal(18,2) NOT NULL,
    CONSTRAINT [PK_KhuyenMai] PRIMARY KEY ([maKhuyenMai])
);
GO

CREATE TABLE [ApDungKhuyenMai] (
    [maDon] varchar(20) NOT NULL,
    [maKhuyenMai] varchar(30) NOT NULL,
    [hinhThucChup] varchar(15) NOT NULL,
    [mucGiamChup] decimal(18,2) NOT NULL,
    [giaTriGiam] decimal(18,2) NOT NULL,
    [apDungLuc] datetime2 NOT NULL,
    CONSTRAINT [PK_ApDungKhuyenMai] PRIMARY KEY ([maDon]),
    CONSTRAINT [FK_ApDungKhuyenMai_KhuyenMai_maKhuyenMai] FOREIGN KEY ([maKhuyenMai]) REFERENCES [KhuyenMai] ([maKhuyenMai]) ON DELETE NO ACTION
);
GO

CREATE TABLE [DieuKienKhuyenMai] (
    [maDieuKien] bigint NOT NULL IDENTITY,
    [maKhuyenMai] varchar(30) NOT NULL,
    [loaiDieuKien] varchar(20) NOT NULL,
    [maNhom] varchar(20) NULL,
    [giaTriMuaVu] nvarchar(80) NULL,
    [tuLuc] datetime2 NULL,
    [denLuc] datetime2 NULL,
    CONSTRAINT [PK_DieuKienKhuyenMai] PRIMARY KEY ([maDieuKien]),
    CONSTRAINT [FK_DieuKienKhuyenMai_KhuyenMai_maKhuyenMai] FOREIGN KEY ([maKhuyenMai]) REFERENCES [KhuyenMai] ([maKhuyenMai]) ON DELETE NO ACTION
);
GO

CREATE INDEX [IX_ApDungKhuyenMai_maKhuyenMai] ON [ApDungKhuyenMai] ([maKhuyenMai]);
GO

CREATE INDEX [IX_DieuKienKhuyenMai_maKhuyenMai] ON [DieuKienKhuyenMai] ([maKhuyenMai]);
GO
ALTER TABLE KhuyenMai ADD CONSTRAINT CK_KhuyenMai CHECK(ketThuc>batDau AND mucGiam>0 AND (hinhThucGiam='SO_TIEN' OR (hinhThucGiam='PHAN_TRAM' AND mucGiam<=100)) AND trangThai IN ('SapApDung','DangApDung','TamNgung','DaKetThuc'));
ALTER TABLE DieuKienKhuyenMai ADD CONSTRAINT FK_DieuKien_Nhom FOREIGN KEY(maNhom) REFERENCES NhomKhachHang(maNhom), CONSTRAINT CK_DieuKien CHECK((loaiDieuKien='NHOM_KHACH' AND maNhom IS NOT NULL AND giaTriMuaVu IS NULL AND tuLuc IS NULL AND denLuc IS NULL) OR (loaiDieuKien='MUA_VU' AND giaTriMuaVu IS NOT NULL AND maNhom IS NULL AND tuLuc IS NULL AND denLuc IS NULL) OR (loaiDieuKien='THOI_DIEM' AND tuLuc IS NOT NULL AND denLuc IS NOT NULL AND denLuc>tuLuc AND maNhom IS NULL AND giaTriMuaVu IS NULL));
ALTER TABLE ApDungKhuyenMai ADD CONSTRAINT FK_ApDung_Don FOREIGN KEY(maDon) REFERENCES DonThue(maDon), CONSTRAINT CK_ApDung CHECK(giaTriGiam>=0 AND mucGiamChup>0 AND (hinhThucChup='SO_TIEN' OR (hinhThucChup='PHAN_TRAM' AND mucGiamChup<=100)));
GO

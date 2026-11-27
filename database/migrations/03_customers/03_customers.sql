SET XACT_ABORT ON;
BEGIN TRANSACTION;

CREATE TABLE dbo.NhomKhachHang (
    maNhom varchar(20) NOT NULL CONSTRAINT PK_NhomKhachHang PRIMARY KEY,
    tenNhom nvarchar(80) NOT NULL CONSTRAINT UQ_NhomKhachHang_Ten UNIQUE
);
CREATE TABLE dbo.KhachHang (
    maKhachHang varchar(20) NOT NULL CONSTRAINT PK_KhachHang PRIMARY KEY,
    maNhom varchar(20) NULL REFERENCES dbo.NhomKhachHang(maNhom),
    hoTen nvarchar(100) NOT NULL,
    soDienThoai varchar(20) NOT NULL,
    cccd varchar(20) NOT NULL CONSTRAINT UQ_KhachHang_CCCD UNIQUE,
    diaChi nvarchar(255) NOT NULL,
    soGPLX varchar(30) NOT NULL CONSTRAINT UQ_KhachHang_GPLX UNIQUE,
    hangGPLX varchar(20) NOT NULL,
    hetHanGPLX date NOT NULL,
    thongTinXacMinh nvarchar(500) NULL
);
CREATE INDEX IX_KhachHang_DienThoai ON dbo.KhachHang (soDienThoai);
CREATE TABLE dbo.DanhGiaTinNhiem (
    maTinNhiem bigint IDENTITY NOT NULL CONSTRAINT PK_DanhGiaTinNhiem PRIMARY KEY,
    maKhachHang varchar(20) NOT NULL REFERENCES dbo.KhachHang(maKhachHang),
    maNguoiDanhGia varchar(20) NOT NULL REFERENCES dbo.TaiKhoan(maTaiKhoan),
    mucTinNhiem smallint NOT NULL CHECK (mucTinNhiem > 0),
    nhanXet nvarchar(1000) NULL,
    thoiDiem datetime2 NOT NULL
);
CREATE INDEX IX_DanhGiaTinNhiem_MoiNhat ON dbo.DanhGiaTinNhiem (maKhachHang, thoiDiem DESC, maTinNhiem DESC);
INSERT dbo.NhomKhachHang (maNhom, tenNhom) VALUES ('THUONG', N'Khách thường'), ('THANTHIET', N'Khách thân thiết');
COMMIT;

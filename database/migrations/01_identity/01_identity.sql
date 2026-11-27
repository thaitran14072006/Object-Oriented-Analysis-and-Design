SET XACT_ABORT ON;
BEGIN TRANSACTION;

CREATE TABLE dbo.VaiTro (
    maVaiTro varchar(20) NOT NULL CONSTRAINT PK_VaiTro PRIMARY KEY,
    tenVaiTro nvarchar(80) NOT NULL CONSTRAINT UQ_VaiTro_Ten UNIQUE,
    toanQuyen bit NOT NULL CONSTRAINT DF_VaiTro_ToanQuyen DEFAULT 0
);
CREATE TABLE dbo.TaiKhoan (
    maTaiKhoan varchar(20) NOT NULL CONSTRAINT PK_TaiKhoan PRIMARY KEY,
    maVaiTro varchar(20) NOT NULL REFERENCES dbo.VaiTro(maVaiTro),
    hoTen nvarchar(100) NOT NULL,
    email varchar(254) NOT NULL CONSTRAINT UQ_TaiKhoan_Email UNIQUE,
    soDienThoai varchar(20) NOT NULL CONSTRAINT UQ_TaiKhoan_DienThoai UNIQUE,
    diaChi nvarchar(255) NULL,
    matKhauBam varchar(255) NOT NULL,
    trangThai varchar(20) NOT NULL CONSTRAINT CK_TaiKhoan_TrangThai CHECK (trangThai IN ('HoatDong','VoHieuHoa')),
    batHaiYeuTo bit NOT NULL CONSTRAINT DF_TaiKhoan_2FA DEFAULT 0,
    kenhOTP varchar(10) NULL,
    soLanSai int NOT NULL CONSTRAINT DF_TaiKhoan_Sai DEFAULT 0 CHECK (soLanSai >= 0),
    khoaDen datetime2 NULL,
    CONSTRAINT CK_TaiKhoan_Kenh CHECK (
        (kenhOTP IS NULL OR kenhOTP IN ('SMS','EMAIL')) AND (batHaiYeuTo = 0 OR kenhOTP IS NOT NULL)
    )
);
CREATE TABLE dbo.Quyen (
    maQuyen varchar(40) NOT NULL CONSTRAINT PK_Quyen PRIMARY KEY,
    chucNang varchar(50) NOT NULL,
    thaoTac varchar(30) NOT NULL,
    CONSTRAINT UQ_Quyen_ChucNang_ThaoTac UNIQUE (chucNang, thaoTac)
);
CREATE TABLE dbo.VaiTroQuyen (
    maVaiTro varchar(20) NOT NULL REFERENCES dbo.VaiTro(maVaiTro),
    maQuyen varchar(40) NOT NULL REFERENCES dbo.Quyen(maQuyen),
    CONSTRAINT PK_VaiTroQuyen PRIMARY KEY (maVaiTro, maQuyen)
);
CREATE TABLE dbo.QuyenCaNhan (
    maTaiKhoan varchar(20) NOT NULL REFERENCES dbo.TaiKhoan(maTaiKhoan),
    maQuyen varchar(40) NOT NULL REFERENCES dbo.Quyen(maQuyen),
    choPhep bit NOT NULL,
    CONSTRAINT PK_QuyenCaNhan PRIMARY KEY (maTaiKhoan, maQuyen)
);
CREATE TABLE dbo.DanhTinhLienKet (
    maLienKet bigint IDENTITY NOT NULL CONSTRAINT PK_DanhTinhLienKet PRIMARY KEY,
    maTaiKhoan varchar(20) NOT NULL REFERENCES dbo.TaiKhoan(maTaiKhoan),
    nhacungcap varchar(40) COLLATE Latin1_General_100_BIN2 NOT NULL,
    chuTheNgoai varchar(255) COLLATE Latin1_General_100_BIN2 NOT NULL,
    ngayLienKet datetime2 NOT NULL,
    CONSTRAINT UQ_DanhTinh_NhaCungCap_Subject UNIQUE (nhacungcap, chuTheNgoai),
    CONSTRAINT UQ_DanhTinh_TaiKhoan_NhaCungCap UNIQUE (maTaiKhoan, nhacungcap)
);
CREATE TABLE dbo.MaXacThuc (
    maXacThuc bigint IDENTITY NOT NULL CONSTRAINT PK_MaXacThuc PRIMARY KEY,
    maTaiKhoan varchar(20) NOT NULL REFERENCES dbo.TaiKhoan(maTaiKhoan),
    mucDich varchar(30) NOT NULL CHECK (mucDich IN ('DangNhap','KhoiPhuc','Bat2FA','DoiLienHe')),
    kenh varchar(10) NOT NULL CHECK (kenh IN ('SMS','EMAIL')),
    dichNhan varchar(254) NOT NULL,
    maBam varchar(255) NOT NULL,
    taoLuc datetime2 NOT NULL,
    hetHanLuc datetime2 NOT NULL,
    daDungLuc datetime2 NULL,
    soLanSai int NOT NULL CONSTRAINT DF_MaXacThuc_Sai DEFAULT 0 CHECK (soLanSai >= 0),
    CONSTRAINT CK_MaXacThuc_Han CHECK (hetHanLuc > taoLuc)
);
CREATE INDEX IX_MaXacThuc_MucDich ON dbo.MaXacThuc (maTaiKhoan, mucDich, kenh, maXacThuc DESC);
CREATE TABLE dbo.PhienDangNhap (
    maPhien bigint IDENTITY NOT NULL CONSTRAINT PK_PhienDangNhap PRIMARY KEY,
    maTaiKhoan varchar(20) NOT NULL REFERENCES dbo.TaiKhoan(maTaiKhoan),
    tokenBam varchar(255) NOT NULL CONSTRAINT UQ_PhienDangNhap_Token UNIQUE,
    taoLuc datetime2 NOT NULL,
    hoatDongCuoi datetime2 NOT NULL,
    thuHoiLuc datetime2 NULL
);
CREATE INDEX IX_PhienDangNhap_TaiKhoan ON dbo.PhienDangNhap (maTaiKhoan, thuHoiLuc);
CREATE TABLE dbo.TuyChonGiaoDien (
    maTaiKhoan varchar(20) NOT NULL CONSTRAINT PK_TuyChonGiaoDien PRIMARY KEY REFERENCES dbo.TaiKhoan(maTaiKhoan),
    mauSac varchar(20) NOT NULL,
    phongChu nvarchar(80) NOT NULL,
    boCuc varchar(40) NOT NULL,
    kichThuoc int NOT NULL CHECK (kichThuoc > 0)
);
CREATE TABLE dbo.NhatKyThaoTac (
    maNhatKy bigint IDENTITY NOT NULL CONSTRAINT PK_NhatKyThaoTac PRIMARY KEY,
    maTaiKhoan varchar(20) NULL REFERENCES dbo.TaiKhoan(maTaiKhoan),
    thoiDiem datetime2 NOT NULL,
    hanhDong varchar(60) NOT NULL,
    loaiDoiTuong varchar(50) NOT NULL,
    maDoiTuong varchar(50) NULL,
    ketQua varchar(30) NOT NULL CHECK (ketQua IN ('ThanhCong','ThatBai'))
);

CREATE INDEX IX_NhatKyThaoTac_TheoTaiKhoan ON dbo.NhatKyThaoTac
    (maTaiKhoan, hanhDong, maNhatKy DESC) INCLUDE (ketQua, thoiDiem);

INSERT dbo.VaiTro (maVaiTro, tenVaiTro, toanQuyen) VALUES
    ('ADMIN', N'Quản trị toàn quyền', 1), ('OPERATOR', N'Nhân viên vận hành', 0);

INSERT dbo.Quyen (maQuyen, chucNang, thaoTac) VALUES
    ('Admin.Access', 'Admin', 'Access'), ('Admin.Security', 'Admin', 'Security'),
    ('Customers.Read', 'Customers', 'Read'), ('Customers.Edit', 'Customers', 'Edit'),
    ('Customers.Rate', 'Customers', 'Rate'), ('Settings.Personal', 'Settings', 'Personal'),
    ('Fleet.Access', 'Fleet', 'Access'), ('Bookings.Access', 'Bookings', 'Access'),
    ('RentalSchedule.Access', 'RentalSchedule', 'Access'), ('Billing.Access', 'Billing', 'Access'),
    ('Reports.Access', 'Reports', 'Access'), ('Maintenance.Access', 'Maintenance', 'Access'),
    ('Promotions.Access', 'Promotions', 'Access'), ('Reviews.Access', 'Reviews', 'Access');
INSERT dbo.VaiTroQuyen (maVaiTro, maQuyen) VALUES
    ('OPERATOR', 'Admin.Access'), ('OPERATOR', 'Customers.Read'), ('OPERATOR', 'Settings.Personal');
COMMIT;

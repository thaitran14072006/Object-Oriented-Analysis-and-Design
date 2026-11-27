CREATE TABLE LichBaoTri (
    maBaoTri varchar(20) NOT NULL PRIMARY KEY,
    maXe varchar(20) NOT NULL REFERENCES Xe(maXe),
    maNguoiLap varchar(20) NOT NULL REFERENCES TaiKhoan(maTaiKhoan),
    loaiBaoTri varchar(20) NOT NULL CHECK(loaiBaoTri IN ('DinhKy','SuaChua','NangCap')),
    batDauDuKien datetime2 NOT NULL, ketThucDuKien datetime2 NOT NULL,
    batDauThucTe datetime2 NULL, ketThucThucTe datetime2 NULL,
    noiDung nvarchar(1000) NOT NULL,
    trangThai varchar(20) NOT NULL DEFAULT 'ChuaThucHien',
    CONSTRAINT CK_LichBaoTri_Lich CHECK(ketThucDuKien>batDauDuKien),
    CONSTRAINT CK_LichBaoTri_TrangThai CHECK(
        (trangThai='ChuaThucHien' AND batDauThucTe IS NULL AND ketThucThucTe IS NULL) OR
        (trangThai='DangThucHien' AND batDauThucTe IS NOT NULL AND ketThucThucTe IS NULL) OR
        (trangThai='HoanTat' AND batDauThucTe IS NOT NULL AND ketThucThucTe>=batDauThucTe AND ketThucThucTe IS NOT NULL))
);
CREATE TABLE TienDoBaoTri (
    maTienDo bigint IDENTITY NOT NULL PRIMARY KEY,
    maBaoTri varchar(20) NOT NULL REFERENCES LichBaoTri(maBaoTri),
    maNguoiCapNhat varchar(20) NOT NULL REFERENCES TaiKhoan(maTaiKhoan),
    noiDung nvarchar(1000) NOT NULL, thoiDiem datetime2 NOT NULL
);
CREATE TABLE ChiPhiBaoTri (
    maChiPhi bigint IDENTITY NOT NULL PRIMARY KEY,
    maBaoTri varchar(20) NOT NULL REFERENCES LichBaoTri(maBaoTri),
    hangMuc nvarchar(255) NOT NULL, donViThucHien nvarchar(255) NOT NULL,
    soTien decimal(18,2) NOT NULL CHECK(soTien>=0), ngayGhiNhan date NOT NULL
);
CREATE TABLE KiemTraAnToan (
    maKiemTra bigint IDENTITY NOT NULL PRIMARY KEY,
    maBaoTri varchar(20) NOT NULL REFERENCES LichBaoTri(maBaoTri),
    maNguoiGhiNhan varchar(20) NOT NULL REFERENCES TaiKhoan(maTaiKhoan),
    nguoiKiemTra nvarchar(100) NOT NULL, thoiDiem datetime2 NOT NULL,
    dat bit NOT NULL, ghiChu nvarchar(1000) NULL
);
CREATE INDEX IX_LichBaoTri_Xe_Lich ON LichBaoTri(maXe,batDauDuKien,ketThucDuKien);
CREATE INDEX IX_TienDoBaoTri_Lich ON TienDoBaoTri(maBaoTri,thoiDiem);
CREATE INDEX IX_ChiPhiBaoTri_Lich ON ChiPhiBaoTri(maBaoTri,ngayGhiNhan);
CREATE INDEX IX_KiemTraAnToan_Lich ON KiemTraAnToan(maBaoTri,thoiDiem DESC,maKiemTra DESC);
ALTER TABLE DonThue ADD CONSTRAINT FK_DonThue_Xe FOREIGN KEY(maXe) REFERENCES Xe(maXe);
GO

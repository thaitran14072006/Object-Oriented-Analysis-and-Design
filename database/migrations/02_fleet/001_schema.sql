CREATE TABLE LoaiXe (
    maLoaiXe varchar(20) NOT NULL PRIMARY KEY,
    tenLoaiXe nvarchar(80) NOT NULL UNIQUE,
    hangGPLXYeuCau varchar(20) NOT NULL
);
CREATE TABLE Xe (
    maXe varchar(20) NOT NULL PRIMARY KEY,
    maLoaiXe varchar(20) NOT NULL REFERENCES LoaiXe(maLoaiXe),
    tenXe nvarchar(100) NOT NULL, hangXe nvarchar(80) NOT NULL,
    bienSo varchar(20) NOT NULL UNIQUE, mauSac nvarchar(40) NOT NULL,
    namSanXuat smallint NOT NULL CHECK(namSanXuat BETWEEN 1900 AND YEAR(GETDATE())),
    soChoNgoi smallint NOT NULL CHECK(soChoNgoi>0), loaiHopSo nvarchar(30) NOT NULL,
    tinhTrang varchar(25) NOT NULL DEFAULT 'SanSang'
        CHECK(tinhTrang IN ('SanSang','DangChoThue','DangBaoTri','NgungKhaiThac')),
    viTriDo nvarchar(255) NULL, daXoa bit NOT NULL DEFAULT 0,
    CONSTRAINT CK_Xe_LuuTru CHECK(daXoa=0 OR tinhTrang='NgungKhaiThac')
);
CREATE TABLE HinhAnhXe (
    maHinhAnh bigint IDENTITY NOT NULL PRIMARY KEY,
    maXe varchar(20) NOT NULL REFERENCES Xe(maXe),
    duongDan nvarchar(500) NOT NULL, thuTu int NOT NULL CHECK(thuTu>=0)
);
CREATE INDEX IX_HinhAnhXe_Xe ON HinhAnhXe(maXe,thuTu);
CREATE INDEX IX_Xe_Loai_TrangThai ON Xe(maLoaiXe,tinhTrang,daXoa);
GO

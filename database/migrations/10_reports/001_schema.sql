CREATE TABLE BaoCaoDinhKy (
    maBaoCao bigint IDENTITY NOT NULL PRIMARY KEY,
    loaiBaoCao varchar(40) NOT NULL CHECK(loaiBaoCao='HIEU_QUA_KHUYEN_MAI'),
    tuNgay date NOT NULL, denNgay date NOT NULL,
    taoLuc datetime2 NOT NULL, duongDanTep nvarchar(500) NOT NULL,
    trangThai varchar(20) NOT NULL CHECK(trangThai IN ('ThanhCong','ThatBai')),
    CONSTRAINT CK_BaoCaoDinhKy_Ky CHECK(denNgay>=tuNgay),
    CONSTRAINT UQ_BaoCaoDinhKy_Ky UNIQUE(loaiBaoCao,tuNgay,denNgay)
);
GO

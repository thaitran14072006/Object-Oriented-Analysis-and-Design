CREATE TABLE [MucGiaThue] (
    [maMucGia] varchar(20) NOT NULL,
    [maLoaiXe] varchar(20) NOT NULL,
    [donViTinh] varchar(10) NOT NULL,
    [hinhThuc] varchar(20) NOT NULL,
    [muaVu] nvarchar(80) NOT NULL,
    [conSuDung] bit NOT NULL,
    [donGia] decimal(18,2) NOT NULL,
    [batDau] datetime2 NOT NULL,
    [ketThuc] datetime2 NOT NULL,
    CONSTRAINT [PK_MucGiaThue] PRIMARY KEY ([maMucGia])
);
GO

CREATE TABLE [PhuPhi] (
    [maPhuPhi] varchar(20) NOT NULL,
    [tenPhuPhi] nvarchar(100) NOT NULL,
    [cachTinh] varchar(20) NOT NULL,
    [moTa] nvarchar(500) NULL,
    [mucPhi] decimal(18,2) NOT NULL,
    [conSuDung] bit NOT NULL,
    CONSTRAINT [PK_PhuPhi] PRIMARY KEY ([maPhuPhi])
);
GO

CREATE UNIQUE INDEX [IX_PhuPhi_tenPhuPhi] ON [PhuPhi] ([tenPhuPhi]);
GO
ALTER TABLE MucGiaThue ADD CONSTRAINT FK_MucGia_Loai FOREIGN KEY (maLoaiXe) REFERENCES LoaiXe(maLoaiXe),
 CONSTRAINT CK_MucGia CHECK (donGia>0 AND ketThuc>batDau AND donViTinh IN ('GIO','NGAY') AND hinhThuc IN ('NGAN_HAN','DAI_HAN'));
ALTER TABLE PhuPhi ADD CONSTRAINT CK_PhuPhi CHECK(mucPhi>=0 AND cachTinh IN ('CO_DINH','THEO_GIO_TRE'));
GO
-- Defaults documented in RDM.
ALTER TABLE MucGiaThue ADD CONSTRAINT DF_MucGia_Active DEFAULT 1 FOR conSuDung;
ALTER TABLE PhuPhi ADD CONSTRAINT DF_PhuPhi_Active DEFAULT 1 FOR conSuDung;
GO

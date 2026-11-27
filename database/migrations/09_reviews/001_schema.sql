CREATE TABLE DanhGiaDichVu (
 Id bigint IDENTITY PRIMARY KEY, maDon varchar(20) NOT NULL UNIQUE REFERENCES DonThue(maDon),
 vehicleScore int NOT NULL CHECK(vehicleScore BETWEEN 1 AND 5), serviceScore int NOT NULL CHECK(serviceScore BETWEEN 1 AND 5),
 feedback nvarchar(1000) NOT NULL, improvement nvarchar(1000) NULL, created datetime2 NOT NULL,
 admin varchar(20) NOT NULL REFERENCES TaiKhoan(maTaiKhoan), reviewed datetime2 NULL
);
GO

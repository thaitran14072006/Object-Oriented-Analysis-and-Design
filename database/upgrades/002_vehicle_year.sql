SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF EXISTS (SELECT 1 FROM dbo.Xe WITH (TABLOCKX,HOLDLOCK) WHERE namSanXuat NOT BETWEEN 2000 AND YEAR(GETDATE()))
    THROW 51002, 'Xe has production years outside 2000-current year. Correct these records before upgrading.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.Xe') AND name=N'CK_Xe_NamSanXuat')
    ALTER TABLE dbo.Xe WITH CHECK ADD CONSTRAINT CK_Xe_NamSanXuat CHECK(namSanXuat BETWEEN 2000 AND YEAR(GETDATE()));
COMMIT TRANSACTION;
GO

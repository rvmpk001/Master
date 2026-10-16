SET XACT_ABORT ON;

IF DB_NAME() <> N'DemoShowcaseDB'
BEGIN
    THROW 51000, 'Run this migration only in DemoShowcaseDB.', 1;
END;

IF OBJECT_ID(N'dbo.Clientes', N'U') IS NULL
BEGIN
    THROW 51001, 'The dbo.Clientes table does not exist.', 1;
END;

BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.Clientes', N'EsVip') IS NULL
BEGIN
    ALTER TABLE dbo.Clientes ADD EsVip BIT NULL;
END;

IF COL_LENGTH(N'dbo.Clientes', N'TipoPersona') IS NULL
BEGIN
    ALTER TABLE dbo.Clientes ADD TipoPersona NVARCHAR(20) NULL;
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.check_constraints
    WHERE name = N'CK_Clientes_TipoPersona'
      AND parent_object_id = OBJECT_ID(N'dbo.Clientes')
)
BEGIN
    EXEC(N'ALTER TABLE dbo.Clientes WITH CHECK
        ADD CONSTRAINT CK_Clientes_TipoPersona
        CHECK (TipoPersona IS NULL OR TipoPersona IN (N''Fisica'', N''Juridica''));');
END;

COMMIT TRANSACTION;

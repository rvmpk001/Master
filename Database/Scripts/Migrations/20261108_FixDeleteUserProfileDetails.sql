SET XACT_ABORT ON;

IF DB_NAME() <> N'BlapaciSQL'
BEGIN
    THROW 51000, 'Run this migration only in BlapaciSQL.', 1;
END;

IF OBJECT_ID(N'dbo.sp_del_Usuario', N'P') IS NULL
BEGIN
    THROW 51001, 'The dbo.sp_del_Usuario procedure does not exist.', 1;
END;
GO

DELETE permissionDetail
FROM dbo.PerfilDetalle AS permissionDetail
WHERE permissionDetail.IdPerfil = 12
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.Perfiles AS profile
      WHERE profile.IdPerfil = permissionDetail.IdPerfil
  );
GO

ALTER PROCEDURE dbo.sp_del_Usuario
    @usuario nvarchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @UserId int = TRY_CONVERT(int, @usuario);
    IF @UserId IS NULL
    BEGIN
        SELECT 50002 AS ErrNumber, N'El identificador del usuario no es válido.' AS ErrMessage, 0 AS Id;
        RETURN;
    END;

    BEGIN TRY
        BEGIN TRANSACTION;

        IF NOT EXISTS
        (
            SELECT 1
            FROM dbo.[Password] WITH (UPDLOCK, HOLDLOCK)
            WHERE IdUsuario = @UserId
        )
        BEGIN
            ROLLBACK TRANSACTION;
            SELECT 50003 AS ErrNumber, N'No se encontró el usuario que se intentó borrar.' AS ErrMessage, 0 AS Id;
            RETURN;
        END;

        DELETE permissionDetail
        FROM dbo.PerfilDetalle AS permissionDetail
        LEFT JOIN dbo.Perfiles AS profile
            ON profile.IdPerfil = permissionDetail.IdPerfil
        WHERE permissionDetail.IdPerfil <> 1
          AND (
              profile.IdUsuario = @UserId
              OR (
                  profile.IdPerfil IS NULL
                  AND permissionDetail.IdUsuario = @UserId
              )
          );

        DELETE FROM dbo.Perfiles
        WHERE IdUsuario = @UserId
          AND IdPerfil <> 1;

        UPDATE dbo.Perfiles
        SET IdUsuario = NULL
        WHERE IdPerfil = 1
          AND IdUsuario = @UserId;

        DELETE FROM dbo.[Password]
        WHERE IdUsuario = @UserId;

        COMMIT TRANSACTION;
        SELECT 0 AS ErrNumber, N'Usuario Eliminado' AS ErrMessage, @UserId AS Id;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0
        BEGIN
            ROLLBACK TRANSACTION;
        END;

        SELECT ERROR_NUMBER() AS ErrNumber, ERROR_MESSAGE() AS ErrMessage, 0 AS Id;
    END CATCH;
END;
GO

using System.Data;
using Microsoft.Data.SqlClient;

namespace Master_WinUI.Services;

public sealed class UserAdministrationService
{
    private readonly SqlConnectionFactory _connectionFactory = new();

    public async Task<IReadOnlyList<SystemUserRecord>> GetUsersAsync(
        CancellationToken cancellationToken = default)
    {
        const string query = """
            SELECT IdUsuario, Usuario, Nombre, Clase
            FROM dbo.[Password]
            ORDER BY Usuario;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var users = new List<SystemUserRecord>();
        while (await reader.ReadAsync(cancellationToken))
        {
            users.Add(new SystemUserRecord(
                reader.GetInt32(0),
                reader.GetString(1).Trim(),
                reader.IsDBNull(2) ? string.Empty : reader.GetString(2).Trim(),
                reader.IsDBNull(3) ? string.Empty : reader.GetString(3).Trim()));
        }

        return users;
    }

    public async Task<IReadOnlyList<string>> GetRolesAsync(
        CancellationToken cancellationToken = default)
    {
        const string query = """
            SELECT Role
            FROM
            (
                SELECT DISTINCT LTRIM(RTRIM(Clase)) AS Role
                FROM dbo.[Password]
                WHERE Clase IS NOT NULL AND LTRIM(RTRIM(Clase)) <> ''

                UNION

                SELECT N'Administrador'

                UNION

                SELECT N'Usuario'
            ) AS roles
            ORDER BY CASE Role
                WHEN N'Administrador' THEN 0
                WHEN N'Usuario' THEN 1
                ELSE 2
            END, Role;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var roles = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
        {
            roles.Add(reader.GetString(0));
        }

        return roles;
    }

    public async Task AddUserAsync(
        string username,
        string password,
        string role,
        string name,
        string registeredBy,
        CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await using (var command = new SqlCommand("dbo.sp_ins_usuario", connection, transaction)
            {
                CommandType = CommandType.StoredProcedure
            })
            {
                command.Parameters.Add("@Save", SqlDbType.Int).Value = 1;
                command.Parameters.Add("@IdUsuario", SqlDbType.Int).Value = 0;
                command.Parameters.Add("@Usuario", SqlDbType.NVarChar, 15).Value = username;
                command.Parameters.Add("@Password", SqlDbType.NVarChar, 15).Value = password;
                command.Parameters.Add("@Clase", SqlDbType.NVarChar, 50).Value = role;
                command.Parameters.Add("@Nombre", SqlDbType.NVarChar, 50).Value = name;
                command.Parameters.Add("@Registra", SqlDbType.NVarChar, 10).Value =
                    registeredBy.Length > 10 ? registeredBy[..10] : registeredBy;
                await EnsureProcedureSucceededAsync(command, cancellationToken);
            }

            const string initializeDefaultPermissionsQuery = """
                UPDATE dbo.PerfilDetalle
                SET [Alta] = @AccessEnabled,
                    [Editar] = @AccessEnabled,
                    [Baja] = @AccessEnabled,
                    [Visualizar] = @AccessEnabled,
                    [Imprimir] = @AccessEnabled,
                    [Exportar] = @AccessEnabled,
                    [Por Vendedor] = @AccessEnabled,
                    [Autorizar] = @AccessEnabled,
                    [AutoOPSP] = @AccessEnabled,
                    [AutoOP] = @AccessEnabled,
                    [Opcion11] = @AccessEnabled,
                    [Opcion12] = @AccessEnabled,
                    [Opcion13] = @AccessEnabled,
                    [Opcion14] = @AccessEnabled,
                    [Opcion15] = @AccessEnabled,
                    [Opcion16] = @AccessEnabled,
                    [Opcion17] = @AccessEnabled,
                    [Opcion18] = @AccessEnabled,
                    [Opcion19] = @AccessEnabled,
                    [Opcion20] = @AccessEnabled,
                    [Estatus] = @AccessEnabled
                WHERE IdPerfil = (
                    SELECT TOP (1) IdPerfil
                    FROM dbo.Perfiles
                    WHERE IdUsuario = (
                        SELECT TOP (1) IdUsuario
                        FROM dbo.[Password]
                        WHERE Usuario = @Username
                    )
                );
                """;
            await using (var command = new SqlCommand(initializeDefaultPermissionsQuery, connection, transaction))
            {
                command.Parameters.Add("@Username", SqlDbType.NVarChar, 15).Value = username;
                command.Parameters.Add("@AccessEnabled", SqlDbType.Int).Value =
                    string.Equals(role.Trim(), "Administrador", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
                if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
                {
                    throw new InvalidOperationException(
                        "El usuario se creó sin un perfil de accesos. Se canceló la operación.");
                }
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task UpdateUserAsync(
        SystemUserRecord user,
        string username,
        string? password,
        string role,
        string name,
        string registeredBy,
        CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            const string updateUserQuery = """
                UPDATE dbo.[Password]
                SET Usuario = @Username,
                    [Password] = CASE WHEN @Password IS NULL THEN [Password] ELSE @Password END,
                    Clase = @Role,
                    Nombre = @Name,
                    Registra = @RegisteredBy
                WHERE IdUsuario = @UserId;
                """;
            await using (var command = new SqlCommand(updateUserQuery, connection, transaction))
            {
                command.Parameters.Add("@Username", SqlDbType.NVarChar, 15).Value = username;
                command.Parameters.Add("@Password", SqlDbType.NVarChar, 15).Value =
                    string.IsNullOrEmpty(password) ? DBNull.Value : password;
                command.Parameters.Add("@Role", SqlDbType.NVarChar, 50).Value = role;
                command.Parameters.Add("@Name", SqlDbType.NVarChar, 100).Value = name;
                command.Parameters.Add("@RegisteredBy", SqlDbType.NVarChar, 50).Value = registeredBy;
                command.Parameters.Add("@UserId", SqlDbType.Int).Value = user.Id;
                if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
                {
                    throw new InvalidOperationException("No se encontró el usuario que se intentó editar.");
                }
            }

            const string updateProfileQuery = """
                UPDATE dbo.Perfiles
                SET Nombre = @Username
                WHERE IdUsuario = @UserId;
                """;
            await using (var command = new SqlCommand(updateProfileQuery, connection, transaction))
            {
                command.Parameters.Add("@Username", SqlDbType.NVarChar, 150).Value = username;
                command.Parameters.Add("@UserId", SqlDbType.Int).Value = user.Id;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task DeleteUserAsync(
        SystemUserRecord user,
        CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            const string deletePermissionDetailsQuery = """
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
                """;
            await using (var command = new SqlCommand(deletePermissionDetailsQuery, connection, transaction))
            {
                command.Parameters.Add("@UserId", SqlDbType.Int).Value = user.Id;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            const string deleteProfilesQuery = """
                DELETE FROM dbo.Perfiles
                WHERE IdUsuario = @UserId
                  AND IdPerfil <> 1;
                """;
            await using (var command = new SqlCommand(deleteProfilesQuery, connection, transaction))
            {
                command.Parameters.Add("@UserId", SqlDbType.Int).Value = user.Id;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            const string detachTemplateProfileQuery = """
                UPDATE dbo.Perfiles
                SET IdUsuario = NULL
                WHERE IdPerfil = 1
                  AND IdUsuario = @UserId;
                """;
            await using (var command = new SqlCommand(detachTemplateProfileQuery, connection, transaction))
            {
                command.Parameters.Add("@UserId", SqlDbType.Int).Value = user.Id;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            const string deleteUserQuery = """
                DELETE FROM dbo.[Password]
                WHERE IdUsuario = @UserId;
                """;
            await using (var command = new SqlCommand(deleteUserQuery, connection, transaction))
            {
                command.Parameters.Add("@UserId", SqlDbType.Int).Value = user.Id;
                if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
                {
                    throw new InvalidOperationException("No se encontró el usuario que se intentó borrar.");
                }
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static async Task EnsureProcedureSucceededAsync(
        SqlCommand command,
        CancellationToken cancellationToken)
    {
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("La operación de usuarios no devolvió un resultado.");
        }

        var errorNumber = reader.GetOrdinal("ErrNumber");
        var errorMessage = reader.GetOrdinal("ErrMessage");
        if (!reader.IsDBNull(errorNumber) && Convert.ToInt32(reader.GetValue(errorNumber)) != 0)
        {
            var message = reader.IsDBNull(errorMessage)
                ? "No se pudo completar la operación de usuarios."
                : reader.GetString(errorMessage);
            throw new InvalidOperationException(message);
        }
    }
}

public sealed class SystemUserRecord(int id, string username, string name, string role)
{
    public int Id { get; set; } = id;
    public string Username { get; set; } = username;
    public string Name { get; set; } = name;
    public string Role { get; set; } = role;

    public override string ToString() => Username;
}

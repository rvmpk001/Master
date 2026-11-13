using System.Data;
using Microsoft.Data.SqlClient;

namespace Master_WinUI.Services;

public sealed class AuthenticationService
{
    private readonly SqlConnectionFactory _connectionFactory = new();

    public async Task<AuthenticatedUser?> AuthenticateAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string query = """
            SELECT TOP (1) IdUsuario, Usuario, Clase, Nombre
            FROM dbo.[Password]
            WHERE Usuario COLLATE Latin1_General_100_CS_AS = @Username COLLATE Latin1_General_100_CS_AS
              AND [Password] COLLATE Latin1_General_100_CS_AS = @Password COLLATE Latin1_General_100_CS_AS
            ORDER BY IdUsuario;
            """;

        await using var command = new SqlCommand(query, connection);
        command.Parameters.Add("@Username", SqlDbType.NVarChar, 15).Value = username;
        command.Parameters.Add("@Password", SqlDbType.NVarChar, 15).Value = password;

        AuthenticatedUser user;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            user = new AuthenticatedUser(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? "Usuario" : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                0);
        }

        var accessRecordId = await ExecuteAccessRegistrationAsync(
            connection,
            user.Username,
            user.Name,
            0,
            "N",
            "registrar la entrada",
            cancellationToken);

        return user with { AccessRecordId = accessRecordId };
    }

    public async Task RecordLogoutAsync(
        string username,
        string? name,
        int accessRecordId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await ExecuteAccessRegistrationAsync(
            connection,
            username,
            name,
            accessRecordId,
            "S",
            "registrar la salida",
            cancellationToken);
    }

    private static async Task<int> ExecuteAccessRegistrationAsync(
        SqlConnection connection,
        string username,
        string? name,
        int accessRecordId,
        string updateFlag,
        string operation,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand("dbo.sp_ins_Registro", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.Add("@Usuario", SqlDbType.NVarChar, 50).Value = username;
        command.Parameters.Add("@Nombre", SqlDbType.NVarChar, 200).Value =
            name is null ? DBNull.Value : name;
        command.Parameters.Add("@IdRegistro", SqlDbType.Int).Value = accessRecordId;
        command.Parameters.Add("@Actualizar", SqlDbType.Char, 1).Value = updateFlag;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new AccessLogException($"No se pudo {operation}: el procedimiento no devolvió un resultado.");
        }

        if (!string.Equals(reader.GetName(0), "ErrNumber", StringComparison.OrdinalIgnoreCase))
        {
            var errorNumber = reader.GetInt32(1);
            var errorMessage = reader.IsDBNull(2) ? "Error de SQL sin descripción." : reader.GetString(2);
            throw new AccessLogException($"No se pudo {operation} (SQL {errorNumber}): {errorMessage}");
        }

        var resultId = reader.GetInt32(2);
        if (resultId <= 0)
        {
            throw new AccessLogException($"No se pudo {operation}: el procedimiento devolvió un identificador inválido.");
        }

        return resultId;
    }
}

public sealed record AuthenticatedUser(int UserId, string Username, string Role, string? Name, int AccessRecordId);

public sealed class AccessLogException : Exception
{
    public AccessLogException(string message) : base(message)
    {
    }
}

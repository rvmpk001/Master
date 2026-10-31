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
            SELECT TOP (1) IdUsuario, Usuario, Clase
            FROM dbo.[Password]
            WHERE Usuario COLLATE Latin1_General_100_CS_AS = @Username COLLATE Latin1_General_100_CS_AS
              AND [Password] COLLATE Latin1_General_100_CS_AS = @Password COLLATE Latin1_General_100_CS_AS
            ORDER BY IdUsuario;
            """;

        await using var command = new SqlCommand(query, connection);
        command.Parameters.Add("@Username", System.Data.SqlDbType.NVarChar, 15).Value = username;
        command.Parameters.Add("@Password", System.Data.SqlDbType.NVarChar, 15).Value = password;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new AuthenticatedUser(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.IsDBNull(2) ? "Usuario" : reader.GetString(2));
    }
}

public sealed record AuthenticatedUser(int UserId, string Username, string Role);

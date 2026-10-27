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
            SELECT TOP (1) Usuario, Clase
            FROM dbo.[Password]
            WHERE Usuario = @Username AND [Password] = @Password
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
            reader.GetString(0),
            reader.IsDBNull(1) ? "Usuario" : reader.GetString(1));
    }
}

public sealed record AuthenticatedUser(string Username, string Role);

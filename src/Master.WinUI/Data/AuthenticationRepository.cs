using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;

namespace Master.WinUI.Data;

internal sealed class AuthenticationRepository(SqlServerConnectionFactory connectionFactory)
{
    public async Task<AuthenticatedUser?> AuthenticateAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT IdUsuario, Username, PasswordHash, NombreCompleto, Rol
            FROM dbo.Usuarios
            WHERE Username = @Username AND Activo = 1;
            """;
        command.Parameters.Add("@Username", SqlDbType.NVarChar, 50).Value = username;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var storedHash = reader.GetString(reader.GetOrdinal("PasswordHash"));
        if (!VerifyLegacySha256(password, storedHash))
        {
            return null;
        }

        return new AuthenticatedUser(
            reader.GetInt32(reader.GetOrdinal("IdUsuario")),
            reader.GetString(reader.GetOrdinal("Username")),
            reader.GetString(reader.GetOrdinal("NombreCompleto")),
            reader.GetString(reader.GetOrdinal("Rol")));
    }

    private static bool VerifyLegacySha256(string password, string storedHash)
    {
        if (storedHash.Length != 64)
        {
            return false;
        }

        byte[] expectedHash;
        try
        {
            expectedHash = Convert.FromHexString(storedHash);
        }
        catch (FormatException)
        {
            return false;
        }

        var passwordHash = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return CryptographicOperations.FixedTimeEquals(passwordHash, expectedHash);
    }
}

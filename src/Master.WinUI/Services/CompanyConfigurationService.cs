using Microsoft.Data.SqlClient;

namespace Master_WinUI.Services;

public sealed class CompanyConfigurationService
{
    private readonly SqlConnectionFactory _connectionFactory = new();

    public async Task<string?> GetPlantNameAsync(CancellationToken cancellationToken = default)
    {
        const string query = """
            SELECT TOP (1) NULLIF(LTRIM(RTRIM(Valor_Param)), N'')
            FROM dbo.Cia_Datos
            WHERE LTRIM(RTRIM(Param)) = N'ptl'
            ORDER BY Id;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull ? null : Convert.ToString(result)?.Trim();
    }
}

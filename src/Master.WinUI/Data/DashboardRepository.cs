using Microsoft.Data.SqlClient;

namespace Master.WinUI.Data;

internal sealed class DashboardRepository(SqlServerConnectionFactory connectionFactory)
{
    public async Task<DashboardMetrics> GetMetricsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                (SELECT COUNT_BIG(*) FROM dbo.Clientes) AS CustomerCount,
                (SELECT COUNT_BIG(*) FROM dbo.Productos) AS ProductCount,
                (SELECT COUNT_BIG(*) FROM dbo.Ventas) AS SalesCount,
                (SELECT COALESCE(SUM(Total), 0) FROM dbo.Ventas) AS TotalRevenue;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("SQL Server no devolvió los indicadores del Dashboard.");
        }

        return new DashboardMetrics(
            reader.GetInt64(reader.GetOrdinal("CustomerCount")),
            reader.GetInt64(reader.GetOrdinal("ProductCount")),
            reader.GetInt64(reader.GetOrdinal("SalesCount")),
            reader.GetDecimal(reader.GetOrdinal("TotalRevenue")));
    }
}

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
            DECLARE @CurrentMonth date =
                DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1);

            WITH MonthSeries AS
            (
                SELECT DATEADD(MONTH, -11, @CurrentMonth) AS MonthStart
                UNION ALL
                SELECT DATEADD(MONTH, 1, MonthStart)
                FROM MonthSeries
                WHERE MonthStart < @CurrentMonth
            ),
            RevenueByMonth AS
            (
                SELECT
                    DATEFROMPARTS(YEAR(FechaVenta), MONTH(FechaVenta), 1) AS MonthStart,
                    SUM(Total) AS TotalRevenue
                FROM dbo.Ventas
                WHERE FechaVenta >= DATEADD(MONTH, -11, @CurrentMonth)
                  AND FechaVenta < DATEADD(MONTH, 1, @CurrentMonth)
                GROUP BY DATEFROMPARTS(YEAR(FechaVenta), MONTH(FechaVenta), 1)
            )
            SELECT
                (SELECT COUNT_BIG(*) FROM dbo.Clientes) AS CustomerCount,
                (SELECT COUNT_BIG(*) FROM dbo.Productos) AS ProductCount,
                (SELECT COUNT_BIG(*) FROM dbo.Ventas) AS SalesCount,
                (SELECT COALESCE(SUM(Total), 0) FROM dbo.Ventas) AS TotalRevenue,
                MonthSeries.MonthStart,
                COALESCE(RevenueByMonth.TotalRevenue, 0) AS MonthlyRevenue
            FROM MonthSeries
            LEFT JOIN RevenueByMonth ON RevenueByMonth.MonthStart = MonthSeries.MonthStart
            ORDER BY MonthSeries.MonthStart
            OPTION (MAXRECURSION 12);
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var monthlyRevenue = new List<MonthlyRevenue>(12);
        long customerCount = 0;
        long productCount = 0;
        long salesCount = 0;
        decimal totalRevenue = 0;
        while (await reader.ReadAsync(cancellationToken))
        {
            if (monthlyRevenue.Count == 0)
            {
                customerCount = reader.GetInt64(reader.GetOrdinal("CustomerCount"));
                productCount = reader.GetInt64(reader.GetOrdinal("ProductCount"));
                salesCount = reader.GetInt64(reader.GetOrdinal("SalesCount"));
                totalRevenue = reader.GetDecimal(reader.GetOrdinal("TotalRevenue"));
            }

            monthlyRevenue.Add(new MonthlyRevenue(
                reader.GetDateTime(reader.GetOrdinal("MonthStart")),
                reader.GetDecimal(reader.GetOrdinal("MonthlyRevenue"))));
        }

        if (monthlyRevenue.Count == 0)
        {
            throw new InvalidOperationException("SQL Server no devolvió los indicadores del Dashboard.");
        }

        return new DashboardMetrics(
            customerCount,
            productCount,
            salesCount,
            totalRevenue,
            monthlyRevenue);
    }
}

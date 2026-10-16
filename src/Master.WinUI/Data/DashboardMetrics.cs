namespace Master.WinUI.Data;

internal sealed record DashboardMetrics(
    long CustomerCount,
    long ProductCount,
    long SalesCount,
    decimal TotalRevenue,
    IReadOnlyList<MonthlyRevenue> MonthlyRevenue);

internal sealed record MonthlyRevenue(DateTime MonthStart, decimal Total);

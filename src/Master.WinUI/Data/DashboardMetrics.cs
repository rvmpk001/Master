namespace Master.WinUI.Data;

internal sealed record DashboardMetrics(
    long CustomerCount,
    long ProductCount,
    long SalesCount,
    decimal TotalRevenue);

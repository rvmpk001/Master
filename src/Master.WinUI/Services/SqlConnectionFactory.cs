using Microsoft.Data.SqlClient;

namespace Master_WinUI.Services;

public sealed class SqlConnectionFactory
{
    private const string DefaultConnectionString =
        "Server=.\\SQLEXPRESS;Database=BlapaciSQL;Integrated Security=True;TrustServerCertificate=True;Encrypt=False;Connect Timeout=5;Application Name=Master.WinUI;";

    public SqlConnection CreateConnection()
    {
        var connectionString = Environment.GetEnvironmentVariable("BLAPACI_SQL_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = DefaultConnectionString;
        }

        return new SqlConnection(connectionString);
    }
}

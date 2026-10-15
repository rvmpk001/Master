using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace Master.WinUI.Data;

internal sealed class SqlServerConnectionFactory
{
    private readonly string _connectionString;

    public SqlServerConnectionFactory()
    {
        var settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        using var settingsStream = File.OpenRead(settingsPath);
        using var settings = JsonDocument.Parse(settingsStream);

        if (!settings.RootElement.TryGetProperty("ConnectionStrings", out var connectionStrings) ||
            !connectionStrings.TryGetProperty("Default", out var defaultConnectionString))
        {
            throw new InvalidDataException(
                $"No se encontró ConnectionStrings:Default en {settingsPath}.");
        }

        var configuredConnectionString = defaultConnectionString.GetString();
        if (string.IsNullOrWhiteSpace(configuredConnectionString))
        {
            throw new InvalidDataException("La cadena de conexión SQL está vacía.");
        }

        var builder = new SqlConnectionStringBuilder(configuredConnectionString);
        if (!builder.IntegratedSecurity)
        {
            throw new InvalidDataException(
                "La conexión a SQL Server debe utilizar autenticación integrada de Windows.");
        }

        _connectionString = builder.ConnectionString;
    }

    public SqlConnection CreateConnection() => new(_connectionString);
}

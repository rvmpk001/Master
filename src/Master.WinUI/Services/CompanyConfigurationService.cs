using System.Data;
using Microsoft.Data.SqlClient;

namespace Master_WinUI.Services;

public sealed class CompanyConfigurationService
{
    private static readonly string[] CompanyParameters =
    {
        "Cia", "Ptl", "UbiPtl", "CalleNum", "Col", "Munp", "CP", "Edo"
    };

    private readonly SqlConnectionFactory _connectionFactory = new();

    public async Task<CompanyConfigurationSettings> GetCompanyConfigurationAsync(
        CancellationToken cancellationToken = default)
    {
        const string query = """
            SELECT Param, Valor_Param
            FROM dbo.Cia_Datos
            WHERE Param IN (N'Cia', N'Ptl', N'UbiPtl', N'CalleNum', N'Col', N'Munp', N'CP', N'Edo');
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync(cancellationToken))
        {
            var parameter = reader.GetString(0).Trim();
            if (!values.TryAdd(parameter, reader.IsDBNull(1) ? string.Empty : reader.GetString(1).Trim()))
            {
                throw new InvalidOperationException($"La tabla dbo.Cia_Datos contiene más de un registro para '{parameter}'.");
            }
        }

        var missingParameters = CompanyParameters.Where(parameter => !values.ContainsKey(parameter)).ToArray();
        if (missingParameters.Length > 0)
        {
            throw new InvalidOperationException(
                $"Faltan parámetros en dbo.Cia_Datos: {string.Join(", ", missingParameters)}.");
        }

        return new CompanyConfigurationSettings(
            values["Cia"],
            values["Ptl"],
            values["UbiPtl"],
            values["CalleNum"],
            values["Col"],
            values["Munp"],
            values["CP"],
            values["Edo"]);
    }

    public async Task SaveCompanyConfigurationAsync(
        CompanyConfigurationSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var values = settings.ToParameterValues();
        foreach (var (parameter, value) in values)
        {
            if (value.Length > 255)
            {
                throw new InvalidOperationException($"El valor para '{parameter}' supera el máximo de 255 caracteres.");
            }
        }

        const string query = """
            UPDATE dbo.Cia_Datos
            SET Valor_Param = @Value
            WHERE Param = @Param;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            foreach (var (parameter, value) in values)
            {
                await using var command = new SqlCommand(query, connection, transaction);
                command.Parameters.Add("@Param", SqlDbType.NVarChar, 50).Value = parameter;
                command.Parameters.Add("@Value", SqlDbType.NVarChar, 255).Value = value;
                if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
                {
                    throw new InvalidOperationException(
                        $"Se esperaba exactamente un registro para '{parameter}' en dbo.Cia_Datos.");
                }
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

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

    public async Task<string?> GetMachineNameAsync(CancellationToken cancellationToken = default)
    {
        const string query = """
            SELECT TOP (1) NULLIF(LTRIM(RTRIM(Valor_Param)), N'')
            FROM dbo.ConfigMaq
            WHERE LTRIM(RTRIM(Param)) = N'Mq'
            ORDER BY Id;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull ? null : Convert.ToString(result)?.Trim();
    }
}

public sealed record CompanyConfigurationSettings(
    string CompanyName,
    string PlantName,
    string PlantLocation,
    string StreetAndNumber,
    string Neighborhood,
    string Municipality,
    string PostalCode,
    string State)
{
    public IReadOnlyDictionary<string, string> ToParameterValues() =>
        new Dictionary<string, string>
        {
            ["Cia"] = CompanyName,
            ["Ptl"] = PlantName,
            ["UbiPtl"] = PlantLocation,
            ["CalleNum"] = StreetAndNumber,
            ["Col"] = Neighborhood,
            ["Munp"] = Municipality,
            ["CP"] = PostalCode,
            ["Edo"] = State
        };
}

using System.Data;
using Microsoft.Data.SqlClient;

namespace Master_WinUI.Services;

public sealed class PlcCommunicationService
{
    private static readonly string[] ParameterNames =
    {
        "DirPLC", "Port", "Velocity", "DataBits", "Parity", "StopBits",
        "TimeOut", "Retry", "Salidas", "Tipo", "Demo"
    };

    private readonly SqlConnectionFactory _connectionFactory = new();

    public async Task<PlcCommunicationSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        const string query = """
            SELECT Param, Valor_Param
            FROM dbo.Comunicacion
            WHERE Param IN (
                N'DirPLC', N'Port', N'Velocity', N'DataBits', N'Parity', N'StopBits',
                N'TimeOut', N'Retry', N'Salidas', N'Tipo', N'Demo'
            );
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
                throw new InvalidOperationException($"La tabla dbo.Comunicacion contiene más de un registro para '{parameter}'.");
            }
        }

        var missingParameters = ParameterNames.Where(parameter => !values.ContainsKey(parameter)).ToArray();
        if (missingParameters.Length > 0)
        {
            throw new InvalidOperationException(
                $"Faltan parámetros en dbo.Comunicacion: {string.Join(", ", missingParameters)}.");
        }

        return new PlcCommunicationSettings(
            values["DirPLC"],
            values["Port"],
            values["Velocity"],
            values["DataBits"],
            values["Parity"],
            values["StopBits"],
            values["TimeOut"],
            values["Retry"],
            values["Salidas"],
            values["Tipo"],
            values["Demo"]);
    }

    public async Task SaveSettingsAsync(
        PlcCommunicationSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var values = settings.ToParameterValues();
        foreach (var (parameter, value) in values)
        {
            if (value.Length > 50)
            {
                throw new InvalidOperationException($"El valor para '{parameter}' supera el límite de 50 caracteres de dbo.Comunicacion.");
            }
        }

        const string query = """
            UPDATE dbo.Comunicacion
            SET Valor_Param = @Value
            WHERE Param = @Param;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        foreach (var (parameter, value) in values)
        {
            await using var command = new SqlCommand(query, connection, transaction);
            command.Parameters.Add("@Param", SqlDbType.NVarChar, 50).Value = parameter;
            command.Parameters.Add("@Value", SqlDbType.NVarChar, 50).Value = value;
            if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw new InvalidOperationException(
                    $"Se esperaba exactamente un registro para '{parameter}' en dbo.Comunicacion.");
            }
        }

        await transaction.CommitAsync(cancellationToken);
    }
}

public sealed record PlcCommunicationSettings(
    string PlcAddress,
    string Port,
    string Velocity,
    string DataBits,
    string Parity,
    string StopBits,
    string Timeout,
    string Retry,
    string Outputs,
    string ConnectionType,
    string ConfigurationMode)
{
    public IReadOnlyDictionary<string, string> ToParameterValues() =>
        new Dictionary<string, string>
        {
            ["DirPLC"] = PlcAddress,
            ["Port"] = Port,
            ["Velocity"] = Velocity,
            ["DataBits"] = DataBits,
            ["Parity"] = Parity,
            ["StopBits"] = StopBits,
            ["TimeOut"] = Timeout,
            ["Retry"] = Retry,
            ["Salidas"] = Outputs,
            ["Tipo"] = ConnectionType,
            ["Demo"] = ConfigurationMode
        };
}

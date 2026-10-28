using System.Data;
using Microsoft.Data.SqlClient;

namespace Master_WinUI.Services;

public sealed class AdditionalConfigurationService
{
    private static readonly string[] Parameters =
        Enumerable.Range(1, 16).Select(index => $"I{index}").ToArray();

    private readonly SqlConnectionFactory _connectionFactory = new();

    public async Task<AdditionalConfigurationSnapshot> GetConfigurationAsync(
        CancellationToken cancellationToken = default)
    {
        const string optionsQuery = """
            SELECT Nombre
            FROM dbo.Adicionales
            ORDER BY Nombre;
            """;
        const string selectionsQuery = """
            SELECT Param, Valor_Param
            FROM dbo.ConfigMaq
            WHERE Param IN
            (
                N'I1', N'I2', N'I3', N'I4', N'I5', N'I6', N'I7', N'I8',
                N'I9', N'I10', N'I11', N'I12', N'I13', N'I14', N'I15', N'I16'
            );
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var options = new List<string>();
        await using (var command = new SqlCommand(optionsQuery, connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                if (!reader.IsDBNull(0))
                {
                    var option = reader.GetString(0).Trim();
                    if (!string.IsNullOrWhiteSpace(option))
                    {
                        options.Add(option);
                    }
                }
            }
        }

        if (options.Count == 0)
        {
            throw new InvalidOperationException("dbo.Adicionales no contiene opciones.");
        }

        var selections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await using (var command = new SqlCommand(selectionsQuery, connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var parameter = reader.GetString(0).Trim();
                if (reader.IsDBNull(1))
                {
                    throw new InvalidOperationException(
                        $"El parámetro '{parameter}' de dbo.ConfigMaq no tiene un valor.");
                }

                if (!selections.TryAdd(parameter, reader.GetString(1).Trim()))
                {
                    throw new InvalidOperationException(
                        $"dbo.ConfigMaq contiene más de un registro para '{parameter}'.");
                }
            }
        }

        var missingParameters = Parameters.Where(parameter => !selections.ContainsKey(parameter)).ToArray();
        if (missingParameters.Length > 0)
        {
            throw new InvalidOperationException(
                $"Faltan parámetros en dbo.ConfigMaq: {string.Join(", ", missingParameters)}.");
        }

        return new AdditionalConfigurationSnapshot(
            options.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            selections);
    }

    public async Task SaveConfigurationAsync(
        IReadOnlyCollection<AdditionalConfigurationSelection> selections,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selections);
        var values = selections.ToDictionary(
            selection => selection.Parameter,
            selection => selection.Value,
            StringComparer.OrdinalIgnoreCase);
        if (Parameters.Any(parameter => !values.ContainsKey(parameter)) || values.Count != Parameters.Length)
        {
            throw new InvalidOperationException("Se requieren selecciones para los 16 parámetros Txt1 a Txt16.");
        }

        const string optionQuery = """
            SELECT COUNT(*)
            FROM dbo.Adicionales
            WHERE Nombre = @Value;
            """;
        const string parameterCountQuery = """
            SELECT COUNT(*)
            FROM dbo.ConfigMaq
            WHERE Param = @Param;
            """;
        const string updateQuery = """
            UPDATE dbo.ConfigMaq
            SET Valor_Param = @Value
            WHERE Param = @Param;
            """;
        const string insertQuery = """
            INSERT INTO dbo.ConfigMaq (Param, Valor_Param)
            VALUES (@Param, @Value);
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            foreach (var parameter in Parameters)
            {
                var value = values[parameter].Trim();
                if (string.IsNullOrWhiteSpace(value) || value.Length > 255)
                {
                    throw new InvalidOperationException(
                        $"El valor seleccionado para '{parameter}' está vacío o supera los 255 caracteres.");
                }

                await using (var optionCommand = new SqlCommand(optionQuery, connection, transaction))
                {
                    optionCommand.Parameters.Add("@Value", SqlDbType.NVarChar, 50).Value = value;
                    if (Convert.ToInt32(await optionCommand.ExecuteScalarAsync(cancellationToken)) != 1)
                    {
                        throw new InvalidOperationException(
                            $"El valor '{value}' ya no está disponible en dbo.Adicionales.");
                    }
                }

                int parameterCount;
                await using (var countCommand = new SqlCommand(parameterCountQuery, connection, transaction))
                {
                    countCommand.Parameters.Add("@Param", SqlDbType.NVarChar, 3).Value = parameter;
                    parameterCount = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken));
                }

                if (parameterCount > 1)
                {
                    throw new InvalidOperationException(
                        $"dbo.ConfigMaq contiene más de un registro para '{parameter}'.");
                }

                await using var saveCommand = new SqlCommand(
                    parameterCount == 0 ? insertQuery : updateQuery,
                    connection,
                    transaction);
                saveCommand.Parameters.Add("@Param", SqlDbType.NVarChar, 3).Value = parameter;
                saveCommand.Parameters.Add("@Value", SqlDbType.NVarChar, 255).Value = value;
                if (await saveCommand.ExecuteNonQueryAsync(cancellationToken) != 1)
                {
                    throw new InvalidOperationException(
                        $"No se pudo guardar exactamente un valor para '{parameter}' en dbo.ConfigMaq.");
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
}

public sealed record AdditionalConfigurationSnapshot(
    IReadOnlyList<string> Options,
    IReadOnlyDictionary<string, string> Selections);

public sealed record AdditionalConfigurationSelection(string Parameter, string Value);

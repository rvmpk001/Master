using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;

namespace Master_WinUI.Services;

public sealed class MachineConfigurationService
{
    private static readonly IReadOnlyDictionary<string, string> MaterialTables = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["AGREGADOS"] = "Agregados",
        ["CEMENTOS"] = "Cementos",
        ["ADITIVOS"] = "Aditivos",
        ["AGUA"] = "Agua",
        ["FIBRAS"] = "Fibras"
    };

    private static readonly IReadOnlyDictionary<string, string[]> MaterialParameters = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["AGREGADOS"] = Enumerable.Range(1, 8).Select(index => $"A{index}").ToArray(),
        ["CEMENTOS"] = Enumerable.Range(1, 5).Select(index => $"C{index}").ToArray(),
        ["ADITIVOS"] = Enumerable.Range(1, 6).Select(index => $"D{index}").ToArray(),
        ["AGUA"] = Enumerable.Range(1, 3).Select(index => $"G{index}").ToArray(),
        ["FIBRAS"] = Enumerable.Range(1, 3).Select(index => $"FI{index}").ToArray()
    };

    private static readonly string[] ConfigurationParameters = new[]
    {
        "Mq", "IM", "MU", "OS", "DC",
    }
        .Concat(MaterialParameters.Values.SelectMany(parameters => parameters))
        .Concat(Enumerable.Range(1, 8).Select(index => $"H{index}"))
        .Concat(Enumerable.Range(1, 8).Select(index => $"O{index}"))
        .ToArray();

    private readonly SqlConnectionFactory _connectionFactory = new();

    public async Task<MachineConfigurationSnapshot> GetConfigurationAsync(CancellationToken cancellationToken = default)
    {
        const string query = """
            SELECT N'AGREGADOS' AS Categoria, Nombre FROM dbo.Agregados
            UNION ALL SELECT N'CEMENTOS', Nombre FROM dbo.Cementos
            UNION ALL SELECT N'ADITIVOS', Nombre FROM dbo.Aditivos
            UNION ALL SELECT N'AGUA', Nombre FROM dbo.Agua
            UNION ALL SELECT N'FIBRAS', Nombre FROM dbo.Fibras
            ORDER BY Categoria, Nombre;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var materials = MaterialTables.Keys.ToDictionary(
            category => category,
            _ => (IReadOnlyList<string>)new List<string>(),
            StringComparer.Ordinal);
        await using (var command = new SqlCommand(query, connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            var mutableMaterials = MaterialTables.Keys.ToDictionary(
                category => category,
                _ => new List<string>(),
                StringComparer.Ordinal);
            while (await reader.ReadAsync(cancellationToken))
            {
                var category = reader.GetString(0);
                var name = reader.IsDBNull(1) ? string.Empty : reader.GetString(1).Trim();
                if (!string.IsNullOrWhiteSpace(name) && mutableMaterials.TryGetValue(category, out var names))
                {
                    names.Add(name);
                }
            }

            foreach (var category in MaterialTables.Keys)
            {
                materials[category] = mutableMaterials[category]
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
                    .ToArray();
            }
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var parameterNames = string.Join(", ", ConfigurationParameters.Select((_, index) => $"@Param{index}"));
        await using (var command = new SqlCommand(
            $"SELECT Param, Valor_Param FROM dbo.ConfigMaq WHERE Param IN ({parameterNames}) ORDER BY Id;",
            connection))
        {
            for (var index = 0; index < ConfigurationParameters.Length; index++)
            {
                command.Parameters.Add($"@Param{index}", SqlDbType.NVarChar, 3).Value = ConfigurationParameters[index];
            }

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var parameter = reader.GetString(0).Trim();
                if (!reader.IsDBNull(1))
                {
                    values.TryAdd(parameter, reader.GetString(1).Trim());
                }
            }
        }

        return new MachineConfigurationSnapshot(materials, values);
    }

    public async Task<MachineDashboardOptions> GetDashboardOptionsAsync(CancellationToken cancellationToken = default)
    {
        const string query = """
            SELECT Param, Valor_Param
            FROM dbo.ConfigMaq
            WHERE Param IN (N'MU', N'OS', N'DC');
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var humidityEnabled = false;
        var absorptionEnabled = false;
        var loadDownloadEnabled = false;

        while (await reader.ReadAsync(cancellationToken))
        {
            var parameter = reader.GetString(0).Trim();
            var isEnabled = !reader.IsDBNull(1) && reader.GetString(1).Trim() == "1";
            if (parameter.Equals("MU", StringComparison.OrdinalIgnoreCase))
            {
                humidityEnabled = isEnabled;
            }
            else if (parameter.Equals("OS", StringComparison.OrdinalIgnoreCase))
            {
                absorptionEnabled = isEnabled;
            }
            else if (parameter.Equals("DC", StringComparison.OrdinalIgnoreCase))
            {
                loadDownloadEnabled = isEnabled;
            }
        }

        return new MachineDashboardOptions(humidityEnabled, absorptionEnabled, loadDownloadEnabled);
    }

    public async Task SaveConfigurationAsync(
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        try
        {
            foreach (var parameter in ConfigurationParameters)
            {
                if (!values.TryGetValue(parameter, out var value))
                {
                    continue;
                }

                if (value.Length > 255)
                {
                    throw new InvalidOperationException($"El valor de {parameter} supera el máximo de 255 caracteres.");
                }

                const string query = """
                    UPDATE dbo.ConfigMaq
                    SET Valor_Param = @Value
                    WHERE Id = (
                        SELECT TOP (1) Id
                        FROM dbo.ConfigMaq
                        WHERE Param = @Param
                        ORDER BY Id
                    );

                    IF @@ROWCOUNT = 0
                        INSERT INTO dbo.ConfigMaq (Param, Valor_Param) VALUES (@Param, @Value);
                    """;
                await using var command = new SqlCommand(query, connection, transaction);
                command.Parameters.Add("@Param", SqlDbType.NVarChar, 3).Value = parameter;
                command.Parameters.Add("@Value", SqlDbType.NVarChar, 255).Value = value;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task AddMaterialAsync(string category, string name, CancellationToken cancellationToken = default)
    {
        var table = GetTable(category);
        var normalizedName = name.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new InvalidOperationException("Escribe el nombre del material.");
        }

        if (normalizedName.Length > 50)
        {
            throw new InvalidOperationException("El nombre no puede superar 50 caracteres.");
        }

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        try
        {
            var existsQuery = $"SELECT COUNT(*) FROM dbo.[{table}] WITH (UPDLOCK, HOLDLOCK) WHERE LTRIM(RTRIM(Nombre)) = @Name;";
            await using (var existsCommand = new SqlCommand(existsQuery, connection, transaction))
            {
                existsCommand.Parameters.Add("@Name", SqlDbType.NVarChar, 50).Value = normalizedName;
                if (Convert.ToInt32(await existsCommand.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) > 0)
                {
                    throw new InvalidOperationException($"El material {normalizedName} ya existe en {category}.");
                }
            }

            var insertQuery = $"INSERT INTO dbo.[{table}] (Nombre, Existencia) VALUES (@Name, N'0');";
            await using (var insertCommand = new SqlCommand(insertQuery, connection, transaction))
            {
                insertCommand.Parameters.Add("@Name", SqlDbType.NVarChar, 50).Value = normalizedName;
                await insertCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task DeleteMaterialAsync(string category, string name, CancellationToken cancellationToken = default)
    {
        var table = GetTable(category);
        var parameters = MaterialParameters[category];
        if (name.Equals("DESHABILITADA", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("No se puede borrar la opción DESHABILITADA.");
        }

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        try
        {
            var stockQuery = $"""
                SELECT Existencia
                FROM dbo.[{table}] WITH (UPDLOCK, HOLDLOCK)
                WHERE LTRIM(RTRIM(Nombre)) = @Name;
                """;
            var stockValues = new List<string?>();
            await using (var stockCommand = new SqlCommand(stockQuery, connection, transaction))
            {
                stockCommand.Parameters.Add("@Name", SqlDbType.NVarChar, 50).Value = name.Trim();
                await using var reader = await stockCommand.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    stockValues.Add(reader.IsDBNull(0) ? null : reader.GetString(0).Trim());
                }
            }

            if (stockValues.Count == 0)
            {
                throw new InvalidOperationException("El material ya no existe en la base de datos. Actualiza la lista.");
            }

            if (stockValues.Count > 1)
            {
                throw new InvalidOperationException("Hay nombres duplicados en el catálogo. No se borró ningún registro.");
            }

            var stockText = stockValues[0];
            if (!IsZeroStock(stockText))
            {
                throw new InvalidOperationException("No se puede borrar un material con existencia distinta de cero o no numérica.");
            }

            var parameterNames = string.Join(", ", parameters.Select((_, index) => $"@Param{index}"));
            var referencesQuery = $"""
                SELECT COUNT(*)
                FROM dbo.ConfigMaq WITH (UPDLOCK, HOLDLOCK)
                WHERE Param IN ({parameterNames})
                  AND LTRIM(RTRIM(Valor_Param)) = @Name;
                """;
            await using (var referencesCommand = new SqlCommand(referencesQuery, connection, transaction))
            {
                for (var index = 0; index < parameters.Length; index++)
                {
                    referencesCommand.Parameters.Add($"@Param{index}", SqlDbType.NVarChar, 3).Value = parameters[index];
                }

                referencesCommand.Parameters.Add("@Name", SqlDbType.NVarChar, 50).Value = name.Trim();
                if (Convert.ToInt32(await referencesCommand.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) > 0)
                {
                    throw new InvalidOperationException("No se puede borrar un material asignado a la configuración de la máquina.");
                }
            }

            var deleteQuery = $"DELETE FROM dbo.[{table}] WHERE LTRIM(RTRIM(Nombre)) = @Name;";
            await using (var deleteCommand = new SqlCommand(deleteQuery, connection, transaction))
            {
                deleteCommand.Parameters.Add("@Name", SqlDbType.NVarChar, 50).Value = name.Trim();
                if (await deleteCommand.ExecuteNonQueryAsync(cancellationToken) != 1)
                {
                    throw new InvalidOperationException("No se pudo borrar el material seleccionado.");
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

    private static string GetTable(string category) =>
        MaterialTables.TryGetValue(category, out var table)
            ? table
            : throw new InvalidOperationException($"Categoría de material no reconocida: {category}.");

    private static bool IsZeroStock(string? value)
    {
        if (value is null)
        {
            return false;
        }

        return (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var stock)
                || decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out stock))
            && stock == 0;
    }
}

public readonly record struct MachineDashboardOptions(
    bool HumidityEnabled,
    bool AbsorptionEnabled,
    bool LoadDownloadEnabled);

public sealed record MachineConfigurationSnapshot(
    IReadOnlyDictionary<string, IReadOnlyList<string>> MaterialOptions,
    IReadOnlyDictionary<string, string> Values);

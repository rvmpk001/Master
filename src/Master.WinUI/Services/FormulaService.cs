using System.Globalization;
using System.Data;
using Microsoft.Data.SqlClient;

namespace Master_WinUI.Services;

public sealed class FormulaService
{
    private readonly SqlConnectionFactory _connectionFactory = new();
    public static IReadOnlyList<string> EditableFields { get; } = new[]
    {
        "Cod_Formula", "Descripcion",
        "A1", "A1C", "A2", "A2C", "A3", "A3C", "A4", "A4C",
        "A5", "A5C", "A6", "A6C", "A7", "A7C", "A8", "A8C",
        "C1", "C1C", "C2", "C2C", "C3", "C3C", "C4", "C4C", "C5", "C5C",
        "D1", "D1C", "D2", "D2C", "D3", "D3C", "D4", "D4C", "D5", "D5C", "D6", "D6C",
        "Agua", "AguaC", "Agua2", "Agua2C", "Agua3", "Agua3C",
        "Fibra1", "Fibra1C", "Fibra2", "Fibra2C", "Fibra3", "Fibra3C",
        "TMA", "Resistencia", "Edad", "Revenimiento", "Aditivos", "Colocacion"
    };

    public async Task<FormulaTableData> GetFormulasAsync(CancellationToken cancellationToken = default)
    {
        const string query = "SELECT * FROM dbo.Formulas ORDER BY Id_Formula;";

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var columns = Enumerable.Range(0, reader.FieldCount)
            .Select(index => new FormulaColumn(GetColumnTitle(reader.GetName(index)), GetColumnWidth(reader.GetName(index))))
            .ToArray();
        var statusOrdinal = reader.GetOrdinal("Estatus");
        var activeRows = new List<FormulaRow>();
        var cancelledRows = new List<FormulaRow>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var cells = new FormulaCell[reader.FieldCount];
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < reader.FieldCount; index++)
            {
                var value = reader.IsDBNull(index)
                    ? string.Empty
                    : FormatValue(reader.GetValue(index));
                var name = reader.GetName(index);
                values[name] = value;
                cells[index] = new FormulaCell(name, columns[index].Title, value, columns[index].Width);
            }

            var id = reader.GetInt32(reader.GetOrdinal("Id_Formula"));
            var status = reader.IsDBNull(statusOrdinal)
                ? "A"
                : Convert.ToString(reader.GetValue(statusOrdinal), CultureInfo.InvariantCulture)?.Trim() ?? "A";
            var row = new FormulaRow(id, status, cells, values);
            (status.Equals("C", StringComparison.OrdinalIgnoreCase) ? cancelledRows : activeRows).Add(row);
        }

        return new FormulaTableData(columns, activeRows, cancelledRows);
    }

    public async Task<int> AddFormulaAsync(
        IReadOnlyDictionary<string, string> values,
        string username,
        CancellationToken cancellationToken = default)
    {
        ValidateFormulaIdentity(values);
        var columns = string.Join(", ", EditableFields.Select(field => $"[{field}]"));
        var parameters = string.Join(", ", EditableFields.Select(field => $"@{field}"));
        var query = $"""
            INSERT INTO dbo.Formulas ({columns}, [Creo], [FechaC], [Estatus])
            OUTPUT INSERTED.[Id_Formula]
            VALUES ({parameters}, @Creo, GETDATE(), 'A');
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
        await EnsureFormulaCodeAvailableAsync(connection, transaction, values, null, cancellationToken);
        await using var command = new SqlCommand(query, connection);
        command.Transaction = transaction;
        AddFormulaParameters(command, values);
        command.Parameters.Add("@Creo", System.Data.SqlDbType.NVarChar, 50).Value = username;
        var formulaId = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
        await transaction.CommitAsync(cancellationToken);
        return formulaId;
    }

    public async Task UpdateFormulaAsync(
        int formulaId,
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken = default)
    {
        ValidateFormulaIdentity(values);
        var assignments = string.Join(", ", EditableFields.Select(field => $"[{field}] = @{field}"));
        var query = $"""
            UPDATE dbo.Formulas
            SET {assignments}
            WHERE [Id_Formula] = @Id_Formula AND ISNULL([Estatus], 'A') <> 'C';
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
        await EnsureFormulaCodeAvailableAsync(connection, transaction, values, formulaId, cancellationToken);
        await using var command = new SqlCommand(query, connection);
        command.Transaction = transaction;
        AddFormulaParameters(command, values);
        command.Parameters.Add("@Id_Formula", System.Data.SqlDbType.Int).Value = formulaId;
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
        {
            throw new InvalidOperationException("La fórmula ya no existe o fue cancelada. Actualiza la lista e inténtalo de nuevo.");
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public static string? GetFormulaIdentityValidationError(string code, string description)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return "El código de fórmula es obligatorio.";
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            return "La descripción de fórmula es obligatoria.";
        }

        var codeWordCount = code.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        var descriptionWordCount = description.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        if (descriptionWordCount <= codeWordCount)
        {
            return "La descripción debe contener más palabras que el código.";
        }

        return null;
    }

    private static void ValidateFormulaIdentity(IReadOnlyDictionary<string, string> values)
    {
        var error = GetFormulaIdentityValidationError(
            values.GetValueOrDefault("Cod_Formula") ?? string.Empty,
            values.GetValueOrDefault("Descripcion") ?? string.Empty);
        if (error is not null)
        {
            throw new InvalidOperationException(error);
        }
    }

    private static async Task EnsureFormulaCodeAvailableAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        IReadOnlyDictionary<string, string> values,
        int? formulaId,
        CancellationToken cancellationToken)
    {
        const string query = """
            SELECT TOP (1) [Id_Formula]
            FROM dbo.Formulas WITH (UPDLOCK, HOLDLOCK)
            WHERE LTRIM(RTRIM([Cod_Formula])) = @Cod_Formula
              AND (@Id_Formula IS NULL OR [Id_Formula] <> @Id_Formula);
            """;

        await using var command = new SqlCommand(query, connection, transaction);
        command.Parameters.Add("@Cod_Formula", System.Data.SqlDbType.NVarChar, 20).Value =
            values.GetValueOrDefault("Cod_Formula")?.Trim() ?? string.Empty;
        command.Parameters.Add("@Id_Formula", System.Data.SqlDbType.Int).Value =
            formulaId.HasValue ? formulaId.Value : DBNull.Value;
        if (await command.ExecuteScalarAsync(cancellationToken) is not null)
        {
            throw new InvalidOperationException("El nombre de la fórmula ya existe. Usa otro nombre.");
        }
    }

    public async Task CancelFormulaAsync(
        int formulaId,
        string username,
        CancellationToken cancellationToken = default)
    {
        const string query = """
            UPDATE dbo.Formulas
            SET [Estatus] = 'C', [Borro] = @Borro, [FechaB] = GETDATE()
            WHERE [Id_Formula] = @Id_Formula AND ISNULL([Estatus], 'A') <> 'C';
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection);
        command.Parameters.Add("@Borro", System.Data.SqlDbType.NVarChar, 50).Value = username;
        command.Parameters.Add("@Id_Formula", System.Data.SqlDbType.Int).Value = formulaId;
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
        {
            throw new InvalidOperationException("La fórmula ya fue cancelada o no existe. Actualiza la lista e inténtalo de nuevo.");
        }
    }

    public async Task RestoreFormulaAsync(int formulaId, CancellationToken cancellationToken = default)
    {
        const string query = """
            UPDATE dbo.Formulas
            SET [Estatus] = 'A', [Borro] = NULL, [FechaB] = NULL
            WHERE [Id_Formula] = @Id_Formula AND [Estatus] = 'C';
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection);
        command.Parameters.Add("@Id_Formula", System.Data.SqlDbType.Int).Value = formulaId;
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
        {
            throw new InvalidOperationException("La fórmula ya está activa o no existe. Actualiza la lista e inténtalo de nuevo.");
        }
    }

    private static void AddFormulaParameters(SqlCommand command, IReadOnlyDictionary<string, string> values)
    {
        foreach (var field in EditableFields)
        {
            var value = values.TryGetValue(field, out var fieldValue) ? fieldValue : string.Empty;
            command.Parameters.Add($"@{field}", System.Data.SqlDbType.NVarChar, GetFieldMaxLength(field)).Value = value;
        }
    }

    public static string GetColumnTitle(string columnName)
    {
        if (columnName.Equals("Cod_Formula", StringComparison.OrdinalIgnoreCase))
        {
            return "Código";
        }

        if (columnName.Equals("Descripcion", StringComparison.OrdinalIgnoreCase))
        {
            return "Descripción";
        }

        if (columnName.Equals("FechaC", StringComparison.OrdinalIgnoreCase))
        {
            return "Fecha";
        }

        if (columnName.Equals("Id_Formula", StringComparison.OrdinalIgnoreCase))
        {
            return "ID";
        }

        if (columnName.Length >= 2 && char.IsDigit(columnName[^1]))
        {
            var suffix = columnName[^1..];
            var prefix = columnName[..^1];
            if (prefix is "A" or "C" or "D")
            {
                return $"{prefix switch { "A" => "Agregado", "C" => "Cemento", _ => "Aditivo" }}{suffix}";
            }

            if (prefix.Equals("Fibra", StringComparison.OrdinalIgnoreCase))
            {
                return $"Fibra {suffix}";
            }
        }

        if (columnName.EndsWith('C') && columnName.Length > 1)
        {
            var materialCode = columnName[..^1];
            if (materialCode.Length >= 2 && char.IsDigit(materialCode[^1]))
            {
                var suffix = materialCode[^1..];
                var prefix = materialCode[..^1];
                return prefix switch
                {
                    "A" => $"Cantidad A{suffix}",
                    "C" => $"Cantidad C{suffix}",
                    "D" => $"Cantidad Ad{suffix}",
                    "Fibra" => $"Cantidad Fb{suffix}",
                    _ => columnName
                };
            }

            if (materialCode is "Agua" or "Agua2" or "Agua3")
            {
                var suffix = materialCode == "Agua" ? "1" : materialCode[^1..];
                return $"Cantidad Ag{suffix}";
            }
        }

        return columnName switch
        {
            "Agua" => "Agua 1",
            "Agua2" => "Agua 2",
            "Agua3" => "Agua 3",
            _ => columnName
        };
    }

    private static double GetColumnWidth(string columnName)
    {
        if (columnName is "Descripcion" or "Cod_Formula" or "Creo" or "Borro")
        {
            return 150;
        }

        if (columnName.StartsWith("A", StringComparison.Ordinal)
            || columnName.StartsWith("C", StringComparison.Ordinal)
            || columnName.StartsWith("D", StringComparison.Ordinal)
            || columnName.StartsWith("Fibra", StringComparison.Ordinal)
            || columnName.StartsWith("Agua", StringComparison.Ordinal))
        {
            return 112;
        }

        return 105;
    }

    public static int GetFieldMaxLength(string fieldName) => fieldName switch
    {
        "Cod_Formula" => 20,
        "Descripcion" => 60,
        "TMA" or "Resistencia" or "Edad" or "Revenimiento" => 20,
        _ when fieldName.EndsWith('C') => 8,
        _ => 50
    };

    private static string FormatValue(object value) => value switch
    {
        DateTime date => date.ToString("dd-MMM-yyyy", CultureInfo.GetCultureInfo("es-MX")),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
    };
}

public sealed class FormulaColumn(string title, double width)
{
    public string Title { get; set; } = title;
    public double Width { get; set; } = width;
}

public sealed class FormulaCell(string name, string title, string value, double width)
{
    public string Name { get; set; } = name;
    public string Title { get; set; } = title;
    public string Value { get; set; } = value;
    public double Width { get; set; } = width;
}

public sealed class FormulaRow(int id, string status, IReadOnlyList<FormulaCell> cells, IReadOnlyDictionary<string, string> values)
{
    public int Id { get; set; } = id;
    public string Status { get; set; } = status;
    public IReadOnlyList<FormulaCell> Cells { get; set; } = cells;
    public IReadOnlyDictionary<string, string> Values { get; set; } = values;
}

public sealed record FormulaTableData(
    IReadOnlyList<FormulaColumn> Columns,
    IReadOnlyList<FormulaRow> ActiveRows,
    IReadOnlyList<FormulaRow> CancelledRows);

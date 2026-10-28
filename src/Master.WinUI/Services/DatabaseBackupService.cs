using System.Data;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace Master_WinUI.Services;

public sealed class DatabaseBackupService
{
    private const string DatabaseBackupHeader = "-- BLAPACI-DATABASE-BACKUP-V1";
    private const string ReportsBackupHeader = "-- BLAPACI-REPORTES-BACKUP-V1";
    private const string DataSectionHeader = "-- BLAPACI-DATA";
    private readonly SqlConnectionFactory _connectionFactory = new();

    public async Task ExportReportsAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await ExportTableAsync("dbo", "Reportes", ReportsBackupHeader, filePath, cancellationToken);
    }

    public async Task RestoreReportsAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var statements = await ReadBackupStatementsAsync(filePath, ReportsBackupHeader, cancellationToken);
        if (statements.Any(statement => statement.Schema != "dbo" || statement.Table != "Reportes"))
        {
            throw new InvalidDataException("El archivo contiene instrucciones para una tabla distinta de dbo.Reportes.");
        }

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var columns = await GetTableColumnsAsync(connection, "dbo", "Reportes", cancellationToken);
        ValidateInsertColumns(statements, new Dictionary<string, IReadOnlyList<ColumnDefinition>>
        {
            ["dbo.Reportes"] = columns
        });

        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await ExecuteNonQueryAsync(connection, transaction, "DELETE FROM [dbo].[Reportes];", cancellationToken);
            foreach (var statement in statements)
            {
                await ExecuteNonQueryAsync(connection, transaction, statement.Sql, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task ExportDatabaseAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var tables = await GetTablesAsync(connection, cancellationToken);
        var schema = new Dictionary<string, IReadOnlyList<ColumnDefinition>>(StringComparer.OrdinalIgnoreCase);
        foreach (var table in tables)
        {
            schema[TableKey(table.Schema, table.Name)] =
                await GetTableColumnsAsync(connection, table.Schema, table.Name, cancellationToken);
        }

        var output = new StringBuilder();
        output.AppendLine(DatabaseBackupHeader);
        output.AppendLine($"USE {QuoteIdentifier(connection.Database)};");
        output.AppendLine("GO");
        output.AppendLine("-- Esquema de tablas y datos de usuario. Para restaurar datos desde la app, la estructura destino debe coincidir.");
        output.AppendLine("SET XACT_ABORT ON;");
        foreach (var schemaName in tables.Select(table => table.Schema).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!string.Equals(schemaName, "dbo", StringComparison.OrdinalIgnoreCase))
            {
                var schemaLiteral = EscapeSqlString(schemaName);
                output.AppendLine($"IF SCHEMA_ID(N'{schemaLiteral}') IS NULL EXEC(N'CREATE SCHEMA {QuoteIdentifier(schemaName)}');");
            }
        }

        foreach (var table in tables)
        {
            var tableKey = TableKey(table.Schema, table.Name);
            var columns = schema[tableKey];
            output.AppendLine($"-- BLAPACI-TABLE:{Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(new TableMarker(table.Schema, table.Name, columns.Select(column => column.Name).ToArray())))}");
            output.AppendLine(await BuildCreateTableSqlAsync(connection, table, columns, cancellationToken));
            output.AppendLine("GO");
        }

        foreach (var table in tables)
        {
            output.AppendLine(await BuildForeignKeySqlAsync(connection, table, cancellationToken));
        }

        output.AppendLine(DataSectionHeader);
        foreach (var table in tables)
        {
            var tableKey = TableKey(table.Schema, table.Name);
            await AppendTableDataAsync(connection, table.Schema, table.Name, schema[tableKey], output, cancellationToken);
        }

        await File.WriteAllTextAsync(filePath, output.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), cancellationToken);
    }

    public async Task RestoreDatabaseAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var backup = await ReadDatabaseBackupAsync(filePath, cancellationToken);
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var targetTables = await GetTablesAsync(connection, cancellationToken);
        var targetSchema = new Dictionary<string, IReadOnlyList<ColumnDefinition>>(StringComparer.OrdinalIgnoreCase);
        foreach (var table in targetTables)
        {
            targetSchema[TableKey(table.Schema, table.Name)] =
                await GetTableColumnsAsync(connection, table.Schema, table.Name, cancellationToken);
        }

        ValidateDatabaseBackup(backup, targetTables, targetSchema);
        var constraintStates = await GetConstraintStatesAsync(connection, cancellationToken);
        var triggerStates = await GetTriggerStatesAsync(connection, cancellationToken);
        var statementsByTable = backup.Statements
            .GroupBy(statement => TableKey(statement.Schema, statement.Table), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);

        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var table in targetTables)
            {
                var name = QualifiedName(table.Schema, table.Name);
                await ExecuteNonQueryAsync(connection, transaction, $"ALTER TABLE {name} NOCHECK CONSTRAINT ALL;", cancellationToken);
                await ExecuteNonQueryAsync(connection, transaction, $"DISABLE TRIGGER ALL ON {name};", cancellationToken);
            }

            foreach (var table in targetTables)
            {
                await ExecuteNonQueryAsync(
                    connection,
                    transaction,
                    $"DELETE FROM {QualifiedName(table.Schema, table.Name)};",
                    cancellationToken);
            }

            foreach (var table in targetTables)
            {
                var key = TableKey(table.Schema, table.Name);
                if (!statementsByTable.TryGetValue(key, out var statements))
                {
                    continue;
                }

                var hasIdentity = targetSchema[key].Any(column => column.IsIdentity && !column.IsComputed);
                if (hasIdentity)
                {
                    await ExecuteNonQueryAsync(
                        connection,
                        transaction,
                        $"SET IDENTITY_INSERT {QualifiedName(table.Schema, table.Name)} ON;",
                        cancellationToken);
                }

                foreach (var statement in statements)
                {
                    await ExecuteNonQueryAsync(connection, transaction, statement.Sql, cancellationToken);
                }

                if (hasIdentity)
                {
                    await ExecuteNonQueryAsync(
                        connection,
                        transaction,
                        $"SET IDENTITY_INSERT {QualifiedName(table.Schema, table.Name)} OFF;",
                        cancellationToken);
                }
            }

            foreach (var state in triggerStates)
            {
                var action = state.IsDisabled ? "DISABLE" : "ENABLE";
                await ExecuteNonQueryAsync(
                    connection,
                    transaction,
                    $"{action} TRIGGER {QuoteIdentifier(state.Name)} ON {QualifiedName(state.Schema, state.Table)};",
                    cancellationToken);
            }

            foreach (var state in constraintStates)
            {
                var name = QualifiedName(state.Schema, state.Table);
                var constraint = QuoteIdentifier(state.Name);
                var sql = state.IsDisabled
                    ? $"ALTER TABLE {name} NOCHECK CONSTRAINT {constraint};"
                    : state.IsNotTrusted
                        ? $"ALTER TABLE {name} CHECK CONSTRAINT {constraint};"
                        : $"ALTER TABLE {name} WITH CHECK CHECK CONSTRAINT {constraint};";
                await ExecuteNonQueryAsync(connection, transaction, sql, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task ExportTableAsync(
        string schemaName,
        string tableName,
        string header,
        string filePath,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var columns = await GetTableColumnsAsync(connection, schemaName, tableName, cancellationToken);
        var output = new StringBuilder();
        output.AppendLine(header);
        output.AppendLine($"USE {QuoteIdentifier(connection.Database)};");
        output.AppendLine("GO");
        output.AppendLine(DataSectionHeader);
        await AppendTableDataAsync(connection, schemaName, tableName, columns, output, cancellationToken);
        await File.WriteAllTextAsync(filePath, output.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), cancellationToken);
    }

    private static async Task AppendTableDataAsync(
        SqlConnection connection,
        string schemaName,
        string tableName,
        IReadOnlyList<ColumnDefinition> columns,
        StringBuilder output,
        CancellationToken cancellationToken)
    {
        var insertColumns = columns
            .Where(column => !column.IsComputed && column.GeneratedAlwaysType == 0 && !column.IsTimestamp)
            .ToArray();
        var selectColumns = insertColumns.Length == 0 ? "[0]" : string.Join(", ", insertColumns.Select(column => QuoteIdentifier(column.Name)));
        var identityColumn = insertColumns.SingleOrDefault(column => column.IsIdentity);
        var orderBy = identityColumn is null
            ? string.Empty
            : $" ORDER BY {QuoteIdentifier(identityColumn.Name)}{(identityColumn.IdentityIncrement?.StartsWith("-", StringComparison.Ordinal) == true ? " DESC" : " ASC")}";
        var query = insertColumns.Length == 0
            ? $"SELECT TOP (1) 1 FROM {QualifiedName(schemaName, tableName)};"
            : $"SELECT {selectColumns} FROM {QualifiedName(schemaName, tableName)}{orderBy};";
        await using var command = new SqlCommand(query, connection) { CommandTimeout = 0 };
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken);
        var tableIdentifier = QualifiedName(schemaName, tableName);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (insertColumns.Length == 0)
            {
                output.AppendLine($"INSERT INTO {tableIdentifier} DEFAULT VALUES;");
                continue;
            }

            var values = new string[insertColumns.Length];
            for (var index = 0; index < insertColumns.Length; index++)
            {
                values[index] = SqlLiteral(reader.GetValue(index), insertColumns[index].TypeName);
            }

            output.Append("INSERT INTO ")
                .Append(tableIdentifier)
                .Append(" (")
                .Append(string.Join(", ", insertColumns.Select(column => QuoteIdentifier(column.Name))))
                .Append(") VALUES (")
                .Append(string.Join(", ", values))
                .AppendLine(");");
        }
    }

    private static async Task<IReadOnlyList<TableDefinition>> GetTablesAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string query = """
            SELECT s.name, t.name
            FROM sys.tables AS t
            INNER JOIN sys.schemas AS s ON s.schema_id = t.schema_id
            WHERE t.is_ms_shipped = 0
            ORDER BY s.name, t.name;
            """;
        var tables = new List<TableDefinition>();
        await using var command = new SqlCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            tables.Add(new TableDefinition(reader.GetString(0), reader.GetString(1)));
        }

        if (tables.Count == 0)
        {
            throw new InvalidOperationException("La base de datos no contiene tablas de usuario para respaldar.");
        }

        return tables;
    }

    private static async Task<IReadOnlyList<ColumnDefinition>> GetTableColumnsAsync(
        SqlConnection connection,
        string schemaName,
        string tableName,
        CancellationToken cancellationToken)
    {
        const string query = """
            SELECT c.name, ty.name, ty.is_user_defined, c.max_length, c.precision, c.scale,
                   c.is_nullable, c.is_identity, c.is_computed, cc.definition, cc.is_persisted,
                   dc.definition, c.collation_name, c.system_type_id,
                   ic.seed_value, ic.increment_value
            FROM sys.tables AS t
            INNER JOIN sys.schemas AS s ON s.schema_id = t.schema_id
            INNER JOIN sys.columns AS c ON c.object_id = t.object_id
            INNER JOIN sys.types AS ty ON ty.user_type_id = c.user_type_id
            LEFT JOIN sys.computed_columns AS cc ON cc.object_id = c.object_id AND cc.column_id = c.column_id
            LEFT JOIN sys.default_constraints AS dc ON dc.object_id = c.default_object_id
            LEFT JOIN sys.identity_columns AS ic ON ic.object_id = c.object_id AND ic.column_id = c.column_id
            WHERE s.name = @schema AND t.name = @table
            ORDER BY c.column_id;
            """;
        var result = new List<ColumnDefinition>();
        await using var command = new SqlCommand(query, connection);
        command.Parameters.Add("@schema", SqlDbType.NVarChar, 128).Value = schemaName;
        command.Parameters.Add("@table", SqlDbType.NVarChar, 128).Value = tableName;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var typeName = reader.GetString(1);
            var isUserDefined = reader.GetBoolean(2);
            if (isUserDefined)
            {
                throw new InvalidOperationException(
                    $"La tabla {schemaName}.{tableName} usa el tipo definido por usuario '{typeName}'. No se puede generar un respaldo autocontenido para este tipo.");
            }

            result.Add(new ColumnDefinition(
                reader.GetString(0),
                typeName,
                reader.GetInt16(3),
                reader.GetByte(4),
                reader.GetByte(5),
                reader.GetBoolean(6),
                reader.GetBoolean(7),
                reader.GetBoolean(8),
                reader.IsDBNull(9) ? null : reader.GetString(9),
                !reader.IsDBNull(10) && reader.GetBoolean(10),
                reader.IsDBNull(11) ? null : reader.GetString(11),
                reader.IsDBNull(12) ? null : reader.GetString(12),
                0,
                reader.GetByte(13) == 189,
                reader.IsDBNull(14) ? null : Convert.ToString(reader.GetValue(14), CultureInfo.InvariantCulture),
                reader.IsDBNull(15) ? null : Convert.ToString(reader.GetValue(15), CultureInfo.InvariantCulture)));
        }

        if (result.Count == 0)
        {
            throw new InvalidOperationException($"No se encontraron columnas para la tabla {schemaName}.{tableName}.");
        }

        return result;
    }

    private static async Task<string> BuildCreateTableSqlAsync(
        SqlConnection connection,
        TableDefinition table,
        IReadOnlyList<ColumnDefinition> columns,
        CancellationToken cancellationToken)
    {
        var definitions = new List<string>();
        foreach (var column in columns)
        {
            if (column.IsComputed)
            {
                definitions.Add($"{QuoteIdentifier(column.Name)} AS ({column.ComputedDefinition}){(column.IsPersisted ? " PERSISTED" : string.Empty)}");
                continue;
            }

            var definition = new StringBuilder()
                .Append(QuoteIdentifier(column.Name))
                .Append(' ')
                .Append(FormatSqlType(column));
            if (column.IsIdentity)
            {
                definition.Append(" IDENTITY(")
                    .Append(column.IdentitySeed ?? "1")
                    .Append(',')
                    .Append(column.IdentityIncrement ?? "1")
                    .Append(')');
            }

            if (!string.IsNullOrWhiteSpace(column.Collation))
            {
                definition.Append(" COLLATE ").Append(QuoteIdentifier(column.Collation));
            }
            definition.Append(column.IsNullable ? " NULL" : " NOT NULL");
            if (!string.IsNullOrWhiteSpace(column.DefaultDefinition))
            {
                definition.Append(" DEFAULT ").Append(column.DefaultDefinition);
            }
            definitions.Add(definition.ToString());
        }

        var keys = await GetKeyConstraintsAsync(connection, table, cancellationToken);
        definitions.AddRange(keys.Select(key =>
            $"CONSTRAINT {QuoteIdentifier(key.Name)} {key.Kind} ({string.Join(", ", key.Columns.Select(QuoteIdentifier))})"));
        var checks = await GetCheckConstraintsAsync(connection, table, cancellationToken);
        definitions.AddRange(checks.Select(check =>
            $"CONSTRAINT {QuoteIdentifier(check.Name)} CHECK {check.Definition}"));

        return $"CREATE TABLE {QualifiedName(table.Schema, table.Name)} (\r\n    {string.Join(",\r\n    ", definitions)}\r\n);";
    }

    private static async Task<IReadOnlyList<KeyConstraint>> GetKeyConstraintsAsync(
        SqlConnection connection,
        TableDefinition table,
        CancellationToken cancellationToken)
    {
        const string query = """
            SELECT i.name, i.is_primary_key, ic.key_ordinal, c.name
            FROM sys.tables AS t
            INNER JOIN sys.schemas AS s ON s.schema_id = t.schema_id
            INNER JOIN sys.indexes AS i ON i.object_id = t.object_id
            INNER JOIN sys.index_columns AS ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            INNER JOIN sys.columns AS c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            WHERE s.name = @schema AND t.name = @table
              AND (i.is_primary_key = 1 OR i.is_unique_constraint = 1)
              AND ic.key_ordinal > 0
            ORDER BY i.name, ic.key_ordinal;
            """;
        var keys = new List<KeyConstraint>();
        await using var command = new SqlCommand(query, connection);
        command.Parameters.Add("@schema", SqlDbType.NVarChar, 128).Value = table.Schema;
        command.Parameters.Add("@table", SqlDbType.NVarChar, 128).Value = table.Name;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var name = reader.GetString(0);
            var isPrimary = reader.GetBoolean(1);
            var key = keys.FirstOrDefault(item => item.Name == name);
            if (key is null)
            {
                key = new KeyConstraint(name, isPrimary ? "PRIMARY KEY" : "UNIQUE", new List<string>());
                keys.Add(key);
            }
            key.Columns.Add(reader.GetString(3));
        }

        return keys;
    }

    private static async Task<IReadOnlyList<CheckConstraint>> GetCheckConstraintsAsync(
        SqlConnection connection,
        TableDefinition table,
        CancellationToken cancellationToken)
    {
        const string query = """
            SELECT cc.name, cc.definition
            FROM sys.check_constraints AS cc
            INNER JOIN sys.tables AS t ON t.object_id = cc.parent_object_id
            INNER JOIN sys.schemas AS s ON s.schema_id = t.schema_id
            WHERE s.name = @schema AND t.name = @table AND cc.is_disabled = 0;
            """;
        var checks = new List<CheckConstraint>();
        await using var command = new SqlCommand(query, connection);
        command.Parameters.Add("@schema", SqlDbType.NVarChar, 128).Value = table.Schema;
        command.Parameters.Add("@table", SqlDbType.NVarChar, 128).Value = table.Name;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            checks.Add(new CheckConstraint(reader.GetString(0), reader.GetString(1)));
        }

        return checks;
    }

    private static async Task<IReadOnlyList<ConstraintState>> GetConstraintStatesAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string query = """
            SELECT s.name, t.name, fk.name, fk.is_disabled, fk.is_not_trusted
            FROM sys.foreign_keys AS fk
            INNER JOIN sys.tables AS t ON t.object_id = fk.parent_object_id
            INNER JOIN sys.schemas AS s ON s.schema_id = t.schema_id
            UNION ALL
            SELECT s.name, t.name, cc.name, cc.is_disabled, cc.is_not_trusted
            FROM sys.check_constraints AS cc
            INNER JOIN sys.tables AS t ON t.object_id = cc.parent_object_id
            INNER JOIN sys.schemas AS s ON s.schema_id = t.schema_id;
            """;
        var result = new List<ConstraintState>();
        await using var command = new SqlCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new ConstraintState(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetBoolean(3),
                reader.GetBoolean(4)));
        }

        return result;
    }

    private static async Task<IReadOnlyList<TriggerState>> GetTriggerStatesAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string query = """
            SELECT s.name, t.name, tr.name, tr.is_disabled
            FROM sys.triggers AS tr
            INNER JOIN sys.tables AS t ON t.object_id = tr.parent_id
            INNER JOIN sys.schemas AS s ON s.schema_id = t.schema_id
            WHERE tr.is_ms_shipped = 0;
            """;
        var result = new List<TriggerState>();
        await using var command = new SqlCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new TriggerState(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetBoolean(3)));
        }

        return result;
    }

    private static async Task<string> BuildForeignKeySqlAsync(
        SqlConnection connection,
        TableDefinition table,
        CancellationToken cancellationToken)
    {
        const string query = """
            SELECT fk.name, rs.name, rt.name, fkc.constraint_column_id, pc.name, rc.name,
                   fk.delete_referential_action_desc, fk.update_referential_action_desc
            FROM sys.foreign_keys AS fk
            INNER JOIN sys.tables AS pt ON pt.object_id = fk.parent_object_id
            INNER JOIN sys.schemas AS ps ON ps.schema_id = pt.schema_id
            INNER JOIN sys.tables AS rt ON rt.object_id = fk.referenced_object_id
            INNER JOIN sys.schemas AS rs ON rs.schema_id = rt.schema_id
            INNER JOIN sys.foreign_key_columns AS fkc ON fkc.constraint_object_id = fk.object_id
            INNER JOIN sys.columns AS pc ON pc.object_id = fkc.parent_object_id AND pc.column_id = fkc.parent_column_id
            INNER JOIN sys.columns AS rc ON rc.object_id = fkc.referenced_object_id AND rc.column_id = fkc.referenced_column_id
            WHERE ps.name = @schema AND pt.name = @table
            ORDER BY fk.name, fkc.constraint_column_id;
            """;
        var foreignKeys = new List<ForeignKeyConstraint>();
        await using var command = new SqlCommand(query, connection);
        command.Parameters.Add("@schema", SqlDbType.NVarChar, 128).Value = table.Schema;
        command.Parameters.Add("@table", SqlDbType.NVarChar, 128).Value = table.Name;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var name = reader.GetString(0);
            var foreignKey = foreignKeys.FirstOrDefault(item => item.Name == name);
            if (foreignKey is null)
            {
                foreignKey = new ForeignKeyConstraint(
                    name,
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(6),
                    reader.GetString(7),
                    new List<string>(),
                    new List<string>());
                foreignKeys.Add(foreignKey);
            }
            foreignKey.Columns.Add(reader.GetString(4));
            foreignKey.ReferencedColumns.Add(reader.GetString(5));
        }

        return string.Join(
            Environment.NewLine,
            foreignKeys.Select(foreignKey =>
                $"ALTER TABLE {QualifiedName(table.Schema, table.Name)} ADD CONSTRAINT {QuoteIdentifier(foreignKey.Name)} FOREIGN KEY ({string.Join(", ", foreignKey.Columns.Select(QuoteIdentifier))}) REFERENCES {QualifiedName(foreignKey.ReferencedSchema, foreignKey.ReferencedTable)} ({string.Join(", ", foreignKey.ReferencedColumns.Select(QuoteIdentifier))}){FormatReferentialAction(" ON DELETE ", foreignKey.DeleteAction)}{FormatReferentialAction(" ON UPDATE ", foreignKey.UpdateAction)};"));
    }

    private static async Task<IReadOnlyList<InsertStatement>> ReadBackupStatementsAsync(
        string filePath,
        string expectedHeader,
        CancellationToken cancellationToken)
    {
        var content = await File.ReadAllTextAsync(filePath, cancellationToken);
        var firstNewLine = content.IndexOfAny(new[] { '\r', '\n' });
        var header = firstNewLine < 0 ? content : content[..firstNewLine];
        if (!string.Equals(header, expectedHeader, StringComparison.Ordinal))
        {
            throw new InvalidDataException("El archivo no es un respaldo SQL compatible generado por esta aplicación.");
        }

        var dataMarker = content.IndexOf(DataSectionHeader, firstNewLine < 0 ? content.Length : firstNewLine, StringComparison.Ordinal);
        if (dataMarker < 0)
        {
            throw new InvalidDataException("El respaldo no contiene la sección de datos.");
        }

        var sectionStart = content.IndexOf('\n', dataMarker);
        var data = sectionStart < 0 ? string.Empty : content[(sectionStart + 1)..];
        return SplitSqlStatements(data).Select(ParseInsertStatement).ToArray();
    }

    private static async Task<ParsedDatabaseBackup> ReadDatabaseBackupAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        var content = await File.ReadAllTextAsync(filePath, cancellationToken);
        var firstNewLine = content.IndexOfAny(new[] { '\r', '\n' });
        var header = firstNewLine < 0 ? content : content[..firstNewLine];
        if (!string.Equals(header, DatabaseBackupHeader, StringComparison.Ordinal))
        {
            throw new InvalidDataException("El archivo no es un respaldo completo compatible generado por esta aplicación.");
        }

        var markers = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        var dataMarker = content.IndexOf(DataSectionHeader, firstNewLine < 0 ? content.Length : firstNewLine, StringComparison.Ordinal);
        if (dataMarker < 0)
        {
            throw new InvalidDataException("El respaldo está incompleto: faltan el esquema de tablas o la sección de datos.");
        }

        var schemaText = content.Substring(firstNewLine + 1, dataMarker - firstNewLine - 1);
        using var reader = new StringReader(schemaText);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            const string markerPrefix = "-- BLAPACI-TABLE:";
            if (line.StartsWith(markerPrefix, StringComparison.Ordinal))
            {
                try
                {
                    var markerBytes = Convert.FromBase64String(line[markerPrefix.Length..]);
                    var marker = JsonSerializer.Deserialize<TableMarker>(markerBytes)
                        ?? throw new InvalidDataException("Marcador de tabla vacío.");
                    if (string.IsNullOrWhiteSpace(marker.Schema)
                        || string.IsNullOrWhiteSpace(marker.Name)
                        || marker.Columns is null
                        || marker.Columns.Length == 0)
                    {
                        throw new InvalidDataException("El respaldo contiene un marcador de tabla incompleto.");
                    }

                    if (!markers.TryAdd(TableKey(marker.Schema, marker.Name), marker.Columns))
                    {
                        throw new InvalidDataException("El respaldo contiene tablas duplicadas.");
                    }
                }
                catch (FormatException exception)
                {
                    throw new InvalidDataException("El respaldo contiene un marcador de tabla inválido.", exception);
                }
                catch (JsonException exception)
                {
                    throw new InvalidDataException("El respaldo contiene metadatos de tabla inválidos.", exception);
                }
            }
        }

        if (markers.Count == 0)
        {
            throw new InvalidDataException("El respaldo está incompleto: faltan el esquema de tablas o la sección de datos.");
        }

        var dataSectionStart = content.IndexOf('\n', dataMarker);
        var data = dataSectionStart < 0 ? string.Empty : content[(dataSectionStart + 1)..];
        return new ParsedDatabaseBackup(markers, SplitSqlStatements(data).Select(ParseInsertStatement).ToArray());
    }

    private static IReadOnlyList<string> SplitSqlStatements(string content)
    {
        var statements = new List<string>();
        var start = 0;
        var inString = false;
        for (var index = 0; index < content.Length; index++)
        {
            if (content[index] == '\'')
            {
                if (inString && index + 1 < content.Length && content[index + 1] == '\'')
                {
                    index++;
                    continue;
                }
                inString = !inString;
            }
            else if (!inString && content[index] == ';')
            {
                var statement = content[start..(index + 1)].Trim();
                if (statement.Length > 0 && !statement.StartsWith("--", StringComparison.Ordinal))
                {
                    statements.Add(statement);
                }
                start = index + 1;
            }
        }

        var remainder = content[start..].Trim();
        if (!string.IsNullOrWhiteSpace(remainder) && !remainder.StartsWith("--", StringComparison.Ordinal))
        {
            throw new InvalidDataException("El respaldo contiene una instrucción SQL incompleta.");
        }

        if (inString)
        {
            throw new InvalidDataException("El respaldo contiene una cadena SQL sin cerrar.");
        }

        return statements;
    }

    private static InsertStatement ParseInsertStatement(string sql)
    {
        const string prefix = "INSERT INTO ";
        if (!sql.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !sql.EndsWith(';'))
        {
            throw new InvalidDataException("El archivo contiene una instrucción distinta de un INSERT permitido.");
        }

        var position = prefix.Length;
        var schema = ReadBracketedIdentifier(sql, ref position);
        Expect(sql, ref position, ".");
        var table = ReadBracketedIdentifier(sql, ref position);
        var columns = new List<string>();
        var values = new List<string>();
        SkipWhitespace(sql, ref position);

        if (sql.AsSpan(position).StartsWith("DEFAULT VALUES;", StringComparison.OrdinalIgnoreCase))
        {
            position += "DEFAULT VALUES;".Length;
            if (position != sql.Length)
            {
                throw new InvalidDataException("El INSERT predeterminado contiene texto no permitido.");
            }
            return new InsertStatement(schema, table, Array.Empty<string>(), Array.Empty<string>(), sql);
        }

        Expect(sql, ref position, "(");
        while (true)
        {
            columns.Add(ReadBracketedIdentifier(sql, ref position));
            SkipWhitespace(sql, ref position);
            if (Consume(sql, ref position, ","))
            {
                continue;
            }
            Expect(sql, ref position, ")");
            break;
        }

        SkipWhitespace(sql, ref position);
        Expect(sql, ref position, "VALUES");
        SkipWhitespace(sql, ref position);
        Expect(sql, ref position, "(");
        values.AddRange(ReadSqlLiterals(sql, ref position));
        SkipWhitespace(sql, ref position);
        Expect(sql, ref position, ";");
        if (position != sql.Length || columns.Count != values.Count)
        {
            throw new InvalidDataException("El INSERT contiene columnas o valores inválidos.");
        }

        return new InsertStatement(schema, table, columns, values, sql);
    }

    private static IEnumerable<string> ReadSqlLiterals(string sql, ref int position)
    {
        var values = new List<string>();
        var start = position;
        var inString = false;
        var nestedParentheses = 0;
        for (; position < sql.Length; position++)
        {
            var character = sql[position];
            if (!inString && (character == 'N' || character == 'n') && position + 1 < sql.Length && sql[position + 1] == '\'')
            {
                inString = true;
                position++;
                continue;
            }

            if (character == '\'')
            {
                if (inString && position + 1 < sql.Length && sql[position + 1] == '\'')
                {
                    position++;
                    continue;
                }
                inString = !inString;
                if (!inString)
                {
                    var literal = sql[start..(position + 1)].Trim();
                    ValidateSqlLiteral(literal);
                }
                continue;
            }

            if (!inString && character == '(')
            {
                nestedParentheses++;
            }
            else if (!inString && character == ')' && nestedParentheses > 0)
            {
                nestedParentheses--;
            }
            else if (!inString && nestedParentheses == 0 && character == ',')
            {
                var literal = sql[start..position].Trim();
                ValidateSqlLiteral(literal);
                values.Add(literal);
                start = position + 1;
            }
            else if (!inString && nestedParentheses == 0 && character == ')')
            {
                var literal = sql[start..position].Trim();
                ValidateSqlLiteral(literal);
                values.Add(literal);
                position++;
                return values;
            }
        }

        throw new InvalidDataException("El INSERT contiene una cadena SQL sin cerrar o una lista de valores incompleta.");
    }

    private static void ValidateSqlLiteral(string literal)
    {
        if (literal.Equals("NULL", StringComparison.OrdinalIgnoreCase)
            || Regex.IsMatch(literal, @"^(?:N)?'(?:''|[^'])*'$", RegexOptions.CultureInvariant)
            || Regex.IsMatch(literal, @"^0x(?:[0-9A-Fa-f]{2})*$", RegexOptions.CultureInvariant)
            || Regex.IsMatch(literal, @"^[+-]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][+-]?\d+)?$", RegexOptions.CultureInvariant))
        {
            return;
        }

        var castMatch = Regex.Match(
            literal,
            @"^CONVERT\(\s*\[?(?:ntext|text|image)\]?\s*,\s*(.*?)\s*\)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (castMatch.Success)
        {
            ValidateSqlLiteral(castMatch.Groups[1].Value);
            return;
        }

        throw new InvalidDataException("El respaldo contiene un valor SQL no permitido.");
    }

    private static void ValidateInsertColumns(
        IReadOnlyList<InsertStatement> statements,
        IReadOnlyDictionary<string, IReadOnlyList<ColumnDefinition>> schema)
    {
        foreach (var statement in statements)
        {
            var key = TableKey(statement.Schema, statement.Table);
            if (!schema.TryGetValue(key, out var columns))
            {
                throw new InvalidDataException($"El respaldo hace referencia a una tabla no permitida: {key}.");
            }

            var validColumns = columns.Select(column => column.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (statement.Columns.Any(column => !validColumns.Contains(column)))
            {
                throw new InvalidDataException($"El respaldo contiene columnas que no existen en {key}.");
            }
        }
    }

    private static void ValidateDatabaseBackup(
        ParsedDatabaseBackup backup,
        IReadOnlyList<TableDefinition> targetTables,
        IReadOnlyDictionary<string, IReadOnlyList<ColumnDefinition>> targetSchema)
    {
        var targetTableKeys = targetTables.Select(table => TableKey(table.Schema, table.Name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!targetTableKeys.SetEquals(backup.TableMarkers.Keys))
        {
            throw new InvalidDataException("La lista de tablas del respaldo no coincide con la base de datos actual.");
        }

        foreach (var (tableKey, backupColumns) in backup.TableMarkers)
        {
            var actualColumns = targetSchema[tableKey].Select(column => column.Name);
            if (!actualColumns.SequenceEqual(backupColumns, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"La estructura de columnas de {tableKey} no coincide con el respaldo.");
            }
        }

        ValidateInsertColumns(backup.Statements, targetSchema);
    }

    private static async Task ExecuteNonQueryAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(sql, connection, transaction) { CommandTimeout = 0 };
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string FormatSqlType(ColumnDefinition column)
    {
        var type = QuoteIdentifier(column.TypeName);
        return column.TypeName.ToLowerInvariant() switch
        {
            "varchar" or "char" or "varbinary" or "binary" =>
                $"{type}({(column.MaxLength == -1 ? "max" : column.MaxLength.ToString(CultureInfo.InvariantCulture))})",
            "nvarchar" or "nchar" =>
                $"{type}({(column.MaxLength == -1 ? "max" : (column.MaxLength / 2).ToString(CultureInfo.InvariantCulture))})",
            "decimal" or "numeric" => $"{type}({column.Precision},{column.Scale})",
            "datetime2" or "datetimeoffset" or "time" => $"{type}({column.Scale})",
            "float" => $"{type}({column.Precision})",
            _ => type
        };
    }

    private static string SqlLiteral(object value, string columnType)
    {
        if (value is DBNull)
        {
            return "NULL";
        }

        var literal = value switch
        {
            string text => $"N'{EscapeSqlString(text)}'",
            char character => $"N'{EscapeSqlString(character.ToString())}'",
            DateTime dateTime => $"'{dateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffffff", CultureInfo.InvariantCulture)}'",
            DateTimeOffset dateTimeOffset => $"'{dateTimeOffset.ToString("yyyy-MM-ddTHH:mm:ss.fffffffzzz", CultureInfo.InvariantCulture)}'",
            TimeSpan time => $"'{time.ToString("c", CultureInfo.InvariantCulture)}'",
            Guid guid => $"'{guid:D}'",
            byte[] bytes => $"0x{Convert.ToHexString(bytes)}",
            bool boolean => boolean ? "1" : "0",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => throw new InvalidOperationException($"No se admite el tipo de dato '{value.GetType().Name}' en el respaldo SQL.")
        };

        return columnType.ToLowerInvariant() switch
        {
            "ntext" or "text" when value is string => $"CONVERT({columnType}, {literal})",
            "image" when value is byte[] => $"CONVERT(image, {literal})",
            _ => literal
        };
    }

    private static string FormatReferentialAction(string prefix, string action) =>
        action switch
        {
            "NO_ACTION" => string.Empty,
            "CASCADE" => $"{prefix}CASCADE",
            "SET_NULL" => $"{prefix}SET NULL",
            "SET_DEFAULT" => $"{prefix}SET DEFAULT",
            _ => throw new InvalidOperationException($"Acción de clave foránea no admitida: {action}.")
        };

    private static string EscapeSqlString(string value) => value.Replace("'", "''", StringComparison.Ordinal);
    private static string TableKey(string schemaName, string tableName) => $"{schemaName}.{tableName}";
    private static string QualifiedName(string schemaName, string tableName) => $"{QuoteIdentifier(schemaName)}.{QuoteIdentifier(tableName)}";
    private static string QuoteIdentifier(string value) => $"[{value.Replace("]", "]]", StringComparison.Ordinal)}]";

    private static void SkipWhitespace(string sql, ref int position)
    {
        while (position < sql.Length && char.IsWhiteSpace(sql[position]))
        {
            position++;
        }
    }

    private static string ReadBracketedIdentifier(string sql, ref int position)
    {
        SkipWhitespace(sql, ref position);
        Expect(sql, ref position, "[");
        var value = new StringBuilder();
        while (position < sql.Length)
        {
            if (sql[position] == ']')
            {
                if (position + 1 < sql.Length && sql[position + 1] == ']')
                {
                    value.Append(']');
                    position += 2;
                    continue;
                }
                position++;
                return value.ToString();
            }
            value.Append(sql[position++]);
        }

        throw new InvalidDataException("El respaldo contiene un identificador SQL sin cerrar.");
    }

    private static void Expect(string sql, ref int position, string expected)
    {
        SkipWhitespace(sql, ref position);
        if (!sql.AsSpan(position).StartsWith(expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"El respaldo SQL no tiene el formato esperado cerca de '{expected}'.");
        }
        position += expected.Length;
    }

    private static bool Consume(string sql, ref int position, string expected)
    {
        SkipWhitespace(sql, ref position);
        if (!sql.AsSpan(position).StartsWith(expected, StringComparison.Ordinal))
        {
            return false;
        }
        position += expected.Length;
        return true;
    }

    private sealed record TableDefinition(string Schema, string Name);
    private sealed record TableMarker(string Schema, string Name, string[] Columns);
    private sealed record CheckConstraint(string Name, string Definition);
    private sealed record ConstraintState(string Schema, string Table, string Name, bool IsDisabled, bool IsNotTrusted);
    private sealed record TriggerState(string Schema, string Table, string Name, bool IsDisabled);
    private sealed record InsertStatement(string Schema, string Table, IReadOnlyList<string> Columns, IReadOnlyList<string> Values, string Sql);
    private sealed record ParsedDatabaseBackup(
        IReadOnlyDictionary<string, string[]> TableMarkers,
        IReadOnlyList<InsertStatement> Statements);
    private sealed record ForeignKeyConstraint(
        string Name,
        string ReferencedSchema,
        string ReferencedTable,
        string DeleteAction,
        string UpdateAction,
        List<string> Columns,
        List<string> ReferencedColumns);
    private sealed record ColumnDefinition(
        string Name,
        string TypeName,
        short MaxLength,
        byte Precision,
        byte Scale,
        bool IsNullable,
        bool IsIdentity,
        bool IsComputed,
        string? ComputedDefinition,
        bool IsPersisted,
        string? DefaultDefinition,
        string? Collation,
        byte GeneratedAlwaysType,
        bool IsTimestamp,
        string? IdentitySeed,
        string? IdentityIncrement);
    private sealed record KeyConstraint(string Name, string Kind, List<string> Columns);
}

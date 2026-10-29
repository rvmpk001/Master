using System.Data;
using System.Globalization;
using System.Text;
using Microsoft.Data.SqlClient;

namespace Master_WinUI.Services;

public sealed class AccessConfigurationService
{
    private static readonly (string Column, string TitleColumn)[] ActionColumns =
    [
        ("Alta", "TituloAlta"),
        ("Editar", "TituloEdicion"),
        ("Baja", "TituloBaja"),
        ("Visualizar", "TituloVisualizar"),
        ("Imprimir", "TituloImprimir"),
        ("Exportar", "TituloExportar"),
        ("Por Vendedor", "TituloVendedor"),
        ("Autorizar", "TituloAutorizar"),
        ("AutoOPSP", "TituloOPSP"),
        ("AutoOP", "TituloOP"),
        ("Opcion11", "TituloOpcion11"),
        ("Opcion12", "TituloOpcion12"),
        ("Opcion13", "TituloOpcion13"),
        ("Opcion14", "TituloOpcion14"),
        ("Opcion15", "TituloOpcion15"),
        ("Opcion16", "TituloOpcion16"),
        ("Opcion17", "TituloOpcion17"),
        ("Opcion18", "TituloOpcion18"),
        ("Opcion19", "TituloOpcion19"),
        ("Opcion20", "TituloOpcion20")
    ];

    private static readonly HashSet<string> WritableColumns = new(
        ActionColumns.Select(action => action.Column).Append("Estatus"),
        StringComparer.OrdinalIgnoreCase);

    private readonly SqlConnectionFactory _connectionFactory = new();

    public async Task<IReadOnlyList<AccessProfile>> GetProfilesAsync(
        CancellationToken cancellationToken = default)
    {
        const string query = """
            SELECT IdPerfil, Nombre
            FROM dbo.Perfiles
            ORDER BY IdPerfil;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var profiles = new List<AccessProfile>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = reader.GetInt32(0);
            var name = reader.IsDBNull(1) ? string.Empty : reader.GetString(1).Trim();
            profiles.Add(new AccessProfile(id, string.IsNullOrWhiteSpace(name) ? $"Perfil {id}" : name));
        }

        return profiles;
    }

    public async Task<int?> GetUserProfileIdAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        const string query = """
            SELECT TOP (1) IdPerfil
            FROM dbo.Perfiles
            WHERE IdUsuario = @UserId AND Estatus = 1
            ORDER BY IdPerfil;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection);
        command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull ? null : Convert.ToInt32(result);
    }

    public async Task<IReadOnlyList<AccessPermissionEntry>> GetUserPermissionsAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var profileId = await GetUserProfileIdAsync(userId, cancellationToken);
        return profileId is int id
            ? await GetPermissionEntriesForProfileAsync(id, cancellationToken)
            : Array.Empty<AccessPermissionEntry>();
    }

    public async Task<IReadOnlyList<AccessPermissionEntry>> GetPermissionEntriesAsync(
        int profileId,
        CancellationToken cancellationToken = default)
    {
        var entries = await GetPermissionEntriesForProfileAsync(profileId, cancellationToken);
        if (entries.Count > 0)
        {
            return entries;
        }

        const string templateProfileQuery = """
            SELECT TOP (1) [IdPerfil]
            FROM dbo.PerfilDetalle
            WHERE [IdPerfil] IS NOT NULL AND [IdPerfil] <> @ProfileId
            GROUP BY [IdPerfil]
            ORDER BY COUNT(*) DESC, [IdPerfil];
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(templateProfileQuery, connection);
        command.Parameters.Add("@ProfileId", SqlDbType.Int).Value = profileId;
        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is null or DBNull)
        {
            return entries;
        }

        var templateProfileId = Convert.ToInt32(result);
        var templateEntries = await GetPermissionEntriesForProfileAsync(templateProfileId, cancellationToken);
        return templateEntries
            .Select(entry => entry with
            {
                IsEnabled = false,
                Actions = entry.Actions
                    .Select(action => action with { IsEnabled = false })
                    .ToArray(),
                TemplateProfileId = templateProfileId
            })
            .ToArray();
    }

    private async Task<IReadOnlyList<AccessPermissionEntry>> GetPermissionEntriesForProfileAsync(
        int profileId,
        CancellationToken cancellationToken)
    {
        var actionSelect = string.Join(
            ", ",
            ActionColumns.SelectMany(action => new[] { $"[{action.Column}]", $"[{action.TitleColumn}]" }));
        var query = $"""
            SELECT [IdDetalle], [Opcion], [Titulo], [Padre], [Estatus], {actionSelect}
            FROM dbo.PerfilDetalle
            WHERE [IdPerfil] = @ProfileId
            ORDER BY [IdDetalle];
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection);
        command.Parameters.Add("@ProfileId", SqlDbType.Int).Value = profileId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var entries = new List<AccessPermissionEntry>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var actions = new List<AccessPermissionAction>();
            foreach (var (column, titleColumn) in ActionColumns)
            {
                var valueOrdinal = reader.GetOrdinal(column);
                var titleOrdinal = reader.GetOrdinal(titleColumn);
                if (reader.IsDBNull(valueOrdinal) || reader.IsDBNull(titleOrdinal))
                {
                    continue;
                }

                var title = reader.GetString(titleOrdinal).Trim();
                if (title.Length == 0)
                {
                    continue;
                }

                actions.Add(new AccessPermissionAction(
                    column,
                    title,
                    Convert.ToInt32(reader.GetValue(valueOrdinal)) != 0));
            }

            entries.Add(new AccessPermissionEntry(
                reader.GetInt32(reader.GetOrdinal("IdDetalle")),
                ReadNullableString(reader, "Opcion"),
                ReadNullableString(reader, "Titulo"),
                ReadNullableString(reader, "Padre"),
                !reader.IsDBNull(reader.GetOrdinal("Estatus"))
                    && Convert.ToInt32(reader.GetValue(reader.GetOrdinal("Estatus"))) != 0,
                actions));
        }

        return entries;
    }

    public async Task SavePermissionChangesAsync(
        int profileId,
        int? templateProfileId,
        IReadOnlyCollection<AccessPermissionChange> changes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(changes);
        if (changes.Count == 0)
        {
            return;
        }

        if (changes.Any(change => !WritableColumns.Contains(change.Column)))
        {
            throw new ArgumentException("La lista contiene una columna de permisos no válida.", nameof(changes));
        }

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        var identityInsertEnabled = false;
        try
        {
            if (templateProfileId is int sourceProfileId)
            {
                if (sourceProfileId == profileId)
                {
                    throw new InvalidOperationException("El perfil de origen no puede ser el perfil seleccionado.");
                }

                await using var countCommand = new SqlCommand(
                    "SELECT COUNT(*) FROM dbo.PerfilDetalle WITH (UPDLOCK, HOLDLOCK) WHERE [IdPerfil] = @ProfileId;",
                    connection,
                    transaction);
                countCommand.Parameters.Add("@ProfileId", SqlDbType.Int).Value = profileId;
                if (Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken)) != 0)
                {
                    throw new InvalidOperationException(
                        "El perfil ya tiene accesos. Vuelve a seleccionarlo para cargar sus valores actuales antes de guardar.");
                }

                await using (var identityInsertCommand = new SqlCommand(
                                 "SET IDENTITY_INSERT dbo.PerfilDetalle ON;",
                                 connection,
                                 transaction))
                {
                    await identityInsertCommand.ExecuteNonQueryAsync(cancellationToken);
                    identityInsertEnabled = true;
                }

                var changesByDetail = changes
                    .GroupBy(change => change.DetailId)
                    .ToDictionary(
                        group => group.Key,
                        group => group.ToDictionary(change => change.Column, StringComparer.OrdinalIgnoreCase));
                foreach (var (detailId, detailChanges) in changesByDetail)
                {
                    if (!detailChanges.TryGetValue("Estatus", out var statusChange))
                    {
                        throw new InvalidOperationException(
                            $"Falta el estado principal del acceso {detailId}.");
                    }

                    await InsertTemplatePermissionAsync(
                        connection,
                        transaction,
                        profileId,
                        sourceProfileId,
                        detailId,
                        detailChanges,
                        statusChange.IsEnabled,
                        cancellationToken);
                }

                await using var identityInsertOffCommand = new SqlCommand(
                    "SET IDENTITY_INSERT dbo.PerfilDetalle OFF;",
                    connection,
                    transaction);
                await identityInsertOffCommand.ExecuteNonQueryAsync(cancellationToken);
                identityInsertEnabled = false;
            }
            else
            {
                foreach (var change in changes)
                {
                    var query = $"""
                        UPDATE dbo.PerfilDetalle
                        SET [{change.Column}] = @Value
                        WHERE [IdDetalle] = @DetailId AND [IdPerfil] = @ProfileId;
                        """;
                    await using var command = new SqlCommand(query, connection, transaction);
                    command.Parameters.Add("@Value", SqlDbType.Int).Value = change.IsEnabled ? 1 : 0;
                    command.Parameters.Add("@DetailId", SqlDbType.Int).Value = change.DetailId;
                    command.Parameters.Add("@ProfileId", SqlDbType.Int).Value = profileId;
                    if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
                    {
                        throw new InvalidOperationException(
                            $"No se encontró el permiso {change.DetailId} para el perfil seleccionado.");
                    }
                }
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception saveException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            if (identityInsertEnabled)
            {
                try
                {
                    await using var identityInsertOffCommand = new SqlCommand(
                        "SET IDENTITY_INSERT dbo.PerfilDetalle OFF;",
                        connection);
                    await identityInsertOffCommand.ExecuteNonQueryAsync(CancellationToken.None);
                }
                catch (SqlException cleanupException)
                {
                    throw new AggregateException(
                        "No se pudo completar el guardado ni restablecer IDENTITY_INSERT.",
                        saveException,
                        cleanupException);
                }
            }

            throw;
        }
    }

    private static async Task InsertTemplatePermissionAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        int profileId,
        int templateProfileId,
        int templateDetailId,
        IReadOnlyDictionary<string, AccessPermissionChange> changes,
        bool isEnabled,
        CancellationToken cancellationToken)
    {
        var columns = new[]
        {
            "IdDetalle", "Opcion", "Titulo", "Alta", "TituloAlta", "Editar", "TituloEdicion", "Baja", "TituloBaja",
            "Visualizar", "TituloVisualizar", "Imprimir", "TituloImprimir", "Exportar", "TituloExportar",
            "Por Vendedor", "TituloVendedor", "Autorizar", "TituloAutorizar", "AutoOPSP", "TituloOPSP",
            "AutoOP", "TituloOP", "Opcion11", "TituloOpcion11", "Opcion12", "TituloOpcion12",
            "Opcion13", "TituloOpcion13", "Opcion14", "TituloOpcion14", "Opcion15", "TituloOpcion15",
            "Opcion16", "TituloOpcion16", "Opcion17", "TituloOpcion17", "Opcion18", "TituloOpcion18",
            "Opcion19", "TituloOpcion19", "Opcion20", "TituloOpcion20", "Estatus", "IdUsuario", "Padre",
            "Imagen", "ImagenInactiva", "Altaedicion", "Impresion", "Exportacion", "Visualizacion",
            "ClientesxVendedor", "IdEmpresa", "Autorizacion", "AutOPSP", "AutOP", "Op11", "Op12", "Op13",
            "Op14", "Op15", "Op16", "Op17", "Op18", "Op19", "Op20"
        };
        var insertColumns = string.Join(", ", new[] { "[IdPerfil]" }.Concat(columns.Select(column => $"[{column}]")));
        var selectValues = new List<string>
        {
            "@ProfileId",
            "ISNULL((SELECT MAX([IdDetalle]) FROM dbo.PerfilDetalle WITH (UPDLOCK, HOLDLOCK)), 0) + 1"
        };
        foreach (var column in columns)
        {
            if (column.Equals("IdDetalle", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (column.Equals("Estatus", StringComparison.OrdinalIgnoreCase))
            {
                selectValues.Add("@Estatus");
            }
            else if (ActionColumns.Any(action =>
                         action.Column.Equals(column, StringComparison.OrdinalIgnoreCase)))
            {
                if (changes.TryGetValue(column, out var change))
                {
                    selectValues.Add($"@{column.Replace(" ", string.Empty)}");
                }
                else
                {
                    selectValues.Add("NULL");
                }
            }
            else
            {
                selectValues.Add($"source.[{column}]");
            }
        }

        var query = $"""
            INSERT INTO dbo.PerfilDetalle ({insertColumns})
            SELECT {string.Join(", ", selectValues)}
            FROM dbo.PerfilDetalle AS source
            WHERE source.[IdDetalle] = @TemplateDetailId
              AND source.[IdPerfil] = @TemplateProfileId;
            """;
        await using var command = new SqlCommand(query, connection, transaction);
        command.Parameters.Add("@ProfileId", SqlDbType.Int).Value = profileId;
        command.Parameters.Add("@Estatus", SqlDbType.Int).Value = isEnabled ? 1 : 0;
        command.Parameters.Add("@TemplateDetailId", SqlDbType.Int).Value = templateDetailId;
        command.Parameters.Add("@TemplateProfileId", SqlDbType.Int).Value = templateProfileId;
        foreach (var (column, change) in changes)
        {
            if (!ActionColumns.Any(action => action.Column.Equals(column, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            command.Parameters.Add($"@{column.Replace(" ", string.Empty)}", SqlDbType.Int)
                .Value = change.IsEnabled ? 1 : 0;
        }

        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
        {
            throw new InvalidOperationException(
                $"No se encontró la definición de acceso {templateDetailId} en el perfil de referencia.");
        }
    }

    private static string? ReadNullableString(SqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal).Trim();
    }
}

public sealed record AccessProfile(int Id, string Name);

public sealed record AccessPermissionEntry(
    int DetailId,
    string? Option,
    string? Title,
    string? ParentOption,
    bool IsEnabled,
    IReadOnlyList<AccessPermissionAction> Actions,
    int? TemplateProfileId = null);

public sealed record AccessPermissionAction(string Column, string Title, bool IsEnabled);

public sealed record AccessPermissionChange(int DetailId, string Column, bool IsEnabled);

public static class AccessPermissionLookup
{
    public static bool IsModuleEnabled(
        IReadOnlyList<AccessPermissionEntry> entries,
        string module)
    {
        var entry = FindModule(entries, module);
        return entry is not null
            && IsEnabledThroughParents(entries, entry, new HashSet<int>());
    }

    public static bool IsActionEnabled(
        IReadOnlyList<AccessPermissionEntry> entries,
        string module,
        string action)
    {
        var entry = FindModule(entries, module);
        if (entry is null || !IsEnabledThroughParents(entries, entry, new HashSet<int>()))
        {
            return false;
        }

        var permission = entry.Actions.FirstOrDefault(candidate =>
            string.Equals(candidate.Column, action, StringComparison.OrdinalIgnoreCase));
        // Actions without a configured title have no separate profile control.
        return permission?.IsEnabled ?? true;
    }

    private static bool IsEnabledThroughParents(
        IReadOnlyList<AccessPermissionEntry> entries,
        AccessPermissionEntry entry,
        HashSet<int> visited)
    {
        if (!entry.IsEnabled || !visited.Add(entry.DetailId))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(entry.ParentOption))
        {
            return true;
        }

        var parent = FindModule(entries, entry.ParentOption);
        return parent is not null && IsEnabledThroughParents(entries, parent, visited);
    }

    private static AccessPermissionEntry? FindModule(
        IEnumerable<AccessPermissionEntry> entries,
        string module)
    {
        var candidates = entries as IReadOnlyList<AccessPermissionEntry> ?? entries.ToArray();
        return candidates.FirstOrDefault(entry => Matches(entry.Option, module))
            ?? candidates.FirstOrDefault(entry => Matches(entry.Title, module));
    }

    private static bool Matches(string? candidate, string requested)
    {
        if (candidate is null)
        {
            return false;
        }

        return string.Equals(Normalize(candidate), Normalize(requested), StringComparison.Ordinal);
    }

    private static string Normalize(string value)
    {
        var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        var normalized = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark
                && char.IsLetterOrDigit(character))
            {
                normalized.Append(char.ToUpperInvariant(character));
            }
        }

        return normalized.ToString();
    }
}

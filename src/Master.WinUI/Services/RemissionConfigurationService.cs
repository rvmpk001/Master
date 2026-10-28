using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;

namespace Master_WinUI.Services;

public sealed class RemissionConfigurationService
{
    private readonly SqlConnectionFactory _connectionFactory = new();

    public async Task<RemissionConfigurationSettings> GetSettingsAsync(
        CancellationToken cancellationToken = default)
    {
        const string query = """
            SELECT TOP (1)
                IdConfigRe, NomCia, ImpDesCa, ImpLineas, Numremision, NumAutomatico,
                NumSalida, NumSAutomatico, Subfijo, NumCopias, Leyendas, LoP,
                Opacidad, Tamaño, AlineacionV, AlineacionH, Rotacion
            FROM dbo.AjusteRemision
            ORDER BY IdConfigRe;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("dbo.AjusteRemision no contiene un registro de configuración.");
        }

        var options = new Dictionary<string, bool>(StringComparer.Ordinal)
        {
            ["NomCia"] = ReadBoolean(reader, 1, "NomCia"),
            ["ImpDesCa"] = ReadBoolean(reader, 2, "ImpDesCa"),
            ["ImpLineas"] = ReadBoolean(reader, 3, "ImpLineas"),
            ["NumAutomatico"] = ReadBoolean(reader, 5, "NumAutomatico"),
            ["NumSAutomatico"] = ReadBoolean(reader, 7, "NumSAutomatico"),
            ["LoP"] = ReadBoolean(reader, 11, "LoP")
        };
        var textValues = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Numremision"] = ReadInteger(reader, 4, "Numremision").ToString(CultureInfo.InvariantCulture),
            ["NumSalida"] = ReadInteger(reader, 6, "NumSalida").ToString(CultureInfo.InvariantCulture),
            ["Subfijo"] = ReadText(reader, 8),
            ["NumCopias"] = ReadInteger(reader, 9, "NumCopias").ToString(CultureInfo.InvariantCulture),
            ["Leyendas"] = ReadText(reader, 10),
            ["Opacidad"] = ReadInteger(reader, 12, "Opacidad").ToString(CultureInfo.InvariantCulture),
            ["Tamaño"] = ReadInteger(reader, 13, "Tamaño").ToString(CultureInfo.InvariantCulture),
            ["Rotacion"] = ReadText(reader, 16)
        };
        var alignmentValues = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["AlineacionV"] = ReadInteger(reader, 14, "AlineacionV"),
            ["AlineacionH"] = ReadInteger(reader, 15, "AlineacionH")
        };

        return new RemissionConfigurationSettings(
            reader.GetInt32(0),
            options,
            textValues,
            alignmentValues);
    }

    public async Task SaveSettingsAsync(
        RemissionConfigurationUpdate settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ValidateSettings(settings);

        const string query = """
            UPDATE dbo.AjusteRemision
            SET NomCia = @NomCia,
                ImpDesCa = @ImpDesCa,
                ImpLineas = @ImpLineas,
                Numremision = @Numremision,
                NumAutomatico = @NumAutomatico,
                NumSalida = @NumSalida,
                NumSAutomatico = @NumSAutomatico,
                Subfijo = @Subfijo,
                NumCopias = @NumCopias,
                Leyendas = @Leyendas,
                LoP = @LoP,
                Opacidad = @Opacidad,
                Tamaño = @Tamaño,
                AlineacionV = @AlineacionV,
                AlineacionH = @AlineacionH,
                Rotacion = @Rotacion
            WHERE IdConfigRe = @IdConfigRe;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection);
        command.Parameters.Add("@IdConfigRe", SqlDbType.Int).Value = settings.Id;
        command.Parameters.Add("@NomCia", SqlDbType.Int).Value = GetOption(settings, "NomCia");
        command.Parameters.Add("@ImpDesCa", SqlDbType.Int).Value = GetOption(settings, "ImpDesCa");
        command.Parameters.Add("@ImpLineas", SqlDbType.Int).Value = GetOption(settings, "ImpLineas");
        command.Parameters.Add("@Numremision", SqlDbType.Int).Value = ParseInteger(settings, "Numremision");
        command.Parameters.Add("@NumAutomatico", SqlDbType.Int).Value = GetOption(settings, "NumAutomatico");
        command.Parameters.Add("@NumSalida", SqlDbType.Int).Value = ParseInteger(settings, "NumSalida");
        command.Parameters.Add("@NumSAutomatico", SqlDbType.Int).Value = GetOption(settings, "NumSAutomatico");
        command.Parameters.Add("@Subfijo", SqlDbType.NVarChar, 50).Value = settings.TextValues["Subfijo"];
        command.Parameters.Add("@NumCopias", SqlDbType.Int).Value = ParseInteger(settings, "NumCopias");
        command.Parameters.Add("@Leyendas", SqlDbType.NVarChar, -1).Value = settings.TextValues["Leyendas"];
        command.Parameters.Add("@LoP", SqlDbType.Int).Value = GetOption(settings, "LoP");
        command.Parameters.Add("@Opacidad", SqlDbType.Int).Value = ParseInteger(settings, "Opacidad");
        command.Parameters.Add("@Tamaño", SqlDbType.Int).Value = ParseInteger(settings, "Tamaño");
        command.Parameters.Add("@AlineacionV", SqlDbType.Int).Value = GetAlignment(settings, "AlineacionV");
        command.Parameters.Add("@AlineacionH", SqlDbType.Int).Value = GetAlignment(settings, "AlineacionH");
        command.Parameters.Add("@Rotacion", SqlDbType.NChar, 10).Value = settings.TextValues["Rotacion"];

        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
        {
            throw new InvalidOperationException(
                $"No se encontró exactamente un registro con IdConfigRe={settings.Id} en dbo.AjusteRemision.");
        }
    }

    private static void ValidateSettings(RemissionConfigurationUpdate settings)
    {
        string[] requiredOptions = ["NomCia", "ImpDesCa", "ImpLineas", "NumAutomatico", "NumSAutomatico", "LoP"];
        string[] requiredText = ["Numremision", "NumSalida", "Subfijo", "NumCopias", "Leyendas", "Opacidad", "Tamaño", "Rotacion"];
        string[] requiredAlignments = ["AlineacionV", "AlineacionH"];

        if (requiredOptions.Any(key => !settings.Options.ContainsKey(key))
            || requiredText.Any(key => !settings.TextValues.ContainsKey(key))
            || requiredAlignments.Any(key => !settings.AlignmentValues.ContainsKey(key)))
        {
            throw new InvalidOperationException("Faltan campos de configuración de remisión para guardar.");
        }

        if (settings.TextValues["Subfijo"].Length > 50)
        {
            throw new InvalidOperationException("El sufijo de remisión no puede superar 50 caracteres.");
        }

        if (settings.TextValues["Rotacion"].Length > 10)
        {
            throw new InvalidOperationException("La rotación no puede superar 10 caracteres.");
        }

        foreach (var column in new[] { "Numremision", "NumSalida", "NumCopias", "Opacidad", "Tamaño" })
        {
            var value = ParseInteger(settings, column);
            if (value < 0)
            {
                throw new InvalidOperationException($"'{column}' debe ser un entero mayor o igual a 0.");
            }
        }

        var opacity = ParseInteger(settings, "Opacidad");
        if (opacity > 100)
        {
            throw new InvalidOperationException("La opacidad debe estar entre 0 y 100.");
        }
    }

    private static int ParseInteger(RemissionConfigurationUpdate settings, string column)
    {
        if (!int.TryParse(
                settings.TextValues[column],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var value))
        {
            throw new InvalidOperationException($"'{column}' debe ser un número entero.");
        }

        return value;
    }

    private static int GetOption(RemissionConfigurationUpdate settings, string column) =>
        settings.Options[column] ? 1 : 0;

    private static int GetAlignment(RemissionConfigurationUpdate settings, string column) =>
        settings.AlignmentValues[column];

    private static bool ReadBoolean(SqlDataReader reader, int ordinal, string column)
    {
        var value = ReadInteger(reader, ordinal, column);
        return value switch
        {
            0 => false,
            1 => true,
            _ => throw new InvalidOperationException(
                $"El valor de '{column}' en dbo.AjusteRemision debe ser 0 o 1.")
        };
    }

    private static int ReadInteger(SqlDataReader reader, int ordinal, string column)
    {
        if (reader.IsDBNull(ordinal))
        {
            throw new InvalidOperationException($"La columna '{column}' de dbo.AjusteRemision no tiene valor.");
        }

        return reader.GetInt32(ordinal);
    }

    private static string ReadText(SqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal).Trim();
}

public sealed record RemissionConfigurationSettings(
    int Id,
    IReadOnlyDictionary<string, bool> Options,
    IReadOnlyDictionary<string, string> TextValues,
    IReadOnlyDictionary<string, int> AlignmentValues);

public sealed record RemissionConfigurationUpdate(
    int Id,
    IReadOnlyDictionary<string, bool> Options,
    IReadOnlyDictionary<string, string> TextValues,
    IReadOnlyDictionary<string, int> AlignmentValues);

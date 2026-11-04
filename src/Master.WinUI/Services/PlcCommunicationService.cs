using System.Data;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
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
    private readonly SemaphoreSlim _plcOperationLock = new(1, 1);
    private readonly PlcTagService _plcTagService = new();

    public async Task<PlcCommunicationSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        const string query = """
            SELECT Param, Valor_Param
            FROM dbo.Comunicacion
            WHERE Param IN (
                N'DirPLC', N'Port', N'Velocity', N'DataBits', N'Parity', N'StopBits',
                N'TimeOut', N'Retry', N'Salidas', N'Tipo', N'Demo',
                N'AnalogInitial', N'AnalogEnd'
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
            values["Demo"],
            values.GetValueOrDefault("AnalogInitial", string.Empty),
            values.GetValueOrDefault("AnalogEnd", string.Empty));
    }

    public async Task<PlcConnectionStatus> CheckConnectionAsync(CancellationToken cancellationToken = default)
    {
        return await CheckConnectionAsync(
            Array.Empty<string>(),
            Array.Empty<string>(),
            cancellationToken);
    }

    public async Task<PlcConnectionStatus> CheckConnectionAsync(
        IReadOnlyCollection<string> additionalImageControls,
        CancellationToken cancellationToken = default)
    {
        return await CheckConnectionAsync(
            additionalImageControls,
            Array.Empty<string>(),
            cancellationToken);
    }

    public async Task<PlcConnectionStatus> CheckConnectionAsync(
        IReadOnlyCollection<string> additionalImageControls,
        IReadOnlyCollection<string> additionalValueControls,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(additionalImageControls);
        ArgumentNullException.ThrowIfNull(additionalValueControls);
        PlcCommunicationSettings settings;
        try
        {
            settings = await GetSettingsAsync(cancellationToken);
        }
        catch (SqlException exception)
        {
            return PlcConnectionStatus.Unavailable(
                $"No se pudo leer la configuración PLC desde dbo.Comunicacion (SQL {exception.Number}): {exception.Message}");
        }
        catch (InvalidOperationException exception)
        {
            return PlcConnectionStatus.Unavailable($"La configuración PLC no es válida: {exception.Message}");
        }

        if (!TryCreateProbeOptions(settings, out var options, out var validationError))
        {
            return PlcConnectionStatus.Unavailable(validationError);
        }

        IReadOnlyDictionary<string, ushort> additionalTagAddresses =
            new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase);
        IReadOnlyDictionary<string, AdditionalValueTagAddress> additionalValueTagAddresses =
            new Dictionary<string, AdditionalValueTagAddress>(StringComparer.OrdinalIgnoreCase);
        if (additionalImageControls.Count > 0 || additionalValueControls.Count > 0)
        {
            try
            {
                var tags = await _plcTagService.GetTagsAsync(cancellationToken);
                var mappedTags = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase);
                foreach (var control in additionalImageControls.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    if (!TryGetImageTagName(control, out var tagName))
                    {
                        return PlcConnectionStatus.Unavailable(
                            $"El control adicional '{control}' no tiene un mapeo PLC válido.");
                    }

                    var tag = tags.SingleOrDefault(candidate =>
                        candidate.Name.Equals(tagName, StringComparison.OrdinalIgnoreCase));
                    if (tag is null || !tag.IsEnabled || tag.AddressType != 2 || tag.PlcAddress is not >= 0)
                    {
                        return PlcConnectionStatus.Unavailable(
                            $"No hay una dirección MI habilitada y válida para el tag '{tagName}' de {control}.");
                    }

                    mappedTags.Add(control, (ushort)tag.PlcAddress.Value);
                }

                var mappedValueTags =
                    new Dictionary<string, AdditionalValueTagAddress>(StringComparer.OrdinalIgnoreCase);
                foreach (var control in additionalValueControls.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    if (!TryGetValueTagName(control, out var tagName))
                    {
                        return PlcConnectionStatus.Unavailable(
                            $"El control de lectura '{control}' no tiene un mapeo PLC válido.");
                    }

                    var tag = tags.SingleOrDefault(candidate =>
                        candidate.Name.Equals(tagName, StringComparison.OrdinalIgnoreCase));
                    if (!TryGetExpectedValueTagAddressType(tagName, out var expectedAddressType))
                    {
                        return PlcConnectionStatus.Unavailable(
                            $"El tag '{tagName}' usa un tipo de dirección que el lector actual de operandos MI no soporta.");
                    }

                    if (tag is null
                        || !tag.IsEnabled
                        || tag.AddressType != expectedAddressType
                        || tag.PlcAddress is not >= 0)
                    {
                        return PlcConnectionStatus.Unavailable(
                            $"No hay una dirección MI habilitada y válida (tipo {expectedAddressType:0}) para la lectura '{tagName}'.");
                    }

                    mappedValueTags.Add(
                        control,
                        new AdditionalValueTagAddress(
                            (ushort)tag.PlcAddress.Value,
                            tag.Multiplier ?? 1));
                }

                additionalTagAddresses = mappedTags;
                additionalValueTagAddresses = mappedValueTags;
            }
            catch (SqlException exception)
            {
                return PlcConnectionStatus.Unavailable(
                    $"No se pudieron leer los tags PLC desde dbo.Tags (SQL {exception.Number}): {exception.Message}");
            }
            catch (InvalidOperationException exception)
            {
                return PlcConnectionStatus.Unavailable($"La configuración de tags PLC no es válida: {exception.Message}");
            }
        }

        await _plcOperationLock.WaitAsync(cancellationToken);
        try
        {
            return await CheckConnectionAsync(
                options,
                cancellationToken,
                additionalTagAddresses,
                additionalValueTagAddresses);
        }
        finally
        {
            _plcOperationLock.Release();
        }
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

    private static async Task<PlcConnectionStatus> CheckConnectionAsync(
        PlcProbeOptions options,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, ushort>? additionalTagAddresses = null,
        IReadOnlyDictionary<string, AdditionalValueTagAddress>? additionalValueTagAddresses = null)
    {
        var executablePath = Path.Combine(
            AppContext.BaseDirectory,
            "PlcProbe",
            "Master.PlcProbe.exe");
        if (!File.Exists(executablePath))
        {
            return PlcConnectionStatus.Unavailable(
                $"No se encontró el auxiliar de comunicación PLC: {executablePath}");
        }

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(executablePath)!
            }
        };
        process.StartInfo.ArgumentList.Add(options.PlcAddress.ToString(CultureInfo.InvariantCulture));
        process.StartInfo.ArgumentList.Add(options.Port);
        process.StartInfo.ArgumentList.Add(options.Velocity);
        process.StartInfo.ArgumentList.Add(options.DataBits);
        process.StartInfo.ArgumentList.Add(options.Parity);
        process.StartInfo.ArgumentList.Add(options.StopBits);
        process.StartInfo.ArgumentList.Add(options.Timeout.ToString(CultureInfo.InvariantCulture));
        process.StartInfo.ArgumentList.Add(options.Retry.ToString(CultureInfo.InvariantCulture));
        process.StartInfo.ArgumentList.Add(options.AnalogInitial.ToString(CultureInfo.InvariantCulture));
        var addressesToRead = (additionalTagAddresses is null
                ? Enumerable.Empty<ushort>()
                : additionalTagAddresses.Values)
            .Concat(additionalValueTagAddresses is null
                ? Enumerable.Empty<ushort>()
                : additionalValueTagAddresses.Values.Select(value => value.Address))
            .Distinct()
            .Order()
            .ToArray();
        if (addressesToRead.Length > 0)
        {
            process.StartInfo.ArgumentList.Add(string.Join(",", addressesToRead));
        }

        try
        {
            if (!process.Start())
            {
                return PlcConnectionStatus.Unavailable(
                    "Windows no pudo iniciar el auxiliar de comunicación PLC.");
            }

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            var timeoutMilliseconds = Math.Min(
                30_000L,
                Math.Max(5_000L, (long)options.Timeout * (options.Retry + 1L) + 3_000L));
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(TimeSpan.FromMilliseconds(timeoutMilliseconds));

            try
            {
                await process.WaitForExitAsync(timeoutSource.Token);
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }

                await process.WaitForExitAsync(CancellationToken.None);
                if (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }

                return PlcConnectionStatus.Create(
                    PlcConnectionState.Disconnected,
                    options.Port,
                    options.AnalogInitial,
                    $"El auxiliar PLC excedió el tiempo de respuesta ({timeoutMilliseconds} ms) en {options.Port}.");
            }

            var standardOutput = await outputTask;
            var standardError = await errorTask;
            var outputParts = standardOutput.Trim().Split('|');
            if (process.ExitCode == 0 && outputParts[0].Equals("CONNECTED", StringComparison.Ordinal))
            {
                var valuesByAddress = new Dictionary<ushort, double>();
                foreach (var valuePart in outputParts.Skip(1))
                {
                    var separator = valuePart.IndexOf('=');
                    if (separator <= 2
                        || !valuePart.StartsWith("MI", StringComparison.Ordinal)
                        || !ushort.TryParse(
                            valuePart.AsSpan(2, separator - 2),
                            NumberStyles.None,
                            CultureInfo.InvariantCulture,
                            out var address)
                        || !double.TryParse(
                            valuePart.AsSpan(separator + 1),
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out var value))
                    {
                        return PlcConnectionStatus.Unavailable(
                            $"El auxiliar PLC devolvió un valor de imagen adicional no válido: '{valuePart}'.");
                    }

                    valuesByAddress.Add(address, value);
                }

                var additionalImageValues = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                if (additionalTagAddresses is not null)
                {
                    foreach (var (control, address) in additionalTagAddresses)
                    {
                        if (!valuesByAddress.TryGetValue(address, out var value))
                        {
                            return PlcConnectionStatus.Unavailable(
                                $"El auxiliar PLC no devolvió lectura para MI{address}, requerido por {control}.");
                        }

                        additionalImageValues.Add(control, value);
                    }
                }

                var additionalTextValues = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                if (additionalValueTagAddresses is not null)
                {
                    foreach (var (control, tagAddress) in additionalValueTagAddresses)
                    {
                        if (!valuesByAddress.TryGetValue(tagAddress.Address, out var value))
                        {
                            return PlcConnectionStatus.Unavailable(
                                $"El auxiliar PLC no devolvió lectura para MI{tagAddress.Address}, requerido por {control}.");
                        }

                        additionalTextValues.Add(control, value * tagAddress.Multiplier);
                    }
                }

                return PlcConnectionStatus.Connected(
                    options.Port,
                    options.AnalogInitial,
                    additionalImageValues,
                    additionalTextValues);
            }

            var detail = standardError.Trim();
            if (string.IsNullOrWhiteSpace(detail))
            {
                detail = $"El auxiliar PLC terminó con código {process.ExitCode} sin confirmar una lectura válida.";
            }

            var state = detail.Contains("|PortInUse|", StringComparison.Ordinal)
                ? PlcConnectionState.PortBusy
                : detail.Contains("|InvalidSettings|", StringComparison.Ordinal)
                    || detail.Contains("|InvalidArguments|", StringComparison.Ordinal)
                    || detail.Contains("|DllNotFoundException|", StringComparison.Ordinal)
                    || detail.Contains("|BadImageFormatException|", StringComparison.Ordinal)
                    ? PlcConnectionState.Unavailable
                    : PlcConnectionState.Disconnected;

            return PlcConnectionStatus.Create(
                state,
                options.Port,
                options.AnalogInitial,
                $"{detail} PLC {options.PlcAddress}, lectura MI{options.AnalogInitial}, " +
                $"parámetros seriales: {options.Velocity}, {options.DataBits}, {options.Parity}, {options.StopBits}.");
        }
        catch (Win32Exception exception)
        {
            return PlcConnectionStatus.Unavailable(
                $"No se pudo iniciar el auxiliar de comunicación PLC: {exception.Message}");
        }
        catch (IOException exception)
        {
            return PlcConnectionStatus.Unavailable(
                $"Error al ejecutar el auxiliar de comunicación PLC: {exception.Message}");
        }
        catch (InvalidOperationException exception)
        {
            return PlcConnectionStatus.Unavailable(
                $"No se pudo iniciar el proceso auxiliar PLC: {exception.Message}");
        }
    }

    private static bool TryCreateProbeOptions(
        PlcCommunicationSettings settings,
        out PlcProbeOptions options,
        out string error)
    {
        options = default;

        if (!settings.ConnectionType.Equals("COM", StringComparison.OrdinalIgnoreCase))
        {
            error = $"El tipo de conexión '{settings.ConnectionType}' no está implementado para la verificación serial.";
            return false;
        }

        if (!int.TryParse(settings.PlcAddress, NumberStyles.Integer, CultureInfo.InvariantCulture, out var plcAddress)
            || plcAddress is < 0 or > 127)
        {
            error = "Dirección PLC debe ser un número entre 0 y 127.";
            return false;
        }

        if (!settings.Port.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
            || !int.TryParse(settings.Port.AsSpan(3), NumberStyles.None, CultureInfo.InvariantCulture, out var portNumber)
            || portNumber is < 1 or > 256)
        {
            error = $"El puerto '{settings.Port}' no es un puerto COM compatible con el driver.";
            return false;
        }

        if (!int.TryParse(settings.Velocity, NumberStyles.Integer, CultureInfo.InvariantCulture, out var baudRate)
            || baudRate <= 0)
        {
            error = $"La velocidad '{settings.Velocity}' no está soportada por el driver Unitronics.";
            return false;
        }

        if (!int.TryParse(settings.DataBits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var dataBits)
            || dataBits <= 0)
        {
            error = $"DataBits '{settings.DataBits}' no es compatible con el driver.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(settings.Parity))
        {
            error = $"La paridad '{settings.Parity}' no es válida.";
            return false;
        }

        if (settings.StopBits is not ("1" or "2" or "One" or "Two"))
        {
            error = $"StopBits '{settings.StopBits}' no es compatible con el driver.";
            return false;
        }

        if (!int.TryParse(settings.Timeout, NumberStyles.Integer, CultureInfo.InvariantCulture, out var timeout)
            || timeout <= 0)
        {
            error = "TimeOut debe ser un número mayor que cero.";
            return false;
        }

        if (!int.TryParse(settings.Retry, NumberStyles.Integer, CultureInfo.InvariantCulture, out var retry)
            || retry < 0)
        {
            error = "Retry debe ser un número igual o mayor que cero.";
            return false;
        }

        if (!ushort.TryParse(settings.AnalogInitial, NumberStyles.Integer, CultureInfo.InvariantCulture, out var analogInitial)
            || !ushort.TryParse(settings.AnalogEnd, NumberStyles.Integer, CultureInfo.InvariantCulture, out var analogEnd)
            || analogInitial > analogEnd)
        {
            error = "AnalogInitial y AnalogEnd deben existir, ser direcciones válidas y estar en orden.";
            return false;
        }

        options = new PlcProbeOptions(
            plcAddress,
            settings.Port,
            settings.Velocity,
            settings.DataBits,
            settings.Parity,
            settings.StopBits is "One" ? "1" : settings.StopBits is "Two" ? "2" : settings.StopBits,
            timeout,
            retry,
            analogInitial);
        error = string.Empty;
        return true;
    }

    private static bool TryGetImageTagName(string control, out string tagName)
    {
        tagName = string.Empty;
        if (ImageConfigurationService.PumpControls.Contains(control, StringComparer.OrdinalIgnoreCase))
        {
            tagName = control;
            return true;
        }

        if (!control.StartsWith("AdI", StringComparison.OrdinalIgnoreCase)
            || !int.TryParse(control.AsSpan(3), NumberStyles.None, CultureInfo.InvariantCulture, out var index)
            || index is < 1 or > 25)
        {
            return false;
        }

        tagName = $"Ad{index}";
        return true;
    }

    private static bool TryGetValueTagName(string control, out string tagName)
    {
        tagName = string.Empty;
        const string waterControlPrefix = "ctlAgua";
        if (control.StartsWith(waterControlPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var waterSuffix = control.AsSpan(waterControlPrefix.Length);
            if (waterSuffix.IsEmpty)
            {
                tagName = "Agua";
                return true;
            }

            if (int.TryParse(waterSuffix, NumberStyles.None, CultureInfo.InvariantCulture, out var waterIndex)
                && waterIndex is 2 or 3)
            {
                tagName = $"Agua{waterIndex}";
                return true;
            }

            return false;
        }

        const string fiberControlPrefix = "ctlFibra";
        if (control.StartsWith(fiberControlPrefix, StringComparison.OrdinalIgnoreCase)
            && int.TryParse(
                control.AsSpan(fiberControlPrefix.Length),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var fiberIndex)
            && fiberIndex is >= 1 and <= 3)
        {
            tagName = $"Fibra{fiberIndex}";
            return true;
        }

        const string additivePrefix = "ctlAditivo";
        if (control.StartsWith(additivePrefix, StringComparison.OrdinalIgnoreCase)
            && int.TryParse(
                control.AsSpan(additivePrefix.Length),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var additiveIndex)
            && additiveIndex is >= 1 and <= 6)
        {
            tagName = $"Aditivo{additiveIndex}";
            return true;
        }

        const string cementPrefix = "ctlCemento";
        if (control.StartsWith(cementPrefix, StringComparison.OrdinalIgnoreCase)
            && int.TryParse(
                control.AsSpan(cementPrefix.Length),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var cementIndex)
            && cementIndex is >= 1 and <= 5)
        {
            tagName = $"Cemento{cementIndex}";
            return true;
        }

        const string aggregatePrefix = "ctlAgregado";
        if (control.StartsWith(aggregatePrefix, StringComparison.OrdinalIgnoreCase)
            && int.TryParse(
                control.AsSpan(aggregatePrefix.Length),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var aggregateIndex)
            && aggregateIndex is >= 1 and <= 8)
        {
            tagName = $"Agregado{aggregateIndex}";
            return true;
        }

        const string prefix = "ctlAdicionale";
        if (!control.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            || !int.TryParse(
                control.AsSpan(prefix.Length),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var index)
            || index is < 1 or > 16)
        {
            return false;
        }

        tagName = $"Adicionale{index}";
        return true;
    }

    private static bool TryGetExpectedValueTagAddressType(string tagName, out float expectedAddressType)
    {
        if (tagName.StartsWith("Aditivo", StringComparison.OrdinalIgnoreCase)
            || tagName.StartsWith("Fibra", StringComparison.OrdinalIgnoreCase))
        {
            expectedAddressType = 3;
            return true;
        }

        if (tagName.Equals("Agua", StringComparison.OrdinalIgnoreCase)
            || tagName.Equals("Agua2", StringComparison.OrdinalIgnoreCase))
        {
            expectedAddressType = 0;
            return false;
        }

        expectedAddressType = 4;
        return true;
    }

    private readonly record struct AdditionalValueTagAddress(ushort Address, double Multiplier);

    private readonly record struct PlcProbeOptions(
        int PlcAddress,
        string Port,
        string Velocity,
        string DataBits,
        string Parity,
        string StopBits,
        int Timeout,
        int Retry,
        ushort AnalogInitial);
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
    string ConfigurationMode,
    string AnalogInitial = "",
    string AnalogEnd = "")
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

public enum PlcConnectionState
{
        Connected,
        Disconnected,
        PortBusy,
        Unavailable
}

public sealed record PlcConnectionStatus(
        PlcConnectionState State,
        string Port,
        ushort? RegisterAddress,
        string Detail,
        IReadOnlyDictionary<string, double>? AdditionalImageValues = null,
        IReadOnlyDictionary<string, double>? AdditionalTextValues = null)
{
        public static PlcConnectionStatus Connected(
            string port,
            ushort address,
            IReadOnlyDictionary<string, double>? additionalImageValues = null,
            IReadOnlyDictionary<string, double>? additionalTextValues = null) =>
            new(
                PlcConnectionState.Connected,
                port,
                address,
                $"Lectura confirmada de MI{address}.",
                additionalImageValues,
                additionalTextValues);

        public static PlcConnectionStatus Unavailable(string detail) =>
            new(PlcConnectionState.Unavailable, string.Empty, null, detail);

        public static PlcConnectionStatus Create(
            PlcConnectionState state,
            string port,
            ushort? registerAddress,
            string detail) =>
            new(state, port, registerAddress, detail);
}

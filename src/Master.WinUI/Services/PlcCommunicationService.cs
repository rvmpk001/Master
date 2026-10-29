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

        await _plcOperationLock.WaitAsync(cancellationToken);
        try
        {
            return await CheckConnectionAsync(options, cancellationToken);
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
        CancellationToken cancellationToken)
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
            if (process.ExitCode == 0 && standardOutput.Trim().Equals("CONNECTED", StringComparison.Ordinal))
            {
                return PlcConnectionStatus.Connected(options.Port, options.AnalogInitial);
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
        string Detail)
{
        public static PlcConnectionStatus Connected(string port, ushort address) =>
            new(PlcConnectionState.Connected, port, address, $"Lectura confirmada de MI{address}.");

        public static PlcConnectionStatus Unavailable(string detail) =>
            new(PlcConnectionState.Unavailable, string.Empty, null, detail);

        public static PlcConnectionStatus Create(
            PlcConnectionState state,
            string port,
            ushort? registerAddress,
            string detail) =>
            new(state, port, registerAddress, detail);
}

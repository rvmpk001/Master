using System.Globalization;

namespace Master_WinUI.Services;

public enum PlcProcessCommand
{
    Load,
    Discharge,
    Cancel
}

/// <summary>Un material de la receta: campo de fórmula (A1, C1, D1, Agua...), nombre y cantidad.</summary>
public sealed record PlcRecipeLine(string FormulaField, string MaterialName, decimal Quantity);

public sealed record PlcProcessResult(bool Success, string Message, bool OutcomeUncertain = false)
{
    public static PlcProcessResult Ok(string message) => new(true, message);

    public static PlcProcessResult Rejected(string message) => new(false, message);

    public static PlcProcessResult Uncertain(string message) => new(false, message, true);
}

/// <summary>
/// Comandos reales de proceso hacia el PLC. Es la ÚNICA clase que llama a
/// <see cref="PlcCommunicationService.WriteAsync"/>; la interfaz solo llama a estos métodos.
///
/// Protocolo (según el ladder y dbo.Tags):
///   Cargar    : escribe la receta (dr*) -> pulso en bitInicioCarga (MI1).
///   Descargar : pulso en bitInicioDescarga (MI2).
///   Cancelar  : pulso en bitCancelar (MI0).
/// Los bits de comando se comparan por NIVEL en el PLC (MI = 1), por lo que SIEMPRE deben
/// volver a 0 después del pulso; de lo contrario el PLC reinicia el ciclo.
/// </summary>
public sealed class PlcProcessService
{
    /// <summary>Variable de entorno que habilita la escritura real. Sin ella, nada se escribe.</summary>
    public const string WritesEnvironmentVariable = "BLAPACI_PLC_WRITES";

    // Tags de comando (dbo.Tags).
    private const string LoadBitTag = "bitInicioCarga";
    private const string DischargeBitTag = "bitInicioDescarga";
    private const string CancelBitTag = "bitCancelar";

    // Señales de lectura (control "tag:<nombre>"). Las dos últimas son opcionales: ver add_tags.sql.
    private const string BatchInProgressControl = "tag:bitBachaEnProceso";
    private const string DischargeStartedControl = "tag:bitDescargaIniciada";

    private static readonly string[] CommandBitControls =
    {
        "tag:" + LoadBitTag,
        "tag:" + DischargeBitTag,
        "tag:" + CancelBitTag
    };

    private static readonly TimeSpan CommandHoldTime = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan ConfirmTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ConfirmPollInterval = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Registros de receta escribibles. Solo los que tienen una dirección real en dbo.Tags
    /// (los demás dr* apuntan a direcciones de relleno como ML99 o MF4 y NO se escriben).
    /// Max: límite superior de seguridad en la unidad de la receta (kg); ajústelo a la capacidad real.
    /// </summary>
    private sealed record RecipeSlot(string FormulaField, string TagName, bool IsInteger, double Max);

    private static readonly RecipeSlot[] RecipeSlots =
    {
        new("C1", "drCemento1", true, 32767),   // MI201
        new("A1", "drAgregado1", true, 32767),  // MI202
        new("A2", "drAgregado2", true, 32767),  // MI203
        new("Agua", "drAgua", true, 32767),     // MI204
        new("D1", "drAditivo1", false, 9999),   // MF5
        new("D2", "drAditivo2", false, 9999)    // MF12
    };

    private readonly PlcCommunicationService _plc;
    private readonly SemaphoreSlim _commandLock = new(1, 1);
    private volatile bool _commandInProgress;

    public PlcProcessService(PlcCommunicationService plc)
    {
        _plc = plc ?? throw new ArgumentNullException(nameof(plc));

        foreach (var slot in RecipeSlots)
        {
            _plc.AllowWrite(new PlcWritableTag(slot.TagName, 0, slot.Max));
        }

        foreach (var tag in new[] { LoadBitTag, DischargeBitTag, CancelBitTag })
        {
            _plc.AllowWrite(new PlcWritableTag(tag, 0, 1));
        }

        // Ninguna escritura es válida fuera de un comando de proceso autorizado.
        _plc.WriteGuard = _ => _commandInProgress
            ? null
            : "Escritura fuera de un comando de proceso autorizado.";
        _plc.WritesEnabled = string.Equals(
            Environment.GetEnvironmentVariable(WritesEnvironmentVariable),
            "1",
            StringComparison.Ordinal);
    }

    public bool WritesEnabled => _plc.WritesEnabled;

    /// <summary>
    /// Autorización y condiciones de la aplicación, evaluadas antes de cada comando.
    /// Devuelve null si puede continuar, o el motivo del rechazo.
    /// </summary>
    public Func<PlcProcessCommand, string?>? Preflight { get; set; }

    public Task<PlcProcessResult> StartLoadAsync(
        IReadOnlyList<PlcRecipeLine> recipe,
        string user,
        string remission,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        return RunCommandAsync(PlcProcessCommand.Load, async () =>
        {
            var plan = new List<(RecipeSlot Slot, double Value)>();
            var byField = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in recipe)
            {
                var slot = RecipeSlots.FirstOrDefault(candidate =>
                    candidate.FormulaField.Equals(line.FormulaField, StringComparison.OrdinalIgnoreCase));
                if (slot is null)
                {
                    return PlcProcessResult.Rejected(
                        $"El material '{line.MaterialName}' ({line.FormulaField}) no tiene una dirección PLC " +
                        "habilitada para escritura. No se envió nada al PLC.");
                }

                var value = (double)line.Quantity;
                if (slot.IsInteger)
                {
                    value = Math.Round(value, MidpointRounding.AwayFromZero);
                }

                if (value < 0 || value > slot.Max)
                {
                    return PlcProcessResult.Rejected(
                        $"La cantidad de {line.MaterialName} ({value.ToString(CultureInfo.CurrentCulture)}) " +
                        $"excede el máximo permitido ({slot.Max}). No se envió nada al PLC.");
                }

                byField[slot.FormulaField] = value;
            }

            foreach (var slot in RecipeSlots)
            {
                plan.Add((slot, byField.GetValueOrDefault(slot.FormulaField, 0)));
            }

            var notClear = await VerifyCommandBitsClearAsync(cancellationToken);
            if (notClear is not null)
            {
                return PlcProcessResult.Rejected(notClear);
            }

            // 1) Receta. Si algo falla aquí no se envía el comando de inicio.
            foreach (var (slot, value) in plan)
            {
                var written = await _plc.WriteAsync(
                    new PlcWriteRequest(slot.TagName, value, UserOrDefault(user), $"Receta remisión {remission}"),
                    cancellationToken);
                if (!written.Success)
                {
                    return PlcProcessResult.Rejected(
                        $"No se pudo escribir {slot.TagName}: {written.Detail} No se inició la carga.");
                }
            }

            // 2) Comando de inicio (pulso) y 3) confirmación del PLC.
            var pulse = await PulseAsync(LoadBitTag, user, $"Inicio de carga remisión {remission}");
            if (!pulse.Success)
            {
                return pulse;
            }

            return await ConfirmAsync(
                BatchInProgressControl,
                expectOn: true,
                "inicio de la carga",
                cancellationToken);
        });
    }

    public Task<PlcProcessResult> StartDischargeAsync(
        string user,
        string remission,
        CancellationToken cancellationToken = default)
    {
        return RunCommandAsync(PlcProcessCommand.Discharge, async () =>
        {
            var notClear = await VerifyCommandBitsClearAsync(cancellationToken);
            if (notClear is not null)
            {
                return PlcProcessResult.Rejected(notClear);
            }

            var pulse = await PulseAsync(DischargeBitTag, user, $"Inicio de descarga remisión {remission}");
            if (!pulse.Success)
            {
                return pulse;
            }

            return await ConfirmAsync(
                DischargeStartedControl,
                expectOn: true,
                "inicio de la descarga",
                cancellationToken);
        });
    }

    public Task<PlcProcessResult> CancelAsync(
        string user,
        string remission,
        CancellationToken cancellationToken = default)
    {
        return RunCommandAsync(PlcProcessCommand.Cancel, async () =>
        {
            // Cancelar no exige bits en 0: es una acción de seguridad y debe poder enviarse siempre.
            var pulse = await PulseAsync(CancelBitTag, user, $"Cancelación remisión {remission}");
            if (!pulse.Success)
            {
                return pulse;
            }

            return await ConfirmAsync(
                BatchInProgressControl,
                expectOn: false,
                "la cancelación",
                cancellationToken);
        });
    }

    private async Task<PlcProcessResult> RunCommandAsync(
        PlcProcessCommand command,
        Func<Task<PlcProcessResult>> body)
    {
        if (!_plc.WritesEnabled)
        {
            return PlcProcessResult.Rejected(
                $"La escritura al PLC está deshabilitada. Defina la variable de entorno {WritesEnvironmentVariable}=1 " +
                "para habilitarla (solo en un equipo autorizado).");
        }

        var rejection = Preflight?.Invoke(command);
        if (rejection is not null)
        {
            return PlcProcessResult.Rejected(rejection);
        }

        if (!await _commandLock.WaitAsync(0))
        {
            return PlcProcessResult.Rejected("Ya hay otro comando PLC en curso.");
        }

        _commandInProgress = true;
        try
        {
            return await body();
        }
        catch (OperationCanceledException)
        {
            return PlcProcessResult.Uncertain(
                "La operación se canceló en la aplicación; el comando pudo haberse enviado. Verifique el estado del PLC.");
        }
        finally
        {
            _commandInProgress = false;
            _commandLock.Release();
        }
    }

    /// <summary>
    /// Escribe 1, espera un instante y escribe 0. El 0 se reintenta y NO se puede cancelar:
    /// un comando que se queda en 1 reinicia el ciclo en el PLC.
    /// </summary>
    private async Task<PlcProcessResult> PulseAsync(string tagName, string user, string reason)
    {
        var set = await _plc.WriteAsync(
            new PlcWriteRequest(tagName, 1, UserOrDefault(user), reason),
            CancellationToken.None);
        if (!set.Success && !set.OutcomeUncertain)
        {
            return PlcProcessResult.Rejected($"No se pudo enviar el comando {tagName}: {set.Detail}");
        }

        await Task.Delay(CommandHoldTime, CancellationToken.None);

        PlcWriteResult? clear = null;
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            clear = await _plc.WriteAsync(
                new PlcWriteRequest(tagName, 0, UserOrDefault(user), $"Liberar {tagName}"),
                CancellationToken.None);
            if (clear.Success)
            {
                break;
            }

            await Task.Delay(500, CancellationToken.None);
        }

        if (clear is null || !clear.Success)
        {
            return PlcProcessResult.Uncertain(
                $"¡ATENCIÓN! El comando {tagName} pudo quedar en 1 en el PLC y no se pudo liberar " +
                $"({clear?.Detail}). Póngalo en 0 manualmente antes de continuar.");
        }

        if (!set.Success)
        {
            return PlcProcessResult.Uncertain(
                $"El comando {tagName} no pudo confirmarse ({set.Detail}). El bit ya se liberó; verifique el estado del PLC.");
        }

        return PlcProcessResult.Ok($"Comando {tagName} enviado y liberado.");
    }

    /// <summary>Lee en vivo los bits de comando; si alguno está en 1 hay un comando anterior sin liberar.</summary>
    private async Task<string?> VerifyCommandBitsClearAsync(CancellationToken cancellationToken)
    {
        var status = await _plc.CheckConnectionAsync(
            Array.Empty<string>(),
            CommandBitControls,
            cancellationToken);
        if (status.State != PlcConnectionState.Connected || status.AdditionalTextValues is null)
        {
            return $"No se pudo leer el estado actual de los bits de comando del PLC: {status.Detail}";
        }

        foreach (var control in CommandBitControls)
        {
            if (!status.AdditionalTextValues.TryGetValue(control, out var value))
            {
                return $"No se pudo leer {control} del PLC.";
            }

            if (value != 0)
            {
                return $"El bit {control["tag:".Length..]} está en {value.ToString(CultureInfo.InvariantCulture)} " +
                       "en el PLC (comando anterior sin liberar). Libérelo antes de continuar.";
            }
        }

        return null;
    }

    /// <summary>
    /// Espera a que el PLC confirme (señal opcional de dbo.Tags). Si la señal no está configurada
    /// devuelve éxito con una nota, porque el comando sí fue escrito y verificado.
    /// </summary>
    private async Task<PlcProcessResult> ConfirmAsync(
        string control,
        bool expectOn,
        string label,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + ConfirmTimeout;
        while (true)
        {
            var status = await _plc.CheckConnectionAsync(
                Array.Empty<string>(),
                new[] { control },
                cancellationToken);
            if (status.State == PlcConnectionState.Connected)
            {
                if (status.AdditionalTextValues is null
                    || !status.AdditionalTextValues.TryGetValue(control, out var value))
                {
                    return PlcProcessResult.Ok(
                        $"Comando enviado. Sin confirmación del PLC para {label}: falta el tag " +
                        $"'{control["tag:".Length..]}' en dbo.Tags (ver add_tags.sql).");
                }

                if ((value != 0) == expectOn)
                {
                    return PlcProcessResult.Ok($"El PLC confirmó {label}.");
                }
            }

            if (DateTime.UtcNow >= deadline)
            {
                return PlcProcessResult.Uncertain(
                    $"El comando se envió, pero el PLC no confirmó {label} en " +
                    $"{ConfirmTimeout.TotalSeconds:0} s. Verifique el estado en el PLC.");
            }

            await Task.Delay(ConfirmPollInterval, cancellationToken);
        }
    }

    private static string UserOrDefault(string user) =>
        string.IsNullOrWhiteSpace(user) ? "operador" : user.Trim();
}

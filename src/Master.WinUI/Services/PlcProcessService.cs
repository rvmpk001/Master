using System.Globalization;
using Microsoft.Data.SqlClient;

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
    /// Todos los registros de receta de dbo.Tags (campo de fórmula -> tag dr*).
    /// Cuáles se pueden ESCRIBIR se decide en cada carga leyendo dbo.Tags: el tag debe existir, estar
    /// habilitado (en=1), tener un tipo numérico (MI, ML o MF) y una dirección ÚNICA entre los registros
    /// de receta. Las direcciones repetidas (ML99, MF4, MI99...) son de relleno y se rechazan, porque
    /// varios materiales se pisarían entre sí en el PLC.
    /// </summary>
    private sealed record RecipeSlot(string FormulaField, string TagName);

    private static readonly RecipeSlot[] RecipeSlots =
    {
        new("A1", "drAgregado1"), new("A2", "drAgregado2"), new("A3", "drAgregado3"), new("A4", "drAgregado4"),
        new("A5", "drAgregado5"), new("A6", "drAgregado6"), new("A7", "drAgregado7"), new("A8", "drAgregado8"),
        new("C1", "drCemento1"), new("C2", "drCemento2"), new("C3", "drCemento3"),
        new("C4", "drCemento4"), new("C5", "drCemento5"),
        new("D1", "drAditivo1"), new("D2", "drAditivo2"), new("D3", "drAditivo3"),
        new("D4", "drAditivo4"), new("D5", "drAditivo5"), new("D6", "drAditivo6"),
        new("Agua", "drAgua"), new("Agua2", "drAgua2"), new("Agua3", "drAgua3"),
        new("Fibra1", "drFibra1"), new("Fibra2", "drFibra2"), new("Fibra3", "drFibra3")
    };

    // Registro en WriteAsync (límite amplio); el máximo real por tipo se aplica en StartLoadAsync.
    private const double RegisteredMaximum = 2_000_000_000;

    // Límites de seguridad por tipo, en la unidad de la receta (kg). Ajústelos a la capacidad real.
    private const double MaxMi = 32767;
    private const double MaxMl = 999_999;
    private const double MaxMf = 9999;

    private sealed record ResolvedSlot(bool IsInteger, double Max, string? Problem);

    private readonly PlcCommunicationService _plc;
    private readonly PlcTagService _tagService = new();
    private readonly SemaphoreSlim _commandLock = new(1, 1);
    private volatile bool _commandInProgress;

    public PlcProcessService(PlcCommunicationService plc)
    {
        _plc = plc ?? throw new ArgumentNullException(nameof(plc));

        foreach (var slot in RecipeSlots)
        {
            _plc.AllowWrite(new PlcWritableTag(slot.TagName, 0, RegisteredMaximum));
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
            Dictionary<string, ResolvedSlot> resolved;
            try
            {
                resolved = ResolveRecipeSlots(await _tagService.GetTagsAsync(cancellationToken));
            }
            catch (SqlException exception)
            {
                return PlcProcessResult.Rejected(
                    $"No se pudieron leer los tags de receta (SQL {exception.Number}): {exception.Message} " +
                    "No se envió nada al PLC.");
            }

            var plan = new List<(RecipeSlot Slot, double Value)>();
            var byField = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in recipe)
            {
                var slot = RecipeSlots.FirstOrDefault(candidate =>
                    candidate.FormulaField.Equals(line.FormulaField, StringComparison.OrdinalIgnoreCase));
                if (slot is null)
                {
                    return PlcProcessResult.Rejected(
                        $"El campo de fórmula '{line.FormulaField}' ({line.MaterialName}) no está asociado a ningún " +
                        "registro de receta del PLC. No se envió nada al PLC.");
                }

                var info = resolved[slot.FormulaField];
                if (info.Problem is not null)
                {
                    return PlcProcessResult.Rejected(
                        $"El material '{line.MaterialName}' ({slot.TagName}) no se puede enviar al PLC: " +
                        $"{info.Problem}. No se envió nada al PLC.");
                }

                var value = (double)line.Quantity;
                if (info.IsInteger)
                {
                    value = Math.Round(value, MidpointRounding.AwayFromZero);
                }

                if (value < 0 || value > info.Max)
                {
                    return PlcProcessResult.Rejected(
                        $"La cantidad de {line.MaterialName} ({value.ToString(CultureInfo.CurrentCulture)}) " +
                        $"excede el máximo permitido ({info.Max}). No se envió nada al PLC.");
                }

                byField[slot.FormulaField] = value;
            }

            // Se escriben (en 0 si la receta no los usa) solo los registros utilizables, para no dejar
            // cantidades de una receta anterior en el PLC.
            foreach (var slot in RecipeSlots)
            {
                if (resolved[slot.FormulaField].Problem is null)
                {
                    plan.Add((slot, byField.GetValueOrDefault(slot.FormulaField, 0)));
                }
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

    private static Dictionary<string, ResolvedSlot> ResolveRecipeSlots(IReadOnlyList<PlcTagRecord> tags)
    {
        var byName = tags
            .GroupBy(tag => tag.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        // Qué registros de receta usan cada (tipo, dirección): si hay más de uno, es una dirección de relleno.
        var owners = new Dictionary<(int Type, int Address), List<string>>();
        foreach (var slot in RecipeSlots)
        {
            if (byName.TryGetValue(slot.TagName, out var list)
                && list.Count == 1
                && list[0].AddressType is { } type
                && list[0].PlcAddress is { } address)
            {
                var key = ((int)type, (int)address);
                if (!owners.TryGetValue(key, out var names))
                {
                    names = new List<string>();
                    owners[key] = names;
                }

                names.Add(slot.TagName);
            }
        }

        var result = new Dictionary<string, ResolvedSlot>(StringComparer.OrdinalIgnoreCase);
        foreach (var slot in RecipeSlots)
        {
            result[slot.FormulaField] = ResolveSlot(slot, byName, owners);
        }

        return result;
    }

    private static ResolvedSlot ResolveSlot(
        RecipeSlot slot,
        IReadOnlyDictionary<string, List<PlcTagRecord>> byName,
        IReadOnlyDictionary<(int Type, int Address), List<string>> owners)
    {
        static ResolvedSlot Blocked(string problem) => new(false, 0, problem);

        if (!byName.TryGetValue(slot.TagName, out var matches))
        {
            return Blocked($"el tag '{slot.TagName}' no existe en dbo.Tags");
        }

        if (matches.Count != 1)
        {
            return Blocked($"el tag '{slot.TagName}' está duplicado en dbo.Tags");
        }

        var tag = matches[0];
        if (!tag.IsEnabled)
        {
            return Blocked($"el tag '{slot.TagName}' está deshabilitado en dbo.Tags (en=0)");
        }

        if (tag.AddressType is not { } type || tag.PlcAddress is not { } address || address < 0)
        {
            return Blocked($"el tag '{slot.TagName}' no tiene tipo o dirección PLC válidos");
        }

        var typeCode = (int)type;
        var typeName = typeCode switch { 0 => "MI", 3 => "MF", 4 => "ML", _ => null };
        if (typeName is null)
        {
            return Blocked($"el tag '{slot.TagName}' no es de un tipo numérico escribible (MI, ML o MF)");
        }

        if (owners.TryGetValue((typeCode, (int)address), out var sharing) && sharing.Count > 1)
        {
            var others = string.Join(", ", sharing.Where(name =>
                !name.Equals(slot.TagName, StringComparison.OrdinalIgnoreCase)));
            return Blocked(
                $"la dirección {typeName}{address} está repetida en otros registros de receta ({others}); " +
                "parece de relleno. Corrija la dirección en dbo.Tags");
        }

        return typeCode switch
        {
            0 => new ResolvedSlot(true, MaxMi, null),
            4 => new ResolvedSlot(true, MaxMl, null),
            _ => new ResolvedSlot(false, MaxMf, null)
        };
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

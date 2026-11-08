using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;

namespace Master_WinUI.Services;

public sealed class ReportCatalogService
{
    private const int MaximumRows = 5000;
    private readonly SqlConnectionFactory _connectionFactory = new();

    private static readonly ReportCatalogDefinition[] Catalog =
    {
        new(
            "production",
            "Producción",
            "Descargas de planta por fecha.",
            new[] { "Produccion General", "Produccion Real" },
            """
            SELECT TOP (@RowLimit)
                Fecha_Hora AS Fecha,
                Cod_Formula AS Formula,
                Cantidad,
                Remision,
                Cliente,
                Obra,
                Turno,
                TipoDescarga AS Tipo,
                FechaCarga
            FROM dbo.Descarga
            WHERE Fecha_Hora >= @StartDate AND Fecha_Hora < @EndDate
            ORDER BY Fecha_Hora DESC;
            """),
        new(
            "remissions",
            "Remisiones",
            "Remisiones emitidas en el periodo.",
            new[] { "Remision Anterior", "Remision Salida" },
            """
            SELECT TOP (@RowLimit)
                Fecha,
                Hora,
                Remision,
                Cliente,
                Obra,
                Pedido,
                Producto,
                Uso,
                Cantidad,
                Operador,
                Despachador,
                Turno,
                Tipo
            FROM dbo.Remisiones
            WHERE Fecha >= @StartDate AND Fecha < @EndDate
            ORDER BY Fecha DESC, Hora DESC;
            """),
        new(
            "inventory-movements",
            "Movimientos de inventario",
            "Entradas y salidas registradas por fecha.",
            new[] { "Entradas Material", "Salidas Material" },
            """
            SELECT TOP (@RowLimit)
                Fecha,
                Tipo,
                Tipo_Material AS Categoria,
                Material,
                Cantidad,
                Proveedor,
                Factura,
                Origen,
                Tolva,
                Usuario,
                Cliente,
                Obra,
                NumSalida,
                Observaciones
            FROM dbo.Carga
            WHERE Fecha >= @StartDate AND Fecha < @EndDate
            ORDER BY Fecha DESC;
            """),
        new(
            "orders",
            "Pedidos",
            "Pedidos creados en el periodo.",
            new[] { "PedidosR" },
            """
            SELECT TOP (@RowLimit)
                Fecha,
                NumPedido AS Pedido,
                Estatus,
                Cliente,
                Obra,
                FechaEntrega,
                Hora,
                Cantidad,
                Receta,
                Descripcion,
                Tipo,
                Producto,
                Uso,
                Envio,
                Surtida,
                xSurtir
            FROM dbo.Pedidos
            WHERE Fecha >= @StartDate AND Fecha < @EndDate
            ORDER BY Fecha DESC, NumPedido DESC;
            """)
    };

    public IReadOnlyList<ReportCatalogDefinition> GetCatalog() => Catalog;

    public async Task<ReportCatalogResult> ExecuteAsync(
        string reportId,
        DateTime startDate,
        DateTime endDateExclusive,
        CancellationToken cancellationToken = default)
    {
        var report = Catalog.FirstOrDefault(item =>
            item.Id.Equals(reportId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException("El reporte seleccionado no está disponible en el catálogo.");

        if (startDate >= endDateExclusive)
        {
            throw new InvalidOperationException("La fecha inicial debe ser anterior o igual a la fecha final.");
        }

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(report.Query, connection)
        {
            CommandTimeout = 30
        };
        command.Parameters.Add("@RowLimit", SqlDbType.Int).Value = MaximumRows + 1;
        command.Parameters.Add("@StartDate", SqlDbType.DateTime).Value = startDate;
        command.Parameters.Add("@EndDate", SqlDbType.DateTime).Value = endDateExclusive;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var columns = Enumerable.Range(0, reader.FieldCount)
            .Select(reader.GetName)
            .ToArray();
        var rows = new List<IReadOnlyList<string>>();
        var isTruncated = false;
        while (await reader.ReadAsync(cancellationToken))
        {
            if (rows.Count == MaximumRows)
            {
                isTruncated = true;
                break;
            }

            var values = new string[reader.FieldCount];
            for (var index = 0; index < reader.FieldCount; index++)
            {
                values[index] = reader.IsDBNull(index)
                    ? string.Empty
                    : FormatValue(reader.GetValue(index));
            }

            rows.Add(values);
        }

        return new ReportCatalogResult(report, columns, rows, isTruncated);
    }

    private static string FormatValue(object value) =>
        value switch
        {
            DateTime dateTime => dateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        };
}

public sealed class ReportCatalogDefinition(
    string id,
    string name,
    string description,
    IReadOnlyList<string> permissionModules,
    string query)
{
    public string Id { get; set; } = id;
    public string Name { get; set; } = name;
    public string Description { get; set; } = description;
    public IReadOnlyList<string> PermissionModules { get; set; } = permissionModules;
    public string Query { get; set; } = query;
}

public sealed record ReportCatalogResult(
    ReportCatalogDefinition Definition,
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<string>> Rows,
    bool IsTruncated);

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
                AND (@Supplier IS NULL OR LTRIM(RTRIM(Proveedor)) = @Supplier)
                AND (@Material IS NULL OR LTRIM(RTRIM(Tipo_Material)) = @Material)
                AND (@MovementType IS NULL OR Tipo = @MovementType)
            ORDER BY Fecha DESC;
            """),
        new(
            "inventory-stock",
            "Existencias",
            "Existencias actuales por categoría de material.",
            new[] { "ExistenciasR" },
            """
            WITH MachineSlots AS
            (
                SELECT Param,
                    UPPER(LTRIM(RTRIM(Valor_Param))) AS Material,
                    ROW_NUMBER() OVER (PARTITION BY Param ORDER BY Id) AS SlotOrder
                FROM dbo.ConfigMaq
                WHERE Param IN
                    (N'A1', N'A2', N'A3', N'A4', N'A5', N'A6', N'A7', N'A8',
                     N'C1', N'C2', N'C3', N'C4', N'C5',
                     N'D1', N'D2', N'D3', N'D4', N'D5', N'D6',
                     N'G1', N'G2', N'G3', N'FI1', N'FI2', N'FI3')
                    AND Valor_Param IS NOT NULL
            ),
            EnabledMachineMaterials AS
            (
                SELECT Param, Material
                FROM MachineSlots
                WHERE SlotOrder = 1
                    AND Material <> N''
                    AND Material <> N'DESHABILITADA'
            ),
            InventoryRows AS
            (
                SELECT 1 AS CategoriaOrden,
                    ROW_NUMBER() OVER (ORDER BY LTRIM(RTRIM(Nombre))) AS Fila,
                    LTRIM(RTRIM(Nombre)) AS Material,
                    COALESCE(Existencia, N'') AS Existencia
                FROM dbo.Agregados
                WHERE Nombre IS NOT NULL
                    AND LTRIM(RTRIM(Nombre)) <> N''
                    AND UPPER(LTRIM(RTRIM(Nombre))) <> N'DESHABILITADA'
                    AND EXISTS
                    (
                        SELECT 1
                        FROM EnabledMachineMaterials
                        WHERE Param IN (N'A1', N'A2', N'A3', N'A4', N'A5', N'A6', N'A7', N'A8')
                            AND Material = UPPER(LTRIM(RTRIM(dbo.Agregados.Nombre)))
                    )
                UNION ALL
                SELECT 2, ROW_NUMBER() OVER (ORDER BY LTRIM(RTRIM(Nombre))),
                    LTRIM(RTRIM(Nombre)), COALESCE(Existencia, N'')
                FROM dbo.Cementos
                WHERE Nombre IS NOT NULL
                    AND LTRIM(RTRIM(Nombre)) <> N''
                    AND UPPER(LTRIM(RTRIM(Nombre))) <> N'DESHABILITADA'
                    AND EXISTS
                    (
                        SELECT 1
                        FROM EnabledMachineMaterials
                        WHERE Param IN (N'C1', N'C2', N'C3', N'C4', N'C5')
                            AND Material = UPPER(LTRIM(RTRIM(dbo.Cementos.Nombre)))
                    )
                UNION ALL
                SELECT 3, ROW_NUMBER() OVER (ORDER BY LTRIM(RTRIM(Nombre))),
                    LTRIM(RTRIM(Nombre)), COALESCE(Existencia, N'')
                FROM dbo.Aditivos
                WHERE Nombre IS NOT NULL
                    AND LTRIM(RTRIM(Nombre)) <> N''
                    AND UPPER(LTRIM(RTRIM(Nombre))) <> N'DESHABILITADA'
                    AND EXISTS
                    (
                        SELECT 1
                        FROM EnabledMachineMaterials
                        WHERE Param IN (N'D1', N'D2', N'D3', N'D4', N'D5', N'D6')
                            AND Material = UPPER(LTRIM(RTRIM(dbo.Aditivos.Nombre)))
                    )
                UNION ALL
                SELECT 4, ROW_NUMBER() OVER (ORDER BY LTRIM(RTRIM(Nombre))),
                    LTRIM(RTRIM(Nombre)), COALESCE(Existencia, N'')
                FROM dbo.Agua
                WHERE Nombre IS NOT NULL
                    AND LTRIM(RTRIM(Nombre)) <> N''
                    AND UPPER(LTRIM(RTRIM(Nombre))) <> N'DESHABILITADA'
                    AND EXISTS
                    (
                        SELECT 1
                        FROM EnabledMachineMaterials
                        WHERE Param IN (N'G1', N'G2', N'G3')
                            AND Material = UPPER(LTRIM(RTRIM(dbo.Agua.Nombre)))
                    )
                UNION ALL
                SELECT 5, ROW_NUMBER() OVER (ORDER BY LTRIM(RTRIM(Nombre))),
                    LTRIM(RTRIM(Nombre)), COALESCE(Existencia, N'')
                FROM dbo.Fibras
                WHERE Nombre IS NOT NULL
                    AND LTRIM(RTRIM(Nombre)) <> N''
                    AND UPPER(LTRIM(RTRIM(Nombre))) <> N'DESHABILITADA'
                    AND EXISTS
                    (
                        SELECT 1
                        FROM EnabledMachineMaterials
                        WHERE Param IN (N'FI1', N'FI2', N'FI3')
                            AND Material = UPPER(LTRIM(RTRIM(dbo.Fibras.Nombre)))
                    )
            ),
            StockByMaterial AS
            (
                SELECT Fila,
                    MAX(CASE WHEN CategoriaOrden = 1 THEN Material + NCHAR(9) + Existencia END) AS [TOTAL AGREGADOS KG],
                    MAX(CASE WHEN CategoriaOrden = 2 THEN Material + NCHAR(9) + Existencia END) AS [TOTAL CEMENTOS KG],
                    MAX(CASE WHEN CategoriaOrden = 3 THEN Material + NCHAR(9) + Existencia END) AS [TOTAL ADITIVOS KG],
                    MAX(CASE WHEN CategoriaOrden = 4 THEN Material + NCHAR(9) + Existencia END) AS [TOTAL AGUA LTS.],
                    MAX(CASE WHEN CategoriaOrden = 5 THEN Material + NCHAR(9) + Existencia END) AS [TOTAL FIBRAS LTS.]
                FROM InventoryRows
                GROUP BY Fila
            )
            SELECT TOP (@RowLimit)
                [TOTAL AGREGADOS KG],
                [TOTAL CEMENTOS KG],
                [TOTAL ADITIVOS KG],
                [TOTAL AGUA LTS.],
                [TOTAL FIBRAS LTS.]
            FROM StockByMaterial
            ORDER BY Fila;
            """,
            usesDateRange: false),
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
        CancellationToken cancellationToken = default,
        string? supplier = null,
        string? material = null,
        string? movementType = null)
    {
        var report = Catalog.FirstOrDefault(item =>
            item.Id.Equals(reportId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException("El reporte seleccionado no está disponible en el catálogo.");

        if (report.UsesDateRange && startDate >= endDateExclusive)
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
        if (report.UsesDateRange)
        {
            command.Parameters.Add("@StartDate", SqlDbType.DateTime).Value = startDate;
            command.Parameters.Add("@EndDate", SqlDbType.DateTime).Value = endDateExclusive;
        }
        if (report.Id == "inventory-movements")
        {
            command.Parameters.Add("@Supplier", SqlDbType.NVarChar, 50).Value =
                string.IsNullOrWhiteSpace(supplier) ? DBNull.Value : supplier.Trim();
            command.Parameters.Add("@Material", SqlDbType.NVarChar, 50).Value =
                string.IsNullOrWhiteSpace(material) ? DBNull.Value : material.Trim();
            command.Parameters.Add("@MovementType", SqlDbType.Char, 1).Value =
                string.IsNullOrWhiteSpace(movementType) ? DBNull.Value : movementType.Trim();
        }

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
    string query,
    bool usesDateRange = true)
{
    public string Id { get; set; } = id;
    public string Name { get; set; } = name;
    public string Description { get; set; } = description;
    public IReadOnlyList<string> PermissionModules { get; set; } = permissionModules;
    public string Query { get; set; } = query;
    public bool UsesDateRange { get; set; } = usesDateRange;
}

public sealed record ReportCatalogResult(
    ReportCatalogDefinition Definition,
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<string>> Rows,
    bool IsTruncated);

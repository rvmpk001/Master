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
            FormulaReportLayout.ReportId,
            "Reporte general de fórmulas",
            "Fórmulas activas y sus materiales configurados.",
            new[] { "FormulasR" },
            """
            SELECT TOP (@RowLimit)
                Cod_Formula, Descripcion,
                A1, A1C, A2, A2C, A3, A3C, A4, A4C, A5, A5C, A6, A6C, A7, A7C, A8, A8C,
                C1, C1C, C2, C2C, C3, C3C, C4, C4C, C5, C5C,
                D1, D1C, D2, D2C, D3, D3C, D4, D4C, D5, D5C, D6, D6C,
                Agua, AguaC, Agua2, Agua2C, Agua3, Agua3C,
                Fibra1, Fibra1C, Fibra2, Fibra2C, Fibra3, Fibra3C
            FROM dbo.Formulas
            WHERE ISNULL(Estatus, 'A') <> 'C'
            ORDER BY Cod_Formula, Id_Formula;
            """,
            usesDateRange: false),
        new(
            "clients",
            "Reporte general de clientes",
            "Directorio general con los datos registrados de cada cliente.",
            new[] { "ClientesR" },
            """
            SELECT TOP (@RowLimit)
                Cod_Cliente AS [Código],
                Cliente,
                RFC,
                Direccion AS [Dirección],
                Colonia,
                Telefono AS [Teléfono],
                CP AS [Código postal],
                Ciudad,
                Estado,
                Obra,
                Residente,
                Comprador,
                email AS [Correo electrónico],
                Pagina AS [Sitio web]
            FROM dbo.Clientes
            ORDER BY Cliente, Cod_Cliente;
            """,
            usesDateRange: false),
        new(
            "suppliers",
            "Reporte general de proveedores",
            "Directorio general con los datos registrados de cada proveedor.",
            new[] { "ProveedoresR" },
            """
            SELECT TOP (@RowLimit)
                Cod_Proveedor AS [Código],
                Proveedor,
                RFC,
                Direccion AS [Dirección],
                Colonia,
                Telefono AS [Teléfono],
                CP AS [Código postal],
                Ciudad,
                Estado,
                Residente,
                email AS [Correo electrónico],
                Pagina AS [Sitio web]
            FROM dbo.Proveedores
            ORDER BY Proveedor, Cod_Proveedor;
            """,
            usesDateRange: false),
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
                AND (@OrderType IS NULL OR LTRIM(RTRIM(Tipo)) = @OrderType)
                AND (@OrderFormula IS NULL OR LTRIM(RTRIM(Receta)) = @OrderFormula)
                AND (@OrderClient IS NULL OR LTRIM(RTRIM(Cliente)) = @OrderClient)
                AND (@OrderUse IS NULL OR LTRIM(RTRIM(Uso)) = @OrderUse)
                AND (@OrderProduct IS NULL OR LTRIM(RTRIM(Producto)) = @OrderProduct)
                AND (@OrderNumber IS NULL OR NumPedido = @OrderNumber)
                AND (@OrderConstruction IS NULL OR LTRIM(RTRIM(Obra)) = @OrderConstruction)
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
        string? movementType = null,
        string? orderType = null,
        string? orderFormula = null,
        string? orderClient = null,
        string? orderUse = null,
        string? orderProduct = null,
        int? orderNumber = null,
        string? orderConstruction = null)
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
        if (report.Id == "orders")
        {
            AddOptionalOrderFilter(command, "@OrderType", orderType);
            AddOptionalOrderFilter(command, "@OrderFormula", orderFormula);
            AddOptionalOrderFilter(command, "@OrderClient", orderClient);
            AddOptionalOrderFilter(command, "@OrderUse", orderUse);
            AddOptionalOrderFilter(command, "@OrderProduct", orderProduct);
            command.Parameters.Add("@OrderNumber", SqlDbType.Int).Value =
                orderNumber.HasValue ? orderNumber.Value : DBNull.Value;
            AddOptionalOrderFilter(command, "@OrderConstruction", orderConstruction);
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

        return report.Id == FormulaReportLayout.ReportId
            ? FormulaReportLayout.CreateResult(report, columns, rows, isTruncated)
            : new ReportCatalogResult(report, columns, rows, isTruncated);
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> GetOrderReportFilterOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        const string query = """
            SELECT Filtro, Valor
            FROM
            (
                SELECT N'Tipo' AS Filtro, CONVERT(nvarchar(500), LTRIM(RTRIM(Tipo))) AS Valor
                FROM dbo.Pedidos
                WHERE NULLIF(LTRIM(RTRIM(Tipo)), N'') IS NOT NULL
                UNION
                SELECT N'Formula', CONVERT(nvarchar(500), LTRIM(RTRIM(Receta)))
                FROM dbo.Pedidos
                WHERE NULLIF(LTRIM(RTRIM(Receta)), N'') IS NOT NULL
                UNION
                SELECT N'Cliente', CONVERT(nvarchar(500), LTRIM(RTRIM(Cliente)))
                FROM dbo.Pedidos
                WHERE NULLIF(LTRIM(RTRIM(Cliente)), N'') IS NOT NULL
                UNION
                SELECT N'Uso', CONVERT(nvarchar(500), LTRIM(RTRIM(Uso)))
                FROM dbo.Pedidos
                WHERE NULLIF(LTRIM(RTRIM(Uso)), N'') IS NOT NULL
                UNION
                SELECT N'Producto', CONVERT(nvarchar(500), LTRIM(RTRIM(Producto)))
                FROM dbo.Pedidos
                WHERE NULLIF(LTRIM(RTRIM(Producto)), N'') IS NOT NULL
                UNION
                SELECT N'Obra', CONVERT(nvarchar(500), LTRIM(RTRIM(Obra)))
                FROM dbo.Pedidos
                WHERE NULLIF(LTRIM(RTRIM(Obra)), N'') IS NOT NULL
            ) AS Filtros
            ORDER BY Filtro, Valor;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection) { CommandTimeout = 30 };
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var options = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        while (await reader.ReadAsync(cancellationToken))
        {
            var filter = reader.GetString(0);
            var value = reader.GetString(1);
            if (!options.TryGetValue(filter, out var values))
            {
                values = new List<string>();
                options.Add(filter, values);
            }

            values.Add(value);
        }

        return options.ToDictionary(
            item => item.Key,
            item => (IReadOnlyList<string>)item.Value,
            StringComparer.Ordinal);
    }

    private static void AddOptionalOrderFilter(SqlCommand command, string name, string? value) =>
        command.Parameters.Add(name, SqlDbType.NVarChar, 500).Value =
            string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();

    private static string FormatValue(object value) =>
        value switch
        {
            DateTime dateTime => dateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        };
}

public sealed record FormulaReportCategory(
    string Column,
    string Unit,
    IReadOnlyList<(string MaterialField, string QuantityField)> Slots);

public static class FormulaReportLayout
{
    public const string ReportId = "formulas";

    public static IReadOnlyList<FormulaReportCategory> Categories { get; } = new[]
    {
        new FormulaReportCategory("Agregados KG", "KG", Enumerable.Range(1, 8).Select(index => ($"A{index}", $"A{index}C")).ToArray()),
        new FormulaReportCategory("Cemento KG", "KG", Enumerable.Range(1, 5).Select(index => ($"C{index}", $"C{index}C")).ToArray()),
        new FormulaReportCategory("Aditivos LTS", "LTS", Enumerable.Range(1, 6).Select(index => ($"D{index}", $"D{index}C")).ToArray()),
        new FormulaReportCategory("Agua LTS", "LTS", new[] { ("Agua", "AguaC"), ("Agua2", "Agua2C"), ("Agua3", "Agua3C") }),
        new FormulaReportCategory("Fibras KG", "KG", Enumerable.Range(1, 3).Select(index => ($"Fibra{index}", $"Fibra{index}C")).ToArray())
    };

    public static IReadOnlyList<string> Columns { get; } =
        new[] { "Código de fórmula", "Descripción" }.Concat(Categories.Select(category => category.Column)).ToArray();

    public static bool TryGetCategory(string column, out FormulaReportCategory category)
    {
        category = Categories.FirstOrDefault(item => item.Column.Equals(column, StringComparison.Ordinal))
            ?? null!;
        return category is not null;
    }

    public static IReadOnlyList<(string Material, string Quantity)> ParseEntries(string value) =>
        value.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line =>
            {
                var separator = line.IndexOf('\t');
                return separator < 0
                    ? (line, string.Empty)
                    : (line[..separator], line[(separator + 1)..]);
            })
            .ToArray();

    public static ReportCatalogResult CreateResult(
        ReportCatalogDefinition definition,
        IReadOnlyList<string> sourceColumns,
        IReadOnlyList<IReadOnlyList<string>> sourceRows,
        bool isTruncated)
    {
        var sourceIndexes = sourceColumns
            .Select((column, index) => (column, index))
            .ToDictionary(item => item.column, item => item.index, StringComparer.OrdinalIgnoreCase);
        var rows = sourceRows.Select(sourceRow =>
        {
            var values = new List<string>(Columns.Count)
            {
                GetSourceValue("Cod_Formula"),
                GetSourceValue("Descripcion")
            };
            foreach (var category in Categories)
            {
                values.Add(string.Join('\n', category.Slots.Select(slot =>
                {
                    var material = GetSourceValue(slot.MaterialField);
                    var quantity = FormatQuantity(GetSourceValue(slot.QuantityField));
                    return $"{(string.IsNullOrWhiteSpace(material) ? "DESHABILITADA" : material.Trim())}\t{quantity}";
                })));
            }

            return (IReadOnlyList<string>)values;

            string GetSourceValue(string name) =>
                sourceIndexes.TryGetValue(name, out var index) ? sourceRow[index] : string.Empty;
        }).ToArray();

        return new ReportCatalogResult(definition, Columns, rows, isTruncated);
    }

    private static string FormatQuantity(string value)
    {
        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out var quantity)
            || decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out quantity))
        {
            return quantity.ToString("0.00", CultureInfo.InvariantCulture);
        }

        return string.IsNullOrWhiteSpace(value) ? "0.00" : value.Trim();
    }
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

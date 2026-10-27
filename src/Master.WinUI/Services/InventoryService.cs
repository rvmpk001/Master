using System.Globalization;
using Microsoft.Data.SqlClient;

namespace Master_WinUI.Services;

public sealed class InventoryService
{
    private readonly SqlConnectionFactory _connectionFactory = new();
    private static readonly IReadOnlyDictionary<string, string> InventoryTables = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["AGREGADOS"] = "Agregados",
        ["ADITIVOS"] = "Aditivos",
        ["AGUAS"] = "Agua",
        ["CEMENTOS"] = "Cementos",
        ["FIBRAS"] = "Fibras"
    };

    public async Task<IReadOnlyList<InventoryMaterial>> GetInventoryAsync(CancellationToken cancellationToken = default)
    {
        const string query = """
            SELECT N'AGREGADOS' AS Categoria, Nombre, Existencia FROM dbo.Agregados
            UNION ALL
            SELECT N'ADITIVOS', Nombre, Existencia FROM dbo.Aditivos
            UNION ALL
            SELECT N'AGUAS', Nombre, Existencia FROM dbo.Agua
            UNION ALL
            SELECT N'CEMENTOS', Nombre, Existencia FROM dbo.Cementos
            UNION ALL
            SELECT N'FIBRAS', Nombre, Existencia FROM dbo.Fibras
            ORDER BY Categoria, Nombre;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var inventory = new List<InventoryMaterial>();

        while (await reader.ReadAsync(cancellationToken))
        {
            inventory.Add(new InventoryMaterial(
                reader.GetString(0),
                reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                reader.IsDBNull(2) ? string.Empty : reader.GetString(2)));
        }

        return inventory;
    }

    public async Task<IReadOnlyList<string>> GetSuppliersAsync(CancellationToken cancellationToken = default)
    {
        const string query = """
            SELECT DISTINCT LTRIM(RTRIM(Proveedor))
            FROM dbo.Proveedores
            WHERE NULLIF(LTRIM(RTRIM(Proveedor)), N'') IS NOT NULL
            ORDER BY LTRIM(RTRIM(Proveedor));
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var suppliers = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
        {
            suppliers.Add(reader.GetString(0));
        }

        return suppliers;
    }

    public async Task SaveInventoryMovementAsync(
        string category,
        string materialName,
        decimal quantity,
        string supplier,
        string origin,
        string invoice,
        string username,
        bool isExit,
        CancellationToken cancellationToken = default)
    {
        if (!InventoryTables.TryGetValue(category, out var tableName))
        {
            throw new InvalidOperationException($"Categoría de inventario no reconocida: {category}.");
        }

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "La cantidad debe ser mayor que cero.");
        }

        var movementType = isExit ? "S" : "E";
        var quantityText = quantity.ToString("0.############################", CultureInfo.InvariantCulture);

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var stockQuery = $"SELECT Existencia FROM dbo.[{tableName}] WITH (UPDLOCK, ROWLOCK) WHERE Nombre = @Name;";
            await using var stockCommand = new SqlCommand(stockQuery, connection, transaction);
            stockCommand.Parameters.Add("@Name", System.Data.SqlDbType.NVarChar, 50).Value = materialName;
            var currentStockValue = await stockCommand.ExecuteScalarAsync(cancellationToken);

            if (currentStockValue is null)
            {
                throw new InvalidOperationException("El material seleccionado ya no existe en el inventario. Actualiza la lista e inténtalo de nuevo.");
            }

            var currentStockText = currentStockValue is DBNull ? "0" : Convert.ToString(currentStockValue, CultureInfo.InvariantCulture);
            if (!decimal.TryParse(currentStockText, NumberStyles.Number, CultureInfo.InvariantCulture, out var currentStock))
            {
                throw new InvalidOperationException($"La existencia actual de {materialName} no es numérica y no se puede actualizar.");
            }

            if (isExit && currentStock < quantity)
            {
                throw new InvalidOperationException($"Existencia insuficiente de {materialName}. Disponible: {currentStockText}.");
            }

            var updatedStock = isExit ? currentStock - quantity : currentStock + quantity;
            var updateQuery = $"UPDATE dbo.[{tableName}] SET Existencia = @Existencia WHERE Nombre = @Name;";
            await using (var updateCommand = new SqlCommand(updateQuery, connection, transaction))
            {
                updateCommand.Parameters.Add("@Existencia", System.Data.SqlDbType.NVarChar, 50).Value =
                    updatedStock.ToString("0.############################", CultureInfo.InvariantCulture);
                updateCommand.Parameters.Add("@Name", System.Data.SqlDbType.NVarChar, 50).Value = materialName;
                if (await updateCommand.ExecuteNonQueryAsync(cancellationToken) != 1)
                {
                    throw new InvalidOperationException("No se pudo actualizar la existencia del material seleccionado.");
                }
            }

            const string movementQuery = """
                INSERT INTO dbo.Carga
                    (Material, Tipo_Material, Cantidad, Proveedor, Factura, Origen, Usuario, Tipo)
                VALUES
                    (@Material, @TipoMaterial, @Cantidad, @Proveedor, @Factura, @Origen, @Usuario, @Tipo);
                """;
            await using (var movementCommand = new SqlCommand(movementQuery, connection, transaction))
            {
                movementCommand.Parameters.Add("@Material", System.Data.SqlDbType.NVarChar, 50).Value = category;
                movementCommand.Parameters.Add("@TipoMaterial", System.Data.SqlDbType.NVarChar, 50).Value = materialName;
                movementCommand.Parameters.Add("@Cantidad", System.Data.SqlDbType.NVarChar, 50).Value = quantityText;
                movementCommand.Parameters.Add("@Proveedor", System.Data.SqlDbType.NVarChar, 50).Value = supplier;
                movementCommand.Parameters.Add("@Factura", System.Data.SqlDbType.NVarChar, 50).Value = invoice;
                movementCommand.Parameters.Add("@Origen", System.Data.SqlDbType.NVarChar, 50).Value = origin;
                movementCommand.Parameters.Add("@Usuario", System.Data.SqlDbType.NVarChar, 10).Value = username;
                movementCommand.Parameters.Add("@Tipo", System.Data.SqlDbType.Char, 1).Value = movementType;
                await movementCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}

public sealed class InventoryMaterial(string category, string name, string quantity)
{
    public string Category { get; set; } = category;
    public string Name { get; set; } = name;
    public string Quantity { get; set; } = quantity;
}

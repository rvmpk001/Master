using System.Data;
using Microsoft.Data.SqlClient;

namespace Master.WinUI.Data;

internal sealed class ProductRepository(SqlServerConnectionFactory connectionFactory)
{
    public async Task<IReadOnlyList<ProductCategory>> GetCategoriesAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT IdCategoria, Nombre
            FROM dbo.Categorias
            ORDER BY Nombre, IdCategoria;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var categories = new List<ProductCategory>();
        while (await reader.ReadAsync(cancellationToken))
        {
            categories.Add(new ProductCategory(
                reader.GetInt32(reader.GetOrdinal("IdCategoria")),
                reader.GetString(reader.GetOrdinal("Nombre"))));
        }

        return categories;
    }

    public async Task<IReadOnlyList<Product>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT p.IdProducto, p.IdCategoria, c.Nombre AS Categoria,
                   p.Codigo, p.Nombre, p.Descripcion, p.PrecioUnitario,
                   p.Stock, p.ImagenBase64, p.Activo, p.FechaCreacion
            FROM dbo.Productos AS p
            INNER JOIN dbo.Categorias AS c ON c.IdCategoria = p.IdCategoria
            ORDER BY p.Nombre, p.IdProducto;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var products = new List<Product>();
        while (await reader.ReadAsync(cancellationToken))
        {
            products.Add(new Product(
                reader.GetInt32(reader.GetOrdinal("IdProducto")),
                reader.GetInt32(reader.GetOrdinal("IdCategoria")),
                reader.GetString(reader.GetOrdinal("Categoria")),
                reader.GetString(reader.GetOrdinal("Codigo")),
                reader.GetString(reader.GetOrdinal("Nombre")),
                ReadNullableString(reader, "Descripcion"),
                reader.GetDecimal(reader.GetOrdinal("PrecioUnitario")),
                reader.GetInt32(reader.GetOrdinal("Stock")),
                ReadNullableString(reader, "ImagenBase64"),
                reader.GetBoolean(reader.GetOrdinal("Activo")),
                reader.GetDateTime(reader.GetOrdinal("FechaCreacion"))));
        }

        return products;
    }

    public async Task<int> InsertAsync(
        ProductDraft product,
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO dbo.Productos
                (IdCategoria, Codigo, Nombre, Descripcion, PrecioUnitario,
                 Stock, ImagenBase64, Activo)
            OUTPUT INSERTED.IdProducto
            VALUES
                (@IdCategoria, @Codigo, @Nombre, @Descripcion, @PrecioUnitario,
                 @Stock, @ImagenBase64, @Activo);
            """;
        AddProductParameters(command, product);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    public async Task UpdateAsync(
        int id,
        ProductDraft product,
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE dbo.Productos
            SET IdCategoria = @IdCategoria,
                Codigo = @Codigo,
                Nombre = @Nombre,
                Descripcion = @Descripcion,
                PrecioUnitario = @PrecioUnitario,
                Stock = @Stock,
                ImagenBase64 = @ImagenBase64,
                Activo = @Activo
            WHERE IdProducto = @IdProducto;
            """;
        AddProductParameters(command, product);
        command.Parameters.Add("@IdProducto", SqlDbType.Int).Value = id;

        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
        {
            throw new InvalidOperationException("El producto seleccionado ya no existe.");
        }
    }

    public async Task DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM dbo.Productos WHERE IdProducto = @IdProducto;";
        command.Parameters.Add("@IdProducto", SqlDbType.Int).Value = id;

        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
        {
            throw new InvalidOperationException("El producto seleccionado ya no existe.");
        }
    }

    private static string? ReadNullableString(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static void AddProductParameters(SqlCommand command, ProductDraft product)
    {
        command.Parameters.Add("@IdCategoria", SqlDbType.Int).Value = product.CategoryId;
        command.Parameters.Add("@Codigo", SqlDbType.NVarChar, 50).Value = product.Code;
        command.Parameters.Add("@Nombre", SqlDbType.NVarChar, 150).Value = product.Name;
        command.Parameters.Add("@Descripcion", SqlDbType.NVarChar, 500).Value =
            (object?)product.Description ?? DBNull.Value;
        var priceParameter = command.Parameters.Add("@PrecioUnitario", SqlDbType.Decimal);
        priceParameter.Precision = 18;
        priceParameter.Scale = 2;
        priceParameter.Value = product.UnitPrice;
        command.Parameters.Add("@Stock", SqlDbType.Int).Value = product.Stock;
        command.Parameters.Add("@ImagenBase64", SqlDbType.NVarChar, -1).Value =
            (object?)product.ImageBase64 ?? DBNull.Value;
        command.Parameters.Add("@Activo", SqlDbType.Bit).Value = product.IsActive;
    }
}

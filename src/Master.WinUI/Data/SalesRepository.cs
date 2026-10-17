using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;

namespace Master.WinUI.Data;

internal sealed class SalesRepository(SqlServerConnectionFactory connectionFactory)
{
    private const decimal MaximumSqlDecimal18Scale2 = 9999999999999999.99m;

    public async Task<SalesCatalog> GetCatalogAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var taxRate = await ReadDecimalSettingAsync(
            connection,
            transaction: null,
            "Sistema_ImpuestoPorcentaje",
            cancellationToken);
        var currencyCode = await ReadStringSettingAsync(
            connection,
            transaction: null,
            "Sistema_Moneda",
            cancellationToken);

        var customers = new List<SaleCustomer>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT IdCliente, Nombres, Apellidos
                FROM dbo.Clientes
                WHERE Activo = 1
                ORDER BY Apellidos, Nombres, IdCliente;
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                customers.Add(new SaleCustomer(
                    reader.GetInt32(reader.GetOrdinal("IdCliente")),
                    reader.GetString(reader.GetOrdinal("Nombres")),
                    reader.GetString(reader.GetOrdinal("Apellidos"))));
            }
        }

        var products = new List<SaleProduct>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT IdProducto, Codigo, Nombre, PrecioUnitario, Stock, Activo
                FROM dbo.Productos
                WHERE Activo = 1
                ORDER BY Nombre, IdProducto;
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                products.Add(new SaleProduct(
                    reader.GetInt32(reader.GetOrdinal("IdProducto")),
                    reader.GetString(reader.GetOrdinal("Codigo")),
                    reader.GetString(reader.GetOrdinal("Nombre")),
                    reader.GetDecimal(reader.GetOrdinal("PrecioUnitario")),
                    reader.GetInt32(reader.GetOrdinal("Stock")),
                    reader.GetBoolean(reader.GetOrdinal("Activo"))));
            }
        }

        var paymentMethods = new List<string>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT DISTINCT MetodoPago
                FROM dbo.Ventas
                WHERE NULLIF(LTRIM(RTRIM(MetodoPago)), N'') IS NOT NULL
                ORDER BY MetodoPago;
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                paymentMethods.Add(reader.GetString(0));
            }
        }

        return new SalesCatalog(customers, products, paymentMethods, taxRate, currencyCode);
    }

    public async Task<SaleReceipt> CompleteSaleAsync(
        SaleDraft draft,
        CancellationToken cancellationToken = default)
    {
        if (draft.Lines.Count == 0)
        {
            throw new InvalidOperationException("Agrega al menos un producto a la factura.");
        }

        if (draft.CustomerId <= 0 ||
            draft.UserId <= 0 ||
            string.IsNullOrWhiteSpace(draft.PaymentMethod) ||
            draft.PaymentMethod.Length > 50 ||
            (draft.Notes?.Length ?? 0) > 300)
        {
            throw new InvalidOperationException("Revisa el cliente, el método de pago y las observaciones.");
        }

        if (draft.Lines.Select(line => line.ProductId).Distinct().Count() != draft.Lines.Count)
        {
            throw new InvalidOperationException("La factura contiene productos repetidos.");
        }

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var taxRate = await ReadDecimalSettingAsync(
            connection,
            transaction,
            "Sistema_ImpuestoPorcentaje",
            cancellationToken);
        if (taxRate != draft.ExpectedTaxRate)
        {
            throw new InvalidOperationException(
                "La tasa de impuesto cambió. Inicia una factura nueva para cargar la configuración actual.");
        }

        var currencyCode = await ReadStringSettingAsync(
            connection,
            transaction,
            "Sistema_Moneda",
            cancellationToken);
        var customerName = await ReadActiveCustomerNameAsync(
            connection,
            transaction,
            draft.CustomerId,
            cancellationToken);

        var currentProducts = new Dictionary<int, SaleProduct>();
        foreach (var line in draft.Lines.OrderBy(line => line.ProductId))
        {
            if (line.Quantity <= 0)
            {
                throw new InvalidOperationException("Las cantidades deben ser mayores que cero.");
            }

            var product = await ReadActiveProductAsync(
                connection,
                transaction,
                line.ProductId,
                cancellationToken);
            if (product.UnitPrice != line.ExpectedUnitPrice)
            {
                throw new InvalidOperationException(
                    $"El precio de «{product.Name}» cambió. Inicia una factura nueva y vuelve a agregarlo.");
            }

            currentProducts.Add(line.ProductId, product);
        }

        var receiptLinesByProductId = new Dictionary<int, SaleReceiptLine>();
        foreach (var line in draft.Lines)
        {
            var product = currentProducts[line.ProductId];
            var lineSubtotal = checked(product.UnitPrice * line.Quantity);
            EnsureSqlDecimal18Scale2(
                lineSubtotal,
                "El subtotal de un producto excede el rango permitido.");
            receiptLinesByProductId.Add(
                line.ProductId,
                new SaleReceiptLine(
                    product.Code,
                    product.Name,
                    line.Quantity,
                    product.UnitPrice,
                    lineSubtotal));
        }

        var receiptLines = draft.Lines
            .Select(line => receiptLinesByProductId[line.ProductId])
            .ToArray();
        var subtotal = receiptLines.Sum(line => line.Subtotal);
        EnsureSqlDecimal18Scale2(subtotal, "El subtotal de la factura excede el rango permitido.");
        var tax = decimal.Round(subtotal * taxRate / 100m, 2, MidpointRounding.AwayFromZero);
        EnsureSqlDecimal18Scale2(tax, "El impuesto excede el rango permitido.");
        var total = checked(subtotal + tax);
        EnsureSqlDecimal18Scale2(total, "El total de la factura excede el rango permitido.");

        var invoiceNumber = await CreateInvoiceNumberAsync(
            connection,
            transaction,
            draft.SaleDate.Year,
            cancellationToken);
        var saleId = await InsertSaleAsync(
            connection,
            transaction,
            draft,
            invoiceNumber,
            subtotal,
            tax,
            total,
            cancellationToken);

        foreach (var draftLine in draft.Lines)
        {
            await InsertSaleLineAsync(
                connection,
                transaction,
                saleId,
                draftLine.ProductId,
                receiptLinesByProductId[draftLine.ProductId],
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new SaleReceipt(
            invoiceNumber,
            draft.SaleDate,
            customerName,
            draft.PaymentMethod,
            currencyCode,
            taxRate,
            subtotal,
            tax,
            total,
            receiptLines);
    }

    private static async Task<decimal> ReadDecimalSettingAsync(
        SqlConnection connection,
        SqlTransaction? transaction,
        string key,
        CancellationToken cancellationToken)
    {
        var value = await ReadStringSettingAsync(connection, transaction, key, cancellationToken);
        if (!decimal.TryParse(
                value,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var setting) ||
            setting < 0)
        {
            throw new InvalidDataException(
                $"La configuración {key} falta o no contiene un porcentaje válido.");
        }

        return setting;
    }

    private static async Task<string> ReadStringSettingAsync(
        SqlConnection connection,
        SqlTransaction? transaction,
        string key,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT Valor
            FROM dbo.Configuracion
            WHERE Clave = @Clave;
            """;
        command.Parameters.Add("@Clave", SqlDbType.NVarChar, 50).Value = key;

        var value = await command.ExecuteScalarAsync(cancellationToken);
        if (value is not string setting || string.IsNullOrWhiteSpace(setting))
        {
            throw new InvalidDataException(
                $"No se encontró un valor válido para la configuración {key}.");
        }

        return setting.Trim();
    }

    private static async Task<string> ReadActiveCustomerNameAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        int customerId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT Nombres, Apellidos
            FROM dbo.Clientes WITH (UPDLOCK, HOLDLOCK)
            WHERE IdCliente = @IdCliente AND Activo = 1;
            """;
        command.Parameters.Add("@IdCliente", SqlDbType.Int).Value = customerId;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException(
                "El cliente seleccionado ya no está disponible. Inicia una factura nueva.");
        }

        return $"{reader.GetString(0)} {reader.GetString(1)}".Trim();
    }

    private static async Task<SaleProduct> ReadActiveProductAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        int productId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT IdProducto, Codigo, Nombre, PrecioUnitario, Stock, Activo
            FROM dbo.Productos WITH (UPDLOCK, HOLDLOCK)
            WHERE IdProducto = @IdProducto AND Activo = 1;
            """;
        command.Parameters.Add("@IdProducto", SqlDbType.Int).Value = productId;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException(
                "Un producto de la factura ya no está disponible. Inicia una factura nueva.");
        }

        return new SaleProduct(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetDecimal(3),
            reader.GetInt32(4),
            reader.GetBoolean(5));
    }

    private static async Task<string> CreateInvoiceNumberAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        int year,
        CancellationToken cancellationToken)
    {
        var prefix = $"FAC-{year}-";
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT COALESCE(
                MAX(TRY_CONVERT(int, SUBSTRING(NumeroFactura, LEN(@Prefijo) + 1, 30))),
                0)
            FROM dbo.Ventas WITH (UPDLOCK, HOLDLOCK)
            WHERE NumeroFactura LIKE @Prefijo + N'%';
            """;
        command.Parameters.Add("@Prefijo", SqlDbType.NVarChar, 20).Value = prefix;

        var lastNumber = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        var nextNumber = checked(lastNumber + 1);
        return $"{prefix}{nextNumber:D4}";
    }

    private static async Task<int> InsertSaleAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        SaleDraft draft,
        string invoiceNumber,
        decimal subtotal,
        decimal tax,
        decimal total,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO dbo.Ventas
                (NumeroFactura, IdCliente, IdUsuario, FechaVenta, Subtotal, Impuesto,
                 Total, MetodoPago, Observaciones)
            OUTPUT INSERTED.IdVenta
            VALUES
                (@NumeroFactura, @IdCliente, @IdUsuario, @FechaVenta, @Subtotal, @Impuesto,
                 @Total, @MetodoPago, @Observaciones);
            """;
        command.Parameters.Add("@NumeroFactura", SqlDbType.NVarChar, 30).Value = invoiceNumber;
        command.Parameters.Add("@IdCliente", SqlDbType.Int).Value = draft.CustomerId;
        command.Parameters.Add("@IdUsuario", SqlDbType.Int).Value = draft.UserId;
        command.Parameters.Add("@FechaVenta", SqlDbType.DateTime).Value = draft.SaleDate;
        AddMoneyParameter(command, "@Subtotal", subtotal);
        AddMoneyParameter(command, "@Impuesto", tax);
        AddMoneyParameter(command, "@Total", total);
        command.Parameters.Add("@MetodoPago", SqlDbType.NVarChar, 50).Value = draft.PaymentMethod;
        command.Parameters.Add("@Observaciones", SqlDbType.NVarChar, 300).Value =
            string.IsNullOrWhiteSpace(draft.Notes) ? DBNull.Value : draft.Notes.Trim();

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull
            ? throw new InvalidDataException("SQL Server no devolvió el identificador de la venta creada.")
            : Convert.ToInt32(result);
    }

    private static async Task InsertSaleLineAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        int saleId,
        int productId,
        SaleReceiptLine line,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO dbo.DetalleVenta
                (IdVenta, IdProducto, Cantidad, PrecioUnitario, Subtotal)
            VALUES
                (@IdVenta, @IdProducto, @Cantidad, @PrecioUnitario, @Subtotal);
            """;
        command.Parameters.Add("@IdVenta", SqlDbType.Int).Value = saleId;
        command.Parameters.Add("@IdProducto", SqlDbType.Int).Value = productId;
        command.Parameters.Add("@Cantidad", SqlDbType.Int).Value = line.Quantity;
        AddMoneyParameter(command, "@PrecioUnitario", line.UnitPrice);
        AddMoneyParameter(command, "@Subtotal", line.Subtotal);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddMoneyParameter(SqlCommand command, string name, decimal value)
    {
        var parameter = command.Parameters.Add(name, SqlDbType.Decimal);
        parameter.Precision = 18;
        parameter.Scale = 2;
        parameter.Value = value;
    }

    private static void EnsureSqlDecimal18Scale2(decimal value, string message)
    {
        if (value < 0 || value > MaximumSqlDecimal18Scale2 || decimal.Round(value, 2) != value)
        {
            throw new InvalidOperationException(message);
        }
    }
}

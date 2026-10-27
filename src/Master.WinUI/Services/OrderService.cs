using System.Globalization;
using System.Data;
using Microsoft.Data.SqlClient;

namespace Master_WinUI.Services;

public sealed class OrderService
{
    private readonly SqlConnectionFactory _connectionFactory = new();

    public async Task<OrderTableData> GetOrdersAsync(CancellationToken cancellationToken = default)
    {
        const string query = """
            SELECT IdPedido, NumPedido, Estatus, Fecha, Cliente, Obra, Comprador, Autoriza,
                   Telefono, Email, FechaEntrega, Hora, Cantidad, Receta, Descripcion,
                   Tipo, Envio, Intervalo, Producto, Uso, Observaciones, Surtida, xSurtir
            FROM dbo.Pedidos
            ORDER BY Fecha DESC, NumPedido DESC;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var active = new List<OrderRecord>();
        var completed = new List<OrderRecord>();
        var cancelled = new List<OrderRecord>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var status = reader["Estatus"] is DBNull
                ? string.Empty
                : Convert.ToString(reader["Estatus"], CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
            var order = new OrderRecord(
                reader.GetInt32(reader.GetOrdinal("IdPedido")),
                FormatValue(reader["NumPedido"]),
                status,
                FormatValue(reader["Fecha"]),
                FormatValue(reader["Cliente"]),
                FormatValue(reader["FechaEntrega"]),
                FormatValue(reader["Hora"]),
                FormatValue(reader["Cantidad"]),
                FormatValue(reader["Receta"]),
                FormatValue(reader["Surtida"]),
                FormatValue(reader["xSurtir"]))
            {
                Construction = FormatValue(reader["Obra"]),
                Buyer = FormatValue(reader["Comprador"]),
                AuthorizedBy = FormatValue(reader["Autoriza"]),
                Phone = FormatValue(reader["Telefono"]),
                Email = FormatValue(reader["Email"]),
                Description = FormatValue(reader["Descripcion"]),
                DischargeType = FormatValue(reader["Tipo"]),
                Shipping = GetDecimal(reader, "Envio"),
                IntervalMinutes = GetDateTime(reader, "Intervalo")?.TimeOfDay.TotalMinutes,
                Product = FormatValue(reader["Producto"]),
                PaymentCondition = FormatValue(reader["Uso"]),
                Observations = FormatValue(reader["Observaciones"]),
                DateValue = GetDateTime(reader, "Fecha"),
                DeliveryDateValue = GetDateTime(reader, "FechaEntrega"),
                DeliveryTimeValue = GetDateTime(reader, "Hora")?.TimeOfDay,
                QuantityValue = GetDecimal(reader, "Cantidad")
            };

            if (status.Equals("C", StringComparison.OrdinalIgnoreCase))
            {
                cancelled.Add(order);
            }
            else if (status.Equals("T", StringComparison.OrdinalIgnoreCase))
            {
                completed.Add(order);
            }
            else
            {
                active.Add(order);
            }
        }

        return new OrderTableData(active, completed, cancelled);
    }

    public async Task<IReadOnlyList<string>> GetOrderNumbersAsync(CancellationToken cancellationToken = default)
    {
        const string query = """
            SELECT DISTINCT NumPedido
            FROM dbo.Pedidos
            WHERE NumPedido IS NOT NULL
              AND LTRIM(RTRIM(Estatus)) IN ('A', 'P')
            ORDER BY NumPedido DESC;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var orderNumbers = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
        {
            orderNumbers.Add(Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? string.Empty);
        }

        return orderNumbers;
    }

    public async Task<OrderRecipe?> GetOrderRecipeAsync(int orderNumber, CancellationToken cancellationToken = default)
    {
        const string query = """
            SELECT TOP (1) Receta, Descripcion
            FROM dbo.Pedidos
            WHERE NumPedido = @NumPedido
              AND LTRIM(RTRIM(Estatus)) IN ('A', 'P')
            ORDER BY IdPedido DESC;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection);
        command.Parameters.Add("@NumPedido", SqlDbType.Int).Value = orderNumber;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new OrderRecipe(
            reader.IsDBNull(0) ? string.Empty : Convert.ToString(reader.GetValue(0), CultureInfo.CurrentCulture)?.Trim() ?? string.Empty,
            reader.IsDBNull(1) ? string.Empty : Convert.ToString(reader.GetValue(1), CultureInfo.CurrentCulture)?.Trim() ?? string.Empty);
    }

    public async Task<ManualLoadOrderDetails?> GetManualLoadOrderDetailsAsync(
        int orderNumber,
        CancellationToken cancellationToken = default)
    {
        const string query = """
            SELECT TOP (1) Estatus, Cliente, Obra, Receta, Descripcion, Tipo, Producto, Uso,
                   Cantidad, Surtida, xSurtir, Observaciones
            FROM dbo.Pedidos
            WHERE NumPedido = @NumPedido
              AND LTRIM(RTRIM(Estatus)) IN ('A', 'P')
            ORDER BY IdPedido DESC;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection);
        command.Parameters.Add("@NumPedido", SqlDbType.Int).Value = orderNumber;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new ManualLoadOrderDetails(
            orderNumber,
            FormatValue(reader["Estatus"]).Trim(),
            FormatValue(reader["Cliente"]),
            FormatValue(reader["Obra"]),
            FormatValue(reader["Receta"]),
            FormatValue(reader["Descripcion"]),
            FormatValue(reader["Tipo"]),
            FormatValue(reader["Producto"]),
            FormatValue(reader["Uso"]),
            GetDecimal(reader, "Cantidad"),
            GetDecimal(reader, "Surtida"),
            GetDecimal(reader, "xSurtir"),
            FormatValue(reader["Observaciones"]));
    }

    public async Task<string> GetNextManualLoadRemissionAsync(CancellationToken cancellationToken = default)
    {
        const string query = """
            SELECT COALESCE(MAX(CONVERT(bigint, LTRIM(RTRIM(Remision)))), 0) + 1
            FROM dbo.Remisiones
            WHERE LEN(LTRIM(RTRIM(Remision))) BETWEEN 1 AND 18
              AND LTRIM(RTRIM(Remision)) NOT LIKE '%[^0-9]%';
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection);
        var nextNumber = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
        return nextNumber.ToString(CultureInfo.InvariantCulture);
    }

    public async Task<string> SaveManualLoadAsync(
        string recipeCode,
        decimal quantity,
        IReadOnlyDictionary<string, string?> materialQuantities,
        string? dischargeType,
        int? orderNumber,
        string? client,
        string? construction,
        string? product,
        string? paymentCondition,
        string description,
        string? operatorName,
        string? truckUnit,
        decimal? orderedQuantity,
        decimal? suppliedQuantity,
        decimal? toSupplyQuantity,
        string? observations,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(recipeCode) || recipeCode.Length > 50)
        {
            throw new InvalidOperationException("El código de receta es obligatorio y no debe exceder 50 caracteres.");
        }

        if (quantity <= 0)
        {
            throw new InvalidOperationException("La cantidad de carga debe ser mayor que cero.");
        }

        var requiredMaterialFields = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["A1C"] = "Cantidad grava",
            ["A2C"] = "Cantidad arena",
            ["C1C"] = "Cantidad cemento",
            ["Agua"] = "Cantidad agua 1",
            ["D1C"] = "Cantidad aditivo 1",
            ["D2C"] = "Cantidad aditivo 2"
        };
        foreach (var (column, fieldName) in requiredMaterialFields)
        {
            if (!materialQuantities.TryGetValue(column, out var value) ||
                !decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var materialQuantity) ||
                materialQuantity <= 0)
            {
                throw new InvalidOperationException($"{fieldName} es obligatorio y debe ser mayor que cero.");
            }
        }

        string? orderClient = null;
        string? orderConstruction = null;
        int? orderId = null;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            const string remissionQuery = """
                SELECT COALESCE(MAX(CONVERT(bigint, LTRIM(RTRIM(Remision)))), 0) + 1
                FROM dbo.Remisiones WITH (TABLOCKX, HOLDLOCK)
                WHERE LEN(LTRIM(RTRIM(Remision))) BETWEEN 1 AND 18
                  AND LTRIM(RTRIM(Remision)) NOT LIKE '%[^0-9]%';
                """;
            await using var remissionCommand = new SqlCommand(remissionQuery, connection, transaction);
            var remissionNumber = Convert.ToInt64(
                await remissionCommand.ExecuteScalarAsync(cancellationToken),
                CultureInfo.InvariantCulture);
            if (remissionNumber > int.MaxValue)
            {
                throw new InvalidOperationException("La secuencia de remisiones excede el máximo permitido por AjusteRemision.Numremision.");
            }

            var remission = remissionNumber.ToString(CultureInfo.InvariantCulture);

            if (orderNumber is not null)
            {
                const string orderQuery = """
                    SELECT TOP (1) IdPedido, Cliente, Obra
                    FROM dbo.Pedidos WITH (UPDLOCK, HOLDLOCK)
                    WHERE NumPedido = @NumPedido
                      AND LTRIM(RTRIM(Estatus)) IN ('A', 'P')
                    ORDER BY IdPedido DESC;
                    """;
                await using var orderCommand = new SqlCommand(orderQuery, connection, transaction);
                orderCommand.Parameters.Add("@NumPedido", SqlDbType.Int).Value = orderNumber.Value;
                await using var reader = await orderCommand.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                {
                    throw new InvalidOperationException("El pedido seleccionado ya no está activo. Actualiza la lista e inténtalo de nuevo.");
                }

                orderId = reader.GetInt32(0);
                orderClient = reader.IsDBNull(1) ? null : Convert.ToString(reader.GetValue(1), CultureInfo.CurrentCulture)?.Trim();
                orderConstruction = reader.IsDBNull(2) ? null : Convert.ToString(reader.GetValue(2), CultureInfo.CurrentCulture)?.Trim();
            }

            const string insertRemission = """
                INSERT INTO dbo.Remisiones
                    (Cliente, Obra, Pedido, Producto, Uso, Cantidad, DescFormula, Operador, Unidad,
                     Remision, Pedidos, Surtidos, xSurtir, Observaciones, Tipo, FechaCarga)
                VALUES
                    (@Cliente, @Obra, @Pedido, @Producto, @Uso, @Cantidad, @DescFormula, @Operador, @Unidad,
                     @Remision, @Pedidos, @Surtidos, @xSurtir, @Observaciones, @Tipo, GETDATE());
                """;
            await using (var remissionInsert = new SqlCommand(insertRemission, connection, transaction))
            {
                AddTextParameter(remissionInsert, "@Cliente", 100, orderClient ?? client);
                AddTextParameter(remissionInsert, "@Obra", 50, orderConstruction ?? construction);
                AddTextParameter(remissionInsert, "@Pedido", 50, orderNumber?.ToString(CultureInfo.InvariantCulture));
                AddTextParameter(remissionInsert, "@Producto", 50, product);
                AddTextParameter(remissionInsert, "@Uso", 50, paymentCondition);
                AddTextParameter(remissionInsert, "@Cantidad", 50, quantity.ToString("0.############################", CultureInfo.InvariantCulture));
                AddTextParameter(remissionInsert, "@DescFormula", 100, description);
                AddTextParameter(remissionInsert, "@Operador", 50, operatorName);
                AddTextParameter(remissionInsert, "@Unidad", 50, truckUnit);
                AddTextParameter(remissionInsert, "@Remision", 50, remission);
                remissionInsert.Parameters.Add("@Pedidos", SqlDbType.Decimal).Value = orderedQuantity is null ? DBNull.Value : orderedQuantity.Value;
                remissionInsert.Parameters["@Pedidos"].Precision = 18;
                remissionInsert.Parameters["@Pedidos"].Scale = 2;
                remissionInsert.Parameters.Add("@Surtidos", SqlDbType.Decimal).Value = suppliedQuantity is null ? DBNull.Value : suppliedQuantity.Value;
                remissionInsert.Parameters["@Surtidos"].Precision = 18;
                remissionInsert.Parameters["@Surtidos"].Scale = 2;
                remissionInsert.Parameters.Add("@xSurtir", SqlDbType.Decimal).Value = toSupplyQuantity is null ? DBNull.Value : toSupplyQuantity.Value;
                remissionInsert.Parameters["@xSurtir"].Precision = 18;
                remissionInsert.Parameters["@xSurtir"].Scale = 2;
                AddTextParameter(remissionInsert, "@Observaciones", -1, observations);
                AddTextParameter(remissionInsert, "@Tipo", 255, dischargeType);
                await remissionInsert.ExecuteNonQueryAsync(cancellationToken);
            }

            var columns = new[]
            {
                "A1C", "A2C", "A3C", "A4C", "A5C", "A6C", "A7C", "A8C",
                "C1C", "C2C", "C3C", "C4C", "C5C",
                "D1C", "D2C", "D3C", "D4C", "D5C", "D6C",
                "Agua", "Agua2", "Agua3", "FI1C", "FI2C", "FI3C"
            };
            var insertColumns = string.Join(", ", columns.Select(column => $"[{column}]"));
            var insertParameters = string.Join(", ", columns.Select(column => $"@{column}"));
            var query = $"""
                INSERT INTO dbo.Descarga
                    (Cod_Formula, Cantidad, {insertColumns}, Remision, Obra, Cliente, TipoDescarga, IdPedido, FechaCarga)
                VALUES
                    (@CodFormula, @Cantidad, {insertParameters}, @Remision, @Obra, @Cliente, @TipoDescarga, @IdPedido, GETDATE());
                """;
            await using var command = new SqlCommand(query, connection, transaction);
            AddTextParameter(command, "@CodFormula", 50, recipeCode);
            AddTextParameter(command, "@Cantidad", 50, quantity.ToString("0.############################", CultureInfo.InvariantCulture));
            foreach (var column in columns)
            {
                materialQuantities.TryGetValue(column, out var value);
                AddTextParameter(command, $"@{column}", 50, value);
            }

            AddTextParameter(command, "@Remision", 50, remission);
            AddTextParameter(command, "@Obra", 50, orderConstruction ?? construction);
            AddTextParameter(command, "@Cliente", 50, orderClient ?? client);
            command.Parameters.Add("@TipoDescarga", SqlDbType.Char, 10).Value =
                string.IsNullOrWhiteSpace(dischargeType) ? DBNull.Value : dischargeType.Trim();
            command.Parameters.Add("@IdPedido", SqlDbType.Int).Value = orderId is null ? DBNull.Value : orderId.Value;
            await command.ExecuteNonQueryAsync(cancellationToken);
            const string updateRemissionSequence = """
                UPDATE dbo.AjusteRemision
                SET Numremision = @Remision
                WHERE IdConfigRe = (SELECT MIN(IdConfigRe) FROM dbo.AjusteRemision);
                """;
            await using (var sequenceCommand = new SqlCommand(updateRemissionSequence, connection, transaction))
            {
                sequenceCommand.Parameters.Add("@Remision", SqlDbType.Int).Value = checked((int)remissionNumber);
                await sequenceCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return remission;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static void AddTextParameter(SqlCommand command, string name, int size, string? value)
    {
        command.Parameters.Add(name, SqlDbType.NVarChar, size).Value =
            string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();
    }

    public async Task<int> AddOrderAsync(
        string client,
        string construction,
        string buyer,
        string authorizedBy,
        string phone,
        string email,
        DateTime date,
        DateTime deliveryDate,
        TimeSpan deliveryTime,
        decimal quantity,
        string recipe,
        string description,
        string dischargeType,
        decimal shipping,
        double intervalMinutes,
        string product,
        string paymentCondition,
        string observations,
        CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            const string nextNumberQuery = """
                SELECT ISNULL(MAX(NumPedido), 0) + 1
                FROM dbo.Pedidos WITH (TABLOCKX, HOLDLOCK);
                """;
            await using var numberCommand = new SqlCommand(nextNumberQuery, connection, transaction);
            var numberValue = await numberCommand.ExecuteScalarAsync(cancellationToken);
            if (numberValue is null or DBNull)
            {
                throw new InvalidOperationException("No se pudo determinar el número del pedido.");
            }

            var number = Convert.ToInt32(numberValue, CultureInfo.InvariantCulture);
            const string insertQuery = """
                INSERT INTO dbo.Pedidos
                    (NumPedido, Estatus, Fecha, Cliente, Obra, Comprador, Autoriza, Telefono, Email,
                     FechaEntrega, Hora, Cantidad, Receta, Descripcion, Tipo, Envio, Intervalo,
                     Producto, Uso, Observaciones, Surtida, xSurtir)
                VALUES
                    (@NumPedido, @Estatus, @Fecha, @Cliente, @Obra, @Comprador, @Autoriza, @Telefono, @Email,
                     @FechaEntrega, @Hora, @Cantidad, @Receta, @Descripcion, @Tipo, @Envio, @Intervalo,
                     @Producto, @Uso, @Observaciones, @Surtida, @xSurtir);
                """;
            await using var command = new SqlCommand(insertQuery, connection, transaction);
            command.Parameters.Add("@NumPedido", SqlDbType.Int).Value = number;
            command.Parameters.Add("@Estatus", SqlDbType.Char, 10).Value = "P";
            command.Parameters.Add("@Fecha", SqlDbType.DateTime).Value = date;
            AddNullableText(command, "@Cliente", 255, client);
            AddNullableText(command, "@Obra", 500, construction);
            AddNullableText(command, "@Comprador", 255, buyer);
            AddNullableText(command, "@Autoriza", 255, authorizedBy);
            AddNullableText(command, "@Telefono", 50, phone);
            AddNullableText(command, "@Email", 80, email);
            command.Parameters.Add("@FechaEntrega", SqlDbType.DateTime).Value = deliveryDate.Date;
            command.Parameters.Add("@Hora", SqlDbType.DateTime).Value = new DateTime(1900, 1, 1).Add(deliveryTime);
            AddDecimal(command, "@Cantidad", quantity);
            AddNullableText(command, "@Receta", 255, recipe);
            AddNullableText(command, "@Descripcion", 550, description);
            AddNullableText(command, "@Tipo", 255, dischargeType);
            AddDecimal(command, "@Envio", shipping);
            command.Parameters.Add("@Intervalo", SqlDbType.DateTime).Value =
                new DateTime(1900, 1, 1).AddMinutes(intervalMinutes);
            AddNullableText(command, "@Producto", 255, product);
            AddNullableText(command, "@Uso", 255, paymentCondition);
            command.Parameters.Add("@Observaciones", SqlDbType.NVarChar, -1).Value =
                string.IsNullOrWhiteSpace(observations) ? DBNull.Value : observations;
            AddDecimal(command, "@Surtida", 0);
            AddDecimal(command, "@xSurtir", quantity);
            await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return number;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task UpdateOrderAsync(
        int id,
        string status,
        string client,
        string construction,
        string buyer,
        string authorizedBy,
        string phone,
        string email,
        DateTime date,
        DateTime deliveryDate,
        TimeSpan deliveryTime,
        decimal quantity,
        string recipe,
        string description,
        string dischargeType,
        decimal shipping,
        double intervalMinutes,
        string product,
        string paymentCondition,
        string observations,
        CancellationToken cancellationToken = default)
    {
        const string query = """
            UPDATE dbo.Pedidos
            SET Estatus = @Estatus, Fecha = @Fecha, Cliente = @Cliente, Obra = @Obra,
                Comprador = @Comprador, Autoriza = @Autoriza, Telefono = @Telefono, Email = @Email,
                FechaEntrega = @FechaEntrega, Hora = @Hora, Cantidad = @Cantidad, Receta = @Receta,
                Descripcion = @Descripcion, Tipo = @Tipo, Envio = @Envio, Intervalo = @Intervalo,
                Producto = @Producto, Uso = @Uso, Observaciones = @Observaciones,
                xSurtir = CASE WHEN @Cantidad > ISNULL(Surtida, 0) THEN @Cantidad - ISNULL(Surtida, 0) ELSE 0 END
            WHERE IdPedido = @IdPedido AND LTRIM(RTRIM(Estatus)) IN ('A', 'P');
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection);
        command.Parameters.Add("@IdPedido", SqlDbType.Int).Value = id;
        command.Parameters.Add("@Estatus", SqlDbType.Char, 10).Value = status;
        command.Parameters.Add("@Fecha", SqlDbType.DateTime).Value = date;
        AddNullableText(command, "@Cliente", 255, client);
        AddNullableText(command, "@Obra", 500, construction);
        AddNullableText(command, "@Comprador", 255, buyer);
        AddNullableText(command, "@Autoriza", 255, authorizedBy);
        AddNullableText(command, "@Telefono", 50, phone);
        AddNullableText(command, "@Email", 80, email);
        command.Parameters.Add("@FechaEntrega", SqlDbType.DateTime).Value = deliveryDate.Date;
        command.Parameters.Add("@Hora", SqlDbType.DateTime).Value = new DateTime(1900, 1, 1).Add(deliveryTime);
        AddDecimal(command, "@Cantidad", quantity);
        AddNullableText(command, "@Receta", 255, recipe);
        AddNullableText(command, "@Descripcion", 550, description);
        AddNullableText(command, "@Tipo", 255, dischargeType);
        AddDecimal(command, "@Envio", shipping);
        command.Parameters.Add("@Intervalo", SqlDbType.DateTime).Value =
            new DateTime(1900, 1, 1).AddMinutes(intervalMinutes);
        AddNullableText(command, "@Producto", 255, product);
        AddNullableText(command, "@Uso", 255, paymentCondition);
        command.Parameters.Add("@Observaciones", SqlDbType.NVarChar, -1).Value =
            string.IsNullOrWhiteSpace(observations) ? DBNull.Value : observations;
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
        {
            throw new InvalidOperationException("El pedido ya no está activo o no existe. Actualiza la lista e inténtalo de nuevo.");
        }
    }

    public async Task CancelOrderAsync(int id, CancellationToken cancellationToken = default)
    {
        const string query = """
            UPDATE dbo.Pedidos
            SET Estatus = 'C'
            WHERE IdPedido = @IdPedido AND LTRIM(RTRIM(Estatus)) IN ('A', 'P');
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection);
        command.Parameters.Add("@IdPedido", SqlDbType.Int).Value = id;
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
        {
            throw new InvalidOperationException("El pedido ya no está activo o no existe. Actualiza la lista e inténtalo de nuevo.");
        }
    }

    public async Task RestoreOrderAsync(int id, CancellationToken cancellationToken = default)
    {
        const string query = """
            UPDATE dbo.Pedidos
            SET Estatus = 'A'
            WHERE IdPedido = @IdPedido AND LTRIM(RTRIM(Estatus)) = 'C';
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection);
        command.Parameters.Add("@IdPedido", SqlDbType.Int).Value = id;
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
        {
            throw new InvalidOperationException("El pedido ya está activo o no existe. Actualiza la lista e inténtalo de nuevo.");
        }
    }

    public async Task<IReadOnlyList<OrderRecipe>> GetRecipesAsync(CancellationToken cancellationToken = default)
    {
        const string query = """
            SELECT Cod_Formula, MAX(Descripcion) AS Descripcion
            FROM dbo.Formulas
            WHERE NULLIF(LTRIM(RTRIM(Cod_Formula)), '') IS NOT NULL
              AND ISNULL(Estatus, 'A') <> 'C'
            GROUP BY Cod_Formula
            ORDER BY Cod_Formula;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var recipes = new List<OrderRecipe>();
        while (await reader.ReadAsync(cancellationToken))
        {
            recipes.Add(new OrderRecipe(
                Convert.ToString(reader.GetValue(0), CultureInfo.CurrentCulture)?.Trim() ?? string.Empty,
                reader.IsDBNull(1) ? string.Empty : Convert.ToString(reader.GetValue(1), CultureInfo.CurrentCulture)?.Trim() ?? string.Empty));
        }

        return recipes;
    }

    public async Task<IReadOnlyList<string>> GetDischargeTypesAsync(CancellationToken cancellationToken = default)
    {
        const string query = """
            SELECT DISTINCT LTRIM(RTRIM(Descripcion))
            FROM dbo.OpcionesPedido
            WHERE Tipo = 'T'
              AND NULLIF(LTRIM(RTRIM(Descripcion)), '') IS NOT NULL
            ORDER BY LTRIM(RTRIM(Descripcion));
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var dischargeTypes = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
        {
            dischargeTypes.Add(Convert.ToString(reader.GetValue(0), CultureInfo.CurrentCulture) ?? string.Empty);
        }

        return dischargeTypes;
    }

    public async Task<IReadOnlyList<string>> GetProductsAsync(CancellationToken cancellationToken = default)
    {
        const string query = """
            SELECT DISTINCT LTRIM(RTRIM(Descripcion))
            FROM dbo.OpcionesPedido
            WHERE Tipo = 'P'
              AND NULLIF(LTRIM(RTRIM(Descripcion)), '') IS NOT NULL
            ORDER BY LTRIM(RTRIM(Descripcion));
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var products = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
        {
            products.Add(Convert.ToString(reader.GetValue(0), CultureInfo.CurrentCulture) ?? string.Empty);
        }

        return products;
    }

    public async Task<IReadOnlyList<string>> GetPaymentConditionsAsync(CancellationToken cancellationToken = default)
    {
        const string query = """
            SELECT DISTINCT LTRIM(RTRIM(Descripcion))
            FROM dbo.OpcionesPedido
            WHERE Tipo = 'U'
            AND NULLIF(LTRIM(RTRIM(Descripcion)), '') IS NOT NULL
            ORDER BY LTRIM(RTRIM(Descripcion));
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var conditions = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
        {
            conditions.Add(Convert.ToString(reader.GetValue(0), CultureInfo.CurrentCulture) ?? string.Empty);
        }

        return conditions;
    }

    private static string FormatValue(object value) => value is DBNull
        ? string.Empty
        : value switch
        {
            DateTime date when date.TimeOfDay == TimeSpan.Zero => date.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture),
            DateTime date => date.ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture),
            decimal number => number.ToString("0.##", CultureInfo.CurrentCulture),
            _ => Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty
        };

    private static void AddNullableText(SqlCommand command, string name, int size, string value)
    {
        command.Parameters.Add(name, SqlDbType.NVarChar, size).Value =
            string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();
    }

    private static void AddDecimal(SqlCommand command, string name, decimal value)
    {
        var parameter = command.Parameters.Add(name, SqlDbType.Decimal);
        parameter.Precision = 18;
        parameter.Scale = 2;
        parameter.Value = value;
    }

    private static DateTime? GetDateTime(SqlDataReader reader, string name) =>
        reader[name] is DBNull ? null : Convert.ToDateTime(reader[name], CultureInfo.InvariantCulture);

    private static decimal? GetDecimal(SqlDataReader reader, string name) =>
        reader[name] is DBNull ? null : Convert.ToDecimal(reader[name], CultureInfo.InvariantCulture);
}

public sealed class OrderRecipe(string code, string description)
{
    public string Code { get; set; } = code;
    public string Description { get; set; } = description;
}

public sealed record ManualLoadOrderDetails(
    int Number,
    string Status,
    string Client,
    string Construction,
    string Recipe,
    string Description,
    string DischargeType,
    string Product,
    string PaymentCondition,
    decimal? Quantity,
    decimal? Supplied,
    decimal? ToSupply,
    string Observations);

public sealed class OrderRecord(
    int id,
    string number,
    string status,
    string date,
    string client,
    string delivery,
    string time,
    string quantity,
    string recipe,
    string supplied,
    string toSupply)
{
    public int Id { get; set; } = id;
    public string Number { get; set; } = number;
    public string Status { get; set; } = status;
    public string Date { get; set; } = date;
    public string Client { get; set; } = client;
    public string Delivery { get; set; } = delivery;
    public string Time { get; set; } = time;
    public string Quantity { get; set; } = quantity;
    public string Recipe { get; set; } = recipe;
    public string Supplied { get; set; } = supplied;
    public string ToSupply { get; set; } = toSupply;
    public string Construction { get; set; } = string.Empty;
    public string Buyer { get; set; } = string.Empty;
    public string AuthorizedBy { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DischargeType { get; set; } = string.Empty;
    public decimal? Shipping { get; set; }
    public double? IntervalMinutes { get; set; }
    public string Product { get; set; } = string.Empty;
    public string PaymentCondition { get; set; } = string.Empty;
    public string Observations { get; set; } = string.Empty;
    public DateTime? DateValue { get; set; }
    public DateTime? DeliveryDateValue { get; set; }
    public TimeSpan? DeliveryTimeValue { get; set; }
    public decimal? QuantityValue { get; set; }
}

public sealed record OrderTableData(
    IReadOnlyList<OrderRecord> Active,
    IReadOnlyList<OrderRecord> Completed,
    IReadOnlyList<OrderRecord> Cancelled);

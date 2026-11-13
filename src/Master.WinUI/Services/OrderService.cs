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

    public async Task<LatestDownloadRecord?> GetLatestDownloadAsync(CancellationToken cancellationToken = default)
    {
        const string query = """
            SELECT TOP (1)
                d.Fecha_Hora,
                d.Cod_Formula,
                d.Cantidad,
                d.Remision,
                d.Obra,
                d.Cliente,
                d.TipoDescarga,
                p.NumPedido,
                p.Estatus,
                r.Producto,
                r.Uso,
                r.Despachador,
                r.Operador,
                r.Unidad,
                r.Pedidos,
                r.Surtidos,
                r.xSurtir,
                r.Observaciones,
                r.DescFormula
            FROM dbo.Descarga AS d
            LEFT JOIN dbo.Pedidos AS p ON p.IdPedido = d.IdPedido
            OUTER APPLY
            (
                SELECT TOP (1)
                    Remisiones.Producto,
                    Remisiones.Uso,
                    Remisiones.Despachador,
                    Remisiones.Operador,
                    Remisiones.Unidad,
                    Remisiones.Pedidos,
                    Remisiones.Surtidos,
                    Remisiones.xSurtir,
                    Remisiones.Observaciones,
                    Remisiones.DescFormula
                FROM dbo.Remisiones
                WHERE Remisiones.Remision = d.Remision
                ORDER BY Remisiones.Id DESC
            ) AS r
            ORDER BY d.Fecha_Hora DESC, d.FechaCarga DESC;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new LatestDownloadRecord(
            GetDateTime(reader, "Fecha_Hora"),
            FormatValue(reader["Cod_Formula"]),
            FormatValue(reader["Cantidad"]),
            FormatValue(reader["Remision"]),
            FormatValue(reader["Obra"]),
            FormatValue(reader["Cliente"]),
            FormatValue(reader["TipoDescarga"]),
            FormatValue(reader["NumPedido"]),
            FormatValue(reader["Estatus"]),
            FormatValue(reader["Producto"]),
            FormatValue(reader["Uso"]),
            FormatValue(reader["Despachador"]),
            FormatValue(reader["Operador"]),
            FormatValue(reader["Unidad"]),
            GetDecimal(reader, "Pedidos"),
            GetDecimal(reader, "Surtidos"),
            GetDecimal(reader, "xSurtir"),
            FormatValue(reader["Observaciones"]),
            FormatValue(reader["DescFormula"]));
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
        IReadOnlySet<string> requiredMaterialFields,
        string? dischargeType,
        int? orderNumber,
        string? client,
        int? clientCode,
        string? construction,
        string? product,
        string? paymentCondition,
        string description,
        string? operatorName,
        string? truckUnit,
        string? dispatcher,
        string? shift,
        DateTime actualRemissionDate,
        TimeSpan actualRemissionTime,
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

        foreach (var (value, label) in new (string? Value, string Label)[]
        {
            (client, "Cliente"),
            (construction, "Obra"),
            (description, "Descripción de la receta"),
            (product, "Producto"),
            (paymentCondition, "Condiciones de pago"),
            (dischargeType, "Tipo de descarga"),
            (operatorName, "Operador"),
            (truckUnit, "Camión"),
            (dispatcher, "Despachador"),
            (observations, "Observaciones"),
            (shift, "Turno")
        })
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"{label} es obligatorio.");
            }
        }

        if (orderedQuantity is null || suppliedQuantity is null || toSupplyQuantity is null)
        {
            throw new InvalidOperationException("Las cantidades de pedidos, surtidos y por surtir son obligatorias.");
        }

        if (actualRemissionDate == default)
        {
            throw new InvalidOperationException("La fecha real de remisión es obligatoria.");
        }

        if (actualRemissionTime < TimeSpan.Zero || actualRemissionTime >= TimeSpan.FromDays(1))
        {
            throw new InvalidOperationException("La hora real de remisión es obligatoria y debe ser válida.");
        }

        if (quantity <= 0)
        {
            throw new InvalidOperationException("La cantidad de carga debe ser mayor que cero.");
        }

        var quantityText = quantity.ToString("0.############################", CultureInfo.InvariantCulture);
        if (quantityText.Length > 8)
        {
            throw new InvalidOperationException("La cantidad excede los 8 caracteres que admite el procedimiento de remisiones.");
        }

        var materialFieldNames = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["A1C"] = "Cantidad grava",
            ["A2C"] = "Cantidad arena",
            ["A3C"] = "Cantidad agregado 3",
            ["A4C"] = "Cantidad agregado 4",
            ["A5C"] = "Cantidad agregado 5",
            ["A6C"] = "Cantidad agregado 6",
            ["A7C"] = "Cantidad agregado 7",
            ["A8C"] = "Cantidad agregado 8",
            ["C1C"] = "Cantidad cemento",
            ["C2C"] = "Cantidad cemento 2",
            ["C3C"] = "Cantidad cemento 3",
            ["C4C"] = "Cantidad cemento 4",
            ["C5C"] = "Cantidad cemento 5",
            ["D1C"] = "Cantidad aditivo 1",
            ["D2C"] = "Cantidad aditivo 2",
            ["D3C"] = "Cantidad aditivo 3",
            ["D4C"] = "Cantidad aditivo 4",
            ["D5C"] = "Cantidad aditivo 5",
            ["D6C"] = "Cantidad aditivo 6",
            ["Agua"] = "Cantidad agua 1",
            ["Agua2"] = "Cantidad agua 2",
            ["Agua3"] = "Cantidad agua 3",
            ["FI1C"] = "Cantidad fibra 1",
            ["FI2C"] = "Cantidad fibra 2",
            ["FI3C"] = "Cantidad fibra 3"
        };
        foreach (var column in requiredMaterialFields)
        {
            var fieldName = materialFieldNames.TryGetValue(column, out var name) ? name : column;
            if (!materialQuantities.TryGetValue(column, out var value) ||
                !decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var materialQuantity) ||
                materialQuantity < 0)
            {
                throw new InvalidOperationException($"{fieldName} es obligatorio y debe ser igual o mayor que cero.");
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

            var loadTimestamp = DateTime.Now;
            await using (var remissionInsert = new SqlCommand("dbo.sp_ins_Remision", connection, transaction)
            {
                CommandType = CommandType.StoredProcedure
            })
            {
                remissionInsert.Parameters.Add("@Manual", SqlDbType.Int).Value = 1;
                AddTextParameter(remissionInsert, "@Cliente", 100, orderClient ?? client);
                AddTextParameter(remissionInsert, "@Obra", 50, orderConstruction ?? construction);
                AddTextParameter(remissionInsert, "@Pedido", 50, orderNumber?.ToString(CultureInfo.InvariantCulture));
                AddTextParameter(remissionInsert, "@Producto", 50, product);
                AddTextParameter(remissionInsert, "@Uso", 50, paymentCondition);
                AddTextParameter(remissionInsert, "@Tipo", 255, dischargeType);
                remissionInsert.Parameters.Add("@Cantidad", SqlDbType.NVarChar, 8).Value = quantityText;
                AddTextParameter(remissionInsert, "@DescFormula", 100, description);
                AddTextParameter(remissionInsert, "@Despachador", 50, dispatcher);
                AddTextParameter(remissionInsert, "@Operador", 50, operatorName);
                AddTextParameter(remissionInsert, "@Unidad", 50, truckUnit);
                AddTextParameter(remissionInsert, "@Remision", 50, remission);
                AddTextParameter(remissionInsert, "@TMA", 20, null);
                AddTextParameter(remissionInsert, "@Resistencia", 20, null);
                AddTextParameter(remissionInsert, "@Edad", 20, null);
                AddTextParameter(remissionInsert, "@Revenimiento", 20, null);
                AddTextParameter(remissionInsert, "@Aditivos", 50, null);
                AddTextParameter(remissionInsert, "@Colocacion", 50, null);
                AddNullableDecimal(remissionInsert, "@Pedidos", orderedQuantity);
                AddNullableDecimal(remissionInsert, "@Surtidos", suppliedQuantity);
                AddNullableDecimal(remissionInsert, "@xSurtir", toSupplyQuantity);
                remissionInsert.Parameters.Add("@Observaciones", SqlDbType.NText).Value =
                    string.IsNullOrWhiteSpace(observations) ? DBNull.Value : observations.Trim();
                remissionInsert.Parameters.Add("@Fecha", SqlDbType.DateTime).Value = actualRemissionDate.Date;
                remissionInsert.Parameters.Add("@Horario", SqlDbType.DateTime).Value =
                    new DateTime(1900, 1, 1).Add(actualRemissionTime);
                remissionInsert.Parameters.Add("@CodCliente", SqlDbType.Int).Value =
                    clientCode is null ? DBNull.Value : clientCode.Value;
                AddTextParameter(remissionInsert, "@SubFijo", 50, null);
                AddTextParameter(remissionInsert, "@Turno", 255, shift);
                remissionInsert.Parameters.Add("@FechaCarga", SqlDbType.DateTime).Value = loadTimestamp;

                await using var resultReader = await remissionInsert.ExecuteReaderAsync(cancellationToken);
                if (!await resultReader.ReadAsync(cancellationToken))
                {
                    throw new InvalidOperationException("El procedimiento de remisiones no devolvió un resultado.");
                }

                var errorNumber = Convert.ToInt32(resultReader["ErrNumber"], CultureInfo.InvariantCulture);
                if (errorNumber != 0)
                {
                    var errorMessage = Convert.ToString(resultReader["ErrMessage"], CultureInfo.CurrentCulture)
                        ?? "Error no especificado por el procedimiento de remisiones.";
                    throw new InvalidOperationException($"No se pudo guardar la remisión (SQL {errorNumber}): {errorMessage}");
                }
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
            AddTextParameter(command, "@Cantidad", 50, quantityText);
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

    private static void AddNullableDecimal(SqlCommand command, string name, decimal? value)
    {
        var parameter = command.Parameters.Add(name, SqlDbType.Decimal);
        parameter.Precision = 18;
        parameter.Scale = 2;
        parameter.Value = value is null ? DBNull.Value : value.Value;
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
        string createdBy,
        CancellationToken cancellationToken = default)
    {
        ValidateRequiredOrderFields(paymentCondition, observations);

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
                     Producto, Uso, Observaciones, Surtida, xSurtir, ObsMod)
                VALUES
                    (@NumPedido, @Estatus, @Fecha, @Cliente, @Obra, @Comprador, @Autoriza, @Telefono, @Email,
                     @FechaEntrega, @Hora, @Cantidad, @Receta, @Descripcion, @Tipo, @Envio, @Intervalo,
                     @Producto, @Uso, @Observaciones, @Surtida, @xSurtir, @ObsMod);
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
            command.Parameters.Add("@ObsMod", SqlDbType.NText).Value =
                $"El pedido se creo el {DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss tt", CultureInfo.GetCultureInfo("es-MX"))} por {createdBy}";
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
        ValidateRequiredOrderFields(paymentCondition, observations);

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

    private static void ValidateRequiredOrderFields(string paymentCondition, string observations)
    {
        if (string.IsNullOrWhiteSpace(paymentCondition))
        {
            throw new InvalidOperationException("Selecciona las condiciones de pago.");
        }

        if (string.IsNullOrWhiteSpace(observations))
        {
            throw new InvalidOperationException("El campo Observaciones es obligatorio.");
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
        var materialSlots = new[]
        {
            "A1", "A2", "A3", "A4", "A5", "A6", "A7", "A8",
            "C1", "C2", "C3", "C4", "C5",
            "D1", "D2", "D3", "D4", "D5", "D6",
            "Agua", "Agua2", "Agua3",
            "Fibra1", "Fibra2", "Fibra3"
        };
        var materialAvailabilityColumns = string.Join(
            ", ",
            materialSlots.Select(slot =>
                $"MAX(CASE WHEN NULLIF(LTRIM(RTRIM([{slot}])), '') IS NOT NULL AND UPPER(LTRIM(RTRIM([{slot}]))) <> N'DESHABILITADA' THEN 1 ELSE 0 END) AS [Has_{slot}]"));
        var query = $"""
            SELECT Cod_Formula, MAX(Descripcion) AS Descripcion, {materialAvailabilityColumns}
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
            var enabledMaterialSlots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < materialSlots.Length; index++)
            {
                if (Convert.ToInt32(reader.GetValue(index + 2), CultureInfo.InvariantCulture) == 1)
                {
                    enabledMaterialSlots.Add(materialSlots[index]);
                }
            }

            recipes.Add(new OrderRecipe(
                Convert.ToString(reader.GetValue(0), CultureInfo.CurrentCulture)?.Trim() ?? string.Empty,
                reader.IsDBNull(1) ? string.Empty : Convert.ToString(reader.GetValue(1), CultureInfo.CurrentCulture)?.Trim() ?? string.Empty,
                enabledMaterialSlots));
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

public sealed class OrderRecipe(string code, string description, IReadOnlySet<string>? materialSlots = null)
{
    public string Code { get; set; } = code;
    public string Description { get; set; } = description;
    public IReadOnlySet<string> MaterialSlots { get; set; } =
        materialSlots ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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

public sealed record LatestDownloadRecord(
    DateTime? Date,
    string RecipeCode,
    string Quantity,
    string Remission,
    string Construction,
    string Client,
    string DischargeType,
    string OrderNumber,
    string OrderStatus,
    string Product,
    string PaymentCondition,
    string Dispatcher,
    string Operator,
    string TruckUnit,
    decimal? OrderedQuantity,
    decimal? SuppliedQuantity,
    decimal? ToSupplyQuantity,
    string Observations,
    string Description);

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

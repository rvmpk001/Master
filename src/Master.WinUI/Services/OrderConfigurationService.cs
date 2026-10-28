using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;

namespace Master_WinUI.Services;

public sealed class OrderConfigurationService
{
    private readonly SqlConnectionFactory _connectionFactory = new();

    public async Task<OrderConfigurationSettings> GetSettingsAsync(
        CancellationToken cancellationToken = default)
    {
        const string query = """
            SELECT TOP (1)
                IdConfigPedido, NumPedido, NomCia, ImpLineas, PedidoOcasional,
                TituloTipo, TituloProducto, TituloUso
            FROM dbo.AjustePedido
            ORDER BY IdConfigPedido;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("dbo.AjustePedido no contiene un registro de configuración.");
        }

        return new OrderConfigurationSettings(
            reader.GetInt32(0),
            reader.IsDBNull(1) ? string.Empty : reader.GetInt32(1).ToString(CultureInfo.InvariantCulture),
            ReadBoolean(reader, 2),
            ReadBoolean(reader, 3),
            ReadBoolean(reader, 4),
            ReadText(reader, 5),
            ReadText(reader, 6),
            ReadText(reader, 7));
    }

    public async Task SaveSettingsAsync(
        OrderConfigurationUpdate settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!int.TryParse(settings.OrderNumber, NumberStyles.None, CultureInfo.InvariantCulture, out var orderNumber)
            || orderNumber < 0)
        {
            throw new InvalidOperationException("Número Pedido debe ser un entero mayor o igual a 0.");
        }

        ValidateTitle(settings.DischargeTitle, "Tipo descarga");
        ValidateTitle(settings.ProductTitle, "Producto");
        ValidateTitle(settings.UseTitle, "Condiciones pago");

        const string query = """
            UPDATE dbo.AjustePedido
            SET NumPedido = @NumPedido,
                NomCia = @NomCia,
                ImpLineas = @ImpLineas,
                PedidoOcasional = @PedidoOcasional,
                TituloTipo = @TituloTipo,
                TituloProducto = @TituloProducto,
                TituloUso = @TituloUso
            WHERE IdConfigPedido = @IdConfigPedido;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection);
        command.Parameters.Add("@IdConfigPedido", SqlDbType.Int).Value = settings.Id;
        command.Parameters.Add("@NumPedido", SqlDbType.Int).Value = orderNumber;
        command.Parameters.Add("@NomCia", SqlDbType.Int).Value = settings.IncludeCompanyName ? 1 : 0;
        command.Parameters.Add("@ImpLineas", SqlDbType.Int).Value = settings.PrintLinesAndTitles ? 1 : 0;
        command.Parameters.Add("@PedidoOcasional", SqlDbType.Int).Value = settings.OccasionalOrders ? 1 : 0;
        command.Parameters.Add("@TituloTipo", SqlDbType.NVarChar, 250).Value = settings.DischargeTitle.Trim();
        command.Parameters.Add("@TituloProducto", SqlDbType.NVarChar, 250).Value = settings.ProductTitle.Trim();
        command.Parameters.Add("@TituloUso", SqlDbType.NVarChar, 250).Value = settings.UseTitle.Trim();

        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
        {
            throw new InvalidOperationException(
                $"No se encontró exactamente un registro con IdConfigPedido={settings.Id} en dbo.AjustePedido.");
        }
    }

    private static bool ReadBoolean(SqlDataReader reader, int ordinal) =>
        !reader.IsDBNull(ordinal) && Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture) != 0;

    private static string ReadText(SqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);

    private static void ValidateTitle(string title, string field)
    {
        if (title.Length > 250)
        {
            throw new InvalidOperationException($"El título de '{field}' no puede superar 250 caracteres.");
        }
    }
}

public sealed record OrderConfigurationSettings(
    int Id,
    string OrderNumber,
    bool IncludeCompanyName,
    bool PrintLinesAndTitles,
    bool OccasionalOrders,
    string DischargeTitle,
    string ProductTitle,
    string UseTitle);

public sealed record OrderConfigurationUpdate(
    int Id,
    string OrderNumber,
    bool IncludeCompanyName,
    bool PrintLinesAndTitles,
    bool OccasionalOrders,
    string DischargeTitle,
    string ProductTitle,
    string UseTitle);

using System.Data;
using Microsoft.Data.SqlClient;

namespace Master_WinUI.Services;

public sealed class MessageConfigurationService
{
    private readonly SqlConnectionFactory _connectionFactory = new();

    public async Task<IReadOnlyDictionary<string, MessageConfigurationRecord>> GetMessagesAsync(
        CancellationToken cancellationToken = default)
    {
        const string query = """
            SELECT IdMensaje, Control, Mensaje, Formato
            FROM dbo.Mensajes;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var messages = new Dictionary<string, MessageConfigurationRecord>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (reader.IsDBNull(1))
            {
                throw new InvalidOperationException("dbo.Mensajes contiene un registro sin valor en Control.");
            }

            var control = reader.GetString(1).Trim();
            var message = new MessageConfigurationRecord(
                reader.GetInt32(0),
                control,
                reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                reader.IsDBNull(3) ? string.Empty : reader.GetString(3));
            if (!messages.TryAdd(control, message))
            {
                throw new InvalidOperationException(
                    $"dbo.Mensajes contiene más de un registro para el control '{control}'.");
            }
        }

        return messages;
    }

    public async Task SaveMessagesAsync(
        IReadOnlyCollection<MessageConfigurationUpdate> updates,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(updates);
        if (updates.Count == 0)
        {
            throw new InvalidOperationException("No hay mensajes para guardar.");
        }

        const string query = """
            UPDATE dbo.Mensajes
            SET Mensaje = @Message,
                Formato = @Format
            WHERE IdMensaje = @IdMensaje
              AND Control = @Control;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            foreach (var update in updates)
            {
                if (update.Message.Length > 100)
                {
                    throw new InvalidOperationException(
                        $"El texto de '{update.Control}' supera el máximo de 100 caracteres.");
                }

                if (update.Format.Length > 800)
                {
                    throw new InvalidOperationException(
                        $"El formato de '{update.Control}' supera el máximo de 800 caracteres.");
                }

                await using var command = new SqlCommand(query, connection, transaction);
                command.Parameters.Add("@IdMensaje", SqlDbType.Int).Value = update.IdMensaje;
                command.Parameters.Add("@Control", SqlDbType.NVarChar, 50).Value = update.Control;
                command.Parameters.Add("@Message", SqlDbType.NVarChar, 100).Value = update.Message;
                command.Parameters.Add("@Format", SqlDbType.NVarChar, 800).Value = update.Format;
                if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
                {
                    throw new InvalidOperationException(
                        $"No se encontró exactamente un registro para '{update.Control}' en dbo.Mensajes.");
                }
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

public sealed record MessageConfigurationRecord(int IdMensaje, string Control, string Text, string Format);

public sealed record MessageConfigurationUpdate(int IdMensaje, string Control, string Message, string Format);

using System.Data;
using Microsoft.Data.SqlClient;

namespace Master_WinUI.Services;

public sealed class OrderScheduleConfigurationService
{
    private readonly SqlConnectionFactory _connectionFactory = new();

    public async Task<IReadOnlyList<OrderScheduleRecord>> GetSchedulesAsync(
        CancellationToken cancellationToken = default)
    {
        const string query = """
            SELECT Posicion, Descripcion, HoraInicial, HoraFinal
            FROM dbo.Horarios
            ORDER BY Posicion, Descripcion;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var schedules = new List<OrderScheduleRecord>();
        while (await reader.ReadAsync(cancellationToken))
        {
            schedules.Add(new OrderScheduleRecord(
                reader.IsDBNull(0) ? null : reader.GetInt32(0),
                reader.IsDBNull(1) ? string.Empty : reader.GetString(1).Trim(),
                reader.IsDBNull(2) ? TimeSpan.Zero : reader.GetDateTime(2).TimeOfDay,
                reader.IsDBNull(3) ? TimeSpan.Zero : reader.GetDateTime(3).TimeOfDay));
        }

        return schedules;
    }

    public async Task SaveSchedulesAsync(
        IReadOnlyList<OrderScheduleRecord> schedules,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(schedules);
        if (schedules.Count != 1)
        {
            throw new InvalidOperationException("La configuración debe contener exactamente un turno.");
        }

        for (var index = 0; index < schedules.Count; index++)
        {
            var schedule = schedules[index];
            if (schedule.Position is not int position || position < 0)
            {
                throw new InvalidOperationException($"Turno {index + 1} debe tener una posición entera mayor o igual a 0.");
            }

            if (schedules.Take(index).Any(previous => previous.Position == position))
            {
                throw new InvalidOperationException($"La posición {position} está repetida en más de un turno.");
            }

            if (schedule.StartTime < TimeSpan.Zero || schedule.StartTime >= TimeSpan.FromDays(1)
                || schedule.EndTime < TimeSpan.Zero || schedule.EndTime >= TimeSpan.FromDays(1))
            {
                throw new InvalidOperationException($"Las horas del turno {index + 1} no son válidas.");
            }
        }

        const string deleteQuery = "DELETE FROM dbo.Horarios;";
        const string insertQuery = """
            INSERT INTO dbo.Horarios (Descripcion, HoraInicial, HoraFinal, Posicion)
            VALUES (@Descripcion, @HoraInicial, @HoraFinal, @Posicion);
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            await using (var deleteCommand = new SqlCommand(deleteQuery, connection, transaction))
            {
                await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            for (var index = 0; index < schedules.Count; index++)
            {
                var schedule = schedules[index];
                await using var insertCommand = new SqlCommand(insertQuery, connection, transaction);
                insertCommand.Parameters.Add("@Descripcion", SqlDbType.NVarChar, -1).Value =
                    string.IsNullOrWhiteSpace(schedule.Description) ? DBNull.Value : schedule.Description.Trim();
                insertCommand.Parameters.Add("@HoraInicial", SqlDbType.DateTime).Value =
                    DateTime.Today.Add(schedule.StartTime);
                insertCommand.Parameters.Add("@HoraFinal", SqlDbType.DateTime).Value =
                    DateTime.Today.Add(schedule.EndTime);
                insertCommand.Parameters.Add("@Posicion", SqlDbType.Int).Value = schedule.Position!.Value;
                await insertCommand.ExecuteNonQueryAsync(cancellationToken);
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

public sealed record OrderScheduleRecord(int? Position, string Description, TimeSpan StartTime, TimeSpan EndTime);

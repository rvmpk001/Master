using System.Data;
using Microsoft.Data.SqlClient;

namespace Master_WinUI.Services;

public sealed class ImageConfigurationService
{
    private readonly SqlConnectionFactory _connectionFactory = new();

    public async Task<IReadOnlyDictionary<string, ImagePathRecord>> GetImagePathsAsync(
        CancellationToken cancellationToken = default)
    {
        const string query = """
            SELECT IdImagen, Control, ImagenOn, ImagenOff
            FROM dbo.Imagenes;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var images = new Dictionary<string, ImagePathRecord>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (reader.IsDBNull(1))
            {
                throw new InvalidOperationException("dbo.Imagenes contiene un registro sin valor en Control.");
            }

            var control = reader.GetString(1).Trim();
            var image = new ImagePathRecord(
                reader.GetInt32(0),
                control,
                reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                reader.IsDBNull(3) ? string.Empty : reader.GetString(3));
            if (!images.TryAdd(control, image))
            {
                throw new InvalidOperationException(
                    $"dbo.Imagenes contiene más de un registro para el control '{control}'.");
            }
        }

        return images;
    }

    public async Task SaveImagePathsAsync(
        IReadOnlyCollection<ImagePathUpdate> updates,
        bool isOn,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(updates);
        if (updates.Count == 0)
        {
            throw new InvalidOperationException("No hay rutas de imágenes para guardar.");
        }

        var columnName = isOn ? "[ImagenOn]" : "[ImagenOff]";
        var query = $"""
            UPDATE dbo.Imagenes
            SET {columnName} = @Path
            WHERE IdImagen = @IdImagen
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
                await using var command = new SqlCommand(query, connection, transaction);
                command.Parameters.Add("@IdImagen", SqlDbType.Int).Value = update.IdImagen;
                command.Parameters.Add("@Control", SqlDbType.NVarChar, 50).Value = update.Control;
                command.Parameters.Add("@Path", SqlDbType.NVarChar, -1).Value = update.Path;
                if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
                {
                    throw new InvalidOperationException(
                        $"No se encontró exactamente un registro para '{update.Control}' en dbo.Imagenes.");
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

public sealed record ImagePathRecord(int IdImagen, string Control, string ImageOn, string ImageOff);

public sealed record ImagePathUpdate(int IdImagen, string Control, string Path);

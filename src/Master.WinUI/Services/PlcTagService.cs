using System.Data;
using Microsoft.Data.SqlClient;

namespace Master_WinUI.Services;

public sealed class PlcTagService
{
    private readonly SqlConnectionFactory _connectionFactory = new();

    public async Task<IReadOnlyList<PlcTagRecord>> GetTagsAsync(CancellationToken cancellationToken = default)
    {
        const string query = """
            SELECT No, TagName, descripcion, tipoDir, dirPLC, dirInterna, typeScan, multip, en
            FROM dbo.Tags
            ORDER BY No;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var tags = new List<PlcTagRecord>();
        while (await reader.ReadAsync(cancellationToken))
        {
            tags.Add(new PlcTagRecord(
                reader.GetInt32(0),
                reader.GetString(1).Trim(),
                reader.IsDBNull(2) ? string.Empty : reader.GetString(2).Trim(),
                reader.IsDBNull(3) ? null : reader.GetFloat(3),
                reader.IsDBNull(4) ? null : reader.GetInt16(4),
                reader.IsDBNull(5) ? null : reader.GetInt16(5),
                reader.GetBoolean(6),
                reader.IsDBNull(7) ? null : reader.GetDouble(7),
                reader.GetBoolean(8)));
        }

        return tags;
    }

    public async Task UpdateTagAsync(PlcTagRecord tag, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tag);
        const string query = """
            UPDATE dbo.Tags
            SET tipoDir = @TipoDir,
                dirPLC = @DirPLC,
                dirInterna = @DirInterna,
                typeScan = @TypeScan,
                multip = @Multip,
                en = @Enabled
            WHERE No = @No;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection);
        command.Parameters.Add("@TipoDir", SqlDbType.Real).Value = (object?)tag.AddressType ?? DBNull.Value;
        command.Parameters.Add("@DirPLC", SqlDbType.SmallInt).Value = (object?)tag.PlcAddress ?? DBNull.Value;
        command.Parameters.Add("@DirInterna", SqlDbType.SmallInt).Value = (object?)tag.InternalAddress ?? DBNull.Value;
        command.Parameters.Add("@TypeScan", SqlDbType.Bit).Value = tag.ScanType;
        command.Parameters.Add("@Multip", SqlDbType.Float).Value = (object?)tag.Multiplier ?? DBNull.Value;
        command.Parameters.Add("@Enabled", SqlDbType.Bit).Value = tag.IsEnabled;
        command.Parameters.Add("@No", SqlDbType.Int).Value = tag.Id;

        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
        {
            throw new InvalidOperationException($"No se encontró exactamente una variable PLC con No={tag.Id}.");
        }
    }
}

public sealed record PlcTagRecord(
    int Id,
    string Name,
    string Description,
    float? AddressType,
    short? PlcAddress,
    short? InternalAddress,
    bool ScanType,
    double? Multiplier,
    bool IsEnabled);

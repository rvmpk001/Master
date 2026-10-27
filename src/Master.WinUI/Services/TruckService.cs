using System.Globalization;
using Microsoft.Data.SqlClient;

namespace Master_WinUI.Services;

public sealed class TruckService
{
    private readonly SqlConnectionFactory _connectionFactory = new();

    public async Task<TruckTableData> GetTrucksAsync(CancellationToken cancellationToken = default)
    {
        const string query = """
            SELECT Cod_Camion, Transportista, Identificador, Placas, No_Economico, Capacidad
            FROM dbo.Camiones
            ORDER BY Cod_Camion;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var trucks = new List<TruckRecord>();
        while (await reader.ReadAsync(cancellationToken))
        {
            trucks.Add(new TruckRecord(
                reader.GetInt32(0),
                Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader.GetValue(2), CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader.GetValue(3), CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader.GetValue(4), CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader.GetValue(5), CultureInfo.InvariantCulture) ?? string.Empty));
        }

        return new TruckTableData(trucks);
    }

    public async Task UpdateTruckAsync(
        int id,
        string operatorInitials,
        string operatorName,
        string plates,
        string unit,
        decimal capacity,
        CancellationToken cancellationToken = default)
    {
        const string query = """
            UPDATE dbo.Camiones
            SET Transportista = @Transportista, Identificador = @Identificador,
                Placas = @Placas, No_Economico = @NoEconomico, Capacidad = @Capacidad
            WHERE Cod_Camion = @Id;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateTruckCommand(connection, query, operatorInitials, operatorName, plates, unit, capacity);
        command.Parameters.Add("@Id", System.Data.SqlDbType.Int).Value = id;
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
        {
            throw new InvalidOperationException($"No se encontró el camión con código {id}.");
        }
    }

    public async Task DeleteTruckAsync(int id, CancellationToken cancellationToken = default)
    {
        const string query = "DELETE FROM dbo.Camiones WHERE Cod_Camion = @Id;";
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection);
        command.Parameters.Add("@Id", System.Data.SqlDbType.Int).Value = id;
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
        {
            throw new InvalidOperationException($"No se encontró el camión con código {id}.");
        }
    }

    public async Task<OperatorTableData> GetOperatorsAsync(CancellationToken cancellationToken = default)
    {
        const string query = "SELECT IdOperador, Iniciales, Nombre FROM dbo.Operadores ORDER BY IdOperador;";
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var operators = new List<OperatorRecord>();
        while (await reader.ReadAsync(cancellationToken))
        {
            operators.Add(new OperatorRecord(
                reader.GetInt32(0),
                Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader.GetValue(2), CultureInfo.InvariantCulture) ?? string.Empty));
        }

        return new OperatorTableData(operators);
    }

    public async Task AddOperatorAsync(string name, string initials, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateOperatorCommand(connection,
            "INSERT INTO dbo.Operadores (Nombre, Iniciales) VALUES (@Nombre, @Iniciales);",
            name, initials);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateOperatorAsync(int id, string name, string initials, CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateOperatorCommand(connection,
            "UPDATE dbo.Operadores SET Nombre = @Nombre, Iniciales = @Iniciales WHERE IdOperador = @Id;",
            name, initials);
        command.Parameters.Add("@Id", System.Data.SqlDbType.Int).Value = id;
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
        {
            throw new InvalidOperationException($"No se encontró el operador con código {id}.");
        }
    }

    public async Task DeleteOperatorAsync(int id, CancellationToken cancellationToken = default)
    {
        const string query = "DELETE FROM dbo.Operadores WHERE IdOperador = @Id;";
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection);
        command.Parameters.Add("@Id", System.Data.SqlDbType.Int).Value = id;
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
        {
            throw new InvalidOperationException($"No se encontró el operador con código {id}.");
        }
    }

    private static SqlCommand CreateOperatorCommand(SqlConnection connection, string query, string name, string initials)
    {
        var command = new SqlCommand(query, connection);
        command.Parameters.Add("@Nombre", System.Data.SqlDbType.NVarChar, 255).Value = name;
        command.Parameters.Add("@Iniciales", System.Data.SqlDbType.NVarChar, 50).Value = initials;
        return command;
    }

    public async Task AddTruckAsync(
        string operatorInitials,
        string operatorName,
        string plates,
        string unit,
        decimal capacity,
        CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateTruckCommand(connection,
            """
            INSERT INTO dbo.Camiones (Transportista, Identificador, Placas, No_Economico, Capacidad)
            VALUES (@Transportista, @Identificador, @Placas, @NoEconomico, @Capacidad);
            """,
            operatorInitials, operatorName, plates, unit, capacity);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static SqlCommand CreateTruckCommand(
        SqlConnection connection,
        string query,
        string operatorInitials,
        string operatorName,
        string plates,
        string unit,
        decimal capacity)
    {
        var command = new SqlCommand(query, connection);
        command.Parameters.Add("@Transportista", System.Data.SqlDbType.NVarChar, 50).Value = operatorInitials;
        command.Parameters.Add("@Identificador", System.Data.SqlDbType.NVarChar, 50).Value = operatorName;
        command.Parameters.Add("@Placas", System.Data.SqlDbType.NVarChar, 50).Value = plates;
        command.Parameters.Add("@NoEconomico", System.Data.SqlDbType.NVarChar, 50).Value = unit;
        var capacityParameter = command.Parameters.Add("@Capacidad", System.Data.SqlDbType.Decimal);
        capacityParameter.Precision = 18;
        capacityParameter.Scale = 2;
        capacityParameter.Value = capacity;
        return command;
    }
}

public sealed class TruckRecord(int id, string initials, string name, string plates, string unit, string capacity)
{
    public int Id { get; set; } = id;
    public string Code { get; set; } = id.ToString(CultureInfo.InvariantCulture);
    public string Initials { get; set; } = initials;
    public string Name { get; set; } = name;
    public string Plates { get; set; } = plates;
    public string Unit { get; set; } = unit;
    public string Capacity { get; set; } = capacity;
}

public sealed class OperatorRecord(int id, string initials, string name)
{
    public int Id { get; set; } = id;
    public string Code { get; set; } = id.ToString(CultureInfo.InvariantCulture);
    public string Initials { get; set; } = initials;
    public string Name { get; set; } = name;
}

public sealed record TruckTableData(IReadOnlyList<TruckRecord> Trucks);
public sealed record OperatorTableData(IReadOnlyList<OperatorRecord> Operators);

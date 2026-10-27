using System.Globalization;
using Microsoft.Data.SqlClient;

namespace Master_WinUI.Services;

public sealed class ClientService
{
    private readonly SqlConnectionFactory _connectionFactory = new();

    public async Task<ClientTableData> GetClientsAsync(CancellationToken cancellationToken = default)
    {
        const string query = """
            SELECT Cod_Cliente, Cliente, Direccion, Colonia, Telefono, CP, Ciudad, Estado,
                   Obra, Residente, Comprador, email, Pagina, RFC
            FROM dbo.Clientes
            ORDER BY Cod_Cliente;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var columns = Enumerable.Range(0, reader.FieldCount)
            .Select(index => new ClientCell(GetColumnTitle(reader.GetName(index)), string.Empty, GetColumnWidth(reader.GetName(index))))
            .ToArray();
        var clients = new List<ClientRecord>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var cells = new ClientCell[reader.FieldCount];
            for (var index = 0; index < reader.FieldCount; index++)
            {
                var name = reader.GetName(index);
                var value = reader.IsDBNull(index)
                    ? string.Empty
                    : Convert.ToString(reader.GetValue(index), CultureInfo.InvariantCulture) ?? string.Empty;
                cells[index] = new ClientCell(GetColumnTitle(name), value, GetColumnWidth(name));
            }

            clients.Add(new ClientRecord(
                reader.GetInt32(0),
                cells,
                GetString(reader, "Cliente"),
                GetString(reader, "RFC"),
                GetString(reader, "Direccion"),
                GetString(reader, "Colonia"),
                GetString(reader, "Ciudad"),
                GetString(reader, "Estado"),
                GetString(reader, "CP"),
                GetString(reader, "Telefono"),
                GetString(reader, "Residente"),
                GetString(reader, "Comprador"),
                GetString(reader, "Obra"),
                GetString(reader, "email"),
                GetString(reader, "Pagina")));
        }

        return new ClientTableData(columns, clients);
    }

    public async Task UpdateClientAsync(
        int id,
        string client,
        string rfc,
        string address,
        string neighborhood,
        string city,
        string state,
        string postalCode,
        string phone,
        string resident,
        string buyer,
        string construction,
        string email,
        string website,
        CancellationToken cancellationToken = default)
    {
        ValidateClient(new ClientDetails(
            client, rfc, address, neighborhood, city, state, postalCode, phone,
            resident, buyer, construction, email, website));
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateClientCommand(connection,
            """
            UPDATE dbo.Clientes
            SET Cliente = @Cliente, RFC = @RFC, Direccion = @Direccion, Colonia = @Colonia,
                Ciudad = @Ciudad, Estado = @Estado, CP = @CP, Telefono = @Telefono,
                Residente = @Residente, Comprador = @Comprador, Obra = @Obra,
                email = @Email, Pagina = @Pagina
            WHERE Cod_Cliente = @Id;
            """,
            client, rfc, address, neighborhood, city, state, postalCode, phone, resident, buyer, construction, email, website);
        command.Parameters.Add("@Id", System.Data.SqlDbType.Int).Value = id;
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
        {
            throw new InvalidOperationException($"No se encontró el cliente con código {id}.");
        }
    }

    public async Task DeleteClientAsync(int id, CancellationToken cancellationToken = default)
    {
        const string query = "DELETE FROM dbo.Clientes WHERE Cod_Cliente = @Id;";
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection);
        command.Parameters.Add("@Id", System.Data.SqlDbType.Int).Value = id;
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
        {
            throw new InvalidOperationException($"No se encontró el cliente con código {id}.");
        }
    }

    public async Task AddClientAsync(
        string client,
        string rfc,
        string address,
        string neighborhood,
        string city,
        string state,
        string postalCode,
        string phone,
        string resident,
        string buyer,
        string construction,
        string email,
        string website,
        CancellationToken cancellationToken = default)
    {
        ValidateClient(new ClientDetails(
            client, rfc, address, neighborhood, city, state, postalCode, phone,
            resident, buyer, construction, email, website));
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateClientCommand(connection,
            """
            INSERT INTO dbo.Clientes
                (Cliente, RFC, Direccion, Colonia, Ciudad, Estado, CP, Telefono,
                 Residente, Comprador, Obra, email, Pagina)
            VALUES
                (@Cliente, @RFC, @Direccion, @Colonia, @Ciudad, @Estado, @CP, @Telefono,
                 @Residente, @Comprador, @Obra, @Email, @Pagina);
            """,
            client, rfc, address, neighborhood, city, state, postalCode, phone, resident, buyer, construction, email, website);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static SqlCommand CreateClientCommand(
        SqlConnection connection,
        string query,
        string client,
        string rfc,
        string address,
        string neighborhood,
        string city,
        string state,
        string postalCode,
        string phone,
        string resident,
        string buyer,
        string construction,
        string email,
        string website)
    {
        var command = new SqlCommand(query, connection);
        command.Parameters.Add("@Cliente", System.Data.SqlDbType.NVarChar, 100).Value = client.Trim();
        command.Parameters.Add("@RFC", System.Data.SqlDbType.NVarChar, 15).Value = rfc.Trim();
        command.Parameters.Add("@Direccion", System.Data.SqlDbType.NVarChar, 100).Value = address.Trim();
        command.Parameters.Add("@Colonia", System.Data.SqlDbType.NVarChar, 50).Value = neighborhood.Trim();
        command.Parameters.Add("@Ciudad", System.Data.SqlDbType.NVarChar, 50).Value = city.Trim();
        command.Parameters.Add("@Estado", System.Data.SqlDbType.NVarChar, 50).Value = state.Trim();
        command.Parameters.Add("@CP", System.Data.SqlDbType.NVarChar, 5).Value = postalCode.Trim();
        command.Parameters.Add("@Telefono", System.Data.SqlDbType.NVarChar, 50).Value = phone.Trim();
        command.Parameters.Add("@Residente", System.Data.SqlDbType.NVarChar, 60).Value = resident.Trim();
        command.Parameters.Add("@Comprador", System.Data.SqlDbType.NVarChar, 255).Value = buyer.Trim();
        command.Parameters.Add("@Obra", System.Data.SqlDbType.NVarChar, 50).Value = construction.Trim();
        command.Parameters.Add("@Email", System.Data.SqlDbType.NVarChar, 100).Value = email.Trim();
        command.Parameters.Add("@Pagina", System.Data.SqlDbType.NVarChar, 80).Value = website.Trim();
        return command;
    }

    private static void ValidateClient(ClientDetails details)
    {
        var error = ClientDataValidator.Validate(details);
        if (error is not null)
        {
            throw new InvalidOperationException(error.Message);
        }
    }

    private static string GetString(SqlDataReader reader, string name)
    {
        var value = reader[name];
        return value is DBNull ? string.Empty : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static string GetColumnTitle(string name) => name switch
    {
        "Cod_Cliente" => "Código cliente",
        "Cliente" => "Cliente",
        "Direccion" => "Dirección",
        "Colonia" => "Colonia",
        "Telefono" => "Teléfono",
        "CP" => "Código Postal",
        "Ciudad" => "Ciudad",
        "Estado" => "Estado",
        "Obra" => "Obra",
        "Residente" => "Residente",
        "Comprador" => "Comprador",
        "email" => "Email",
        "Pagina" => "WWW",
        "RFC" => "RFC",
        _ => name
    };

    private static double GetColumnWidth(string name) => name switch
    {
        "Cod_Cliente" => 140,
        "Cliente" => 190,
        "Direccion" => 180,
        "Colonia" or "Telefono" or "CP" or "Ciudad" or "Estado" => 140,
        "Obra" or "Residente" or "Comprador" => 150,
        "email" or "Pagina" => 180,
        "RFC" => 130,
        _ => 140
    };
}

public sealed class ClientCell(string title, string value, double width)
{
    public string Title { get; set; } = title;
    public string Value { get; set; } = value;
    public double Width { get; set; } = width;
}

public sealed class ClientRecord(
    int id,
    IReadOnlyList<ClientCell> cells,
    string client,
    string rfc,
    string address,
    string neighborhood,
    string city,
    string state,
    string postalCode,
    string phone,
    string resident,
    string buyer,
    string construction,
    string email,
    string website)
{
    public int Id { get; set; } = id;
    public IReadOnlyList<ClientCell> Cells { get; set; } = cells;
    public string Client { get; set; } = client;
    public string Rfc { get; set; } = rfc;
    public string Address { get; set; } = address;
    public string Neighborhood { get; set; } = neighborhood;
    public string City { get; set; } = city;
    public string State { get; set; } = state;
    public string PostalCode { get; set; } = postalCode;
    public string Phone { get; set; } = phone;
    public string Resident { get; set; } = resident;
    public string Buyer { get; set; } = buyer;
    public string Construction { get; set; } = construction;
    public string Email { get; set; } = email;
    public string Website { get; set; } = website;
}

public sealed record ClientTableData(IReadOnlyList<ClientCell> Columns, IReadOnlyList<ClientRecord> Clients);

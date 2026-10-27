using System.Globalization;
using Microsoft.Data.SqlClient;

namespace Master_WinUI.Services;

public sealed class SupplierService
{
    private readonly SqlConnectionFactory _connectionFactory = new();

    public async Task<SupplierTableData> GetSuppliersAsync(CancellationToken cancellationToken = default)
    {
        const string query = """
            SELECT Cod_Proveedor, Proveedor, Direccion, Colonia, Ciudad, Estado, CP, Telefono,
                   Residente, email, Pagina, RFC
            FROM dbo.Proveedores
            ORDER BY Cod_Proveedor;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var columns = Enumerable.Range(0, reader.FieldCount)
            .Select(index => new SupplierCell(GetColumnTitle(reader.GetName(index)), string.Empty, GetColumnWidth(reader.GetName(index))))
            .ToArray();
        var suppliers = new List<SupplierRecord>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var cells = new SupplierCell[reader.FieldCount];
            for (var index = 0; index < reader.FieldCount; index++)
            {
                var name = reader.GetName(index);
                var value = reader.IsDBNull(index)
                    ? string.Empty
                    : Convert.ToString(reader.GetValue(index), CultureInfo.InvariantCulture) ?? string.Empty;
                cells[index] = new SupplierCell(GetColumnTitle(name), value, GetColumnWidth(name));
            }

            suppliers.Add(new SupplierRecord(
                reader.GetInt32(0),
                cells,
                GetString(reader, "Proveedor"),
                GetString(reader, "RFC"),
                GetString(reader, "Direccion"),
                GetString(reader, "Colonia"),
                GetString(reader, "Ciudad"),
                GetString(reader, "Estado"),
                GetString(reader, "CP"),
                GetString(reader, "Telefono"),
                GetString(reader, "Residente"),
                GetString(reader, "email"),
                GetString(reader, "Pagina")));
        }

        return new SupplierTableData(columns, suppliers);
    }

    public async Task AddSupplierAsync(
        SupplierDetails supplier,
        CancellationToken cancellationToken = default)
    {
        ValidateSupplier(supplier);
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateSupplierCommand(connection,
            """
            INSERT INTO dbo.Proveedores
                (Proveedor, RFC, Direccion, Colonia, Ciudad, Estado, CP, Telefono, Residente, email, Pagina)
            VALUES
                (@Proveedor, @RFC, @Direccion, @Colonia, @Ciudad, @Estado, @CP, @Telefono, @Residente, @Email, @Pagina);
            """,
            supplier);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateSupplierAsync(
        int id,
        SupplierDetails supplier,
        CancellationToken cancellationToken = default)
    {
        ValidateSupplier(supplier);
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateSupplierCommand(connection,
            """
            UPDATE dbo.Proveedores
            SET Proveedor = @Proveedor, RFC = @RFC, Direccion = @Direccion, Colonia = @Colonia,
                Ciudad = @Ciudad, Estado = @Estado, CP = @CP, Telefono = @Telefono,
                Residente = @Residente, email = @Email, Pagina = @Pagina
            WHERE Cod_Proveedor = @Id;
            """,
            supplier);
        command.Parameters.Add("@Id", System.Data.SqlDbType.Int).Value = id;
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
        {
            throw new InvalidOperationException($"No se encontró el proveedor con código {id}.");
        }
    }

    public async Task DeleteSupplierAsync(int id, CancellationToken cancellationToken = default)
    {
        const string query = "DELETE FROM dbo.Proveedores WHERE Cod_Proveedor = @Id;";
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection);
        command.Parameters.Add("@Id", System.Data.SqlDbType.Int).Value = id;
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
        {
            throw new InvalidOperationException($"No se encontró el proveedor con código {id}.");
        }
    }

    private static SqlCommand CreateSupplierCommand(
        SqlConnection connection,
        string query,
        SupplierDetails supplier)
    {
        var command = new SqlCommand(query, connection);
        command.Parameters.Add("@Proveedor", System.Data.SqlDbType.NVarChar, 100).Value = supplier.Supplier.Trim();
        command.Parameters.Add("@RFC", System.Data.SqlDbType.NVarChar, 15).Value = supplier.Rfc.Trim();
        command.Parameters.Add("@Direccion", System.Data.SqlDbType.NVarChar, 100).Value = supplier.Address.Trim();
        command.Parameters.Add("@Colonia", System.Data.SqlDbType.NVarChar, 50).Value = supplier.Neighborhood.Trim();
        command.Parameters.Add("@Ciudad", System.Data.SqlDbType.NVarChar, 50).Value = supplier.City.Trim();
        command.Parameters.Add("@Estado", System.Data.SqlDbType.NVarChar, 50).Value = supplier.State.Trim();
        command.Parameters.Add("@CP", System.Data.SqlDbType.NVarChar, 5).Value = supplier.PostalCode.Trim();
        command.Parameters.Add("@Telefono", System.Data.SqlDbType.NVarChar, 50).Value = supplier.Phone.Trim();
        command.Parameters.Add("@Residente", System.Data.SqlDbType.NVarChar, 60).Value = supplier.Contact.Trim();
        command.Parameters.Add("@Email", System.Data.SqlDbType.NVarChar, 100).Value = supplier.Email.Trim();
        command.Parameters.Add("@Pagina", System.Data.SqlDbType.NVarChar, 80).Value = supplier.Website.Trim();
        return command;
    }

    private static void ValidateSupplier(SupplierDetails supplier)
    {
        var error = SupplierDataValidator.Validate(supplier);
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
        "Cod_Proveedor" => "Código de Proveedor",
        "Proveedor" => "Proveedor",
        "Direccion" => "Dirección",
        "Colonia" => "Colonia",
        "Ciudad" => "Ciudad",
        "Estado" => "Estado",
        "CP" => "Código Postal",
        "Telefono" => "Teléfono",
        "Residente" => "Residente",
        "email" => "Email",
        "Pagina" => "WWW",
        "RFC" => "RFC",
        _ => name
    };

    private static double GetColumnWidth(string name) => name switch
    {
        "Cod_Proveedor" => 140,
        "Proveedor" => 190,
        "Direccion" => 180,
        "Colonia" or "Ciudad" or "Estado" => 140,
        "CP" => 120,
        "Telefono" or "Residente" => 150,
        "email" => 190,
        "Pagina" => 150,
        "RFC" => 130,
        _ => 140
    };
}

public sealed class SupplierCell(string title, string value, double width)
{
    public string Title { get; set; } = title;
    public string Value { get; set; } = value;
    public double Width { get; set; } = width;
}

public sealed class SupplierRecord(
    int id,
    IReadOnlyList<SupplierCell> cells,
    string supplier,
    string rfc,
    string address,
    string neighborhood,
    string city,
    string state,
    string postalCode,
    string phone,
    string contact,
    string email,
    string website)
{
    public int Id { get; set; } = id;
    public IReadOnlyList<SupplierCell> Cells { get; set; } = cells;
    public string Supplier { get; set; } = supplier;
    public string Rfc { get; set; } = rfc;
    public string Address { get; set; } = address;
    public string Neighborhood { get; set; } = neighborhood;
    public string City { get; set; } = city;
    public string State { get; set; } = state;
    public string PostalCode { get; set; } = postalCode;
    public string Phone { get; set; } = phone;
    public string Contact { get; set; } = contact;
    public string Email { get; set; } = email;
    public string Website { get; set; } = website;
}

public sealed record SupplierTableData(IReadOnlyList<SupplierCell> Columns, IReadOnlyList<SupplierRecord> Suppliers);

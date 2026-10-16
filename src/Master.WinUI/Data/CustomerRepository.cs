using System.Data;
using Microsoft.Data.SqlClient;

namespace Master.WinUI.Data;

internal sealed class CustomerRepository(SqlServerConnectionFactory connectionFactory)
{
    public async Task<IReadOnlyList<Customer>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT IdCliente, Documento, Nombres, Apellidos, Telefono, Email,
                   Direccion, Ciudad, Preferencias, LimiteCredito, FechaNacimiento,
                   Activo, FechaRegistro, EsVip, TipoPersona
            FROM dbo.Clientes
            ORDER BY Apellidos, Nombres, IdCliente;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var customers = new List<Customer>();
        while (await reader.ReadAsync(cancellationToken))
        {
            customers.Add(ReadCustomer(reader));
        }

        return customers;
    }

    public async Task<int> InsertAsync(
        CustomerDraft customer,
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO dbo.Clientes
                (Documento, Nombres, Apellidos, Telefono, Email, Direccion, Ciudad,
                 Preferencias, LimiteCredito, FechaNacimiento, Activo, EsVip, TipoPersona)
            OUTPUT INSERTED.IdCliente
            VALUES
                (@Documento, @Nombres, @Apellidos, @Telefono, @Email, @Direccion,
                 @Ciudad, @Preferencias, @LimiteCredito, @FechaNacimiento, @Activo,
                 @EsVip, @TipoPersona);
            """;
        AddCustomerParameters(command, customer);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    public async Task UpdateAsync(
        int id,
        CustomerDraft customer,
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE dbo.Clientes
            SET Documento = @Documento,
                Nombres = @Nombres,
                Apellidos = @Apellidos,
                Telefono = @Telefono,
                Email = @Email,
                Direccion = @Direccion,
                Ciudad = @Ciudad,
                Preferencias = @Preferencias,
                LimiteCredito = @LimiteCredito,
                FechaNacimiento = @FechaNacimiento,
                Activo = @Activo,
                EsVip = @EsVip,
                TipoPersona = @TipoPersona
            WHERE IdCliente = @IdCliente;
            """;
        AddCustomerParameters(command, customer);
        command.Parameters.Add("@IdCliente", SqlDbType.Int).Value = id;

        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
        {
            throw new InvalidOperationException("El cliente seleccionado ya no existe.");
        }
    }

    public async Task DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM dbo.Clientes WHERE IdCliente = @IdCliente;";
        command.Parameters.Add("@IdCliente", SqlDbType.Int).Value = id;

        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
        {
            throw new InvalidOperationException("El cliente seleccionado ya no existe.");
        }
    }

    public async Task InsertManyAsync(
        IReadOnlyList<CustomerDraft> customers,
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(
            cancellationToken);

        try
        {
            foreach (var customer in customers)
            {
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = """
                    INSERT INTO dbo.Clientes
                        (Documento, Nombres, Apellidos, Telefono, Email, Direccion, Ciudad,
                         Preferencias, LimiteCredito, FechaNacimiento, Activo, EsVip, TipoPersona)
                    VALUES
                        (@Documento, @Nombres, @Apellidos, @Telefono, @Email, @Direccion,
                         @Ciudad, @Preferencias, @LimiteCredito, @FechaNacimiento, @Activo,
                         @EsVip, @TipoPersona);
                    """;
                AddCustomerParameters(command, customer);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static Customer ReadCustomer(SqlDataReader reader)
    {
        var birthDateOrdinal = reader.GetOrdinal("FechaNacimiento");
        return new Customer(
            reader.GetInt32(reader.GetOrdinal("IdCliente")),
            reader.GetString(reader.GetOrdinal("Documento")),
            reader.GetString(reader.GetOrdinal("Nombres")),
            reader.GetString(reader.GetOrdinal("Apellidos")),
            ReadNullableString(reader, "Telefono"),
            ReadNullableString(reader, "Email"),
            ReadNullableString(reader, "Direccion"),
            reader.GetString(reader.GetOrdinal("Ciudad")),
            ReadNullableString(reader, "Preferencias"),
            reader.GetDecimal(reader.GetOrdinal("LimiteCredito")),
            reader.IsDBNull(birthDateOrdinal) ? null : reader.GetDateTime(birthDateOrdinal),
            reader.GetBoolean(reader.GetOrdinal("Activo")),
            reader.GetDateTime(reader.GetOrdinal("FechaRegistro")),
            ReadNullableBoolean(reader, "EsVip"),
            ReadPersonType(reader));
    }

    private static string? ReadNullableString(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static bool? ReadNullableBoolean(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetBoolean(ordinal);
    }

    private static CustomerPersonType? ReadPersonType(SqlDataReader reader)
    {
        var ordinal = reader.GetOrdinal("TipoPersona");
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        var value = reader.GetString(ordinal);
        if (string.Equals(value, "Fisica", StringComparison.OrdinalIgnoreCase))
        {
            return CustomerPersonType.Physical;
        }

        if (string.Equals(value, "Juridica", StringComparison.OrdinalIgnoreCase))
        {
            return CustomerPersonType.Legal;
        }

        throw new InvalidDataException(
            $"dbo.Clientes.TipoPersona contiene un valor no reconocido: {value}.");
    }

    private static void AddCustomerParameters(SqlCommand command, CustomerDraft customer)
    {
        command.Parameters.Add("@Documento", SqlDbType.NVarChar, 20).Value = customer.Document;
        command.Parameters.Add("@Nombres", SqlDbType.NVarChar, 100).Value = customer.FirstName;
        command.Parameters.Add("@Apellidos", SqlDbType.NVarChar, 100).Value = customer.LastName;
        AddNullableString(command, "@Telefono", 30, customer.Phone);
        AddNullableString(command, "@Email", 100, customer.Email);
        AddNullableString(command, "@Direccion", 200, customer.Address);
        command.Parameters.Add("@Ciudad", SqlDbType.NVarChar, 80).Value = customer.City;
        AddNullableString(command, "@Preferencias", 250, customer.Preferences);
        command.Parameters.Add("@LimiteCredito", SqlDbType.Decimal).Value = customer.CreditLimit;
        command.Parameters["@LimiteCredito"].Precision = 18;
        command.Parameters["@LimiteCredito"].Scale = 2;
        command.Parameters.Add("@FechaNacimiento", SqlDbType.Date).Value =
            customer.BirthDate is null ? DBNull.Value : customer.BirthDate.Value.Date;
        command.Parameters.Add("@Activo", SqlDbType.Bit).Value = customer.IsActive;
        command.Parameters.Add("@EsVip", SqlDbType.Bit).Value =
            customer.IsVip is null ? DBNull.Value : customer.IsVip.Value;
        command.Parameters.Add("@TipoPersona", SqlDbType.NVarChar, 20).Value =
            customer.PersonType switch
            {
                CustomerPersonType.Physical => "Fisica",
                CustomerPersonType.Legal => "Juridica",
                null => DBNull.Value,
                _ => throw new ArgumentOutOfRangeException(nameof(customer))
            };
    }

    private static void AddNullableString(
        SqlCommand command,
        string parameterName,
        int size,
        string? value)
    {
        command.Parameters.Add(parameterName, SqlDbType.NVarChar, size).Value =
            string.IsNullOrEmpty(value) ? DBNull.Value : value;
    }
}

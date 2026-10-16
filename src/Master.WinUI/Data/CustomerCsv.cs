using System.Globalization;
using System.Text;
using Microsoft.VisualBasic.FileIO;

namespace Master.WinUI.Data;

internal static class CustomerCsv
{
    private static readonly string[] LegacyHeaders =
    [
        "Documento", "Nombres", "Apellidos", "Telefono", "Email", "Direccion",
        "Ciudad", "Preferencias", "LimiteCredito", "FechaNacimiento", "Activo"
    ];
    private static readonly string[] Headers =
    [
        .. LegacyHeaders,
        "EsVip", "TipoPersona"
    ];

    public static string Export(IEnumerable<Customer> customers)
    {
        var csv = new StringBuilder();
        AppendRow(csv, Headers);

        foreach (var customer in customers)
        {
            AppendRow(csv,
            [
                customer.Document,
                customer.FirstName,
                customer.LastName,
                customer.Phone ?? string.Empty,
                customer.Email ?? string.Empty,
                customer.Address ?? string.Empty,
                customer.City,
                customer.Preferences ?? string.Empty,
                customer.CreditLimit.ToString("0.00", CultureInfo.InvariantCulture),
                customer.BirthDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
                customer.IsActive ? "true" : "false",
                customer.IsVip?.ToString().ToLowerInvariant() ?? string.Empty,
                customer.PersonType switch
                {
                    CustomerPersonType.Physical => "Fisica",
                    CustomerPersonType.Legal => "Juridica",
                    null => string.Empty,
                    _ => throw new ArgumentOutOfRangeException(nameof(customers))
                }
            ]);
        }

        return csv.ToString();
    }

    public static IReadOnlyList<CustomerDraft> Import(string content)
    {
        using var reader = new StringReader(content);
        using var parser = new TextFieldParser(reader)
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = false
        };
        parser.SetDelimiters(",");

        if (parser.EndOfData)
        {
            throw new InvalidDataException("El archivo CSV está vacío.");
        }

        var headers = parser.ReadFields();
        var isLegacyFormat = headers is not null &&
            headers.SequenceEqual(LegacyHeaders, StringComparer.OrdinalIgnoreCase);
        if (headers is null ||
            (!isLegacyFormat && !headers.SequenceEqual(Headers, StringComparer.OrdinalIgnoreCase)))
        {
            throw new InvalidDataException(
                "El archivo no usa el formato de exportación de Clientes de esta aplicación.");
        }

        var customers = new List<CustomerDraft>();
        var rowNumber = 1;
        while (!parser.EndOfData)
        {
            rowNumber++;
            var fields = parser.ReadFields();
            var expectedFieldCount = isLegacyFormat ? LegacyHeaders.Length : Headers.Length;
            if (fields is null || fields.Length != expectedFieldCount)
            {
                throw new InvalidDataException(
                    $"La fila {rowNumber} no tiene las {expectedFieldCount} columnas requeridas.");
            }

            if (!decimal.TryParse(
                    fields[8],
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out var creditLimit))
            {
                throw new InvalidDataException($"El límite de crédito de la fila {rowNumber} no es válido.");
            }

            DateTime? birthDate = null;
            if (!string.IsNullOrWhiteSpace(fields[9]))
            {
                if (!DateTime.TryParseExact(
                        fields[9],
                        "yyyy-MM-dd",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var parsedDate))
                {
                    throw new InvalidDataException($"La fecha de la fila {rowNumber} debe usar AAAA-MM-DD.");
                }

                birthDate = parsedDate;
            }

            if (!bool.TryParse(fields[10], out var isActive))
            {
                throw new InvalidDataException($"El estado activo de la fila {rowNumber} debe ser true o false.");
            }

            bool? isVip = null;
            CustomerPersonType? personType = null;
            if (!isLegacyFormat)
            {
                if (!string.IsNullOrWhiteSpace(fields[11]))
                {
                    if (!bool.TryParse(fields[11], out var parsedIsVip))
                    {
                        throw new InvalidDataException(
                            $"El valor VIP de la fila {rowNumber} debe ser true, false o vacío.");
                    }

                    isVip = parsedIsVip;
                }

                personType = ParsePersonType(fields[12], rowNumber);
            }

            customers.Add(new CustomerDraft(
                fields[0],
                fields[1],
                fields[2],
                EmptyToNull(fields[3]),
                EmptyToNull(fields[4]),
                EmptyToNull(fields[5]),
                fields[6],
                EmptyToNull(fields[7]),
                creditLimit,
                birthDate,
                isActive,
                isVip,
                personType));
        }

        return customers;
    }

    private static void AppendRow(StringBuilder csv, IEnumerable<string> fields)
    {
        csv.AppendLine(string.Join(",", fields.Select(Escape)));
    }

    private static string Escape(string value) =>
        $"\"{value.Replace("\"", "\"\"")}\"";

    private static string? EmptyToNull(string value) =>
        string.IsNullOrEmpty(value) ? null : value;

    private static CustomerPersonType? ParsePersonType(string value, int rowNumber)
    {
        var normalizedValue = value.Trim();
        if (string.IsNullOrEmpty(normalizedValue))
        {
            return null;
        }

        if (string.Equals(normalizedValue, "Fisica", StringComparison.OrdinalIgnoreCase))
        {
            return CustomerPersonType.Physical;
        }

        if (string.Equals(normalizedValue, "Juridica", StringComparison.OrdinalIgnoreCase))
        {
            return CustomerPersonType.Legal;
        }

        throw new InvalidDataException(
            $"El tipo de persona de la fila {rowNumber} debe ser Fisica, Juridica o vacío.");
    }
}

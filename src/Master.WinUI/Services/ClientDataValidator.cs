namespace Master_WinUI.Services;

public enum ClientField
{
    Client,
    Rfc,
    Address,
    Neighborhood,
    City,
    State,
    PostalCode,
    Phone,
    Resident,
    Buyer,
    Construction,
    Email,
    Website
}

public sealed record ClientDetails(
    string Client,
    string Rfc,
    string Address,
    string Neighborhood,
    string City,
    string State,
    string PostalCode,
    string Phone,
    string Resident,
    string Buyer,
    string Construction,
    string Email,
    string Website);

public sealed record ClientValidationError(ClientField Field, string Message);

public static class ClientDataValidator
{
    private static readonly (ClientField Field, string Label, int MaxLength)[] RequiredFields =
    [
        (ClientField.Client, "La razón social", 100),
        (ClientField.Rfc, "El RFC", 15),
        (ClientField.Address, "La dirección", 100),
        (ClientField.Neighborhood, "La colonia", 50),
        (ClientField.City, "La ciudad", 50),
        (ClientField.State, "El estado", 50),
        (ClientField.PostalCode, "El código postal", 5),
        (ClientField.Phone, "El teléfono", 50),
        (ClientField.Resident, "El residente", 60),
        (ClientField.Buyer, "El comprador", 255),
        (ClientField.Construction, "La obra", 50),
        (ClientField.Email, "El email", 100),
        (ClientField.Website, "La página web", 80)
    ];

    public static ClientValidationError? Validate(ClientDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);

        foreach (var (field, label, maxLength) in RequiredFields)
        {
            var value = GetValue(details, field);
            if (string.IsNullOrWhiteSpace(value))
            {
                return new ClientValidationError(field, $"{label} es obligatorio.");
            }

            if (value.Trim().Length > maxLength)
            {
                return new ClientValidationError(field, $"{label} no puede exceder {maxLength} caracteres.");
            }
        }

        if (!SupplierDataValidator.IsValidRfc(details.Rfc.Trim()))
        {
            return new ClientValidationError(
                ClientField.Rfc,
                "El RFC debe tener formato válido de persona física (13 caracteres) o moral (12 caracteres).");
        }

        var postalCode = details.PostalCode.Trim();
        if (postalCode.Length != 5 || !postalCode.All(char.IsAsciiDigit))
        {
            return new ClientValidationError(ClientField.PostalCode, "El código postal debe contener exactamente 5 dígitos.");
        }

        if (!SupplierDataValidator.IsValidPhone(details.Phone.Trim()))
        {
            return new ClientValidationError(
                ClientField.Phone,
                "El teléfono debe contener entre 7 y 15 dígitos; puedes usar +, espacios, guiones, puntos y paréntesis.");
        }

        if (!SupplierDataValidator.IsValidEmail(details.Email.Trim()))
        {
            return new ClientValidationError(ClientField.Email, "Escribe un correo válido, por ejemplo nombre@dominio.com.");
        }

        if (!SupplierDataValidator.IsValidWebsite(details.Website.Trim()))
        {
            return new ClientValidationError(
                ClientField.Website,
                "Escribe una dirección web válida, por ejemplo www.dominio.com o https://dominio.com.");
        }

        return null;
    }

    private static string GetValue(ClientDetails details, ClientField field) => field switch
    {
        ClientField.Client => details.Client,
        ClientField.Rfc => details.Rfc,
        ClientField.Address => details.Address,
        ClientField.Neighborhood => details.Neighborhood,
        ClientField.City => details.City,
        ClientField.State => details.State,
        ClientField.PostalCode => details.PostalCode,
        ClientField.Phone => details.Phone,
        ClientField.Resident => details.Resident,
        ClientField.Buyer => details.Buyer,
        ClientField.Construction => details.Construction,
        ClientField.Email => details.Email,
        ClientField.Website => details.Website,
        _ => throw new ArgumentOutOfRangeException(nameof(field), field, null)
    };
}

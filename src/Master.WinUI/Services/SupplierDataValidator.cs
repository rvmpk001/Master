using System.Net.Mail;
using System.Text.RegularExpressions;

namespace Master_WinUI.Services;

public enum SupplierField
{
    Supplier,
    Rfc,
    Address,
    Neighborhood,
    City,
    State,
    PostalCode,
    Phone,
    Contact,
    Email,
    Website
}

public sealed record SupplierDetails(
    string Supplier,
    string Rfc,
    string Address,
    string Neighborhood,
    string City,
    string State,
    string PostalCode,
    string Phone,
    string Contact,
    string Email,
    string Website);

public sealed record SupplierValidationError(SupplierField Field, string Message);

public static partial class SupplierDataValidator
{
    private static readonly (SupplierField Field, string Label, int MaxLength)[] RequiredFields =
    [
        (SupplierField.Supplier, "La razón social", 100),
        (SupplierField.Rfc, "El RFC", 15),
        (SupplierField.Address, "La dirección", 100),
        (SupplierField.Neighborhood, "La colonia", 50),
        (SupplierField.City, "La ciudad", 50),
        (SupplierField.State, "El estado", 50),
        (SupplierField.PostalCode, "El código postal", 5),
        (SupplierField.Phone, "El teléfono", 50),
        (SupplierField.Contact, "El contacto", 60),
        (SupplierField.Email, "El email", 100),
        (SupplierField.Website, "La página web", 80)
    ];

    public static SupplierValidationError? Validate(SupplierDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);

        foreach (var (field, label, maxLength) in RequiredFields)
        {
            var value = GetValue(details, field);
            if (string.IsNullOrWhiteSpace(value))
            {
                return new SupplierValidationError(field, $"{label} es obligatoria.");
            }

            if (value.Trim().Length > maxLength)
            {
                return new SupplierValidationError(field, $"{label} no puede exceder {maxLength} caracteres.");
            }
        }

        if (!IsValidRfc(details.Rfc.Trim()))
        {
            return new SupplierValidationError(
                SupplierField.Rfc,
                "El RFC debe tener formato válido de persona física (13 caracteres) o moral (12 caracteres).");
        }

        var postalCode = details.PostalCode.Trim();
        if (postalCode.Length != 5 || !postalCode.All(char.IsAsciiDigit))
        {
            return new SupplierValidationError(SupplierField.PostalCode, "El código postal debe contener exactamente 5 dígitos.");
        }

        var phone = details.Phone.Trim();
        if (!IsValidPhone(phone))
        {
            return new SupplierValidationError(
                SupplierField.Phone,
                "El teléfono debe contener entre 7 y 15 dígitos; puedes usar +, espacios, guiones, puntos y paréntesis.");
        }

        var email = details.Email.Trim();
        if (!IsValidEmail(email))
        {
            return new SupplierValidationError(SupplierField.Email, "Escribe un correo válido, por ejemplo nombre@dominio.com.");
        }

        var website = details.Website.Trim();
        if (!IsValidWebsite(website))
        {
            return new SupplierValidationError(
                SupplierField.Website,
                "Escribe una dirección web válida, por ejemplo www.dominio.com o https://dominio.com.");
        }

        return null;
    }

    private static string GetValue(SupplierDetails details, SupplierField field) => field switch
    {
        SupplierField.Supplier => details.Supplier,
        SupplierField.Rfc => details.Rfc,
        SupplierField.Address => details.Address,
        SupplierField.Neighborhood => details.Neighborhood,
        SupplierField.City => details.City,
        SupplierField.State => details.State,
        SupplierField.PostalCode => details.PostalCode,
        SupplierField.Phone => details.Phone,
        SupplierField.Contact => details.Contact,
        SupplierField.Email => details.Email,
        SupplierField.Website => details.Website,
        _ => throw new ArgumentOutOfRangeException(nameof(field), field, null)
    };

    public static bool IsValidPhone(string phone)
    {
        var digitCount = 0;
        var plusSeen = false;
        for (var index = 0; index < phone.Length; index++)
        {
            var character = phone[index];
            if (char.IsDigit(character))
            {
                digitCount++;
                continue;
            }

            if (character == '+')
            {
                if (plusSeen || phone[..index].Any(previous => !char.IsWhiteSpace(previous)))
                {
                    return false;
                }

                plusSeen = true;
                continue;
            }

            if (character is '-' or '(' or ')' or '.' || char.IsWhiteSpace(character))
            {
                continue;
            }

            return false;
        }

        return digitCount is >= 7 and <= 15;
    }

    public static bool IsValidRfc(string rfc) => RfcRegex().IsMatch(rfc);

    public static bool IsValidEmail(string email)
    {
        try
        {
            var address = new MailAddress(email);
            var domain = address.Host;
            return string.Equals(address.Address, email, StringComparison.OrdinalIgnoreCase)
                && domain.Contains(".", StringComparison.Ordinal)
                && !domain.StartsWith(".", StringComparison.Ordinal)
                && !domain.EndsWith(".", StringComparison.Ordinal);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static bool IsValidWebsite(string website)
    {
        var address = website.Contains("://", StringComparison.Ordinal)
            ? website
            : $"https://{website}";
        return Uri.TryCreate(address, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && string.IsNullOrEmpty(uri.UserInfo)
            && uri.Host.Contains(".", StringComparison.Ordinal)
            && !uri.Host.StartsWith(".", StringComparison.Ordinal)
            && !uri.Host.EndsWith(".", StringComparison.Ordinal);
    }

    [GeneratedRegex(@"^[A-ZÑ&]{3,4}\d{6}[A-Z0-9]{3}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RfcRegex();
}

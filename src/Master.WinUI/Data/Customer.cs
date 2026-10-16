namespace Master.WinUI.Data;

internal enum CustomerPersonType
{
    Physical,
    Legal
}

internal sealed record Customer(
    int Id,
    string Document,
    string FirstName,
    string LastName,
    string? Phone,
    string? Email,
    string? Address,
    string City,
    string? Preferences,
    decimal CreditLimit,
    DateTime? BirthDate,
    bool IsActive,
    DateTime RegisteredAt,
    bool? IsVip,
    CustomerPersonType? PersonType)
{
    public string FullName => $"{FirstName} {LastName}";
}

internal sealed record CustomerDraft(
    string Document,
    string FirstName,
    string LastName,
    string? Phone,
    string? Email,
    string? Address,
    string City,
    string? Preferences,
    decimal CreditLimit,
    DateTime? BirthDate,
    bool IsActive,
    bool? IsVip,
    CustomerPersonType? PersonType);

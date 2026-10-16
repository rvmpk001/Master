namespace Master.WinUI.Data;

internal sealed record ProductCategory(int Id, string Name)
{
    public override string ToString() => Name;
}

internal sealed record Product(
    int Id,
    int CategoryId,
    string CategoryName,
    string Code,
    string Name,
    string? Description,
    decimal UnitPrice,
    int Stock,
    string? ImageBase64,
    bool IsActive,
    DateTime CreatedAt);

internal sealed record ProductDraft(
    int CategoryId,
    string Code,
    string Name,
    string? Description,
    decimal UnitPrice,
    int Stock,
    string? ImageBase64,
    bool IsActive);

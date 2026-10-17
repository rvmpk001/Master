namespace Master.WinUI.Data;

internal sealed record SaleCustomer(int Id, string FirstName, string LastName)
{
    public string DisplayName => $"{FirstName} {LastName}".Trim();
}

internal sealed record SaleProduct(
    int Id,
    string Code,
    string Name,
    decimal UnitPrice,
    int Stock,
    bool IsActive)
{
    public string DisplayName => $"{Code} · {Name}";
}

internal sealed record SalesCatalog(
    IReadOnlyList<SaleCustomer> Customers,
    IReadOnlyList<SaleProduct> Products,
    IReadOnlyList<string> PaymentMethods,
    decimal TaxRate,
    string CurrencyCode);

internal sealed record SaleLineDraft(int ProductId, int Quantity, decimal ExpectedUnitPrice);

internal sealed record SaleDraft(
    int CustomerId,
    int UserId,
    DateTime SaleDate,
    string PaymentMethod,
    string? Notes,
    decimal ExpectedTaxRate,
    IReadOnlyList<SaleLineDraft> Lines);

internal sealed record SaleReceiptLine(
    string ProductCode,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal Subtotal);

internal sealed record SaleReceipt(
    string InvoiceNumber,
    DateTime SaleDate,
    string CustomerName,
    string PaymentMethod,
    string CurrencyCode,
    decimal TaxRate,
    decimal Subtotal,
    decimal Tax,
    decimal Total,
    IReadOnlyList<SaleReceiptLine> Lines);

internal sealed record SaleCartLine(
    int ProductId,
    string ProductCode,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal Subtotal,
    string CurrencyCode)
{
    public string UnitPriceText => $"{CurrencyCode} {UnitPrice:N2}";

    public string SubtotalText => $"{CurrencyCode} {Subtotal:N2}";
}

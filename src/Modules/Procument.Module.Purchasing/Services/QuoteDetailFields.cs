namespace Procument.Module.Purchasing.Services;

/// <summary>
/// Serial Number, Trace To, Tag Type, Price Type, Tagged By, MoQ and Warranty — optional free-text
/// details on a supplier quote (SupplierPartQuote) and an email quote (PendingSupplierQuote).
/// Max lengths match AppDbContext.
/// </summary>
internal static class QuoteDetailFields
{
    public const int SerialNumberLength = 200;
    public const int TraceToLength = 300;
    public const int TagTypeLength = 100;
    public const int PriceTypeLength = 100;
    public const int TaggedByLength = 300;
    public const int MoQLength = 100;
    public const int WarrantyLength = 200;
    public const int CRLength = 200;

    /// <summary>Trimmed, truncated to the column, null when blank.</summary>
    public static string? Clean(string? value, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;
        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }
}

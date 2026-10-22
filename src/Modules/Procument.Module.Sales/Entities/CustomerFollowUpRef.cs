using Procument.Shared.Entities;

namespace Procument.Module.Sales.Entities;

/// <summary>
/// An "Other Refs" link of a follow up row: a purchase order, quote, proforma invoice or final invoice.
/// RefId is a plain column (no FK) — the documents live in other tables and modules.
/// </summary>
public class CustomerFollowUpRef : BaseEntity
{
    public long CustomerFollowUpId { get; set; }
    /// <summary>One of <see cref="CustomerFollowUpRefTypes"/>.</summary>
    public string RefType { get; set; } = string.Empty;
    public long RefId { get; set; }
    public string? AddedBy { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    public CustomerFollowUp CustomerFollowUp { get; set; } = null!;
}

public static class CustomerFollowUpRefTypes
{
    public const string PurchaseOrder = "PurchaseOrder";
    public const string Quote = "Quote";
    public const string Invoice = "Invoice";
    public const string FinalInvoice = "FinalInvoice";

    public static readonly string[] All = [PurchaseOrder, Quote, Invoice, FinalInvoice];

    /// <summary>The canonical name for a type, matched case-insensitively; null when unknown.</summary>
    public static string? Normalize(string? type) =>
        All.FirstOrDefault(t => string.Equals(t, type?.Trim(), StringComparison.OrdinalIgnoreCase));
}

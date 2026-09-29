using Procument.Module.Identity.Entities;

namespace Procument.Module.Sales.Entities;

/// <summary>
/// A file proving a supplier credit (the supplier's PI, the credit memo, e-mails…). Documents are
/// evidence only — adding one never changes a balance, so they may be attached after the entry.
/// </summary>
public class SupplierCreditDocument
{
    public long Id { get; set; }
    public long EntryId { get; set; }
    /// <summary>See <see cref="SupplierCreditDocumentCategories"/>.</summary>
    public string Category { get; set; } = SupplierCreditDocumentCategories.Other;
    /// <summary>Name on disk (unique, prefixed); OriginalFileName is what the user uploaded.</summary>
    public string FileName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public long? UploadedByUserId { get; set; }

    public SupplierCreditEntry Entry { get; set; } = null!;
    public User? UploadedBy { get; set; }
}

public static class SupplierCreditDocumentCategories
{
    public const string SupplierPI = "SupplierPI";
    public const string CreditMemo = "CreditMemo";
    public const string Other = "Other";
    public static readonly string[] All = [SupplierPI, CreditMemo, Other];
}

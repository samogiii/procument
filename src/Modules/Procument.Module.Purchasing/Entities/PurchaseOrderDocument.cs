using Procument.Module.Identity.Entities;
using Procument.Shared.Entities;

namespace Procument.Module.Purchasing.Entities;

public class PurchaseOrderDocument : BaseEntity
{
    public long POId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string StoredName { get; set; } = string.Empty;
    public string? DocumentNumber { get; set; }
    public decimal? Amount { get; set; }
    public long UploadedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public PurchaseOrder PurchaseOrder { get; set; } = null!;
    public User UploadedByUser { get; set; } = null!;
}

public static class PurchaseOrderDocumentCategories
{
    public const string SupplierPI = "SupplierPI";
    public const string SupplierInvoice = "SupplierInvoice";
    public const string Certificate = "Certificate";
    public const string PackingList = "PackingList";
    public const string Other = "Other";
}

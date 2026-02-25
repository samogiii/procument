using Procument.Module.Catalog.Entities;
using Procument.Shared.Entities;

namespace Procument.Module.Purchasing.Entities;

public class PurchaseOrder : BaseEntity
{
    public string PONumber { get; set; } = string.Empty;
    public decimal? TotalAmount { get; set; }
    public string Status { get; set; } = "Draft";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? RejectionNote { get; set; }

    // Foreign keys
    public long SupplierId { get; set; }
    public long? InvoiceId { get; set; }

    // Navigation
    public Supplier Supplier { get; set; } = null!;
    public ICollection<POItem> POItems { get; set; } = new List<POItem>();
    public POImportDetail? ImportDetail { get; set; }
}

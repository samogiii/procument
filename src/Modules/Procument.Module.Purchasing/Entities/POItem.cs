using Procument.Module.Catalog.Entities;
using Procument.Shared.Entities;

namespace Procument.Module.Purchasing.Entities;

public class POItem : BaseEntity
{
    public int Qty { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public string? Condition { get; set; }
    public long? SupplierId { get; set; }

    // Foreign keys
    public long? POId { get; set; }
    public long? ProcumentId { get; set; }
    public long? PartNumberId { get; set; }
    public long? InvoiceItemId { get; set; }

    // Navigation
    public PurchaseOrder? PurchaseOrder { get; set; }
    public ProcumentRecord? ProcumentRecord { get; set; }
    public PartNumber? PartNumber { get; set; }
}

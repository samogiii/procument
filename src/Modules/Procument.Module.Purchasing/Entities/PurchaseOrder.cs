using Procument.Module.Catalog.Entities;
using Procument.Module.RFQ.Entities;
using Procument.Shared.Entities;

namespace Procument.Module.Purchasing.Entities;

public class PurchaseOrder : BaseEntity
{
    public string PONumber { get; set; } = string.Empty;
    public decimal? TotalAmount { get; set; }
    public string Status { get; set; } = "Draft";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Foreign keys
    public long SupplierId { get; set; }
    public long? RFQId { get; set; }

    // Navigation
    public Supplier Supplier { get; set; } = null!;
    public RFQHeader? RFQ { get; set; }
    public ICollection<POItem> POItems { get; set; } = new List<POItem>();
}

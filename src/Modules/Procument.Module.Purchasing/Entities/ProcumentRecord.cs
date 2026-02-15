using Procument.Module.Catalog.Entities;
using Procument.Module.RFQ.Entities;
using Procument.Shared.Entities;

namespace Procument.Module.Purchasing.Entities;

public class ProcumentRecord : BaseEntity
{
    public string? Alt { get; set; }
    public decimal Price { get; set; }
    public int Qty { get; set; }
    public string? Condition { get; set; }

    // Foreign keys
    public long RFQItemId { get; set; }
    public long SupplierId { get; set; }

    // Navigation
    public RFQItem RFQItem { get; set; } = null!;
    public Supplier Supplier { get; set; } = null!;
    public ICollection<POItem> POItems { get; set; } = new List<POItem>();
}

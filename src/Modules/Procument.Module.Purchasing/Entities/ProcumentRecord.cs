using Procument.Module.Catalog.Entities;
using Procument.Module.RFQ.Entities;
using Procument.Module.Identity.Entities;
using Procument.Shared.Entities;

namespace Procument.Module.Purchasing.Entities;

public class ProcumentRecord : BaseEntity
{
    public string? Alt { get; set; }
    public decimal Price { get; set; }
    public double Qty { get; set; }
    public string? Condition { get; set; }
    public string? Unit { get; set; }
    public string? LeadTime { get; set; }
    public double? Coef_1 { get; set; }
    public double? Coef_2 { get; set; }
    public double? Coef_3 { get; set; }
    public double? ShippingCost { get; set; }
    public string? ShippingPoint { get; set; }
    public string? CertName { get; set; }
    public double? UnitPrice { get; set; }
    public double? TotalPrice { get; set; }
    public DateOnly? TagDate { get; set; }
    public string? Note { get; set; }
    public string? MyNotes { get; set; }

    // Foreign keys
    public long RFQItemId { get; set; }
    public long SupplierId { get; set; }
    public long? UserId { get; set; }

    // Navigation
    public RFQItem RFQItem { get; set; } = null!;
    public Supplier Supplier { get; set; } = null!;
    public User? User { get; set; }
    public ICollection<POItem> POItems { get; set; } = new List<POItem>();
}

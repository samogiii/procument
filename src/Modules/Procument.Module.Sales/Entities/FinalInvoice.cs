using Procument.Module.Catalog.Entities;
using Procument.Shared.Entities;

namespace Procument.Module.Sales.Entities;

public class FinalInvoice : BaseEntity
{
    public string InvoiceNumber { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "Draft";
    public string? ShippingMethod { get; set; }
    public decimal? ShippingCost { get; set; }
    public string? Notes { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? PaidDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Foreign keys
    public long ProformaInvoiceId { get; set; }
    public long CustomerId { get; set; }

    // Navigation
    public Invoice ProformaInvoice { get; set; } = null!;
    public Customer Customer { get; set; } = null!;
    public ICollection<FinalInvoiceItem> Items { get; set; } = new List<FinalInvoiceItem>();
}

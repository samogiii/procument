using Procument.Module.Catalog.Entities;
using Procument.Shared.Entities;

namespace Procument.Module.Sales.Entities;

public class Invoice : BaseEntity
{
    public string InvoiceNumber { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "Draft";
    public string? PaymentStatus { get; set; }      // Net30 | CAD | Prepayment
    public decimal? PrepaymentPercent { get; set; } // only set when PaymentStatus = "Prepayment"
    public DateTime? DueDate { get; set; }
    public DateTime? PaidDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CustomerPONumber { get; set; }
    public string? Subject { get; set; }

    // Foreign keys
    public long QuoteId { get; set; }
    public long CustomerId { get; set; }

    // Navigation
    public Quote Quote { get; set; } = null!;
    public Customer Customer { get; set; } = null!;
    public ICollection<InvoiceItem> InvoiceItems { get; set; } = new List<InvoiceItem>();
}

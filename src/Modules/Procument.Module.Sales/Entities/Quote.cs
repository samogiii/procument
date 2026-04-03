using Procument.Module.Catalog.Entities;
using Procument.Module.Identity.Entities;
using Procument.Module.RFQ.Entities;
using Procument.Module.Sales.Enums;
using Procument.Shared.Entities;

namespace Procument.Module.Sales.Entities;

public class Quote : BaseEntity
{
    public string QuoteNumber { get; set; } = string.Empty;
    public decimal? TotalAmount { get; set; }
    public string Status { get; set; } = "Draft";
    public DateTime? ValidUntil { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ModifyAt { get; set; }
    public int? Type { get; set; }
    public string? TypeAdditional { get; set; }
    public string? RejectionNote { get; set; }
    public decimal? FinalPrice { get; set; }
    public DateTime? SentAt { get; set; }

    // Foreign keys
    public long RFQId { get; set; }
    public long CustomerId { get; set; }
    public long UserId { get; set; }

    // Navigation
    public RFQHeader RFQ { get; set; } = null!;
    public Customer Customer { get; set; } = null!;
    public User User { get; set; } = null!;
    public ICollection<QuoteItem> QuoteItems { get; set; } = new List<QuoteItem>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}

using Procument.Module.Catalog.Entities;
using Procument.Module.Identity.Entities;
using Procument.Shared.Entities;

namespace Procument.Module.RFQ.Entities;

public class RFQHeader : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "Open";
    public DateTime LeadTime { get; set; }
    public DateTime ReceivedDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ModifyAt { get; set; }
    public string? Notes { get; set; }
    public string? NoQuoteReason { get; set; }
    public string? RejectionNote { get; set; }
    public int? ExType { get; set; }

    // Foreign keys
    public long CustomerId { get; set; }
    public long? UserId { get; set; }

    // Navigation
    public Customer Customer { get; set; } = null!;
    public User? User { get; set; }
    public ICollection<RFQItem> RFQItems { get; set; } = new List<RFQItem>();
}

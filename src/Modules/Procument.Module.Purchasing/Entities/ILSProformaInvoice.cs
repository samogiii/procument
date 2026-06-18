using Procument.Shared.Entities;

namespace Procument.Module.Purchasing.Entities;

/// <summary>
/// An ILS Proforma Invoice (PI) created from one or more accepted ILS quotes
/// of a single customer. Lines snapshot the chosen quote items.
/// </summary>
public class ILSProformaInvoice : BaseEntity
{
    public string PINumber { get; set; } = string.Empty;
    public string Status { get; set; } = "Open";
    public long ILSCustomerId { get; set; }
    public string? BillTo { get; set; }
    public string? ShipTo { get; set; }
    public string? Subject { get; set; }
    public string? CustomerPONumber { get; set; }
    public string? Notes { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ILSCustomer ILSCustomer { get; set; } = null!;
    public ICollection<ILSProformaInvoiceItem> Items { get; set; } = new List<ILSProformaInvoiceItem>();
}

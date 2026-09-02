using Procument.Module.Catalog.Entities;

using Procument.Shared.Entities;



namespace Procument.Module.Sales.Entities;



public class FinalInvoice : BaseEntity

{

    public string InvoiceNumber { get; set; } = string.Empty;

    /// <summary>
    /// Base 1 final invoice number, inherited from the source proforma's B1 number with the
    /// leading letter swapped to 'I' — e.g. proforma "P101-60701-10" produces "I101-60701-10".
    /// Null when the source proforma has none (any base other than 1).
    /// Surfaced to the client as B1InvoiceNumber, the front-end's name for it.
    /// </summary>
    public string? B1FinalInvoiceNumber { get; set; }

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


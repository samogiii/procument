using Procument.Module.Catalog.Entities;
using Procument.Shared.Entities;

namespace Procument.Module.Sales.Entities;

public class Invoice : BaseEntity
{
    public string InvoiceNumber { get; set; } = string.Empty;
    /// <summary>
    /// Base 1 invoice number, inherited from the source quote's B1 number with the leading
    /// letter swapped to 'P' — e.g. quote "Q101-60701-10" produces "P101-60701-10".
    /// Null when the source quote has none (any base other than 1).
    /// Surfaced to the client as B1ProformaInvoiceNumber, the front-end's name for it.
    /// </summary>
    public string? B1InvoiceNumber { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "Draft";
    public string? PaymentStatus { get; set; }      // Prepayment | CAD | Net | Credit
    public int? PaymentTermDays { get; set; }
    public DateTime? PaymentTermStartedAt { get; set; }
    public decimal? PrepaymentPercent { get; set; } // only set when PaymentStatus = "Prepayment"
    public DateTime? DueDate { get; set; }
    public DateTime? DeadlineDate { get; set; }
    public DateTime? PaidDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CustomerPONumber { get; set; }
    public DateTime? CustomerPODate { get; set; }
    public string? Subject { get; set; }

    // ─── Cost adjustments (rendered in the PI PDF totals block) ───
    public decimal? Tax { get; set; }
    public decimal? Shipping { get; set; }
    public decimal? ProcessingFee { get; set; }

    // Soft delete / cancellation
    public bool IsCancelled { get; set; }
    public DateTime? CancelledAt { get; set; }

    // Foreign keys
    public long QuoteId { get; set; }
    public long CustomerId { get; set; }
    public long? DefaultDepositWalletId { get; set; }
    public long? DefaultBankAccountId { get; set; }

    // Navigation
    public Quote Quote { get; set; } = null!;
    public Customer Customer { get; set; } = null!;
    public ICollection<InvoiceItem> InvoiceItems { get; set; } = new List<InvoiceItem>();
}

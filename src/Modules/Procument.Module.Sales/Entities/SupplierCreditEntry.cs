using Procument.Module.Identity.Entities;
using Procument.Module.Purchasing.Entities;

namespace Procument.Module.Sales.Entities;

/// <summary>
/// One append-only line of a supplier credit account. Entries are never edited or deleted;
/// an entry is undone by a mirror entry with Source = Reversal.
/// </summary>
public class SupplierCreditEntry
{
    public long Id { get; set; }
    public long AccountId { get; set; }
    /// <summary>In (credit grows) | Out (credit used).</summary>
    public string Direction { get; set; } = string.Empty;
    /// <summary>See <see cref="SupplierCreditSources"/>.</summary>
    public string Source { get; set; } = string.Empty;
    /// <summary>Always positive; Direction gives the sign.</summary>
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public long? PurchaseOrderId { get; set; }
    /// <summary>Set only on PO applications and their reversals.</summary>
    public long? PaymentRequestId { get; set; }
    /// <summary>Wallet row of a WalletTopUp (or of the deposit written by its reversal).</summary>
    public long? PaymentTransactionId { get; set; }
    public long? ReversesEntryId { get; set; }

    // ── Proof of where a credit came from (credit notes) ──
    /// <summary>Our PO the credit came from (e.g. returned parts). Exclusive with SourcePoReference.</summary>
    public long? SourcePurchaseOrderId { get; set; }
    /// <summary>Free-text origin when it is not one of our POs (e.g. "Opening balance 2026").</summary>
    public string? SourcePoReference { get; set; }
    /// <summary>The supplier's credit memo (number or text).</summary>
    public string? CreditMemo { get; set; }
    public string? Notes { get; set; }
    /// <summary>Client-generated idempotency key; a repeated request returns the original entry.</summary>
    public Guid RequestId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public long? CreatedByUserId { get; set; }

    public SupplierCreditAccount Account { get; set; } = null!;
    public PurchaseOrder? PurchaseOrder { get; set; }
    public PaymentRequest? PaymentRequest { get; set; }
    public PaymentTransaction? PaymentTransaction { get; set; }
    public SupplierCreditEntry? ReversesEntry { get; set; }
    public PurchaseOrder? SourcePurchaseOrder { get; set; }
    public User? CreatedBy { get; set; }
    public ICollection<SupplierCreditDocument> Documents { get; set; } = [];
}

public static class SupplierCreditDirections
{
    public const string In = "In";
    public const string Out = "Out";
}

public static class SupplierCreditSources
{
    public const string CreditNote = "CreditNote";
    public const string WalletTopUp = "WalletTopUp";
    public const string POApplication = "POApplication";
    public const string Adjustment = "Adjustment";
    public const string Reversal = "Reversal";
    /// <summary>Credit lines: the supplier granted (In) or lowered (Out) the limit.</summary>
    public const string CreditLimit = "CreditLimit";
    /// <summary>Credit lines: we paid the supplier back (In, linked to the POP wallet withdrawal).</summary>
    public const string Repayment = "Repayment";
}

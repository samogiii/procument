using Procument.Module.Catalog.Entities;
using Procument.Module.Identity.Entities;
using Procument.Module.Purchasing.Entities;

namespace Procument.Module.Sales.Entities;

public class PaymentTransaction
{
    public long Id { get; set; }
    public long PaymentBoxId { get; set; }
    public string Type { get; set; } = "";          // "Deposit" | "Withdraw"
    public decimal Amount { get; set; }
    public string FromType { get; set; } = "";       // "MotherWallet" | "Customer" | "Wallet" | "SupplierCredit" (credit top-up reversal)
    public long? FromCustomerId { get; set; }
    public string ToType { get; set; } = "";         // "MotherWallet" | "Supplier" | "BankFee" | "Wallet" | "SupplierCredit" (credit top-up; never counts as paying a PO)
    public long? ToSupplierId { get; set; }
    public long? InvoiceId { get; set; }             // PI reference
    public long? PaymentRequestId { get; set; }      // RP reference
    public Guid? PopUploadId { get; set; }
    public string? PopFileName { get; set; }
    public string? PopInvoiceNumber { get; set; }
    public string? Notes { get; set; }
    public bool IsAuto { get; set; }                 // true = system-created
    /// <summary>
    /// False = an automatic row written while Wallets "Auto Import" was off. It still counts for
    /// PI/PO paid totals and supplier credits, but no wallet balance or ledger includes it.
    /// </summary>
    public bool InWallet { get; set; } = true;
    /// <summary>
    /// Wallet-side tags, one or more of "B1".."B7" comma-separated ("B1,B3"). Automatic rows are filled from
    /// the customers' bases (see TransactionBases); this is not used for access control.
    /// </summary>
    public string? Base { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Multi-currency support
    public string? TxCurrency { get; set; }           // null = box base currency
    public decimal? ExchangeRate { get; set; }         // Amount * ExchangeRate = amount in box base currency
    public long? ToPaymentBoxId { get; set; }          // wallet-to-wallet: target box

    // Review and exchange-rate correction audit
    public DateTime? ReviewedAt { get; set; }
    public long? ReviewedByUserId { get; set; }
    public decimal? OriginalExchangeRate { get; set; }
    public DateTime? RateEditedAt { get; set; }
    public long? RateEditedByUserId { get; set; }

    public PaymentBox PaymentBox { get; set; } = null!;
    public Customer? FromCustomer { get; set; }
    public Supplier? ToSupplier { get; set; }
    public Invoice? Invoice { get; set; }
    public PaymentRequest? PaymentRequest { get; set; }
    public User? ReviewedBy { get; set; }
    public User? RateEditedBy { get; set; }
}

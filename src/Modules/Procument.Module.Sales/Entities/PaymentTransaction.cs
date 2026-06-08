using Procument.Module.Catalog.Entities;
using Procument.Module.Purchasing.Entities;

namespace Procument.Module.Sales.Entities;

public class PaymentTransaction
{
    public long Id { get; set; }
    public long PaymentBoxId { get; set; }
    public string Type { get; set; } = "";          // "Deposit" | "Withdraw"
    public decimal Amount { get; set; }
    public string FromType { get; set; } = "";       // "MotherWallet" | "Customer"
    public long? FromCustomerId { get; set; }
    public string ToType { get; set; } = "";         // "MotherWallet" | "Supplier"
    public long? ToSupplierId { get; set; }
    public long? InvoiceId { get; set; }             // PI reference
    public long? PaymentRequestId { get; set; }      // RP reference
    public string? Notes { get; set; }
    public bool IsAuto { get; set; }                 // true = system-created
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Multi-currency support
    public string? TxCurrency { get; set; }           // null = box base currency
    public decimal? ExchangeRate { get; set; }         // Amount * ExchangeRate = amount in box base currency
    public long? ToPaymentBoxId { get; set; }          // wallet-to-wallet: target box

    public PaymentBox PaymentBox { get; set; } = null!;
    public Customer? FromCustomer { get; set; }
    public Supplier? ToSupplier { get; set; }
    public Invoice? Invoice { get; set; }
    public PaymentRequest? PaymentRequest { get; set; }
}

namespace Procument.API.Services;

// Payment half of the warehouse-app wire contract (procument-warehouse/server/Warehouse.Api/Services/
// PaymentSyncContracts.cs keeps the same shapes). Envelopes are WarehouseSyncEnvelope (AES-GCM).
// A payment is identified by the PR's PendingUploadId: one id per payment attempt.

// ── Office → warehouse: payments saved in Ready To Pay, no POP yet ──

public class PaymentPush
{
    public DateTime SentAt { get; set; }
    public List<PaymentPushItem> Payments { get; set; } = new();
}

public class PaymentPushItem
{
    public Guid UploadId { get; set; }
    public long MainPaymentRequestId { get; set; }
    public long PoId { get; set; }
    public long? PrNumber { get; set; }
    public string PoNumber { get; set; } = string.Empty;
    public string PoType { get; set; } = string.Empty;
    public string? SalesOrder { get; set; }

    public string SupplierName { get; set; } = string.Empty;
    public string? SupplierEmail { get; set; }
    public string? SupplierPhone { get; set; }
    public string? SupplierAddress { get; set; }
    public List<string> SupplierContacts { get; set; } = new();

    public string? Beneficiary { get; set; }
    public string? Reference { get; set; }
    public string? BankName { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankAddress { get; set; }
    public string? BankCity { get; set; }
    public string? BankCountry { get; set; }
    public string? SwiftCode { get; set; }
    public string? Aba { get; set; }

    /// <summary>USD to pay (the saved "Amount paid"), wire fee included in the PR.</summary>
    public decimal AmountUsd { get; set; }
    public decimal WireFee { get; set; }
    public decimal? PrAmount { get; set; }
    public string WalletName { get; set; } = string.Empty;
    /// <summary>Our company that owns the wallet (pays the supplier).</summary>
    public string? PayingFrom { get; set; }
    public string WalletCurrency { get; set; } = "USD";
    /// <summary>Wallet currency per 1 USD as saved in the office (1 for USD wallets).</summary>
    public decimal? OfficeRate { get; set; }

    /// <summary>The warehouse result is stored here and waits for office review.</summary>
    public bool OfficeHasResult { get; set; }
    /// <summary>Why the office sent the last warehouse result back.</summary>
    public string? RejectNote { get; set; }

    public List<PaymentDocInfo> Docs { get; set; } = new();
}

public class PaymentDocInfo
{
    public string Key { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long Size { get; set; }
}

// ── Warehouse → office: paid (POP + final amount) ──

public class PaymentResults
{
    public List<PaymentResult> Payments { get; set; } = new();
}

public class PaymentResult
{
    public long PaymentId { get; set; }
    public int Version { get; set; }
    public Guid UploadId { get; set; }
    /// <summary>Total the bank took (amount × rate + fees), wallet currency. Worked out again here.</summary>
    public decimal FinalAmount { get; set; }
    /// <summary>Bank handling and wire charges, wallet currency.</summary>
    public decimal HandlingFee { get; set; }
    public decimal BankWireFee { get; set; }
    /// <summary>Wallet currency per 1 USD (null for USD wallets).</summary>
    public decimal? ExchangeRate { get; set; }
    public string? PaidBy { get; set; }
    public DateTime PaidAt { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string MimeType { get; set; } = "application/octet-stream";
    public string DataBase64 { get; set; } = string.Empty;
}

public class PaymentAck
{
    public List<PaymentResultAck> Payments { get; set; } = new();
}

public class PaymentResultAck
{
    public long PaymentId { get; set; }
    public int Version { get; set; }
    public bool Stored { get; set; }
    public string? Reason { get; set; }
}

// ── Supplier documents the warehouse is missing ──

public class PaymentDocRequest
{
    public List<PaymentDocRef> Items { get; set; } = new();
}

public class PaymentDocRef
{
    public Guid UploadId { get; set; }
    public string Key { get; set; } = string.Empty;
}

public class PaymentDocBatch
{
    public List<PaymentDocData> Docs { get; set; } = new();
}

public class PaymentDocData
{
    public Guid UploadId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string MimeType { get; set; } = "application/octet-stream";
    public string DataBase64 { get; set; } = string.Empty;
    /// <summary>Too big to relay; the phone lists it and says to open it in the office.</summary>
    public bool TooLarge { get; set; }
}

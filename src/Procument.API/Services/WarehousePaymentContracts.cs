namespace Procument.API.Services;

// Payment half of the warehouse-app wire contract (procument-warehouse/server/Warehouse.Api/Services/
// PaymentSyncContracts.cs keeps the same shapes). Envelopes are WarehouseSyncEnvelope (AES-GCM).
// A payment is identified by the PR's PendingUploadId: one id per payment attempt.

// ── Office → warehouse: payments saved in Ready To Pay, no POP yet ──

public class PaymentPush
{
    public DateTime SentAt { get; set; }
    public List<PaymentPushItem> Payments { get; set; } = new();
    public List<ShippingCostDecision> CostDecisions { get; set; } = new();
    public List<DepositPushItem> Deposits { get; set; } = new();
}

/// <summary>A Customer POP waiting for the warehouse to confirm it against the bank.</summary>
public class DepositPushItem
{
    public Guid UploadId { get; set; }
    public long MainDepositId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string? Customer { get; set; }
    /// <summary>The POP's price, in Currency.</summary>
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    /// <summary>The wallet's surname when it has one; then no CompanyName is sent.</summary>
    public string WalletName { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string WalletCurrency { get; set; } = "USD";
    /// <summary>Wallet currency per 1 Currency; null when they match.</summary>
    public decimal? OfficeRate { get; set; }
    /// <summary>Amount × OfficeRate, wallet currency.</summary>
    public decimal OfficeAmount { get; set; }
    public string? Notes { get; set; }
    public bool OfficeHasResult { get; set; }
    public string? RejectNote { get; set; }
    public PaymentDocInfo? Pop { get; set; }
}

/// <summary>What the office decided about a shipment's cost (ShipmentId = the warehouse's id).</summary>
public class ShippingCostDecision
{
    public long ShipmentId { get; set; }
    public int Version { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Note { get; set; }
    public decimal FixedCost { get; set; }
    public decimal OtherCost { get; set; }
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
    /// <summary>The wallet's surname when it has one: the warehouse runs on a public server.</summary>
    public string WalletName { get; set; } = string.Empty;
    /// <summary>Our company that owns the wallet (pays the supplier). Not sent for a wallet with a surname.</summary>
    public string? PayingFrom { get; set; }
    public string WalletCurrency { get; set; } = "USD";
    /// <summary>Wallet currency per 1 USD as saved in the office (1 for USD wallets).</summary>
    public decimal? OfficeRate { get; set; }
    /// <summary>The wallet's fixed bank charges in its currency; they fill the phone's fee boxes.</summary>
    public decimal? WalletHandlingFee { get; set; }
    public decimal? WalletWireFee { get; set; }

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
    public List<ShippingCostChange> Costs { get; set; } = new();
    public List<DepositResult> Deposits { get; set; } = new();
}

/// <summary>What reached the bank for a Customer POP: the bank POP, its price and (wallet differs) the rate.</summary>
public class DepositResult
{
    public long DepositId { get; set; }
    public int Version { get; set; }
    public Guid UploadId { get; set; }
    /// <summary>In the POP's currency.</summary>
    public decimal Amount { get; set; }
    /// <summary>Wallet currency per 1 POP currency (null when they match).</summary>
    public decimal? ExchangeRate { get; set; }
    public string? By { get; set; }
    public DateTime At { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string MimeType { get; set; } = "application/octet-stream";
    public string DataBase64 { get; set; } = string.Empty;
}

/// <summary>A shipment's cost (CNY), entered or changed on the phone. Totals 0 when it was removed.</summary>
public class ShippingCostChange
{
    public long ShipmentId { get; set; }
    public int Version { get; set; }
    public string TrackNumber { get; set; } = string.Empty;
    public string? Carrier { get; set; }
    public string? WarehouseName { get; set; }
    public List<long> MainTrackIds { get; set; } = new();
    public decimal FixedCost { get; set; }
    public decimal OtherCost { get; set; }
    public string? By { get; set; }
    public DateTime At { get; set; }
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
    public List<ShippingCostAck> Costs { get; set; } = new();
    public List<DepositResultAck> Deposits { get; set; } = new();
}

public class DepositResultAck
{
    public long DepositId { get; set; }
    public int Version { get; set; }
    public bool Stored { get; set; }
    public string? Reason { get; set; }
}

public class PaymentResultAck
{
    public long PaymentId { get; set; }
    public int Version { get; set; }
    public bool Stored { get; set; }
    public string? Reason { get; set; }
}

public class ShippingCostAck
{
    public long ShipmentId { get; set; }
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

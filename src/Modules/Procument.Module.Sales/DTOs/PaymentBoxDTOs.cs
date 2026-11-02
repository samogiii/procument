namespace Procument.Module.Sales.DTOs;

public record CurrencyBreakdown(string Currency, string Symbol, decimal TotalDeposit, decimal TotalWithdraw);

/// <summary>Lightweight wallet entry used in wallet selection dropdowns (PO creation, payment acceptance).</summary>
public record WalletSelectionResponse(long Id, string Name, string CompanyName, string Currency);

// Upcoming withdraws — USD reminders only, never applied to any wallet.
public record UpcomingWithdrawResponse(long Id, decimal Price, string Description, DateTime CreatedAt);
public record CreateUpcomingWithdrawRequest(decimal Price, string Description);

// Global wallet switches (Wallets page).
public record WalletSettingsResponse(bool AutoImport);
public record UpdateWalletSettingsRequest(bool AutoImport);

/// <summary>Customer bases behind a PR / PI / customer, as transaction Base tags ("B1,B3"); null = none found.</summary>
public record SuggestedBasesResponse(string? Base);

/// <summary>A currency in a wallet group that has no rate to the main wallet's currency.</summary>
public record UnconvertedBalance(string Currency, string Symbol, decimal Balance);

/// <summary>
/// A main wallet plus its sub-wallets, in the main wallet's currency. Display only — sub-wallet money
/// is converted at the fixed page rate and never posted anywhere. NotConverted is left out of the totals.
/// </summary>
public record WalletGroupTotals(
    int SubWalletCount,
    decimal TotalDeposit,
    decimal TotalWithdraw,
    decimal Balance,
    List<UnconvertedBalance> NotConverted);

public record PaymentBoxSummaryResponse(
    long Id,
    long CompanyPresetId,
    string CompanyPresetName,
    string Name,
    string Currency,
    decimal TotalDeposit,
    decimal TotalWithdraw,
    decimal Balance,
    List<CurrencyBreakdown> CurrencyBreakdowns,
    string? BankName = null,
    string? BankAddress = null,
    string? AccountNumber = null,
    string? BeneficiaryName = null,
    string? SwiftCode = null,
    // Same as CurrencyBreakdowns, but only the transactions tagged Base "B1"
    List<CurrencyBreakdown>? B1Breakdowns = null,
    // Null = a main wallet; set = a sub-wallet of that wallet
    long? ParentBoxId = null,
    // Sub-wallets only (full wallet list): Balance in the main wallet's currency, null when there is no rate
    decimal? BalanceInParentCurrency = null,
    // Main wallets with sub-wallets only (full wallet list)
    WalletGroupTotals? Group = null,
    // What the warehouse app sees instead of Name
    string? Surname = null,
    // Fixed bank charges in the wallet currency, they fill the phone's fee boxes
    decimal? HandlingFee = null,
    decimal? WireFee = null);

public record PaymentTransactionRow(
    long Id,
    string Type,
    decimal? Deposit,
    decimal? Withdraw,
    string FromType,
    string? FromName,
    long? FromCustomerId,
    string ToType,
    string? ToName,
    long? ToSupplierId,
    string? PINumber,
    long? PIId,
    string? PRNumber,
    long? PRId,
    long? POId,
    string? Notes,
    bool IsAuto,
    DateTime CreatedAt,
    decimal Balance,
    string? TxCurrency,
    decimal? ExchangeRate,
    /// <summary>Wallet-side tags, one or more of "B1".."B7" comma-separated ("B1,B3").</summary>
    string? Base = null,
    string? PONumber = null,
    string? POType = null,
    string? POLink = null,
    bool IsNew = false,
    DateTime? ReviewedAt = null,
    string? ReviewedByName = null,
    decimal? OriginalExchangeRate = null,
    DateTime? RateEditedAt = null,
    string? RateEditedByName = null);

public record PaymentBoxDetailResponse(
    long Id,
    string CompanyPresetName,
    string Currency,
    decimal TotalDeposit,
    decimal TotalWithdraw,
    decimal Balance,
    List<PaymentTransactionRow> Transactions,
    /// <summary>The wallet's own name — how wallets are labelled in the UI.</summary>
    string? Name = null);

public record AllTransactionRow(
    long Id,
    long BoxId,
    string AccountName,
    string Currency,
    decimal? Deposit,
    decimal? Withdraw,
    string FromType,
    string? FromName,
    long? FromCustomerId,
    string ToType,
    string? ToName,
    long? ToSupplierId,
    string? PINumber,
    long? PIId,
    string? PRNumber,
    long? PRId,
    long? POId,
    string? Notes,
    bool IsAuto,
    DateTime CreatedAt,
    decimal Balance,
    string? TxCurrency,
    decimal? ExchangeRate,
    /// <summary>Wallet-side tags, one or more of "B1".."B7" comma-separated ("B1,B3").</summary>
    string? Base = null,
    string? PONumber = null,
    string? POType = null,
    string? POLink = null,
    bool IsNew = false,
    DateTime? ReviewedAt = null,
    string? ReviewedByName = null,
    decimal? OriginalExchangeRate = null,
    DateTime? RateEditedAt = null,
    string? RateEditedByName = null);

/// <summary>CompanyPresetId is optional — omit it for a wallet that belongs to no company. ParentBoxId makes it a sub-wallet.</summary>
public record CreatePaymentBoxRequest(long? CompanyPresetId, string Currency, string Name = "",
    string? BankName = null, string? BankAddress = null, string? AccountNumber = null,
    string? BeneficiaryName = null, string? SwiftCode = null, long? ParentBoxId = null,
    string? Surname = null, decimal? HandlingFee = null, decimal? WireFee = null);

public record UpdateWalletBankDetailsRequest(
    string? BankName, string? BankAddress, string? AccountNumber,
    string? BeneficiaryName, string? SwiftCode);

/// <summary>A null CompanyPresetId detaches the wallet from any company; a null ParentBoxId makes it a main wallet.</summary>
public record RenamePaymentBoxRequest(string Name, long? CompanyPresetId = null, long? ParentBoxId = null,
    string? Surname = null, decimal? HandlingFee = null, decimal? WireFee = null);

public record CreateTransactionRequest(
    string Type,
    decimal Amount,
    string FromType,
    long? FromCustomerId,
    string ToType,
    long? ToSupplierId,
    long? InvoiceId,
    long? PaymentRequestId,
    string? Notes,
    string? Currency,
    decimal? ExchangeRate,
    long? ToPaymentBoxId,
    /// <summary>When the transaction occurred. Defaults to the current time when omitted.</summary>
    DateTime? CreatedAt = null,
    /// <summary>Wallet-side tags, one or more of "B1".."B7" comma-separated ("B1,B3").</summary>
    string? Base = null);

public record UpdateTransactionRequest(
    string Type,
    decimal Amount,
    string FromType,
    long? FromCustomerId,
    string ToType,
    long? ToSupplierId,
    long? InvoiceId,
    long? PaymentRequestId,
    string? Notes,
    string? Currency,
    decimal? ExchangeRate,
    long? ToPaymentBoxId,
    DateTime CreatedAt,
    /// <summary>Wallet-side tags, one or more of "B1".."B7" comma-separated ("B1,B3").</summary>
    string? Base = null);

public record UpdateExchangeRateRequest(decimal? ExchangeRate, string? Note = null);
public record SetTransactionReviewRequest(bool Reviewed);
public record UpdateExchangeRateResponse(
    PaymentTransactionRow Transaction,
    decimal WalletBalance,
    decimal? BankFeeAmount);
public record UnreviewedTransactionCountResponse(int Total, Dictionary<long, int> ByBoxId);

public record WalletTransferRequest(
    long ToBoxId,
    decimal WithdrawAmount,
    decimal DepositAmount,
    decimal? ExchangeRate,
    string? Notes);

// ── Wallet transfer workflow ──────────────────────────────────────────────────

public record CreateWalletTransferPendingRequest(
    long FromBoxId,
    long ToBoxId,
    decimal WithdrawAmount,
    decimal DepositAmount,
    decimal? ExchangeRate,
    string? Notes);

public record WalletTransferPendingResponse(
    long Id,
    long FromBoxId,
    string FromBoxName,
    string FromCurrency,
    long ToBoxId,
    string ToBoxName,
    string ToCurrency,
    decimal WithdrawAmount,
    decimal DepositAmount,
    decimal? ExchangeRate,
    string? Notes,
    string Status,
    string? PopFileName,
    string? RejectionNote,
    long CreatedByUserId,
    string CreatedByName,
    DateTime CreatedAt,
    DateTime? AcceptedAt,
    DateTime? CompletedAt);

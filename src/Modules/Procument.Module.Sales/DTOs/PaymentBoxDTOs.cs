namespace Procument.Module.Sales.DTOs;

public record CurrencyBreakdown(string Currency, string Symbol, decimal TotalDeposit, decimal TotalWithdraw);

/// <summary>Lightweight wallet entry used in wallet selection dropdowns (PO creation, payment acceptance).</summary>
public record WalletSelectionResponse(long Id, string Name, string CompanyName, string Currency);

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
    string? SwiftCode = null);

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
    decimal? ExchangeRate);

public record PaymentBoxDetailResponse(
    long Id,
    string CompanyPresetName,
    string Currency,
    decimal TotalDeposit,
    decimal TotalWithdraw,
    decimal Balance,
    List<PaymentTransactionRow> Transactions);

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
    decimal? ExchangeRate);

public record CreatePaymentBoxRequest(long CompanyPresetId, string Currency, string Name = "",
    string? BankName = null, string? BankAddress = null, string? AccountNumber = null,
    string? BeneficiaryName = null, string? SwiftCode = null);

public record UpdateWalletBankDetailsRequest(
    string? BankName, string? BankAddress, string? AccountNumber,
    string? BeneficiaryName, string? SwiftCode);

public record RenamePaymentBoxRequest(string Name);

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
    long? ToPaymentBoxId);

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
    DateTime CreatedAt);

public record WalletTransferRequest(
    long ToBoxId,
    decimal WithdrawAmount,
    decimal DepositAmount,
    decimal? ExchangeRate,
    string? Notes);

// ── Pending wallet transfer workflow ──────────────────────────────────────────

public record CreateWalletTransferPendingRequest(
    long FromBoxId,
    long ToBoxId,
    decimal WithdrawAmount,
    decimal DepositAmount,
    decimal? ExchangeRate,
    string? Notes);

public record ReviewWalletTransferRequest(string Decision, string? Note);

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

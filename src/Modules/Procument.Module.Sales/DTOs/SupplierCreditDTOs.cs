using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Procument.Shared.DTOs;

namespace Procument.Module.Sales.DTOs;

// ── Queries ──────────────────────────────────────────────────────────────────

/// <summary>
/// List filter for credit accounts. Sorting follows DataListPage: sortBy = supplierName | companyPresetName |
/// balance | totalIn | totalOut | lastEntryAt with sortDesc. The inherited Sort token ("-balance") also works.
/// Default: balance descending.
/// </summary>
public sealed class SupplierCreditAccountQuery : PageQuery
{
    public List<long>? SupplierIds { get; set; }
    public List<long>? CompanyPresetIds { get; set; }
    public string? SortBy { get; set; }
    public bool SortDesc { get; set; }
    /// <summary>Prepaid (default) or CreditLine — the two tabs of the Supplier Credits page.</summary>
    public string Kind { get; set; } = "Prepaid";
    /// <summary>Credit lines only: hide lines with nothing owed.</summary>
    public bool OnlyOwing { get; set; }
    /// <summary>Prepaid only: hide accounts with a zero balance (default true).</summary>
    public bool OnlyWithBalance { get; set; } = true;
}

// ── Rows ─────────────────────────────────────────────────────────────────────

public sealed record SupplierCreditAccountRow(
    long Id,
    long SupplierId,
    string SupplierName,
    long CompanyPresetId,
    string CompanyPresetName,
    string Currency,
    decimal Balance,
    decimal TotalIn,
    decimal TotalOut,
    DateTime? LastEntryAt,
    string Kind,
    // Credit lines: the granted limit; Balance is what is still available.
    decimal? CreditLimit,
    // Credit lines: what we owe the supplier (CreditLimit − Balance). 0 for prepaid.
    decimal Owed,
    // Credit lines: first use since the line was last fully repaid (a reminder hint).
    DateTime? OldestUnpaidAt,
    // Credit lines: the repayment request still waiting for its POP or final amount.
    long? OpenRepaymentId);

public sealed record SupplierCreditEntryRow(
    long Id,
    long AccountId,
    string Direction,
    string Source,
    decimal Amount,
    decimal BalanceAfter,
    long? PoId,
    string? PoNumber,
    string? PoLink,
    long? PaymentRequestId,
    long? PrNumber,
    long? WalletTransactionId,
    string? WalletName,
    long? SourcePoId,
    string? SourcePoNumber,
    string? SourcePoLink,
    string? SourcePoReference,
    string? CreditMemo,
    string? Notes,
    long? ReversesEntryId,
    long? ReversedByEntryId,
    DateTime CreatedAt,
    string? CreatedByName)
{
    /// <summary>Proof files (supplier PI, credit memo, others). Filled after the row query.</summary>
    public List<SupplierCreditDocumentRow> Documents { get; init; } = [];
}

public sealed record SupplierCreditDocumentRow(
    long Id,
    long EntryId,
    string Category,
    string OriginalFileName,
    long SizeBytes,
    DateTime UploadedAt,
    string? UploadedByName);

/// <summary>One of the supplier's POs, offered as the origin of a credit note.</summary>
public sealed record SupplierCreditPoOption(
    long Id,
    string PoNumber,
    string? CompanyPresetName,
    string Status,
    decimal? TotalAmount,
    DateTime CreatedAt);

public sealed record SupplierCreditOption(long Id, string Name);

public sealed class SupplierCreditFilterOptions
{
    public List<SupplierCreditOption> Suppliers { get; set; } = [];
    public List<SupplierCreditOption> Companies { get; set; } = [];
}

/// <summary>Balance = prepaid credit, or what is still available on a credit line (see Kind).</summary>
public sealed record SupplierCreditCompanyBalance(long CompanyPresetId, string CompanyPresetName, decimal Balance, string Kind);

/// <summary>The PO's own company's credit line with the supplier.</summary>
public sealed record SupplierCreditLineInfo(
    long AccountId, decimal CreditLimit, decimal Available, decimal Owed, decimal SuggestedAmount);

public sealed record SupplierCreditSummary(decimal PrepaidTotal, int CreditLinesOwingCount, decimal CreditLinesOwedTotal);

/// <summary>One pay-back of a credit line: a payment request without a PO and its wallet rows.</summary>
public sealed record SupplierCreditRepaymentRow(
    long Id,
    long? PrNumber,
    string? Status,
    decimal Amount,
    long? WalletId,
    string? WalletName,
    string? WalletCurrency,
    decimal? ExchangeRate,
    long? PopTransactionId,
    string? PopFileName,
    // The POP debit in the wallet's currency (Amount × rate).
    decimal? WalletAmount,
    // "Bank Fee and Others" in the wallet's currency, once the final amount is saved.
    decimal? BankFee,
    DateTime CreatedAt);

public sealed record SupplierCreditPrOption(long PaymentRequestId, long? PrNumber, decimal Remaining);

public sealed class SupplierCreditSuggestion
{
    public long PoId { get; set; }
    public long SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public long? CompanyPresetId { get; set; }
    public string? CompanyPresetName { get; set; }
    public long? AccountId { get; set; }
    /// <summary>Prepaid credit of the PO's own company with its supplier.</summary>
    public decimal UsableBalance { get; set; }
    /// <summary>The PO's own company's credit line with the supplier, if it has one.</summary>
    public SupplierCreditLineInfo? CreditLine { get; set; }
    /// <summary>After wallet payments, credit applied and any saved-but-unpaid wallet draft.</summary>
    public decimal PoRemaining { get; set; }
    public List<SupplierCreditPrOption> PrOptions { get; set; } = [];
    public decimal SuggestedAmount { get; set; }
    /// <summary>Other companies holding usable credit with this supplier — prepaid or credit line (see Kind; balance &gt; 0 only).</summary>
    public List<SupplierCreditCompanyBalance> OtherCompanies { get; set; } = [];
    /// <summary>False when the PO's payment is not accepted yet, or the PO is cancelled/returned.</summary>
    public bool CanApply { get; set; }
    public string? CannotApplyReason { get; set; }
    /// <summary>A wallet draft is saved or a POP waits for its final amount on this PO.</summary>
    public bool WalletPaymentInProgress { get; set; }
    public bool CanChangeCompany { get; set; }
    public string? ChangeCompanyBlockedReason { get; set; }
}

// ── Commands ─────────────────────────────────────────────────────────────────

public sealed class AddSupplierCreditNoteRequest
{
    public long SupplierId { get; set; }
    public long CompanyPresetId { get; set; }
    public decimal Amount { get; set; }
    /// <summary>Our PO the credit came from — or SourcePoReference, exactly one of the two.</summary>
    public long? SourcePurchaseOrderId { get; set; }
    public string? SourcePoReference { get; set; }
    public string? CreditMemo { get; set; }
    public string? Notes { get; set; }
    /// <summary>The supplier's PI (one file).</summary>
    public IFormFile? SupplierPi { get; set; }
    /// <summary>Any other proof (credit memo scan, e-mails, …).</summary>
    public List<IFormFile>? Documents { get; set; }
    public Guid RequestId { get; set; }
}

/// <summary>Attach more proof to an existing entry (multipart: category + files).</summary>
public sealed class AddSupplierCreditDocumentsRequest
{
    public string Category { get; set; } = "Other";
    public List<IFormFile>? Files { get; set; }
}

public sealed record AddSupplierCreditTopUpRequest(
    long SupplierId, long CompanyPresetId, long WalletId, decimal Amount, decimal? ExchangeRate, string? Notes, Guid RequestId);

public sealed record AddSupplierCreditAdjustmentRequest(
    long SupplierId, long CompanyPresetId, string Direction, decimal Amount, string Notes, Guid RequestId);

/// <summary>Kind: Prepaid (default) or CreditLine — which of the company's accounts pays.</summary>
public sealed record ApplySupplierCreditRequest(long PaymentRequestId, decimal Amount, Guid RequestId, string? Kind = null);

/// <summary>Create a credit line or change its limit (a CreditLimit ledger entry for the difference).</summary>
public sealed record SetSupplierCreditLineRequest(long SupplierId, long CompanyPresetId, decimal CreditLimit, string? Notes, Guid RequestId);

/// <summary>Step 1 of paying a credit line back — same shape as a PO payment draft.</summary>
public sealed record SaveSupplierCreditRepaymentRequest(long WalletId, decimal Amount, decimal? ExchangeRate);

/// <summary>Step 3 — the bank's final debit in the wallet's currency; the difference is the bank fee.</summary>
public sealed record FinalizeSupplierCreditRepaymentRequest(decimal FinalWalletAmount);

public sealed record ReverseSupplierCreditRequest(string Notes, Guid RequestId);

// ── Results ──────────────────────────────────────────────────────────────────

public sealed record SupplierCreditWriteResult(SupplierCreditEntryRow Entry, decimal Balance);

public sealed record ApplySupplierCreditResult(
    SupplierCreditEntryRow Entry,
    decimal Balance,
    decimal PaidAmount,
    decimal RemainingAmount,
    string? PrStatus,
    string PoPaymentStatus,
    bool Complete);

/// <summary>
/// A proof file passed from the controller to the service. OpenRead may be called more than once
/// (the write is retried on transient SQL errors), so it must return a fresh stream each time.
/// </summary>
public sealed record SupplierCreditUpload(string Category, string FileName, long SizeBytes, Func<Stream> OpenRead);

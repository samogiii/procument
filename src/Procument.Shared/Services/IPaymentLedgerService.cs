namespace Procument.Shared.Services;

public interface IPaymentLedgerService
{
    /// <summary>Paid per payment request: wallet payments plus net supplier credit applied.</summary>
    Task<Dictionary<long, decimal>> GetSupplierPaidAmountsAsync(IEnumerable<long> paymentRequestIds);
    /// <summary>The supplier-credit part of the paid amount, per payment request (net of reversals).</summary>
    Task<Dictionary<long, decimal>> GetCreditAppliedAmountsAsync(IEnumerable<long> paymentRequestIds);
    /// <summary>True when supplier credit was ever applied to the PR (even if later reversed); its ledger lines keep it.</summary>
    Task<bool> HasSupplierCreditHistoryAsync(long paymentRequestId);
    Task<bool> TryAutoDepositAsync(long invoiceId, decimal amount, long customerId, string? currency = null, decimal? exchangeRate = null, long? explicitBoxId = null, string? popFileName = null);
    Task TryAutoWithdrawAsync(long supplierId, decimal amount, long? companyPresetId, long? paymentRequestId, long? explicitBoxId = null);
}

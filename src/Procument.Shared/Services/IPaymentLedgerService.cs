namespace Procument.Shared.Services;

public interface IPaymentLedgerService
{
    Task<Dictionary<long, decimal>> GetSupplierPaidAmountsAsync(IEnumerable<long> paymentRequestIds);
    Task<bool> TryAutoDepositAsync(long invoiceId, decimal amount, long customerId, string? currency = null, decimal? exchangeRate = null, long? explicitBoxId = null, string? popFileName = null);
    Task TryAutoWithdrawAsync(long supplierId, decimal amount, long? companyPresetId, long? paymentRequestId, long? explicitBoxId = null);
}

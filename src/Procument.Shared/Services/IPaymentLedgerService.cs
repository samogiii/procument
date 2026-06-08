namespace Procument.Shared.Services;

public interface IPaymentLedgerService
{
    Task TryAutoDepositAsync(long invoiceId, decimal amount, long customerId, string? currency = null, decimal? exchangeRate = null, long? explicitBoxId = null);
    Task TryAutoWithdrawAsync(long supplierId, decimal amount, long? companyPresetId, long? paymentRequestId, long? explicitBoxId = null);
}

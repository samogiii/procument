using Procument.Module.Sales.DTOs;
using Procument.Shared.Services;

namespace Procument.Module.Sales.Services;

public interface IPaymentBoxService : IPaymentLedgerService
{
    Task<List<PaymentBoxSummaryResponse>> GetAllAsync(long? companyPresetId = null);
    Task<PaymentBoxDetailResponse?> GetByIdAsync(long id);
    Task<List<AllTransactionRow>> GetAllTransactionsAsync();
    Task<PaymentBoxSummaryResponse> CreateBoxAsync(CreatePaymentBoxRequest req);
    Task<PaymentBoxSummaryResponse?> RenameBoxAsync(long id, RenamePaymentBoxRequest req);
    Task<PaymentBoxSummaryResponse?> UpdateBankDetailsAsync(long id, UpdateWalletBankDetailsRequest req);
    Task<bool> DeleteBoxAsync(long id);
    Task<PaymentTransactionRow?> AddTransactionAsync(long boxId, CreateTransactionRequest req);
    Task<PaymentTransactionRow?> UpdateTransactionAsync(long txId, UpdateTransactionRequest req);
    Task<UpdateExchangeRateResponse?> UpdateExchangeRateAsync(long boxId, long txId, decimal? exchangeRate, string? note, long userId);
    Task<PaymentTransactionRow?> SetReviewedAsync(long boxId, long txId, bool reviewed, long userId);
    Task<UnreviewedTransactionCountResponse> GetUnreviewedCountAsync();
    Task<bool> DeleteTransactionAsync(long txId);
    Task<bool> TransferAsync(long sourceBoxId, WalletTransferRequest req);
    Task<List<PaymentBoxSummaryResponse>> GetBoxesForCustomerAsync(long customerId);
    /// <summary>Lightweight list for wallet selection dropdowns — accessible to Payment and Expert roles.</summary>
    Task<List<WalletSelectionResponse>> GetSimpleListAsync();
    /// <summary>Upcoming withdraws — reminder list only, never applied to wallet balances.</summary>
    Task<List<UpcomingWithdrawResponse>> GetUpcomingWithdrawsAsync();
    Task<UpcomingWithdrawResponse> AddUpcomingWithdrawAsync(CreateUpcomingWithdrawRequest req, long userId);
    Task<bool> DeleteUpcomingWithdrawAsync(long id);
    /// <summary>Global wallet switches. A missing settings row means everything is off.</summary>
    Task<WalletSettingsResponse> GetSettingsAsync();
    Task<WalletSettingsResponse> UpdateSettingsAsync(UpdateWalletSettingsRequest req, long userId);
    /// <summary>Base tags a new transaction would get from its PR / PI / customer ("B1,B3").</summary>
    Task<SuggestedBasesResponse> SuggestBasesAsync(long? paymentRequestId, long? invoiceId, long? customerId);
}

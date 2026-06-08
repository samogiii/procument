using Procument.Module.Sales.DTOs;
using Procument.Shared.Services;

namespace Procument.Module.Sales.Services;

public interface IPaymentBoxService : IPaymentLedgerService
{
    Task<List<PaymentBoxSummaryResponse>> GetAllAsync(long? companyPresetId = null);
    Task<PaymentBoxDetailResponse?> GetByIdAsync(long id);
    Task<List<AllTransactionRow>> GetAllTransactionsAsync();
    Task<PaymentBoxSummaryResponse> CreateBoxAsync(CreatePaymentBoxRequest req);
    Task<PaymentBoxSummaryResponse?> RenameBoxAsync(long id, string name);
    Task<PaymentBoxSummaryResponse?> UpdateBankDetailsAsync(long id, UpdateWalletBankDetailsRequest req);
    Task<bool> DeleteBoxAsync(long id);
    Task<PaymentTransactionRow?> AddTransactionAsync(long boxId, CreateTransactionRequest req);
    Task<PaymentTransactionRow?> UpdateTransactionAsync(long txId, UpdateTransactionRequest req);
    Task<bool> DeleteTransactionAsync(long txId);
    Task<bool> TransferAsync(long sourceBoxId, WalletTransferRequest req);
    Task<List<PaymentBoxSummaryResponse>> GetBoxesForCustomerAsync(long customerId);
    /// <summary>Lightweight list for wallet selection dropdowns — accessible to Payment and Expert roles.</summary>
    Task<List<WalletSelectionResponse>> GetSimpleListAsync();
}

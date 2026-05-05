using Procument.Module.Purchasing.DTOs;
using Procument.Shared.DTOs;

namespace Procument.Module.Purchasing.Services;

public interface IProcurementService
{
    /// <summary>
    /// Clone the accepted Proforma Invoice (and its Quote/RFQ/selected-supplier chain) into
    /// a new Procurement. Idempotent — returns the existing Procurement for this invoice
    /// when one is already present.
    /// </summary>
    Task<ProcurementResponse> CreateFromAcceptedInvoiceAsync(long invoiceId, long userId);

    Task<PagedResult<ProcurementResponse>> GetAllAsync(PageQuery page, long userId, bool isAdmin);
    Task<List<ProcurementItemFlatResponse>> GetAllItemsFlatAsync(long userId, bool isAdmin);
    Task<ProcurementResponse?> GetByIdAsync(long id, long userId, bool isAdmin);
    Task<bool> UserCanAccessAsync(long procurementId, long userId, bool isAdmin);
    Task<bool> UserCanAccessItemAsync(long procurementId, long itemId, long userId, bool isAdmin);

    Task<bool> UpdateItemAsync(long procurementId, long itemId, UpdateProcurementItemRequest request);

    Task<ProcurementSupplierQuoteResponse?> UpsertSupplierQuoteAsync(long procurementId, long itemId, UpsertSupplierQuoteRequest request, long userId);
    Task<bool> DeleteSupplierQuoteAsync(long procurementId, long itemId, long supplierQuoteId);
    Task<bool> SelectSupplierQuoteAsync(long procurementId, long itemId, long supplierQuoteId);

    Task<FinalizeProcurementResponse?> FinalizeAsync(long procurementId, long userId, FinalizeProcurementRequest? request = null);

    /// <summary>
    /// Finalize a single ProcurementItem (one supplier row) independently of the rest.
    /// Creates POItem(s) for that item only. Auto-finalizes the procurement when all items are done.
    /// </summary>
    Task<FinalizeProcurementItemResponse?> FinalizeItemAsync(long procurementId, long itemId, long userId);

    /// <summary>
    /// Admin approves a single selected supplier quote row — creates exactly ONE POItem from that quote.
    /// Auto-finalizes the procurement when every selected quote has an active POItem.
    /// </summary>
    Task<FinalizeProcurementItemResponse?> FinalizeSupplierQuoteAsync(long procurementId, long itemId, long supplierQuoteId, long userId);

    Task<bool> CancelAsync(long procurementId, long userId);

    /// <summary>
    /// Recycle the given POItems back into the Procurement layer. For each POItem:
    /// soft-delete it (POId=null, ReturnedAt=now), flip its source ProcurementItem's ItemStatus to Open,
    /// increment LoopCount (capped at 5), stamp LastReturnReason / LastReturnedAt, and transition the
    /// parent Procurement from Finalized → Reopened. Returns the set of re-opened Procurement ids and
    /// any POItems that were skipped (capped or untraceable).
    /// </summary>
    Task<(List<long> reopenedProcurementIds, List<long> skippedPOItemIds, List<string> warnings)> RecyclePOItemsAsync(
        IEnumerable<long> poItemIds, long poId, string reason, long userId);

    /// <summary>
    /// Called when a PO is marked Completed — stamps FulfilledByPOItemId on the source ProcurementItems,
    /// closing the loop for those lines. Safe to call repeatedly.
    /// </summary>
    Task MarkFulfilledByPOAsync(long poId);
}

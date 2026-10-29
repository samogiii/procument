namespace Procument.Module.Purchasing.Services;

/// <summary>What we quoted a customer for one RFQ item (the latest customer quote line for it).</summary>
public record CustomerQuotePrice(long QuoteId, string QuoteNumber, string Status, DateTime CreatedAt, decimal UnitPrice, int Qty, string? Condition);

/// <summary>
/// Reads our customer quotes (Sales module) for RFQ items. The interface lives here so Purchasing (Email RFQs)
/// can use it; the implementation is in Sales, which sees both sides (same pattern as IProcurementService).
/// </summary>
public interface ICustomerQuotePriceLookup
{
    /// <summary>Latest quote line per RFQ item that has one.</summary>
    Task<Dictionary<long, CustomerQuotePrice>> ByRfqItemsAsync(IReadOnlyCollection<long> rfqItemIds);
    /// <summary>The supplier quotes (SupplierPartQuote ids) that one of our customer quote lines uses.</summary>
    Task<HashSet<long>> UsedSupplierQuoteIdsAsync(IReadOnlyCollection<long> supplierQuoteIds);
}

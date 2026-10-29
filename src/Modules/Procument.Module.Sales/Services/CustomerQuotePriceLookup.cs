using Microsoft.EntityFrameworkCore;
using Procument.Module.Purchasing.Services;
using Procument.Module.Sales.Entities;

namespace Procument.Module.Sales.Services;

public class CustomerQuotePriceLookup : ICustomerQuotePriceLookup
{
    private readonly DbContext _db;

    public CustomerQuotePriceLookup(DbContext db) => _db = db;

    public async Task<Dictionary<long, CustomerQuotePrice>> ByRfqItemsAsync(IReadOnlyCollection<long> rfqItemIds)
    {
        if (rfqItemIds.Count == 0) return new();
        var ids = rfqItemIds.Distinct().ToList();
        var rows = await _db.Set<QuoteItem>().AsNoTracking()
            .Where(qi => qi.RFQItemId != null && ids.Contains(qi.RFQItemId.Value))
            .Select(qi => new
            {
                RfqItemId = qi.RFQItemId!.Value,
                qi.QuoteId,
                qi.Quote.QuoteNumber,
                qi.Quote.Status,
                qi.Quote.CreatedAt,
                qi.UnitPrice,
                qi.Qty,
                qi.Condition,
            })
            .ToListAsync();
        return rows
            .GroupBy(r => r.RfqItemId)
            .ToDictionary(g => g.Key, g =>
            {
                var r = g.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.QuoteId).First();
                return new CustomerQuotePrice(r.QuoteId, r.QuoteNumber, r.Status, r.CreatedAt, r.UnitPrice, r.Qty, r.Condition);
            });
    }

    public async Task<HashSet<long>> UsedSupplierQuoteIdsAsync(IReadOnlyCollection<long> supplierQuoteIds)
    {
        if (supplierQuoteIds.Count == 0) return new();
        var ids = supplierQuoteIds.Distinct().ToList();
        return (await _db.Set<QuoteItem>().AsNoTracking()
                .Where(qi => qi.ProcumentRecordId != null && ids.Contains(qi.ProcumentRecordId.Value))
                .Select(qi => qi.ProcumentRecordId!.Value)
                .Distinct()
                .ToListAsync())
            .ToHashSet();
    }
}

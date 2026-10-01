using Microsoft.EntityFrameworkCore;
using Procument.Module.Purchasing.Entities;
using Procument.Module.RFQ.Entities;

namespace Procument.Module.Purchasing.Services;

/// <summary>
/// Customer.Base behind each PO line, so the shipping pages can be filtered to the bases
/// granted to the user (see <see cref="ShippingBaseAccess"/>). Read-only — nothing is stored.
///
/// Done as a chain of small primary-key lookups instead of one wide join: the single
/// 8-table join timed out on the full data set. Each id list goes to SQL as one JSON
/// parameter (EF.Parameter → OPENJSON), not one parameter per id.
/// </summary>
internal static class ShippingCustomerBases
{
    /// <summary>POItem.Id → Customer.Base (null when the line has no customer, e.g. a Stock PO).</summary>
    public static async Task<Dictionary<long, int?>> LoadAsync(DbContext db, IEnumerable<long> poItemIds)
    {
        var ids = poItemIds.Distinct().ToList();
        if (ids.Count == 0) return new();

        // A PO line reaches its RFQ two ways: through the procurement item it was cloned
        // from (SourceProcurementItemId → SourceRfqId), or through the older
        // ProcumentRecord → RFQItem link. The first wins; the second covers older lines.
        var lines = await db.Set<POItem>().AsNoTracking()
            .Where(p => EF.Parameter(ids).Contains(p.Id))
            .Select(p => new { p.Id, p.SourceProcurementItemId, p.ProcumentId })
            .ToListAsync();

        var procItemIds = lines.Where(l => l.SourceProcurementItemId.HasValue).Select(l => l.SourceProcurementItemId!.Value).Distinct().ToList();
        var rfqByProcItem = procItemIds.Count == 0 ? new Dictionary<long, long?>() : await db.Set<ProcurementItem>().AsNoTracking()
            .Where(pi => EF.Parameter(procItemIds).Contains(pi.Id))
            .Select(pi => new { pi.Id, pi.SourceRfqId })
            .ToDictionaryAsync(pi => pi.Id, pi => pi.SourceRfqId);

        var recordIds = lines.Where(l => l.ProcumentId.HasValue).Select(l => l.ProcumentId!.Value).Distinct().ToList();
        var rfqByRecord = recordIds.Count == 0 ? new Dictionary<long, long>() : await db.Set<ProcumentRecord>().AsNoTracking()
            .Where(r => EF.Parameter(recordIds).Contains(r.Id))
            .Select(r => new { r.Id, r.RFQItem.RFQId })
            .ToDictionaryAsync(r => r.Id, r => r.RFQId);

        long? RfqOf(long? procItemId, long? recordId)
        {
            if (procItemId.HasValue && rfqByProcItem.TryGetValue(procItemId.Value, out var viaProc) && viaProc.HasValue) return viaProc;
            if (recordId.HasValue && rfqByRecord.TryGetValue(recordId.Value, out var viaRecord)) return viaRecord;
            return null;
        }

        var rfqByLine = lines.ToDictionary(l => l.Id, l => RfqOf(l.SourceProcurementItemId, l.ProcumentId));

        var rfqIds = rfqByLine.Values.Where(v => v.HasValue).Select(v => v!.Value).Distinct().ToList();
        var baseByRfq = rfqIds.Count == 0 ? new Dictionary<long, int?>() : await db.Set<RFQHeader>().AsNoTracking()
            .Where(r => EF.Parameter(rfqIds).Contains(r.Id))
            .Select(r => new { r.Id, r.Customer.Base })
            .ToDictionaryAsync(r => r.Id, r => r.Base);

        return rfqByLine.ToDictionary(
            kv => kv.Key,
            kv => kv.Value.HasValue ? baseByRfq.GetValueOrDefault(kv.Value.Value) : null);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Procument.Module.Purchasing.Entities;
using Procument.Module.RFQ.Entities;
using Procument.Shared.Services;

namespace Procument.Module.Purchasing.Services;

/// <summary>
/// Procurement Plus commissions live 7 days (Sam): while active, Coef 1 is locked on the Add Quote page and new RFQs
/// for the same part + supplier + customer get it too; after that it is a plain Coef 1 again.
/// </summary>
public static class CommissionRules
{
    public const int ValidDays = 7;

    public static DateTime ActiveSince => DateTime.UtcNow.AddDays(-ValidDays);

    /// <summary>A commission saved at <paramref name="at"/> is still active (locks Coef 1, reaches new RFQs).</summary>
    public static bool IsActive(DateTime? at) => at != null && at >= ActiveSince;
}

/// <summary>
/// New RFQ items: a supplier quote on them (auto-filled from an accepted email / Procurement Plus line) gets the
/// active commission of the same supplier, customer, part and condition — Coef 1, locked, with the ORIGINAL commission
/// time, so it still ends 7 days after it was saved. Registered after PendingSupplierQuoteService, which writes the quotes.
/// </summary>
public class CommissionAutoApplyHandler : IRfqItemsCreatedHandler
{
    private static readonly System.Text.RegularExpressions.Regex Whitespace = new(@"\s+");
    private readonly DbContext _db;
    private readonly ILogger<CommissionAutoApplyHandler> _logger;

    public CommissionAutoApplyHandler(DbContext db, ILogger<CommissionAutoApplyHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    private static string PnKey(string? pn) => Whitespace.Replace(pn ?? "", "").ToUpperInvariant();
    private static string CondKey(string? c) => (c ?? "").Trim().ToUpperInvariant();

    private static HashSet<string> ItemKeys(RFQItem i) =>
        new[] { i.PartNumber.Name, i.PartNumber.NewName, i.Alt }
            .Concat(i.PartNumber.Alternatives.Select(a => a.Name))
            .Select(PnKey).Where(k => k.Length > 0).ToHashSet();

    public async Task OnRfqItemsCreatedAsync(IReadOnlyCollection<long> rfqItemIds, long? userId, CancellationToken cancellationToken = default)
    {
        if (rfqItemIds.Count == 0) return;
        var quotes = await _db.Set<ProcumentRecord>()
            .Include(q => q.RFQItem).ThenInclude(i => i.RFQ)
            .Include(q => q.RFQItem).ThenInclude(i => i.PartNumber).ThenInclude(p => p.Alternatives)
            .Where(q => rfqItemIds.Contains(q.RFQItemId) && q.CommissionAt == null && (q.Type ?? "Procument") != "Shop")
            .ToListAsync(cancellationToken);
        if (quotes.Count == 0) return;

        var since = CommissionRules.ActiveSince;
        var supplierIds = quotes.Select(q => q.SupplierId).Distinct().ToList();
        var customerIds = quotes.Select(q => q.RFQItem.RFQ.CustomerId).Distinct().ToList();
        var active = await _db.Set<ProcumentRecord>().AsNoTracking()
            .Include(q => q.RFQItem).ThenInclude(i => i.RFQ)
            .Include(q => q.RFQItem).ThenInclude(i => i.PartNumber).ThenInclude(p => p.Alternatives)
            .Where(q => q.CommissionAt >= since && supplierIds.Contains(q.SupplierId)
                && customerIds.Contains(q.RFQItem.RFQ.CustomerId) && !rfqItemIds.Contains(q.RFQItemId))
            .OrderByDescending(q => q.CommissionAt)
            .ToListAsync(cancellationToken);
        if (active.Count == 0) return;

        var applied = 0;
        foreach (var quote in quotes)
        {
            var keys = ItemKeys(quote.RFQItem);
            var cond = CondKey(quote.Condition ?? quote.RFQItem.Condition);
            var source = active.FirstOrDefault(a => a.SupplierId == quote.SupplierId
                && a.RFQItem.RFQ.CustomerId == quote.RFQItem.RFQ.CustomerId
                && CondKey(a.Condition ?? a.RFQItem.Condition) == cond
                && ItemKeys(a.RFQItem).Overlaps(keys));
            if (source == null) continue;
            quote.Coef_1 = source.Coef_1;
            quote.CommissionAt = source.CommissionAt;
            quote.CommissionByUserId = source.CommissionByUserId;
            applied++;
        }
        if (applied == 0) return;
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Commission auto-applied to {Count} supplier quote(s) on new RFQ items", applied);
    }
}

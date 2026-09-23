using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Procument.Module.Catalog.Entities;
using Procument.Module.OurInventory.Entities;
using Procument.Shared.Services;

namespace Procument.Module.OurInventory.Services;

/// <summary>
/// Sales-facing reservation boundary, keyed by the Sales Order (invoice) line. All quantity changes go
/// through <see cref="IStockLedgerService"/>; this class only decides which reservations to touch.
/// </summary>
public sealed class StockReservationService(DbContext db, IStockLedgerService ledger, IOptions<OurInventoryOptions> options) : IStockReservationService
{
    public Task<long?> GetStockSupplierIdAsync(CancellationToken cancellationToken = default)
    {
        var name = options.Value.OurStockSupplierName.Trim().ToUpper();
        return db.Set<Supplier>().Where(s => s.Name.ToUpper() == name).Select(s => (long?)s.Id).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<long?> ResolveStockLotAsync(long? sourceStockItemId, long? supplierId, long? partNumberId, string? condition,
        CancellationToken cancellationToken = default)
    {
        if (sourceStockItemId.HasValue)
        {
            var lot = await db.Set<OurStockItem>().AsNoTracking()
                .Where(l => l.Id == sourceStockItemId.Value)
                .Select(l => new { l.Id, l.PartNumberId }).FirstOrDefaultAsync(cancellationToken);
            // A lot of a different part (e.g. the quote was re-pointed at an alt) is not used.
            if (lot is not null && (!partNumberId.HasValue || lot.PartNumberId == partNumberId.Value)) return lot.Id;
        }

        if (!supplierId.HasValue || !partNumberId.HasValue) return null;
        if (supplierId != await GetStockSupplierIdAsync(cancellationToken)) return null;

        var candidates = db.Set<OurStockItem>().AsNoTracking().Where(l => l.PartNumberId == partNumberId.Value && l.QtyAvailable > 0);
        if (!string.IsNullOrWhiteSpace(condition))
        {
            var c = condition.Trim();
            var sameCondition = candidates.Where(l => l.Condition == c);
            if (await sameCondition.AnyAsync(cancellationToken)) candidates = sameCondition;
        }
        return await candidates.OrderByDescending(l => l.QtyAvailable).ThenBy(l => l.Id)
            .Select(l => (long?)l.Id).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<long, decimal>> GetReceivedByPoItemAsync(IReadOnlyCollection<long> poItemIds,
        CancellationToken cancellationToken = default)
    {
        if (poItemIds.Count == 0) return new Dictionary<long, decimal>();
        var ids = poItemIds.ToList();
        return await db.Set<OurStockMovement>().AsNoTracking()
            .Where(m => m.POItemId.HasValue && ids.Contains(m.POItemId.Value)
                && (m.Type == OurStockMovementTypes.Receipt || m.Type == OurStockMovementTypes.Adjust))
            .GroupBy(m => m.POItemId!.Value)
            .Select(g => new { g.Key, Qty = g.Sum(m => m.Qty) })
            .ToDictionaryAsync(x => x.Key, x => x.Qty, cancellationToken);
    }

    public async Task<StockLineStatus> GetLineStatusAsync(long invoiceItemId, CancellationToken cancellationToken = default)
    {
        var statuses = await GetLineStatusesAsync([invoiceItemId], cancellationToken);
        return statuses.GetValueOrDefault(invoiceItemId) ?? new StockLineStatus(0, 0, null);
    }

    public async Task<IReadOnlyDictionary<long, StockLineStatus>> GetLineStatusesAsync(
        IReadOnlyCollection<long> invoiceItemIds,
        CancellationToken cancellationToken = default)
    {
        if (invoiceItemIds.Count == 0) return new Dictionary<long, StockLineStatus>();
        var ids = invoiceItemIds.Distinct().ToList();
        var reservations = await db.Set<OurStockReservation>().AsNoTracking()
            .Where(r => r.InvoiceItemId.HasValue && ids.Contains(r.InvoiceItemId.Value) && r.Status == OurStockReservationStatuses.Active)
            .GroupBy(r => r.InvoiceItemId!.Value)
            .Select(g => new { InvoiceItemId = g.Key, Qty = g.Sum(r => r.Qty) })
            .ToDictionaryAsync(x => x.InvoiceItemId, x => x.Qty, cancellationToken);
        var issues = await db.Set<OurStockMovement>().AsNoTracking()
            .Where(m => m.InvoiceItemId.HasValue && ids.Contains(m.InvoiceItemId.Value) && m.Type == OurStockMovementTypes.Issue)
            .GroupBy(m => m.InvoiceItemId!.Value)
            .Select(g => new
            {
                InvoiceItemId = g.Key,
                Issued = -g.Sum(m => m.Qty),
                CostTotal = g.Sum(m => -m.Qty * (m.UnitCost ?? 0))
            })
            .ToDictionaryAsync(x => x.InvoiceItemId, cancellationToken);

        return ids.ToDictionary(id => id, id =>
        {
            var issue = issues.GetValueOrDefault(id);
            var issued = issue?.Issued ?? 0;
            var cost = issued > 0 ? Math.Round(issue!.CostTotal / issued, 4) : (decimal?)null;
            return new StockLineStatus(reservations.GetValueOrDefault(id), issued, cost);
        });
    }

    /// <summary>Tops the line's active reservation up to <paramref name="quantity"/>; reserves what is available and reports the rest.</summary>
    public Task<StockReservationResult> ReserveAsync(
        long stockItemId,
        decimal quantity,
        long invoiceItemId,
        long createdByUserId,
        CancellationToken cancellationToken = default)
        => ledger.ExecuteAsync(async () =>
        {
            // Idempotent: a repeated accept only reserves the part that is not held yet.
            var alreadyHeld = await ActiveFor(invoiceItemId).SumAsync(r => r.Qty, cancellationToken);
            var missing = quantity - alreadyHeld;
            if (missing > 0)
            {
                var reservation = await ledger.ReserveAsync(stockItemId, missing, new StockMovementContext
                {
                    UserId = createdByUserId,
                    Reference = $"Sales order line {invoiceItemId}",
                    InvoiceItemId = invoiceItemId,
                }, expiresAt: null, allowPartial: true, cancellationToken);
                alreadyHeld += reservation?.Qty ?? 0;
            }

            var reserved = Math.Min(quantity, alreadyHeld);
            return new StockReservationResult(quantity, reserved, quantity - reserved);
        }, cancellationToken);

    public Task ReleaseAsync(
        long invoiceItemId,
        long closedByUserId,
        CancellationToken cancellationToken = default)
        => ledger.ExecuteAsync(async () =>
        {
            var ids = await ActiveFor(invoiceItemId).Select(r => r.Id).ToListAsync(cancellationToken);
            foreach (var id in ids)
                await ledger.ReleaseAsync(id, OurStockReservationStatuses.Released, cancellationToken);
            return ids.Count;
        }, cancellationToken);

    /// <summary>Issues up to <paramref name="quantity"/> from the line's reservations, oldest first.</summary>
    public Task<StockReservationResult> ConsumeAsync(
        long invoiceItemId,
        decimal quantity,
        long closedByUserId,
        CancellationToken cancellationToken = default)
        => ledger.ExecuteAsync(async () =>
        {
            var reservations = await ActiveFor(invoiceItemId).OrderBy(r => r.CreatedAt).ThenBy(r => r.Id)
                .Select(r => new { r.Id, r.Qty }).ToListAsync(cancellationToken);
            var left = quantity;
            foreach (var reservation in reservations)
            {
                if (left <= 0) break;
                var take = Math.Min(left, reservation.Qty);
                await ledger.ConsumeAsync(reservation.Id, take, new StockMovementContext
                {
                    UserId = closedByUserId,
                    Reference = $"Sales order line {invoiceItemId}",
                    InvoiceItemId = invoiceItemId,
                }, cancellationToken);
                left -= take;
            }
            return new StockReservationResult(quantity, quantity - left, left);
        }, cancellationToken);

    private IQueryable<OurStockReservation> ActiveFor(long invoiceItemId)
        => db.Set<OurStockReservation>()
            .Where(r => r.InvoiceItemId == invoiceItemId && r.Status == OurStockReservationStatuses.Active);
}

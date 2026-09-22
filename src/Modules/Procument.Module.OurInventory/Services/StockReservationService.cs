using Microsoft.EntityFrameworkCore;
using Procument.Module.OurInventory.Entities;
using Procument.Shared.Services;

namespace Procument.Module.OurInventory.Services;

/// <summary>
/// Sales-facing reservation boundary, keyed by the Sales Order (invoice) line. All quantity changes go
/// through <see cref="IStockLedgerService"/>; this class only decides which reservations to touch.
/// </summary>
public sealed class StockReservationService(DbContext db, IStockLedgerService ledger) : IStockReservationService
{
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

using Microsoft.EntityFrameworkCore;
using Procument.Module.OurInventory.DTOs;
using Procument.Module.OurInventory.Entities;
using Procument.Module.Purchasing.Entities;
using Procument.Module.Purchasing.Services;

namespace Procument.Module.OurInventory.Services;

public interface IStockReceiptService
{
    Task<StockReceiptSummaryResponse?> ReceiveManualAsync(long poId, ManualStockReceiptRequest request, long userId,
        CancellationToken cancellationToken = default);
    Task<StockReceiptSummaryResponse?> GetSummaryAsync(long poId, CancellationToken cancellationToken = default);
    Task<int> AddSerialsAsync(long movementId, IReadOnlyCollection<string> serials, CancellationToken cancellationToken = default);
}

/// <summary>
/// Moves Stock PO quantities into the ledger, either from a warehouse track number
/// (<see cref="IStockReceiptHandler"/>, called by ShippingService) or by a manual receipt.
/// </summary>
public sealed class StockReceiptService(DbContext db, IStockLedgerService ledger) : IStockReceiptService, IStockReceiptHandler
{
    private const string StockOrigin = "Stock";
    private const string DefaultCondition = "New";

    /// <summary>
    /// Reconciles the stock received through one track with its accepted counts. Each (track, PO line) holds
    /// one Receipt movement; a later change to the accepted quantity (recount, rejection) posts the difference
    /// as an Adjust on the same lot, so calling this repeatedly never double-counts.
    /// </summary>
    public async Task OnReceivedAsync(long trackNumberId, long userId, CancellationToken cancellationToken = default)
    {
        var track = await db.Set<POItemTrackNumber>().AsNoTracking()
            .Where(t => t.Id == trackNumberId)
            .Select(t => new { t.Id, t.TrackNumber, t.WarehouseId, t.Origin })
            .FirstOrDefaultAsync(cancellationToken);
        if (track is null) return;
        // A transfer leg moves stock that is already in the ledger from one warehouse to another.
        if (track.Origin == "Transfer")
        {
            await ReconcileTransferLegAsync(trackNumberId, userId, cancellationToken);
            return;
        }

        var items = await db.Set<TrackNumberItem>().AsNoTracking()
            .Where(i => i.TrackNumberId == trackNumberId && i.POItem.PurchaseOrder!.Origin == StockOrigin)
            .Select(i => new { i.POItemId, i.Status, i.ActualQty, i.ExpectedQty })
            .ToListAsync(cancellationToken);
        if (items.Count == 0) return;

        await ledger.ExecuteAsync(async () =>
        {
            var touchedPoIds = new HashSet<long>();
            foreach (var item in items)
            {
                var target = item.Status == "Accepted" ? Math.Max(0, item.ActualQty ?? item.ExpectedQty) : 0;
                var existing = await db.Set<OurStockMovement>().AsNoTracking()
                    .Where(m => m.TrackNumberId == trackNumberId && m.POItemId == item.POItemId
                        && (m.Type == OurStockMovementTypes.Receipt || m.Type == OurStockMovementTypes.Adjust))
                    .Select(m => new { m.Type, m.Qty, m.StockItemId })
                    .ToListAsync(cancellationToken);
                var delta = target - existing.Sum(m => m.Qty);
                if (delta == 0) continue;

                var line = await db.Set<POItem>().Include(i => i.PurchaseOrder)
                    .FirstAsync(i => i.Id == item.POItemId, cancellationToken);
                var po = line.PurchaseOrder!;
                var context = new StockMovementContext
                {
                    UserId = userId,
                    Reference = $"{po.PONumber} / {track.TrackNumber}",
                    POId = po.Id,
                    POItemId = line.Id,
                    TrackNumberId = trackNumberId,
                    Reason = existing.Count > 0 ? $"Receipt correction on track {track.TrackNumber}" : null,
                };

                var receipt = existing.FirstOrDefault(m => m.Type == OurStockMovementTypes.Receipt);
                if (receipt is not null)
                {
                    await ledger.AdjustAsync(receipt.StockItemId, delta, context, cancellationToken);
                }
                else if (delta > 0)
                {
                    var lot = await ledger.GetOrCreateLotAsync(LotKey(po, line, track.WarehouseId, certName: null), cancellationToken);
                    await ledger.ReceiveAsync(lot, delta, line.UnitPrice, OurStockMovementTypes.Receipt, context,
                        cancellationToken: cancellationToken);
                    line.StockItemId = lot.Id;
                }
                touchedPoIds.Add(po.Id);
            }

            await db.SaveChangesAsync(cancellationToken);
            foreach (var poId in touchedPoIds) await UpdatePoReceiptStatusAsync(poId, cancellationToken);
            return true;
        }, cancellationToken);
    }

    /// <summary>
    /// Destination leg of a <see cref="WarehouseTransfer"/>: once its items are accepted, the accepted quantity moves
    /// from the lot at the transfer's source warehouse to the same lot key at the destination. Like receipts, it
    /// reconciles — a recount or rejection moves the difference back — so repeated calls never double-move.
    /// Stock stays at the source (and sellable) while in transit.
    /// </summary>
    private async Task ReconcileTransferLegAsync(long trackNumberId, long userId, CancellationToken cancellationToken)
    {
        var leg = await db.Set<POItemTrackNumber>().AsNoTracking()
            .Where(t => t.Id == trackNumberId && t.SourceTransferId != null && t.WarehouseId != null)
            .Select(t => new
            {
                t.TrackNumber, DestinationWarehouseId = t.WarehouseId!.Value, TransferId = t.SourceTransferId!.Value,
                SourceWarehouseId = t.SourceTransfer!.FromWarehouseId, t.SourceTransfer.TransferNumber,
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (leg is null || leg.SourceWarehouseId == leg.DestinationWarehouseId) return;

        var items = await db.Set<TrackNumberItem>().AsNoTracking()
            .Where(i => i.TrackNumberId == trackNumberId && i.POItem.PurchaseOrder!.Origin == StockOrigin && i.POItem.StockItemId != null)
            .Select(i => new { i.POItemId, i.Status, i.ActualQty, i.ExpectedQty, ReceiptLotId = i.POItem.StockItemId!.Value, i.POItem.POId })
            .ToListAsync(cancellationToken);
        if (items.Count == 0) return;

        await ledger.ExecuteAsync(async () =>
        {
            foreach (var item in items)
            {
                var target = item.Status == "Accepted" ? Math.Max(0, item.ActualQty ?? item.ExpectedQty) : 0;
                // Net quantity this leg has already put into the destination warehouse.
                var moved = await db.Set<OurStockMovement>().AsNoTracking()
                    .Where(m => m.TrackNumberId == trackNumberId && m.POItemId == item.POItemId
                        && m.StockItem.WarehouseId == leg.DestinationWarehouseId
                        && (m.Type == OurStockMovementTypes.TransferIn || m.Type == OurStockMovementTypes.TransferOut))
                    .SumAsync(m => (decimal?)m.Qty, cancellationToken) ?? 0;
                var delta = target - moved;
                if (delta == 0) continue;

                var receiptLot = await db.Set<OurStockItem>().AsNoTracking().FirstAsync(l => l.Id == item.ReceiptLotId, cancellationToken);
                var (fromWarehouse, toWarehouse) = delta > 0
                    ? (leg.SourceWarehouseId, leg.DestinationWarehouseId)
                    : (leg.DestinationWarehouseId, leg.SourceWarehouseId);
                var fromLot = await db.Set<OurStockItem>().AsNoTracking()
                    .Where(l => l.PartNumberId == receiptLot.PartNumberId && l.Condition == receiptLot.Condition
                        && l.CompanyPresetId == receiptLot.CompanyPresetId && l.WarehouseId == fromWarehouse
                        && (l.CertName == receiptLot.CertName || (l.CertName == null && receiptLot.CertName == null)))
                    .Select(l => (long?)l.Id).FirstOrDefaultAsync(cancellationToken)
                    ?? throw new InvalidOperationException(
                        $"Transfer {leg.TransferNumber}: the stock for this part is not recorded at the sending warehouse, so it cannot be moved.");

                await ledger.TransferAsync(fromLot, toWarehouse, Math.Abs(delta), new StockMovementContext
                {
                    UserId = userId,
                    Reference = $"{leg.TransferNumber} / {leg.TrackNumber}",
                    Reason = delta > 0 ? "Warehouse transfer received" : "Warehouse transfer correction",
                    POId = item.POId,
                    POItemId = item.POItemId,
                    TrackNumberId = trackNumberId,
                    WarehouseTransferId = leg.TransferId,
                }, cancellationToken);
            }
            return true;
        }, cancellationToken);
    }

    public async Task<StockReceiptSummaryResponse?> ReceiveManualAsync(long poId, ManualStockReceiptRequest request, long userId,
        CancellationToken cancellationToken = default)
    {
        if (request.Lines.Count == 0) throw new InvalidOperationException("Add at least one line to receive.");
        if (request.Lines.Any(l => l.Qty <= 0)) throw new InvalidOperationException("Received quantities must be greater than zero.");
        if (request.Lines.GroupBy(l => l.POItemId).Any(g => g.Count() > 1))
            throw new InvalidOperationException("Each PO line can appear only once per receipt.");

        var received = await ledger.ExecuteAsync(async () =>
        {
            var po = await db.Set<PurchaseOrder>()
                .Include(p => p.POItems.Where(i => i.ReturnedAt == null))
                .FirstOrDefaultAsync(p => p.Id == poId && p.Origin == StockOrigin, cancellationToken);
            if (po is null) return false;
            if (!string.Equals(po.AdminApproval, "Approved", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The Stock PO must be approved before stock can be received.");
            if (po.Status is "Draft" or PurchaseOrderStatusFlow.Cancelled or PurchaseOrderStatusFlow.Returned)
                throw new InvalidOperationException($"A {po.Status} Stock PO cannot be received.");

            var warehouseId = request.WarehouseId ?? po.DestinationWarehouseId
                ?? throw new InvalidOperationException("Choose the warehouse the stock arrived at.");
            if (!await db.Set<Warehouse>().AnyAsync(w => w.Id == warehouseId && w.IsActive, cancellationToken))
                throw new InvalidOperationException("The receiving warehouse was not found or is inactive.");

            var receivedByLine = await ReceivedByLineAsync(po.POItems.Select(i => i.Id).ToList(), cancellationToken);
            var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
            foreach (var input in request.Lines)
            {
                var line = po.POItems.FirstOrDefault(i => i.Id == input.POItemId)
                    ?? throw new InvalidOperationException($"PO line {input.POItemId} does not belong to {po.PONumber}.");
                var remaining = line.Qty - receivedByLine.GetValueOrDefault(line.Id);
                if (input.Qty > remaining)
                    throw new InvalidOperationException(
                        $"Line {line.PORef}: only {Math.Max(0, remaining):0.##} of {line.Qty} is still to be received.");
                var serials = input.Serials?.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
                if (serials is { Count: > 0 } && serials.Count != input.Qty)
                    throw new InvalidOperationException(
                        $"Line {line.PORef}: {serials.Count} serial(s) were entered for a quantity of {input.Qty:0.##}.");

                var lot = await ledger.GetOrCreateLotAsync(LotKey(po, line, warehouseId, input.CertName), cancellationToken);
                if (!string.IsNullOrWhiteSpace(input.BinLocation)) lot.BinLocation = input.BinLocation.Trim();
                if (input.TagDate.HasValue) lot.TagDate = input.TagDate.Value.Date;
                await ledger.ReceiveAsync(lot, input.Qty, line.UnitPrice, OurStockMovementTypes.Receipt, new StockMovementContext
                {
                    UserId = userId,
                    Reference = $"{po.PONumber} / manual",
                    Reason = note,
                    POId = po.Id,
                    POItemId = line.Id,
                }, serials, cancellationToken);
                line.StockItemId = lot.Id;
            }

            await db.SaveChangesAsync(cancellationToken);
            await UpdatePoReceiptStatusAsync(po.Id, cancellationToken);
            return true;
        }, cancellationToken);

        return received ? await GetSummaryAsync(poId, cancellationToken) : null;
    }

    public async Task<StockReceiptSummaryResponse?> GetSummaryAsync(long poId, CancellationToken cancellationToken = default)
    {
        var po = await db.Set<PurchaseOrder>().AsNoTracking()
            .Include(p => p.POItems.Where(i => i.ReturnedAt == null)).ThenInclude(i => i.PartNumber)
            .FirstOrDefaultAsync(p => p.Id == poId && p.Origin == StockOrigin, cancellationToken);
        if (po is null) return null;

        var lineIds = po.POItems.Select(i => i.Id).ToList();
        var movements = await db.Set<OurStockMovement>().AsNoTracking()
            .Where(m => m.POItemId.HasValue && lineIds.Contains(m.POItemId.Value)
                && (m.Type == OurStockMovementTypes.Receipt || m.Type == OurStockMovementTypes.Adjust))
            .OrderBy(m => m.CreatedAt).ThenBy(m => m.Id)
            .Select(m => new
            {
                POItemId = m.POItemId!.Value,
                Row = new StockReceiptMovementResponse
                {
                    Id = m.Id, Type = m.Type, Qty = m.Qty, UnitCost = m.UnitCost, StockItemId = m.StockItemId,
                    WarehouseName = m.StockItem.Warehouse.DisplayName ?? m.StockItem.Warehouse.Name,
                    TrackNumberId = m.TrackNumberId,
                    TrackNumber = m.TrackNumber != null ? m.TrackNumber.TrackNumber : null,
                    Reference = m.Reference, Reason = m.Reason, CreatedByName = m.CreatedByUser.Name, CreatedAt = m.CreatedAt,
                },
            })
            .ToListAsync(cancellationToken);

        var movementIds = movements.Select(m => m.Row.Id).ToList();
        var serials = await db.Set<OurStockSerial>().AsNoTracking()
            .Where(s => s.ReceiptMovementId.HasValue && movementIds.Contains(s.ReceiptMovementId.Value))
            .Select(s => new { MovementId = s.ReceiptMovementId!.Value, s.SerialNumber })
            .ToListAsync(cancellationToken);
        foreach (var movement in movements)
            movement.Row.Serials = serials.Where(s => s.MovementId == movement.Row.Id).Select(s => s.SerialNumber).ToList();

        var lines = po.POItems.OrderBy(i => i.PORef).Select(line =>
        {
            var rows = movements.Where(m => m.POItemId == line.Id).Select(m => m.Row).ToList();
            var qtyReceived = rows.Sum(r => r.Qty);
            return new StockReceiptLineResponse
            {
                POItemId = line.Id, PORef = line.PORef ?? 0,
                PartNumberId = line.PartNumberId.GetValueOrDefault(), PartNumber = line.PartNumber?.Name ?? string.Empty,
                Condition = line.Condition ?? DefaultCondition, QtyOrdered = line.Qty,
                QtyReceived = qtyReceived, QtyRemaining = Math.Max(0, line.Qty - qtyReceived),
                StockItemId = line.StockItemId, Movements = rows,
            };
        }).ToList();

        return new StockReceiptSummaryResponse
        {
            POId = po.Id, PONumber = po.PONumber, Status = po.Status, DestinationWarehouseId = po.DestinationWarehouseId,
            FullyReceived = lines.Count > 0 && lines.All(l => l.QtyRemaining == 0),
            Lines = lines,
        };
    }

    public Task<int> AddSerialsAsync(long movementId, IReadOnlyCollection<string> serials, CancellationToken cancellationToken = default)
        => ledger.ExecuteAsync(() => ledger.AddSerialsAsync(movementId, serials, cancellationToken), cancellationToken);

    /// <summary>
    /// Line status follows the received quantity; the PO becomes Completed once every line is fully received.
    /// A PO an admin already completed (e.g. a short delivery that will not be topped up) is never reopened.
    /// </summary>
    private async Task UpdatePoReceiptStatusAsync(long poId, CancellationToken cancellationToken)
    {
        var po = await db.Set<PurchaseOrder>()
            .Include(p => p.POItems.Where(i => i.ReturnedAt == null))
            .FirstAsync(p => p.Id == poId, cancellationToken);
        if (po.POItems.Count == 0) return;

        var received = await ReceivedByLineAsync(po.POItems.Select(i => i.Id).ToList(), cancellationToken);
        foreach (var line in po.POItems)
        {
            var qty = received.GetValueOrDefault(line.Id);
            if (qty >= line.Qty) line.Status = PurchaseOrderStatusFlow.Completed;
            else if (qty > 0) line.Status = PurchaseOrderStatusFlow.ReceivedInWarehouse;
        }

        if (po.POItems.All(line => received.GetValueOrDefault(line.Id) >= line.Qty))
            po.Status = PurchaseOrderStatusFlow.Completed;
        else if (received.Values.Any(q => q > 0) && po.Status != PurchaseOrderStatusFlow.Completed)
            po.Status = PurchaseOrderStatusFlow.ReceivedInWarehouse;

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Dictionary<long, decimal>> ReceivedByLineAsync(List<long> poItemIds, CancellationToken cancellationToken)
        => await db.Set<OurStockMovement>().AsNoTracking()
            .Where(m => m.POItemId.HasValue && poItemIds.Contains(m.POItemId.Value)
                && (m.Type == OurStockMovementTypes.Receipt || m.Type == OurStockMovementTypes.Adjust))
            .GroupBy(m => m.POItemId!.Value)
            .Select(g => new { POItemId = g.Key, Qty = g.Sum(m => m.Qty) })
            .ToDictionaryAsync(x => x.POItemId, x => x.Qty, cancellationToken);

    private static StockLotKey LotKey(PurchaseOrder po, POItem line, long? warehouseId, string? certName)
        => new(
            line.PartNumberId ?? throw new InvalidOperationException($"{po.PONumber} line {line.PORef} has no part number."),
            string.IsNullOrWhiteSpace(line.Condition) ? DefaultCondition : line.Condition,
            warehouseId ?? po.DestinationWarehouseId
                ?? throw new InvalidOperationException($"{po.PONumber} has no destination warehouse and the track has none."),
            po.CompanyPresetId ?? throw new InvalidOperationException($"{po.PONumber} has no company preset, so the stock has no owner."),
            certName);
}

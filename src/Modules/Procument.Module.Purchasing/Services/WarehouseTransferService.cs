using Microsoft.EntityFrameworkCore;
using Procument.Module.Purchasing.DTOs;
using Procument.Module.Purchasing.Entities;
using Procument.Module.RFQ.Entities;
using Procument.Shared.Services;

namespace Procument.Module.Purchasing.Services;

public interface IWarehouseTransferService
{
    /// <summary>Stock verified at a warehouse and still free to move (not fully transferred, not on an SN#).</summary>
    Task<List<TransferableStockResponse>> GetTransferableStockAsync(long? warehouseId, IReadOnlyCollection<long>? restrictToWarehouseIds);

    Task<WarehouseTransferResponse> CreateAsync(long userId, CreateWarehouseTransferRequest request, IReadOnlyCollection<long>? restrictToWarehouseIds);

    Task<List<WarehouseTransferResponse>> GetAllAsync(long? warehouseId = null, string? status = null);
    Task<WarehouseTransferResponse?> GetByIdAsync(long id);
    Task<bool> CancelAsync(long id, long userId);

    /// <summary>
    /// Called after the destination warehouse submits quantities for a transfer-emitted track.
    /// Rolls the confirmed quantities up onto the transfer lines and header.
    /// </summary>
    Task SyncReceiptFromTrackAsync(long trackNumberId, long userId);

    /// <summary>Full journey of one part: supplier → warehouse(s) → shipment note → office/customer.</summary>
    Task<ShippingTraceResponse?> GetTraceAsync(long poItemId);
}

public class WarehouseTransferService : IWarehouseTransferService
{
    private readonly DbContext _db;
    private readonly INotificationService _notifications;

    /// <summary>SN statuses that mean the goods have landed at their final destination.</summary>
    private static readonly string[] TerminalSnStatuses = ["Received in Office", "Delivered to Customer"];

    public WarehouseTransferService(DbContext db, INotificationService notifications)
    {
        _db = db;
        _notifications = notifications;
    }

    // ── Transferable stock ────────────────────────────────────────────────

    public async Task<List<TransferableStockResponse>> GetTransferableStockAsync(
        long? warehouseId, IReadOnlyCollection<long>? restrictToWarehouseIds)
    {
        // Stock already committed to a shipment note has left the warehouse's free pool.
        var committedTrackIds = await _db.Set<ShipmentNoteTrackNumber>()
            .Select(s => s.TrackNumberId)
            .Distinct()
            .ToListAsync();

        var query =
            from item in _db.Set<TrackNumberItem>().AsNoTracking()
            join track in _db.Set<POItemTrackNumber>().AsNoTracking() on item.TrackNumberId equals track.Id
            join warehouse in _db.Set<Warehouse>().AsNoTracking() on track.WarehouseId equals (long?)warehouse.Id into wh
            from warehouse in wh.DefaultIfEmpty()
            join poItem in _db.Set<POItem>().AsNoTracking() on item.POItemId equals poItem.Id
            join po in _db.Set<PurchaseOrder>().AsNoTracking() on poItem.POId equals (long?)po.Id into pos
            from po in pos.DefaultIfEmpty()
            join partNumber in _db.Set<Procument.Module.Catalog.Entities.PartNumber>().AsNoTracking()
                on poItem.PartNumberId equals (long?)partNumber.Id into pn
            from partNumber in pn.DefaultIfEmpty()
            join procItem in _db.Set<ProcurementItem>().AsNoTracking()
                on (long?)poItem.SourceProcurementItemId equals (long?)procItem.Id into pi
            from procItem in pi.DefaultIfEmpty()
            join rfq in _db.Set<RFQHeader>().AsNoTracking() on (long?)procItem.SourceRfqId equals (long?)rfq.Id into rfqs
            from rfq in rfqs.DefaultIfEmpty()
            join customer in _db.Set<Procument.Module.Catalog.Entities.Customer>().AsNoTracking()
                on (long?)rfq.CustomerId equals (long?)customer.Id into cu
            from customer in cu.DefaultIfEmpty()
            where item.Status == "Accepted"
                  && item.ActualQty.HasValue
                  && item.ActualQty.Value > item.TransferredOutQty
                  && track.WarehouseId.HasValue
                  && !committedTrackIds.Contains(item.TrackNumberId)
            select new TransferableStockResponse
            {
                TrackNumberItemId = item.Id,
                TrackNumberId = item.TrackNumberId,
                TrackNumber = track.TrackNumber,
                POItemId = item.POItemId,
                POId = poItem.POId,
                PONumber = po != null ? po.PONumber : null,
                PartNumberName = partNumber != null ? partNumber.Name : null,
                PartDescription = partNumber != null ? partNumber.Description : null,
                Condition = poItem.Condition,
                WarehouseId = track.WarehouseId,
                WarehouseName = warehouse != null ? warehouse.Name : null,
                CustomerName = customer != null ? customer.Name : null,
                CustomerCode = customer != null ? customer.CustomerCode : null,
                ActualQty = item.ActualQty!.Value,
                TransferredOutQty = item.TransferredOutQty,
                AvailableQty = item.ActualQty!.Value - item.TransferredOutQty,
            };

        if (warehouseId.HasValue)
            query = query.Where(i => i.WarehouseId == warehouseId.Value);

        if (restrictToWarehouseIds != null)
        {
            var allowed = restrictToWarehouseIds.ToList();
            query = query.Where(i => i.WarehouseId.HasValue && allowed.Contains(i.WarehouseId.Value));
        }

        return await query.OrderBy(i => i.PartNumberName).ToListAsync();
    }

    // ── Create ────────────────────────────────────────────────────────────

    public async Task<WarehouseTransferResponse> CreateAsync(
        long userId, CreateWarehouseTransferRequest request, IReadOnlyCollection<long>? restrictToWarehouseIds)
    {
        if (request.FromWarehouseId == request.ToWarehouseId)
            throw new InvalidOperationException("Source and destination warehouse must be different.");

        if (string.IsNullOrWhiteSpace(request.TrackNumber))
            throw new InvalidOperationException("A track number is required for the transfer.");

        if (request.Items == null || request.Items.Count == 0)
            throw new InvalidOperationException("Select at least one item to transfer.");

        if (restrictToWarehouseIds != null && !restrictToWarehouseIds.Contains(request.FromWarehouseId))
            throw new UnauthorizedAccessException("You are not assigned to the source warehouse.");

        var fromWarehouse = await _db.Set<Warehouse>().FindAsync(request.FromWarehouseId)
            ?? throw new KeyNotFoundException("Source warehouse not found.");
        var toWarehouse = await _db.Set<Warehouse>().FindAsync(request.ToWarehouseId)
            ?? throw new KeyNotFoundException("Destination warehouse not found.");

        // Collapse duplicate lines for the same source item up front — otherwise each would be
        // validated against the full available quantity and their sum could overdraw the stock.
        var lines = request.Items
            .GroupBy(i => i.SourceTrackNumberItemId)
            .Select(g => new WarehouseTransferItemInput
            {
                SourceTrackNumberItemId = g.Key,
                Qty = g.Sum(i => i.Qty),
            })
            .ToList();

        var sourceItemIds = lines.Select(i => i.SourceTrackNumberItemId).ToList();

        var sourceItems = await _db.Set<TrackNumberItem>()
            .Include(i => i.TrackNumber)
            .Include(i => i.POItem).ThenInclude(p => p.PartNumber)
            .Where(i => sourceItemIds.Contains(i.Id))
            .ToListAsync();

        if (sourceItems.Count != sourceItemIds.Count)
            throw new KeyNotFoundException("One or more selected items no longer exist.");

        var committedTrackIds = await _db.Set<ShipmentNoteTrackNumber>()
            .Where(s => sourceItems.Select(i => i.TrackNumberId).Contains(s.TrackNumberId))
            .Select(s => s.TrackNumberId)
            .Distinct()
            .ToListAsync();

        // ── Validate every line before writing anything ──
        foreach (var input in lines)
        {
            var source = sourceItems.First(i => i.Id == input.SourceTrackNumberItemId);
            var partName = source.POItem?.PartNumber?.Name ?? $"item #{source.Id}";

            if (input.Qty <= 0)
                throw new InvalidOperationException($"Quantity for {partName} must be greater than zero.");

            if (source.TrackNumber?.WarehouseId != request.FromWarehouseId)
                throw new InvalidOperationException($"{partName} is not held at {fromWarehouse.Name}.");

            if (source.Status != "Accepted" || !source.ActualQty.HasValue)
                throw new InvalidOperationException($"{partName} has not been verified and accepted yet, so it cannot be transferred.");

            if (committedTrackIds.Contains(source.TrackNumberId))
                throw new InvalidOperationException($"{partName} is already assigned to a shipment note and cannot be transferred.");

            var available = source.ActualQty.Value - source.TransferredOutQty;
            if (input.Qty > available)
                throw new InvalidOperationException($"Only {available} unit(s) of {partName} are available to transfer (requested {input.Qty}).");
        }

        // ── Header ──
        var year = DateTime.UtcNow.Year;
        var prefix = $"WT-{year}-";
        var existingCount = await _db.Set<WarehouseTransfer>().CountAsync(t => t.TransferNumber.StartsWith(prefix));

        var transfer = new WarehouseTransfer
        {
            TransferNumber = $"{prefix}{(existingCount + 1):000}",
            FromWarehouseId = request.FromWarehouseId,
            ToWarehouseId = request.ToWarehouseId,
            TrackNumber = request.TrackNumber.Trim(),
            Carrier = request.Carrier?.Trim(),
            Notes = request.Notes?.Trim(),
            Status = "In Transit",
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = userId,
        };
        _db.Set<WarehouseTransfer>().Add(transfer);
        await _db.SaveChangesAsync();

        // ── Lines + stock decrement at the source ──
        foreach (var input in lines)
        {
            var source = sourceItems.First(i => i.Id == input.SourceTrackNumberItemId);

            _db.Set<WarehouseTransferItem>().Add(new WarehouseTransferItem
            {
                WarehouseTransferId = transfer.Id,
                SourceTrackNumberItemId = source.Id,
                POItemId = source.POItemId,
                Qty = input.Qty,
                Status = "In Transit",
                CreatedAt = DateTime.UtcNow,
            });

            // Keeps these units out of Ready-for-SN at the source warehouse.
            source.TransferredOutQty += input.Qty;
        }

        // ── Destination legs: one track per distinct POItem, all sharing the carrier track number ──
        // (POItemTrackNumber holds a single POItemId, and the UI groups rows by track-number string,
        //  so a multi-part transfer renders as one card at the destination.)
        var byPoItem = lines
            .Select(input => new { Input = input, Source = sourceItems.First(s => s.Id == input.SourceTrackNumberItemId) })
            .GroupBy(x => x.Source.POItemId)
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    Qty = g.Sum(x => x.Input.Qty),
                    ParentTrackId = g.First().Source.TrackNumberId,
                });

        foreach (var (poItemId, info) in byPoItem)
        {
            _db.Set<POItemTrackNumber>().Add(new POItemTrackNumber
            {
                TrackNumber = transfer.TrackNumber,
                Carrier = transfer.Carrier,
                Notes = $"Transfer {transfer.TransferNumber}: {fromWarehouse.Name} → {toWarehouse.Name}",
                WarehouseId = request.ToWarehouseId,
                Status = "Ship to Warehouse",
                Origin = "Transfer",
                SourceTransferId = transfer.Id,
                ParentTrackNumberId = info.ParentTrackId,
                POItemId = poItemId,
                CreatedAt = DateTime.UtcNow,
                // Pre-seed the receipt line so the destination user only fills in the actual quantity.
                // Adding it through the navigation lets EF assign TrackNumberId, so the whole
                // batch lands in one SaveChanges.
                Items = new List<TrackNumberItem>
                {
                    new()
                    {
                        POItemId = poItemId,
                        ExpectedQty = info.Qty,
                        Status = "Pending",
                        CreatedAt = DateTime.UtcNow,
                    },
                },
            });
        }

        // Single save: transfer lines, the source stock decrement, and the destination legs commit
        // together, so a failure cannot leave stock deducted with nowhere for it to arrive.
        await _db.SaveChangesAsync();

        // ── Notify the receiving warehouse ──
        var destUserIds = await _db.Set<UserWarehouse>()
            .Where(uw => uw.WarehouseId == request.ToWarehouseId)
            .Select(uw => uw.UserId)
            .ToListAsync();

        if (destUserIds.Count > 0)
        {
            var actor = await _db.Set<Module.Identity.Entities.User>().FindAsync(userId);
            await _notifications.CreateForUsersAsync(
                destUserIds, "WarehouseTransferIncoming", "WarehouseTransfer", transfer.Id, transfer.TransferNumber,
                $"Incoming transfer {transfer.TransferNumber} from {fromWarehouse.Name} — track {transfer.TrackNumber}",
                userId, actor?.Name);
        }

        return (await GetByIdAsync(transfer.Id))!;
    }

    // ── Read ──────────────────────────────────────────────────────────────

    public async Task<List<WarehouseTransferResponse>> GetAllAsync(long? warehouseId = null, string? status = null)
    {
        var query = BaseQuery();

        if (warehouseId.HasValue)
            query = query.Where(t => t.FromWarehouseId == warehouseId.Value || t.ToWarehouseId == warehouseId.Value);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(t => t.Status == status);

        var transfers = await query.OrderByDescending(t => t.CreatedAt).ToListAsync();
        return transfers.Select(MapTransfer).ToList();
    }

    public async Task<WarehouseTransferResponse?> GetByIdAsync(long id)
    {
        var transfer = await BaseQuery().FirstOrDefaultAsync(t => t.Id == id);
        return transfer == null ? null : MapTransfer(transfer);
    }

    private IQueryable<WarehouseTransfer> BaseQuery() => _db.Set<WarehouseTransfer>()
        .AsNoTracking()
        .Include(t => t.FromWarehouse)
        .Include(t => t.ToWarehouse)
        .Include(t => t.CreatedBy)
        .Include(t => t.ReceivedBy)
        .Include(t => t.DestinationTracks)
        .Include(t => t.Items).ThenInclude(i => i.POItem).ThenInclude(p => p.PartNumber)
        .Include(t => t.Items).ThenInclude(i => i.POItem).ThenInclude(p => p.PurchaseOrder);

    // ── Cancel ────────────────────────────────────────────────────────────

    public async Task<bool> CancelAsync(long id, long userId)
    {
        var transfer = await _db.Set<WarehouseTransfer>()
            .Include(t => t.Items)
            .Include(t => t.DestinationTracks).ThenInclude(d => d.Items)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (transfer == null) return false;

        if (transfer.Status is "Received" or "Cancelled")
            throw new InvalidOperationException($"Transfer {transfer.TransferNumber} is {transfer.Status} and can no longer be cancelled.");

        if (transfer.DestinationTracks.Any(d => d.Items.Any(i => i.ActualQty.HasValue)))
            throw new InvalidOperationException("The destination warehouse has already started receiving this transfer.");

        // Return the reserved units to the source warehouse's free pool.
        var sourceItemIds = transfer.Items.Select(i => i.SourceTrackNumberItemId).ToList();
        var sourceItems = await _db.Set<TrackNumberItem>().Where(i => sourceItemIds.Contains(i.Id)).ToListAsync();

        foreach (var line in transfer.Items)
        {
            var source = sourceItems.FirstOrDefault(i => i.Id == line.SourceTrackNumberItemId);
            if (source != null)
                source.TransferredOutQty = Math.Max(0, source.TransferredOutQty - line.Qty);
            line.Status = "Cancelled";
        }

        // Drop the destination legs — nothing was received against them.
        foreach (var dest in transfer.DestinationTracks)
        {
            _db.Set<TrackNumberItem>().RemoveRange(dest.Items);
            _db.Set<POItemTrackNumber>().Remove(dest);
        }

        transfer.Status = "Cancelled";
        await _db.SaveChangesAsync();
        return true;
    }

    // ── Receipt sync (called from ShippingService when the destination submits) ──

    public async Task SyncReceiptFromTrackAsync(long trackNumberId, long userId)
    {
        var track = await _db.Set<POItemTrackNumber>()
            .Include(t => t.Items)
            .FirstOrDefaultAsync(t => t.Id == trackNumberId);

        if (track?.SourceTransferId == null) return;

        var transfer = await _db.Set<WarehouseTransfer>()
            .Include(t => t.Items)
            .Include(t => t.DestinationTracks).ThenInclude(d => d.Items)
            .FirstOrDefaultAsync(t => t.Id == track.SourceTransferId.Value);

        if (transfer == null || transfer.Status == "Cancelled") return;

        // Roll the destination receipt quantities back onto the transfer lines, per part.
        var receivedByPoItem = transfer.DestinationTracks
            .SelectMany(d => d.Items)
            .Where(i => i.ActualQty.HasValue)
            .GroupBy(i => i.POItemId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.ActualQty!.Value));

        foreach (var line in transfer.Items.Where(i => i.Status != "Cancelled"))
        {
            if (!receivedByPoItem.TryGetValue(line.POItemId, out var received)) continue;

            // Several lines can share one POItem; hand each its share in order.
            var take = Math.Min(line.Qty, received);
            line.ReceivedQty = take;
            line.Status = take >= line.Qty ? "Received" : "Short";
            receivedByPoItem[line.POItemId] = received - take;
        }

        var active = transfer.Items.Where(i => i.Status != "Cancelled").ToList();
        var allSettled = active.Count > 0 && active.All(i => i.ReceivedQty.HasValue);

        if (allSettled)
        {
            transfer.Status = "Received";
            // A re-submit re-runs this sync; keep the original completion stamp.
            transfer.ReceivedAt ??= DateTime.UtcNow;
            transfer.ReceivedByUserId ??= userId;
        }
        else if (active.Any(i => i.ReceivedQty.HasValue))
        {
            transfer.Status = "Partially Received";
        }

        await _db.SaveChangesAsync();
    }

    // ── Trace ─────────────────────────────────────────────────────────────

    public async Task<ShippingTraceResponse?> GetTraceAsync(long poItemId)
    {
        var poItem = await _db.Set<POItem>()
            .AsNoTracking()
            .Include(p => p.PartNumber)
            .Include(p => p.PurchaseOrder).ThenInclude(po => po!.Supplier)
            .FirstOrDefaultAsync(p => p.Id == poItemId);

        if (poItem == null) return null;

        var tracks = await _db.Set<POItemTrackNumber>()
            .AsNoTracking()
            .Include(t => t.Warehouse)
            .Include(t => t.Items)
            .Include(t => t.SourceTransfer).ThenInclude(tr => tr!.FromWarehouse)
            .Include(t => t.SourceTransfer).ThenInclude(tr => tr!.ToWarehouse)
            .Include(t => t.SourceTransfer).ThenInclude(tr => tr!.CreatedBy)
            .Include(t => t.SourceTransfer).ThenInclude(tr => tr!.ReceivedBy)
            .Include(t => t.SourceTransfer).ThenInclude(tr => tr!.Items)
            .Where(t => t.POItemId == poItemId)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync();

        var customer = await (
            from pi in _db.Set<ProcurementItem>().AsNoTracking()
            join rfq in _db.Set<RFQHeader>().AsNoTracking() on pi.SourceRfqId equals rfq.Id
            join c in _db.Set<Procument.Module.Catalog.Entities.Customer>().AsNoTracking() on rfq.CustomerId equals c.Id
            where pi.Id == poItem.SourceProcurementItemId
            select new { c.Name, c.CustomerCode }
        ).FirstOrDefaultAsync();

        var legs = new List<ShippingTraceLeg>();

        foreach (var track in tracks)
        {
            var isTransfer = track.Origin == "Transfer" && track.SourceTransfer != null;
            var transfer = track.SourceTransfer;

            legs.Add(new ShippingTraceLeg
            {
                LegType = isTransfer ? "Transfer" : "Inbound",
                FromName = isTransfer
                    ? transfer!.FromWarehouse?.Name
                    : poItem.PurchaseOrder?.Supplier?.Name ?? "Supplier",
                ToName = isTransfer ? transfer!.ToWarehouse?.Name : track.Warehouse?.Name,
                FromWarehouseId = isTransfer ? transfer!.FromWarehouseId : null,
                ToWarehouseId = track.WarehouseId,
                TrackNumber = track.TrackNumber,
                Carrier = track.Carrier,
                Reference = isTransfer ? transfer!.TransferNumber : null,
                ReferenceId = isTransfer ? transfer!.Id : null,
                TrackNumberId = track.Id,
                Status = track.Status,
                Qty = isTransfer
                    ? transfer!.Items.Where(i => i.POItemId == poItemId && i.Status != "Cancelled").Sum(i => i.Qty)
                    : track.Items.Sum(i => i.ActualQty ?? i.ExpectedQty),
                StartedAt = track.CreatedAt,
                CompletedAt = isTransfer
                    ? transfer!.ReceivedAt
                    : track.Items.Where(i => i.ReviewedAt.HasValue).Max(i => (DateTime?)i.ReviewedAt),
                ActorName = isTransfer ? transfer!.CreatedBy?.Name : null,
                Notes = isTransfer ? transfer!.Notes : track.Notes,
            });
        }

        // ── Shipment note legs (warehouse → office / customer) ──
        var trackIds = tracks.Select(t => t.Id).ToList();

        // Resolve ids first — an Include followed by a Select is dropped by EF, which would
        // silently null out Warehouse/CreatedBy on the shipment legs.
        var noteIds = await _db.Set<ShipmentNoteTrackNumber>()
            .AsNoTracking()
            .Where(s => trackIds.Contains(s.TrackNumberId))
            .Select(s => s.ShipmentNoteId)
            .Distinct()
            .ToListAsync();

        var notes = await _db.Set<ShipmentNote>()
            .AsNoTracking()
            .Include(n => n.Warehouse)
            .Include(n => n.CreatedBy)
            .Where(n => noteIds.Contains(n.Id))
            .OrderBy(n => n.CreatedAt)
            .ToListAsync();

        foreach (var note in notes)
        {
            legs.Add(new ShippingTraceLeg
            {
                LegType = "Shipment",
                FromName = note.Warehouse?.Name,
                ToName = note.Destination ?? (note.Type == "CPT" ? "Customer" : "Office"),
                FromWarehouseId = note.WarehouseId,
                TrackNumber = note.AWBNumber,
                Reference = note.SNNumber,
                ReferenceId = note.Id,
                Status = note.Status,
                StartedAt = note.CreatedAt,
                CompletedAt = TerminalSnStatuses.Contains(note.Status) ? note.CustomsUploadedAt : null,
                ActorName = note.CreatedBy?.Name,
                IsTerminal = TerminalSnStatuses.Contains(note.Status),
            });
        }

        var ordered = legs.OrderBy(l => l.StartedAt ?? DateTime.MaxValue).ToList();
        for (var i = 0; i < ordered.Count; i++)
            ordered[i].Sequence = i + 1;

        var lastLeg = ordered.LastOrDefault();

        return new ShippingTraceResponse
        {
            POItemId = poItemId,
            PartNumberName = poItem.PartNumber?.Name,
            PartDescription = poItem.PartNumber?.Description,
            PONumber = poItem.PurchaseOrder?.PONumber,
            SupplierName = poItem.PurchaseOrder?.Supplier?.Name,
            CustomerName = customer?.Name,
            CustomerCode = customer?.CustomerCode,
            Legs = ordered,
            IsComplete = ordered.Any(l => l.IsTerminal),
            FinalStatus = lastLeg?.Status,
        };
    }

    // ── Mapping ───────────────────────────────────────────────────────────

    private static WarehouseTransferResponse MapTransfer(WarehouseTransfer t) => new()
    {
        Id = t.Id,
        TransferNumber = t.TransferNumber,
        FromWarehouseId = t.FromWarehouseId,
        FromWarehouseName = t.FromWarehouse?.Name,
        ToWarehouseId = t.ToWarehouseId,
        ToWarehouseName = t.ToWarehouse?.Name,
        TrackNumber = t.TrackNumber,
        Carrier = t.Carrier,
        Notes = t.Notes,
        Status = t.Status,
        CreatedAt = t.CreatedAt,
        CreatedByUserId = t.CreatedByUserId,
        CreatedByName = t.CreatedBy?.Name,
        ReceivedAt = t.ReceivedAt,
        ReceivedByName = t.ReceivedBy?.Name,
        TotalQty = t.Items?.Where(i => i.Status != "Cancelled").Sum(i => i.Qty) ?? 0,
        ReceivedQty = t.Items?.Sum(i => i.ReceivedQty ?? 0) ?? 0,
        DestinationTrackNumberIds = t.DestinationTracks?.Select(d => d.Id).ToList() ?? new(),
        Items = t.Items?.Select(i => new WarehouseTransferItemResponse
        {
            Id = i.Id,
            WarehouseTransferId = i.WarehouseTransferId,
            SourceTrackNumberItemId = i.SourceTrackNumberItemId,
            POItemId = i.POItemId,
            PartNumberName = i.POItem?.PartNumber?.Name,
            PartDescription = i.POItem?.PartNumber?.Description,
            PONumber = i.POItem?.PurchaseOrder?.PONumber,
            Qty = i.Qty,
            ReceivedQty = i.ReceivedQty,
            Status = i.Status,
        }).ToList() ?? new(),
    };
}

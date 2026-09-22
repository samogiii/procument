using Microsoft.EntityFrameworkCore;
using Procument.Module.Catalog.Entities;
using Procument.Module.OurInventory.DTOs;
using Procument.Module.OurInventory.Entities;
using Procument.Module.Purchasing.Entities;
using Procument.Module.Purchasing.Services;
using Procument.Shared.DTOs;

namespace Procument.Module.OurInventory.Services;

public interface IStockQueryService
{
    Task<PagedResult<StockItemResponse>> GetAllAsync(StockItemQuery query, bool includeCost);
    Task<StockItemFilterOptions> GetFilterOptionsAsync(StockItemQuery query);
    Task<StockItemDetailResponse?> GetByIdAsync(long id, bool includeCost);
    Task<PagedResult<StockMovementResponse>> GetMovementsAsync(StockMovementQuery query, bool includeCost);
    Task<List<IncomingStockLineResponse>> GetIncomingAsync(long? warehouseId = null, long? partNumberId = null);
    Task<StockValuationResponse> GetValuationAsync(StockItemQuery query, bool includeCost);
    Task<StockReceiptNoteResponse?> GetReceiptNoteAsync(long poId, IReadOnlyCollection<long>? movementIds = null);
}

/// <summary>Read side of Our Stock. Cost fields are only filled when the caller may see them.</summary>
public sealed class StockQueryService(DbContext db) : IStockQueryService
{
    private static readonly string[] ClosedPoStatuses =
        ["Draft", PurchaseOrderStatusFlow.Cancelled, PurchaseOrderStatusFlow.Returned, PurchaseOrderStatusFlow.Completed];

    public async Task<PagedResult<StockItemResponse>> GetAllAsync(StockItemQuery query, bool includeCost)
    {
        var filtered = ApplyFilters(db.Set<OurStockItem>().AsNoTracking(), query);
        var count = await filtered.CountAsync();
        decimal? value = includeCost ? await filtered.SumAsync(i => i.QtyOnHand * i.AvgUnitCost) : null;
        var rows = await Project(filtered
                .OrderBy(i => i.PartNumber.Name).ThenBy(i => i.Condition).ThenBy(i => i.Id)
                .ApplyPaging(query), includeCost)
            .ToListAsync();
        return new PagedResult<StockItemResponse>
        {
            Items = rows, TotalCount = count, Page = query.Page, PageSize = query.PageSize, TotalAmountSum = value,
        };
    }

    public async Task<StockItemFilterOptions> GetFilterOptionsAsync(StockItemQuery query)
    {
        var root = db.Set<OurStockItem>().AsNoTracking();
        var warehouseIds = await ApplyFilters(root, query, "warehouse").Select(i => i.WarehouseId).Distinct().ToListAsync();
        var presetIds = await ApplyFilters(root, query, "preset").Select(i => i.CompanyPresetId).Distinct().ToListAsync();
        return new StockItemFilterOptions
        {
            Warehouses = await db.Set<Warehouse>().Where(w => warehouseIds.Contains(w.Id))
                .OrderBy(w => w.DisplayName ?? w.Name).Select(w => new StockOption(w.Id, w.DisplayName ?? w.Name)).ToListAsync(),
            CompanyPresets = await db.Set<CompanyPreset>().Where(p => presetIds.Contains(p.Id))
                .OrderBy(p => p.Name).Select(p => new StockOption(p.Id, p.Name)).ToListAsync(),
            Conditions = await ApplyFilters(root, query, "condition").Select(i => i.Condition).Distinct().OrderBy(c => c).ToListAsync(),
        };
    }

    public async Task<StockItemDetailResponse?> GetByIdAsync(long id, bool includeCost)
    {
        var item = await Project(db.Set<OurStockItem>().AsNoTracking().Where(i => i.Id == id), includeCost).FirstOrDefaultAsync();
        if (item is null) return null;

        return new StockItemDetailResponse
        {
            Item = item,
            Reservations = await db.Set<OurStockReservation>().AsNoTracking()
                .Where(r => r.StockItemId == id && r.Status == OurStockReservationStatuses.Active)
                .OrderBy(r => r.CreatedAt)
                .Select(r => new StockReservationResponse
                {
                    Id = r.Id, Qty = r.Qty, Status = r.Status, InvoiceItemId = r.InvoiceItemId, QuoteItemId = r.QuoteItemId,
                    RFQItemId = r.RFQItemId, ExpiresAt = r.ExpiresAt, CreatedByName = r.CreatedByUser.Name, CreatedAt = r.CreatedAt,
                }).ToListAsync(),
            Incoming = await GetIncomingAsync(partNumberId: item.PartNumberId),
            Serials = await db.Set<OurStockSerial>().AsNoTracking()
                .Where(s => s.StockItemId == id && s.Status != OurStockSerialStatuses.Issued)
                .OrderBy(s => s.SerialNumber)
                .Select(s => new StockSerialResponse { Id = s.Id, SerialNumber = s.SerialNumber, Status = s.Status })
                .ToListAsync(),
        };
    }

    public async Task<PagedResult<StockMovementResponse>> GetMovementsAsync(StockMovementQuery query, bool includeCost)
    {
        var q = db.Set<OurStockMovement>().AsNoTracking();
        if (query.StockItemId.HasValue) q = q.Where(m => m.StockItemId == query.StockItemId.Value);
        if (query.PartNumberId.HasValue) q = q.Where(m => m.StockItem.PartNumberId == query.PartNumberId.Value);
        if (query.Types?.Count > 0) q = q.Where(m => query.Types.Contains(m.Type));
        if (query.From.HasValue) q = q.Where(m => m.CreatedAt >= query.From.Value);
        if (query.To.HasValue) q = q.Where(m => m.CreatedAt < query.To.Value.Date.AddDays(1));
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            q = q.Where(m => m.Reference.Contains(term) || m.StockItem.PartNumber.Name.Contains(term)
                || (m.Reason != null && m.Reason.Contains(term)));
        }

        var count = await q.CountAsync();
        var rows = await q.OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id)
            .ApplyPaging(query)
            .Select(m => new StockMovementResponse
            {
                Id = m.Id, StockItemId = m.StockItemId, PartNumber = m.StockItem.PartNumber.Name,
                Condition = m.StockItem.Condition,
                WarehouseName = m.StockItem.Warehouse.DisplayName ?? m.StockItem.Warehouse.Name,
                Type = m.Type, Qty = m.Qty, UnitCost = includeCost ? m.UnitCost : null,
                POId = m.POId, PONumber = m.PurchaseOrder != null ? m.PurchaseOrder.PONumber : null,
                TrackNumberId = m.TrackNumberId, TrackNumber = m.TrackNumber != null ? m.TrackNumber.TrackNumber : null,
                InvoiceItemId = m.InvoiceItemId, Reference = m.Reference, Reason = m.Reason,
                CreatedByName = m.CreatedByUser.Name, CreatedAt = m.CreatedAt,
            }).ToListAsync();
        return new PagedResult<StockMovementResponse> { Items = rows, TotalCount = count, Page = query.Page, PageSize = query.PageSize };
    }

    /// <summary>Approved Stock PO lines that still have quantity to arrive.</summary>
    public async Task<List<IncomingStockLineResponse>> GetIncomingAsync(long? warehouseId = null, long? partNumberId = null)
    {
        var lines =
            from line in db.Set<POItem>().AsNoTracking()
            where line.ReturnedAt == null && line.PurchaseOrder!.Origin == "Stock"
                  && line.PurchaseOrder.AdminApproval == "Approved"
                  && !ClosedPoStatuses.Contains(line.PurchaseOrder.Status)
            let received = db.Set<OurStockMovement>()
                .Where(m => m.POItemId == line.Id
                    && (m.Type == OurStockMovementTypes.Receipt || m.Type == OurStockMovementTypes.Adjust))
                .Sum(m => (decimal?)m.Qty) ?? 0
            where line.Qty > received
            select new { line, received };
        if (warehouseId.HasValue) lines = lines.Where(x => x.line.PurchaseOrder!.DestinationWarehouseId == warehouseId.Value);
        if (partNumberId.HasValue) lines = lines.Where(x => x.line.PartNumberId == partNumberId.Value);

        var rows = await lines
            .OrderBy(x => x.line.PurchaseOrder!.ExpectedDeliveryDate ?? DateTime.MaxValue)
            .ThenBy(x => x.line.POId).ThenBy(x => x.line.PORef)
            .Select(x => new IncomingStockLineResponse
            {
                POId = x.line.POId!.Value, PONumber = x.line.PurchaseOrder!.PONumber, POStatus = x.line.PurchaseOrder.Status,
                POItemId = x.line.Id, PORef = x.line.PORef ?? 0,
                PartNumberId = x.line.PartNumberId ?? 0,
                PartNumber = x.line.PartNumber != null ? x.line.PartNumber.Name : string.Empty,
                Description = x.line.PartNumber != null ? x.line.PartNumber.Description : null,
                Condition = x.line.Condition ?? "New",
                SupplierName = x.line.PurchaseOrder.Supplier.Name,
                DestinationWarehouseId = x.line.PurchaseOrder.DestinationWarehouseId,
                DestinationWarehouseName = x.line.PurchaseOrder.DestinationWarehouse != null
                    ? x.line.PurchaseOrder.DestinationWarehouse.DisplayName ?? x.line.PurchaseOrder.DestinationWarehouse.Name
                    : string.Empty,
                QtyOrdered = x.line.Qty, QtyReceived = x.received, QtyRemaining = x.line.Qty - x.received,
                ExpectedDeliveryDate = x.line.PurchaseOrder.ExpectedDeliveryDate,
            }).ToListAsync();

        var presetIds = await db.Set<PurchaseOrder>().AsNoTracking()
            .Where(p => rows.Select(r => r.POId).Contains(p.Id) && p.CompanyPresetId.HasValue)
            .Select(p => new { p.Id, PresetId = p.CompanyPresetId!.Value }).ToListAsync();
        var presetNames = await db.Set<CompanyPreset>().AsNoTracking()
            .Where(p => presetIds.Select(x => x.PresetId).Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Name);
        foreach (var row in rows)
        {
            var preset = presetIds.FirstOrDefault(p => p.Id == row.POId);
            row.CompanyPresetName = preset is null ? string.Empty : presetNames.GetValueOrDefault(preset.PresetId, string.Empty);
        }
        return rows;
    }

    /// <summary>Every lot matching the filters (no paging), grouped by owner company and warehouse.</summary>
    public async Task<StockValuationResponse> GetValuationAsync(StockItemQuery query, bool includeCost)
    {
        var items = await Project(ApplyFilters(db.Set<OurStockItem>().AsNoTracking(), query)
                .OrderBy(i => i.PartNumber.Name).ThenBy(i => i.Condition).ThenBy(i => i.Id), includeCost)
            .ToListAsync();

        var groups = items
            .GroupBy(i => new { i.CompanyPresetId, i.CompanyPresetName, i.WarehouseId, i.WarehouseName })
            .OrderBy(g => g.Key.CompanyPresetName).ThenBy(g => g.Key.WarehouseName)
            .Select(g => new StockValuationGroup
            {
                CompanyPresetId = g.Key.CompanyPresetId, CompanyPresetName = g.Key.CompanyPresetName,
                WarehouseId = g.Key.WarehouseId, WarehouseName = g.Key.WarehouseName,
                QtyOnHand = g.Sum(i => i.QtyOnHand), QtyReserved = g.Sum(i => i.QtyReserved),
                Value = includeCost ? g.Sum(i => i.TotalAmount ?? 0) : null,
                Items = g.ToList(),
            }).ToList();

        return new StockValuationResponse
        {
            GeneratedAt = DateTime.UtcNow, IncludesCost = includeCost, LotCount = items.Count,
            TotalQtyOnHand = items.Sum(i => i.QtyOnHand), TotalQtyReserved = items.Sum(i => i.QtyReserved),
            TotalValue = includeCost ? items.Sum(i => i.TotalAmount ?? 0) : null,
            Groups = groups,
        };
    }

    /// <summary>
    /// Goods Receipt Note for a Stock PO. Without <paramref name="movementIds"/> it covers every receipt so far;
    /// with them, only those receipts (e.g. the one just booked). Quantities include recount corrections.
    /// </summary>
    public async Task<StockReceiptNoteResponse?> GetReceiptNoteAsync(long poId, IReadOnlyCollection<long>? movementIds = null)
    {
        var po = await db.Set<PurchaseOrder>().AsNoTracking()
            .Include(p => p.Supplier)
            .Include(p => p.POItems.Where(i => i.ReturnedAt == null)).ThenInclude(i => i.PartNumber)
            .FirstOrDefaultAsync(p => p.Id == poId && p.Origin == "Stock");
        if (po is null) return null;

        var lineIds = po.POItems.Select(i => i.Id).ToList();
        var all = await db.Set<OurStockMovement>().AsNoTracking()
            .Where(m => m.POItemId.HasValue && lineIds.Contains(m.POItemId.Value)
                && (m.Type == OurStockMovementTypes.Receipt || m.Type == OurStockMovementTypes.Adjust))
            .Select(m => new
            {
                m.Id, POItemId = m.POItemId!.Value, m.Qty, m.CreatedAt, m.Reason, m.StockItemId,
                CreatedBy = m.CreatedByUser.Name,
                TrackNumber = m.TrackNumber != null ? m.TrackNumber.TrackNumber : null,
                WarehouseName = m.StockItem.Warehouse.DisplayName ?? m.StockItem.Warehouse.Name,
                m.StockItem.CertName, m.StockItem.BinLocation,
            })
            .ToListAsync();

        var selected = movementIds is { Count: > 0 } ? all.Where(m => movementIds.Contains(m.Id)).ToList() : all;
        if (selected.Count == 0) return null;

        var selectedIds = selected.Select(m => m.Id).ToList();
        var serials = await db.Set<OurStockSerial>().AsNoTracking()
            .Where(s => s.ReceiptMovementId.HasValue && selectedIds.Contains(s.ReceiptMovementId.Value))
            .Select(s => new { MovementId = s.ReceiptMovementId!.Value, s.SerialNumber })
            .ToListAsync();

        var lines = po.POItems.OrderBy(i => i.PORef)
            .Select(line =>
            {
                var mine = selected.Where(m => m.POItemId == line.Id).ToList();
                var total = all.Where(m => m.POItemId == line.Id).Sum(m => m.Qty);
                var latest = mine.OrderByDescending(m => m.CreatedAt).FirstOrDefault();
                return new { line, mine, total, latest };
            })
            .Where(x => x.mine.Count > 0)
            .Select(x => new StockReceiptNoteLine
            {
                PORef = x.line.PORef ?? 0,
                PartNumber = x.line.PartNumber?.Name ?? string.Empty,
                Description = x.line.PartNumber?.Description,
                Condition = x.line.Condition ?? "New",
                QtyOrdered = x.line.Qty,
                QtyThisNote = x.mine.Sum(m => m.Qty),
                QtyReceivedTotal = x.total,
                QtyRemaining = Math.Max(0, x.line.Qty - x.total),
                CertName = x.latest?.CertName,
                BinLocation = x.latest?.BinLocation,
                WarehouseName = x.latest?.WarehouseName,
                TrackNumbers = x.mine.Where(m => m.TrackNumber != null).Select(m => m.TrackNumber!).Distinct().ToList(),
                Serials = serials.Where(s => x.mine.Any(m => m.Id == s.MovementId)).Select(s => s.SerialNumber).ToList(),
            }).ToList();

        return new StockReceiptNoteResponse
        {
            POId = po.Id,
            PONumber = po.PONumber,
            NoteNumber = $"GRN-{po.PONumber}-{selected.Max(m => m.Id)}",
            PODate = po.PODate ?? po.CreatedAt,
            SupplierName = po.Supplier?.Name ?? string.Empty,
            SupplierPIRef = po.SupplierPIRef,
            CompanyPresetId = po.CompanyPresetId,
            WarehouseNames = string.Join(", ", selected.Select(m => m.WarehouseName).Distinct()),
            ReceivedFrom = selected.Min(m => m.CreatedAt),
            ReceivedTo = selected.Max(m => m.CreatedAt),
            ReceivedBy = string.Join(", ", selected.Select(m => m.CreatedBy).Distinct()),
            Notes = selected.Where(m => !string.IsNullOrWhiteSpace(m.Reason)).Select(m => m.Reason!).Distinct().ToList(),
            IsPartialSelection = selected.Count < all.Count,
            Lines = lines,
        };
    }

    private static IQueryable<OurStockItem> ApplyFilters(IQueryable<OurStockItem> query, StockItemQuery filter, string? exclude = null)
    {
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(i => i.PartNumber.Name.Contains(term)
                || (i.PartNumber.Description != null && i.PartNumber.Description.Contains(term))
                || (i.BinLocation != null && i.BinLocation.Contains(term))
                || (i.CertName != null && i.CertName.Contains(term)));
        }
        if (exclude != "warehouse" && filter.WarehouseIds?.Count > 0) query = query.Where(i => filter.WarehouseIds.Contains(i.WarehouseId));
        if (exclude != "preset" && filter.CompanyPresetIds?.Count > 0) query = query.Where(i => filter.CompanyPresetIds.Contains(i.CompanyPresetId));
        if (exclude != "condition" && filter.Conditions?.Count > 0) query = query.Where(i => filter.Conditions.Contains(i.Condition));
        if (filter.OnlyAvailable) query = query.Where(i => i.QtyAvailable > 0);
        if (filter.LowStock) query = query.Where(i => i.MinQty.HasValue && i.QtyAvailable < i.MinQty.Value);
        return query;
    }

    private static IQueryable<StockItemResponse> Project(IQueryable<OurStockItem> query, bool includeCost)
        => query.Select(i => new StockItemResponse
        {
            Id = i.Id, PartNumberId = i.PartNumberId, PartNumber = i.PartNumber.Name, Description = i.PartNumber.Description,
            Condition = i.Condition, WarehouseId = i.WarehouseId, WarehouseName = i.Warehouse.DisplayName ?? i.Warehouse.Name,
            CompanyPresetId = i.CompanyPresetId, CompanyPresetName = i.CompanyPreset.Name,
            QtyOnHand = i.QtyOnHand, QtyReserved = i.QtyReserved, QtyAvailable = i.QtyAvailable,
            AvgUnitCost = includeCost ? i.AvgUnitCost : null,
            TotalAmount = includeCost ? i.QtyOnHand * i.AvgUnitCost : null,
            CertName = i.CertName, TagDate = i.TagDate, BinLocation = i.BinLocation, MinQty = i.MinQty,
            IsLowStock = i.MinQty.HasValue && i.QtyAvailable < i.MinQty.Value,
            Notes = i.Notes, UpdatedAt = i.UpdatedAt,
        });
}

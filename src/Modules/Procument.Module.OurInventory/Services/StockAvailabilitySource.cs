using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Procument.Module.OurInventory.Entities;
using Procument.Module.Purchasing.DTOs;
using Procument.Module.Purchasing.Entities;
using Procument.Module.Purchasing.Services;

namespace Procument.Module.OurInventory.Services;

/// <summary>
/// Our Stock rows for the RFQ availability chips: one per lot with something available, plus incoming
/// quantities on approved Stock POs (lead time = the PO's expected delivery date).
/// </summary>
public sealed class StockAvailabilitySource(DbContext db, IOptions<OurInventoryOptions> options) : IPartAvailabilitySource
{
    private static readonly string[] ClosedPoStatuses =
        ["Draft", PurchaseOrderStatusFlow.Cancelled, PurchaseOrderStatusFlow.Returned, PurchaseOrderStatusFlow.Completed];

    public async Task<IReadOnlyList<PartAvailabilitySourceRecord>> GetAvailabilityAsync(
        IReadOnlyCollection<long> partNumberIds,
        bool includeCost,
        CancellationToken cancellationToken = default)
    {
        if (partNumberIds.Count == 0) return [];
        var ids = partNumberIds.ToList();

        var lots = await db.Set<OurStockItem>().AsNoTracking()
            .Where(l => ids.Contains(l.PartNumberId) && l.QtyAvailable > 0)
            .OrderByDescending(l => l.QtyAvailable)
            .Select(l => new PartAvailabilitySourceRecord
            {
                PartNumberId = l.PartNumberId,
                StockItemId = l.Id,
                Label = "Our Stock · " + (l.Warehouse.DisplayName ?? l.Warehouse.Name),
                Qty = (double)l.QtyAvailable,
                Condition = l.Condition,
                CertName = l.CertName,
                TagDate = l.TagDate.HasValue ? l.TagDate.Value.ToString("yyyy-MM-dd") : null,
                Price = includeCost ? l.AvgUnitCost : null,
            })
            .ToListAsync(cancellationToken);

        var incoming = await db.Set<POItem>().AsNoTracking()
            .Where(i => i.ReturnedAt == null && i.PartNumberId.HasValue && ids.Contains(i.PartNumberId.Value)
                && i.PurchaseOrder!.Origin == "Stock" && i.PurchaseOrder.AdminApproval == "Approved"
                && !ClosedPoStatuses.Contains(i.PurchaseOrder.Status))
            .Select(i => new
            {
                PartNumberId = i.PartNumberId!.Value,
                i.Qty,
                i.Condition,
                i.PurchaseOrder!.PONumber,
                i.PurchaseOrder.ExpectedDeliveryDate,
                Received = db.Set<OurStockMovement>()
                    .Where(m => m.POItemId == i.Id && (m.Type == OurStockMovementTypes.Receipt || m.Type == OurStockMovementTypes.Adjust))
                    .Sum(m => (decimal?)m.Qty) ?? 0,
            })
            .ToListAsync(cancellationToken);

        lots.AddRange(incoming.Where(i => i.Qty > i.Received).Select(i => new PartAvailabilitySourceRecord
        {
            PartNumberId = i.PartNumberId,
            IsIncoming = true,
            Label = $"Incoming · {i.PONumber}",
            Qty = (double)(i.Qty - i.Received),
            Condition = i.Condition,
            LeadTime = i.ExpectedDeliveryDate?.ToString("yyyy-MM-dd"),
        }));
        foreach (var record in lots) record.SupplierName = options.Value.OurStockSupplierName;
        return lots;
    }
}

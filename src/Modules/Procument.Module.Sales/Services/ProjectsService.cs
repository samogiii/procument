using Microsoft.EntityFrameworkCore;
using Procument.Module.Purchasing.Entities;
using Procument.Module.Sales.DTOs;
using Procument.Module.Sales.Entities;
using Procument.Shared.DTOs;

namespace Procument.Module.Sales.Services;

public interface IProjectsService
{
    Task<PagedResult<ProjectItemResponse>> GetAsync(PageQuery page, string? status = null, string? customer = null);
}

public class ProjectsService : IProjectsService
{
    private readonly DbContext _db;

    public ProjectsService(DbContext db) { _db = db; }

    public async Task<PagedResult<ProjectItemResponse>> GetAsync(PageQuery page, string? status = null, string? customer = null)
    {
        // 1. Load all InvoiceItems with Invoice + Customer + QuoteItem + PartNumber
        var invoiceItemsQuery = _db.Set<InvoiceItem>()
            .Include(ii => ii.Invoice)
                .ThenInclude(inv => inv!.Customer)
            .Include(ii => ii.QuoteItem)
                .ThenInclude(qi => qi!.PartNumber)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(customer))
        {
            var c = customer.Trim();
            invoiceItemsQuery = invoiceItemsQuery.Where(ii =>
                (ii.Invoice.Customer != null && ii.Invoice.Customer.Name.Contains(c)) ||
                (ii.Invoice.Customer != null && ii.Invoice.Customer.CustomerCode != null && ii.Invoice.Customer.CustomerCode.Contains(c)));
        }

        if (!string.IsNullOrWhiteSpace(page.Search))
        {
            var s = page.Search.Trim();
            invoiceItemsQuery = invoiceItemsQuery.Where(ii =>
                ii.Invoice.InvoiceNumber.Contains(s) ||
                (ii.QuoteItem != null && ii.QuoteItem.PartNumber != null && ii.QuoteItem.PartNumber.Name.Contains(s)));
        }

        var invoiceItems = await invoiceItemsQuery
            .OrderByDescending(ii => ii.Invoice.CreatedAt)
            .ToListAsync();

        var invoiceItemIds = invoiceItems.Select(ii => ii.Id).ToList();
        if (invoiceItemIds.Count == 0)
            return new PagedResult<ProjectItemResponse> { Items = new(), TotalCount = 0, Page = page.Page, PageSize = page.PageSize };

        // 2. Batch-load ProcurementItems keyed by SourceInvoiceItemId
        var procItems = await _db.Set<ProcurementItem>()
            .Where(pi => invoiceItemIds.Contains(pi.SourceInvoiceItemId) && pi.ItemStatus != "Cancelled")
            .Include(pi => pi.CurrentSupplier)
            .ToListAsync();

        // One active ProcurementItem per InvoiceItemId (take most recent by Id)
        var procByInvoiceItem = procItems
            .GroupBy(pi => pi.SourceInvoiceItemId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(pi => pi.Id).First());

        // 3. Batch-load active POItems keyed by SourceProcurementItemId
        var procItemIds = procByInvoiceItem.Values.Select(pi => pi.Id).ToList();
        Dictionary<long, POItem> poItemByProcItem = new();
        Dictionary<long, PurchaseOrder> poMap = new();

        if (procItemIds.Count > 0)
        {
            var poItems = await _db.Set<POItem>()
                .Where(poi => poi.SourceProcurementItemId.HasValue
                              && procItemIds.Contains(poi.SourceProcurementItemId!.Value)
                              && poi.ReturnedAt == null)
                .Include(poi => poi.TrackNumbers)
                .ToListAsync();

            // For each ProcurementItem prefer the POItem that has a POId (already inside a PO)
            poItemByProcItem = poItems
                .GroupBy(poi => poi.SourceProcurementItemId!.Value)
                .ToDictionary(g => g.Key,
                    g => g.OrderByDescending(poi => poi.POId.HasValue).ThenByDescending(poi => poi.Id).First());

            // 4. Batch-load PurchaseOrders
            var poIds = poItemByProcItem.Values
                .Where(poi => poi.POId.HasValue)
                .Select(poi => poi.POId!.Value)
                .Distinct()
                .ToList();

            if (poIds.Count > 0)
            {
                poMap = await _db.Set<PurchaseOrder>()
                    .Where(p => poIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id);
            }
        }

        // 5. Build rows with computed status
        var rows = invoiceItems.Select(ii =>
        {
            var procItem = procByInvoiceItem.GetValueOrDefault(ii.Id);
            var poItem = procItem != null ? poItemByProcItem.GetValueOrDefault(procItem.Id) : null;
            var po = poItem?.POId.HasValue == true ? poMap.GetValueOrDefault(poItem.POId!.Value) : null;

            return new ProjectItemResponse
            {
                InvoiceItemId = ii.Id,
                InvoiceId = ii.InvoiceId,
                InvoiceNumber = ii.Invoice?.InvoiceNumber,
                CustomerId = ii.Invoice?.CustomerId,
                CustomerName = ii.Invoice?.Customer?.Name,
                CustomerCode = ii.Invoice?.Customer?.CustomerCode,
                PartNumberName = ii.QuoteItem?.PartNumber?.Name,
                Description = ii.QuoteItem?.PartNumber?.Description,
                Qty = ii.Qty,
                UnitPrice = procItem?.UnitPrice ?? ii.UnitPrice,
                SupplierName = procItem?.CurrentSupplier?.Name ?? procItem?.SupplierName,
                POId = po?.Id,
                PONumber = po?.PONumber,
                Status = ComputeStatus(procItem, poItem, po),
                InvoiceCreatedAt = ii.Invoice?.CreatedAt,
            };
        }).ToList();

        // 6. Apply status filter in-memory (computed after query)
        if (!string.IsNullOrWhiteSpace(status) && status != "All")
            rows = rows.Where(r => r.Status == status).ToList();

        var totalCount = rows.Count;
        var paged = rows
            .Skip((page.Page - 1) * page.PageSize)
            .Take(page.PageSize)
            .ToList();

        return new PagedResult<ProjectItemResponse>
        {
            Items = paged,
            TotalCount = totalCount,
            Page = page.Page,
            PageSize = page.PageSize
        };
    }

    private static string ComputeStatus(ProcurementItem? pi, POItem? poi, PurchaseOrder? po)
    {
        if (pi == null) return "Not Started";
        if (poi?.POId == null) return "Under Contract";

        // Item is assigned to a PO — walk the timestamps in priority order (highest wins)
        if (poi.TrackNumbers.Count > 0) return "Ship to Warehouse/Customer";
        if (po?.OurPOPDownloadedAt != null) return "Waiting For Supplier to Ship";
        if (po?.OurPOPSentAt != null) return "Payment Done";
        if (po?.SupplierDocumentReceivedAt != null) return "Document Added";
        if (po?.PDFSentAt != null) return "PO Sent";
        return "Waiting For Payment";
    }
}

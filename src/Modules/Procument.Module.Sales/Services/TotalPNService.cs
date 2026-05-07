using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Procument.Module.Catalog.Entities;
using Procument.Module.Identity.Entities;
using Procument.Module.Purchasing.DTOs;
using Procument.Module.Purchasing.Entities;
using Procument.Module.RFQ.Entities;
using Procument.Module.Sales.Entities;
using Procument.Shared.DTOs;
using Procument.Shared.Services;

namespace Procument.Module.Sales.Services;

/// <summary>
/// Builds the Total P/N (TPP) report — one row per POItem joined across
/// PO → Procurement → Invoice → Quote → FinalInvoice → Customer → Payments → TrackNumbers.
/// Lives in Sales because Purchasing can't reference Sales entities (Invoice / CustomerPayment / FinalInvoice / Quote).
/// </summary>
public interface ITotalPNService
{
    Task<PagedResult<TotalPNRowResponse>> GetAsync(PageQuery page, long userId, bool isAdmin);
    Task<bool> UpdateAsync(long poItemId, UpdatePOItemTotalPNRequest request);
}

public class TotalPNService : ITotalPNService
{
    private readonly DbContext _db;

    public TotalPNService(DbContext db) { _db = db; }

    public async Task<PagedResult<TotalPNRowResponse>> GetAsync(PageQuery page, long userId, bool isAdmin)
    {
        // ── Base query: start from InvoiceItems (so they show up immediately on PI creation) ──
        // Left-join with ProcurementItem (the worksheet) and POItem (the purchase).
        // Applying Includes to the source sets because they can't be applied to the anonymous type after join.
        var iiSet = _db.Set<InvoiceItem>()
            .Include(i => i.Invoice).ThenInclude(inv => inv.Customer)
            .Include(i => i.QuoteItem).ThenInclude(qi => qi!.ProcumentRecord).ThenInclude(pr => pr!.Supplier);

        var piSet = _db.Set<ProcurementItem>()
            .Include(p => p.CurrentSupplier);

        var poiSet = _db.Set<POItem>().Where(x => x.ReturnedAt == null)
            .Include(p => p.PurchaseOrder).ThenInclude(po => po!.Supplier)
            .Include(p => p.PartNumber)
            .Include(p => p.TrackNumbers);

        var baseQuery = from ii in iiSet
                        join pi in piSet on ii.Id equals pi.SourceInvoiceItemId into pij
                        from pi in pij.DefaultIfEmpty()
                        join poi in poiSet on ii.Id equals poi.InvoiceItemId into poij
                        from poi in poij.DefaultIfEmpty()
                        select new { ii, pi, poi };

        // ── Permission filter for non-admins ──
        if (!isAdmin)
        {
            var perms = await _db.Set<EntityPermission>()
                .Where(p => p.UserId == userId && (p.EntityName == "Invoice" || p.EntityName == "Procurement" || p.EntityName == "PO"))
                .Select(p => new { p.EntityName, p.EntityId })
                .ToListAsync();

            var invIds = perms.Where(p => p.EntityName == "Invoice").Select(p => long.TryParse(p.EntityId, out var l) ? l : -1L).Where(l => l > 0).ToHashSet();
            var procIds = perms.Where(p => p.EntityName == "Procurement").Select(p => long.TryParse(p.EntityId, out var l) ? l : -1L).Where(l => l > 0).ToHashSet();
            var poIds = perms.Where(p => p.EntityName == "PO").Select(p => long.TryParse(p.EntityId, out var l) ? l : -1L).Where(l => l > 0).ToHashSet();

            baseQuery = baseQuery.Where(x =>
                invIds.Contains(x.ii.InvoiceId) ||
                (x.pi != null && procIds.Contains(x.pi.ProcurementId)) ||
                (x.poi != null && x.poi.POId.HasValue && poIds.Contains(x.poi.POId.Value)));
        }

        // Search filtering (Invoice Number or Part Number)
        if (!string.IsNullOrWhiteSpace(page.Search))
        {
            var s = page.Search.Trim().ToLower();
            baseQuery = baseQuery.Where(x =>
                x.ii.Invoice.InvoiceNumber.Contains(s) ||
                (x.pi != null && x.pi.PartNumberName != null && x.pi.PartNumberName.ToLower().Contains(s)) ||
                (x.poi != null && x.poi.PartNumber != null && x.poi.PartNumber.Name.ToLower().Contains(s)));
        }

        var totalCount = await baseQuery.CountAsync();

        var pageItems = await baseQuery
            .OrderByDescending(x => x.ii.Invoice.CreatedAt)
            .ThenBy(x => x.ii.Id)
            .Skip((page.Page - 1) * page.PageSize)
            .Take(page.PageSize)
            .ToListAsync();

        // ── Batch-load helpers for creators/experts ──
        var procIdsInPage = pageItems.Where(x => x.pi != null).Select(x => x.pi!.ProcurementId).Distinct().ToList();
        var procMap = procIdsInPage.Count > 0
            ? await _db.Set<Procurement>().Where(p => procIdsInPage.Contains(p.Id)).ToDictionaryAsync(p => p.Id)
            : new Dictionary<long, Procurement>();

        var quoteIdsInPage = pageItems.Select(x => x.ii.Invoice.QuoteId).Distinct().ToList();
        var quoteMap = quoteIdsInPage.Count > 0
            ? await _db.Set<Quote>().Where(q => quoteIdsInPage.Contains(q.Id)).ToDictionaryAsync(q => q.Id)
            : new Dictionary<long, Quote>();

        var allUserIds = procMap.Values.Where(p => p.CreatedByUserId.HasValue).Select(p => p.CreatedByUserId!.Value)
            .Concat(quoteMap.Values.Select(q => q.UserId))
            .Distinct().ToList();
        var userMap = allUserIds.Count > 0
            ? await _db.Set<User>().Where(u => allUserIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name)
            : new Dictionary<long, string>();

        // ── Final Invoices ──
        var invoiceIds = pageItems.Select(x => x.ii.InvoiceId).Distinct().ToList();
        var finalInvoiceMap = invoiceIds.Count > 0
            ? await _db.Set<FinalInvoice>()
                .Where(fi => invoiceIds.Contains(fi.ProformaInvoiceId))
                .GroupBy(fi => fi.ProformaInvoiceId)
                .Select(g => g.OrderByDescending(x => x.Id).First())
                .ToDictionaryAsync(fi => fi.ProformaInvoiceId)
            : new Dictionary<long, FinalInvoice>();

        // ── Customer Payments ──
        var paymentAgg = invoiceIds.Count > 0
            ? await _db.Set<CustomerPayment>()
                .Where(cp => invoiceIds.Contains(cp.InvoiceId))
                .GroupBy(cp => cp.InvoiceId)
                .Select(g => new { InvoiceId = g.Key, Total = g.Sum(x => x.Amount), LastDate = g.Max(x => x.CreatedAt) })
                .ToDictionaryAsync(r => r.InvoiceId, r => (Total: r.Total, LastDate: r.LastDate))
            : new Dictionary<long, (decimal Total, DateTime LastDate)>();

        // ── File checks (batch) ──
        var docStorage = (IDocumentStorageService)_db.GetService<IDocumentStorageService>();
        var fileCategories = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var x in pageItems)
        {
            var invNum = x.ii.Invoice.InvoiceNumber;
            if (string.IsNullOrEmpty(invNum)) continue;

            // Resolve supplier name for this row
            string? sName = x.pi?.SupplierName ?? x.poi?.PurchaseOrder?.Supplier?.Name ?? x.ii.QuoteItem?.ProcumentRecord?.Supplier?.Name;
            if (string.IsNullOrEmpty(sName)) continue;

            var key = $"{invNum}|{sName}";
            if (!fileCategories.ContainsKey(key))
            {
                var files = docStorage.ListFilesInSupplierCategories(invNum, sName, new[]
                {
                    ("po", "PO"),
                    ("supplier_invoice", "Supplier Invoice"),
                    ("our_pop", "Our POP to Supplier")
                }).Select(f => f.Category).ToHashSet();
                fileCategories[key] = files;
            }
        }

        // ── Project rows ──
        var rows = pageItems.Select(x =>
        {
            var ii = x.ii;
            var invoice = ii.Invoice;
            var customer = invoice.Customer;
            var pi = x.pi;
            var poi = x.poi;
            var po = poi?.PurchaseOrder;

            //專家
            string? qExpert = quoteMap.TryGetValue(invoice.QuoteId, out var qv) && userMap.TryGetValue(qv.UserId, out var qun) ? qun : null;
            string? pExpert = pi != null && procMap.TryGetValue(pi.ProcurementId, out var ph) && ph.CreatedByUserId.HasValue && userMap.TryGetValue(ph.CreatedByUserId.Value, out var pun) ? pun : null;

            // Purchasing Price & Supplier Sync (prioritize Procurement layer edits)
            decimal purchUnit = pi?.UnitPrice ?? poi?.UnitPrice ?? 0m;
            decimal purchTotal = (poi?.Qty ?? ii.Qty) * purchUnit;
            string? supplierName = pi?.SupplierName ?? poi?.PurchaseOrder?.Supplier?.Name ?? ii.QuoteItem?.ProcumentRecord?.Supplier?.Name;

            // Selling Price
            decimal sellUnit = ii.QuoteItem?.UnitPrice ?? ii.UnitPrice;
            decimal sellTotal = (poi?.Qty ?? ii.Qty) * sellUnit;
            decimal rate = (customer?.CurrencyType == "Yuan" || customer?.CurrencyType == "Both") ? 7m : 1m;

            // Status derivation logic
            var status = "Not Started";
            if (invoice.Status == "Running") status = "Under Contract";
            if (poi != null) status = "Waiting For Payment";

            // Check files
            var fileKey = $"{invoice.InvoiceNumber}|{supplierName}";
            var hasFiles = fileCategories.TryGetValue(fileKey, out var cats);
            if (hasFiles && cats!.Contains("po")) status = "PO Sent";
            if (hasFiles && cats!.Contains("supplier_invoice")) status = "Document Added";
            if (po?.Status == "Waiting For Payment") status = "Waiting For Payment";
            if (hasFiles && cats!.Contains("our_pop")) status = "Payment Done";
            
            // Explicit tracking status from DB (download triggers)
            if (poi?.Status == "Waiting For Shipment") status = "Waiting For Shipment";
            if (poi?.Status == "Waiting For supplier to ship") status = "Waiting For Shipment";
            
            // Terminal status: tracking added
            if (poi?.TrackNumbers != null && poi.TrackNumbers.Any()) status = "Ship to Warehouse/Customer";

            // Payment / Invoice meta
            FinalInvoice? fi = finalInvoiceMap.TryGetValue(invoice.Id, out var fiv) ? fiv : null;
            decimal? recvTotal = paymentAgg.TryGetValue(invoice.Id, out var agg) ? agg.Total : null;
            DateTime? recvDate = paymentAgg.TryGetValue(invoice.Id, out var agg2) ? agg2.LastDate : null;

            return new TotalPNRowResponse
            {
                Id = poi?.Id ?? -ii.Id, // Use negative InvoiceItemID as stable ID for unassigned rows
                PONumber = po?.PONumber,
                PORef = poi?.PORef,
                QuotationExpert = qExpert,
                ProcurementExpert = pExpert,
                Customer = customer?.CustomerCode,
                Supplier = supplierName,
                PartNumber = poi?.PartNumber?.Name ?? pi?.PartNumberName ?? ii.QuoteItem?.PartNumber?.Name,
                Description = poi?.PartNumber?.Description ?? pi?.PartNumberDescription ?? ii.QuoteItem?.PartNumber?.Description,
                Qty = poi?.Qty ?? ii.Qty,
                Condition = poi?.Condition ?? pi?.Condition ?? ii.QuoteItem?.Condition,
                Priority = pi?.RfqPriority,
                Warehouse = (ii.QuoteItem?.RFQItem?.RFQ?.ExType ?? pi?.RfqExType) switch { 0 => "Warehouse", 1 => "Vendor", 2 => "Customer", _ => null },
                SerialNumber = null,
                CustomerInvoiceNumber = invoice.InvoiceNumber,
                PurchasingUnitPriceUsd = purchUnit,
                PurchasingTotalPriceUsd = purchTotal,
                POAmount = po?.TotalAmount,
                DPNumber = null,
                SupplierDeliveryTime = pi?.LeadTime,
                Status = status,
                SellingUnitPriceUsd = sellUnit,
                SellingTotalPriceUsd = sellTotal,
                SellingUnitPriceYuan = sellUnit * 7m,
                SellingTotalPriceYuan = sellTotal * 7m,
                InvAmount = fi?.TotalAmount,
                PODate = po?.CreatedAt,
                InvDate = fi?.CreatedAt,
                Received = recvTotal,
                ReceivedDate = recvDate,
                PaymentTerm = invoice.Status,
                CustomerDeliveryTime = invoice.DueDate,
                Rate = rate,
                TrackNumbers = poi?.TrackNumbers != null ? string.Join(", ", poi.TrackNumbers.Select(t => t.TrackNumber)) : null,
                ShippingCost = pi?.ShippingCost != null ? (decimal)pi.ShippingCost.Value : null,
                Note = poi?.Note,
            };
        }).ToList();

        return new PagedResult<TotalPNRowResponse>
        {
            Items = rows,
            TotalCount = totalCount,
            Page = page.Page,
            PageSize = page.PageSize,
        };
    }

    public async Task<bool> UpdateAsync(long poItemId, UpdatePOItemTotalPNRequest request)
    {
        var item = await _db.Set<POItem>().FindAsync(poItemId);
        if (item == null) return false;
        if (request.Status != null) item.Status = request.Status;
        if (request.Note != null) item.Note = request.Note;
        await _db.SaveChangesAsync();
        return true;
    }
}

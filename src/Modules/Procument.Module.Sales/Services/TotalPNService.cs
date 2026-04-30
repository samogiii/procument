using Microsoft.EntityFrameworkCore;
using Procument.Module.Catalog.Entities;
using Procument.Module.Identity.Entities;
using Procument.Module.Purchasing.DTOs;
using Procument.Module.Purchasing.Entities;
using Procument.Module.RFQ.Entities;
using Procument.Module.Sales.Entities;
using Procument.Shared.DTOs;

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
        // ── Base query: every live POItem (returned items hidden) ──
        IQueryable<POItem> q = _db.Set<POItem>().Where(i => i.ReturnedAt == null);

        // ── Permission filter for non-admins ──
        // A non-admin sees a POItem if EITHER:
        //   (a) they have EntityPermission("Procurement") on its SourceProcurementItemId, OR
        //   (b) the item already lives on a PO they have EntityPermission("PO") on.
        // (b) covers the auto-grant we added when a non-admin creates a PO.
        if (!isAdmin)
        {
            var perms = await _db.Set<EntityPermission>()
                .Where(p => p.UserId == userId && (p.EntityName == "Procurement" || p.EntityName == "PO"))
                .Select(p => new { p.EntityName, p.EntityId })
                .ToListAsync();

            var procIds = perms.Where(p => p.EntityName == "Procurement")
                .Select(p => long.TryParse(p.EntityId, out var l) ? l : -1L)
                .Where(l => l > 0).ToHashSet();
            var poIds = perms.Where(p => p.EntityName == "PO")
                .Select(p => long.TryParse(p.EntityId, out var l) ? l : -1L)
                .Where(l => l > 0).ToHashSet();

            if (procIds.Count == 0 && poIds.Count == 0)
                return new PagedResult<TotalPNRowResponse> { Items = new(), TotalCount = 0, Page = page.Page, PageSize = page.PageSize };

            q = q.Where(i =>
                (i.SourceProcurementItemId.HasValue && procIds.Contains(i.SourceProcurementItemId.Value)) ||
                (i.POId.HasValue && poIds.Contains(i.POId.Value)));
        }

        var totalCount = await q.CountAsync();

        // Order: assigned PO first (by PONumber), unassigned last
        var pageItems = await q
            .OrderByDescending(i => i.POId.HasValue)
            .ThenBy(i => i.POId)
            .ThenBy(i => i.PORef)
            .ThenBy(i => i.Id)
            .Skip((page.Page - 1) * page.PageSize)
            .Take(page.PageSize)
            .Include(i => i.PartNumber)
            .Include(i => i.PurchaseOrder)
            .Include(i => i.SourceProcurementItem)
                .ThenInclude(pi => pi!.CurrentSupplier)
            .Include(i => i.ProcumentRecord)
                .ThenInclude(pr => pr!.Supplier)
            .Include(i => i.ProcumentRecord)
                .ThenInclude(pr => pr!.RFQItem)
                    .ThenInclude(ri => ri.RFQ)
            .Include(i => i.TrackNumbers)
            .ToListAsync();

        // ── Batch-load related entities to avoid N+1 ──
        var supplierIds = pageItems.Where(i => i.SupplierId.HasValue).Select(i => i.SupplierId!.Value).Distinct().ToList();
        var supplierMap = supplierIds.Count > 0
            ? await _db.Set<Supplier>().Where(s => supplierIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id)
            : new Dictionary<long, Supplier>();

        // PO TotalAmount lookup (header)
        var poIdSet = pageItems.Where(i => i.POId.HasValue).Select(i => i.POId!.Value).Distinct().ToList();
        var poMap = poIdSet.Count > 0
            ? await _db.Set<PurchaseOrder>().Where(p => poIdSet.Contains(p.Id)).ToDictionaryAsync(p => p.Id)
            : new Dictionary<long, PurchaseOrder>();

        // Procurement → CreatedByUser
        var procurementIds = pageItems
            .Where(i => i.SourceProcurementItem != null)
            .Select(i => i.SourceProcurementItem!.ProcurementId).Distinct().ToList();
        var procMap = procurementIds.Count > 0
            ? await _db.Set<Procurement>().Where(p => procurementIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id)
            : new Dictionary<long, Procurement>();
        var procurementCreatorIds = procMap.Values
            .Where(p => p.CreatedByUserId.HasValue && p.CreatedByUserId.Value > 0)
            .Select(p => p.CreatedByUserId!.Value)
            .Distinct().ToList();

        // InvoiceItem → Invoice (so we get Customer + DueDate + Status + InvoiceNumber + QuoteId)
        var invoiceItemIds = pageItems.Where(i => i.InvoiceItemId.HasValue).Select(i => i.InvoiceItemId!.Value).Distinct().ToList();
        var invoiceItemMap = invoiceItemIds.Count > 0
            ? await _db.Set<InvoiceItem>()
                .Where(ii => invoiceItemIds.Contains(ii.Id))
                .Include(ii => ii.Invoice).ThenInclude(inv => inv.Customer)
                .Include(ii => ii.QuoteItem)
                .ToDictionaryAsync(ii => ii.Id)
            : new Dictionary<long, InvoiceItem>();

        // Invoice ids touched by these rows
        var invoiceIds = invoiceItemMap.Values.Select(ii => ii.InvoiceId).Distinct().ToList();

        // Quote → quotation expert User
        var quoteIds = invoiceItemMap.Values.Select(ii => ii.Invoice.QuoteId).Distinct().ToList();
        var quoteMap = quoteIds.Count > 0
            ? await _db.Set<Quote>().Where(q => quoteIds.Contains(q.Id)).ToDictionaryAsync(q => q.Id)
            : new Dictionary<long, Quote>();
        var quoteUserIds = quoteMap.Values.Where(q => q.UserId > 0).Select(q => q.UserId).Distinct().ToList();

        // FinalInvoice — lookup by ProformaInvoiceId
        var finalInvoiceMap = invoiceIds.Count > 0
            ? await _db.Set<FinalInvoice>()
                .Where(fi => invoiceIds.Contains(fi.ProformaInvoiceId))
                .GroupBy(fi => fi.ProformaInvoiceId)
                .Select(g => g.OrderByDescending(x => x.Id).First())
                .ToDictionaryAsync(fi => fi.ProformaInvoiceId)
            : new Dictionary<long, FinalInvoice>();

        // CustomerPayment aggregation (Received & Received Date)
        var paymentRows = invoiceIds.Count > 0
            ? await _db.Set<CustomerPayment>()
                .Where(cp => invoiceIds.Contains(cp.InvoiceId))
                .GroupBy(cp => cp.InvoiceId)
                .Select(g => new { InvoiceId = g.Key, Total = g.Sum(x => x.Amount), LastDate = g.Max(x => x.CreatedAt) })
                .ToListAsync()
            : new();
        var paymentAgg = paymentRows.ToDictionary(r => r.InvoiceId, r => (Total: r.Total, LastDate: r.LastDate));

        // Resolve all User names in one shot
        var allUserIds = procurementCreatorIds.Concat(quoteUserIds).Distinct().ToList();
        var userMap = allUserIds.Count > 0
            ? await _db.Set<User>().Where(u => allUserIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name)
            : new Dictionary<long, string>();

        // ── Project rows ──
        var rows = pageItems.Select(i =>
        {
            var srcProcItem = i.SourceProcurementItem;
            var procRecord = i.ProcumentRecord;
            var rfq = procRecord?.RFQItem?.RFQ;

            // Supplier name priority: explicit POItem.SupplierId → Procurement-layer pick → ProcumentRecord supplier
            string? supplierName = null;
            if (i.SupplierId.HasValue && supplierMap.TryGetValue(i.SupplierId.Value, out var s)) supplierName = s.Name;
            supplierName ??= srcProcItem?.CurrentSupplier?.Name;
            supplierName ??= procRecord?.Supplier?.Name;

            // ExType → "Warehouse" / "Vendor" / "Customer". Fall back to ProcurementItem.RfqExType snapshot.
            int? exType = rfq?.ExType ?? srcProcItem?.RfqExType;
            string? warehouse = exType switch
            {
                0 => "Warehouse",
                1 => "Vendor",
                2 => "Customer",
                _ => null
            };

            // Invoice / Quote / Customer chain
            InvoiceItem? ii = i.InvoiceItemId.HasValue && invoiceItemMap.TryGetValue(i.InvoiceItemId.Value, out var iiv) ? iiv : null;
            var invoice = ii?.Invoice;
            var customer = invoice?.Customer;
            Quote? quote = invoice != null && quoteMap.TryGetValue(invoice.QuoteId, out var qv) ? qv : null;

            // Selling price from QuoteItem (the Sales-side commitment)
            decimal sellUnit = ii?.QuoteItem?.UnitPrice ?? 0m;
            decimal sellTotal = i.Qty * sellUnit;

            // Currency rate: Yuan or Both → 7, anything else (Dollar/null) → 1
            decimal rate = (customer?.CurrencyType == "Yuan" || customer?.CurrencyType == "Both") ? 7m : 1m;
            decimal sellUnitYuan = sellUnit * 7m;        // user said: always USD * 7 for Yuan columns
            decimal sellTotalYuan = sellTotal * 7m;

            // Final invoice header (INV Amount + INV Date)
            FinalInvoice? fi = invoice != null && finalInvoiceMap.TryGetValue(invoice.Id, out var fiv) ? fiv : null;

            // Customer payments (Received total + last date)
            decimal? recvTotal = null;
            DateTime? recvDate = null;
            if (invoice != null && paymentAgg.TryGetValue(invoice.Id, out var agg))
            {
                recvTotal = agg.Total;
                recvDate = agg.LastDate;
            }

            // Procurement creator name (ProcurementExpert column)
            string? procExpert = null;
            if (srcProcItem != null
                && procMap.TryGetValue(srcProcItem.ProcurementId, out var procHeader)
                && procHeader.CreatedByUserId.HasValue
                && userMap.TryGetValue(procHeader.CreatedByUserId.Value, out var puName))
            {
                procExpert = puName;
            }

            // Quote creator (quotationExpert)
            string? qExpert = quote != null && userMap.TryGetValue(quote.UserId, out var qun) ? qun : null;

            // PO header
            PurchaseOrder? po = i.POId.HasValue && poMap.TryGetValue(i.POId.Value, out var poh) ? poh : null;

            return new TotalPNRowResponse
            {
                Id = i.Id,
                PONumber = po?.PONumber,
                PORef = i.PORef,
                QuotationExpert = qExpert,
                ProcurementExpert = procExpert,
                Customer = customer?.CustomerCode,
                Supplier = supplierName,
                PartNumber = i.PartNumber?.Name ?? srcProcItem?.PartNumberName,
                Description = i.PartNumber?.Description ?? srcProcItem?.PartNumberDescription,
                Qty = i.Qty,
                Condition = i.Condition,
                Priority = srcProcItem?.RfqPriority,
                Warehouse = warehouse,
                SerialNumber = null,                  // not implemented yet
                CustomerInvoiceNumber = invoice?.InvoiceNumber,
                PurchasingUnitPriceUsd = i.UnitPrice,
                PurchasingTotalPriceUsd = i.TotalPrice,
                POAmount = po?.TotalAmount,
                DPNumber = null,                       // not implemented yet
                SupplierDeliveryTime = srcProcItem?.LeadTime,
                Status = i.Status,
                SellingUnitPriceUsd = sellUnit,
                SellingTotalPriceUsd = sellTotal,
                SellingUnitPriceYuan = sellUnitYuan,
                SellingTotalPriceYuan = sellTotalYuan,
                InvAmount = fi?.TotalAmount,
                PODate = po?.CreatedAt,
                InvDate = fi?.CreatedAt,
                Received = recvTotal,
                ReceivedDate = recvDate,
                PaymentTerm = invoice?.Status,
                CustomerDeliveryTime = invoice?.DueDate,
                Rate = rate,
                TrackNumbers = i.TrackNumbers != null && i.TrackNumbers.Count > 0
                    ? string.Join(", ", i.TrackNumbers.Select(t => t.TrackNumber))
                    : null,
                ShippingCost = srcProcItem?.ShippingCost.HasValue == true ? (decimal)srcProcItem.ShippingCost.Value : (decimal?)null,
                Note = i.Note,
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

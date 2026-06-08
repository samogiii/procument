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
    Task<PagedResult<TotalPNRowResponse>> GetAsync(PageQuery page, long userId, bool isAdmin, string? sortBy = null, bool sortDesc = false, bool isSuperAdmin = true, int[]? userBases = null,
        List<string>? customers = null, List<string>? invoiceNumbers = null, List<string>? partNumbers = null,
        List<string>? conditions = null, List<string>? poNumbers = null, List<string>? suppliers = null,
        List<string>? paymentTerms = null, List<string>? poStatuses = null, List<string>? shippingStatuses = null);
    Task<TotalPNFilterOptions> GetFilterOptionsAsync(long userId, bool isAdmin, bool isSuperAdmin, int[]? userBases);
    Task<PagedResult<TotalPNRowResponse>> GetTotalOrderAsync(PageQuery page, long userId, bool isAdmin, bool isSuperAdmin = true, int[]? userBases = null);
    Task<bool> UpdateAsync(long poItemId, UpdatePOItemTotalPNRequest request);
}

public class TotalPNService : ITotalPNService
{
    private readonly DbContext _db;

    public TotalPNService(DbContext db) { _db = db; }

    public async Task<PagedResult<TotalPNRowResponse>> GetAsync(PageQuery page, long userId, bool isAdmin, string? sortBy = null, bool sortDesc = false, bool isSuperAdmin = true, int[]? userBases = null,
        List<string>? customers = null, List<string>? invoiceNumbers = null, List<string>? partNumbers = null,
        List<string>? conditions = null, List<string>? poNumbers = null, List<string>? suppliers = null,
        List<string>? paymentTerms = null, List<string>? poStatuses = null, List<string>? shippingStatuses = null)
    {
        // ── Base query: start from InvoiceItems (so they show up immediately on PI creation) ──
        // Left-join with ProcurementItem (the worksheet) and POItem (the purchase).
        // Applying Includes to the source sets because they can't be applied to the anonymous type after join.
        var iiSet = _db.Set<InvoiceItem>()
            .Include(i => i.Invoice).ThenInclude(inv => inv.Customer)
            .Include(i => i.Invoice).ThenInclude(inv => inv.Quote)
            .Include(i => i.QuoteItem).ThenInclude(qi => qi!.ProcumentRecord).ThenInclude(pr => pr!.Supplier)
            .Include(i => i.QuoteItem).ThenInclude(qi => qi!.PartNumber);

        var piSet = _db.Set<ProcurementItem>()
            .Include(p => p.CurrentSupplier);

        var poiSet = _db.Set<POItem>().Where(x => x.ReturnedAt == null)
            .Include(p => p.PurchaseOrder).ThenInclude(po => po!.Supplier)
            .Include(p => p.PartNumber)
            .Include(p => p.TrackNumbers)
                .ThenInclude(t => t.ShipmentNotes)
                .ThenInclude(snt => snt.ShipmentNote);

        var baseQuery = from ii in iiSet
                        join pi in piSet on ii.Id equals pi.SourceInvoiceItemId into pij
                        from pi in pij.DefaultIfEmpty()
                        join poi in poiSet on pi.Id equals poi.SourceProcurementItemId into poij
                        from poi in poij.DefaultIfEmpty()
                        select new { ii, pi, poi };

        // ── Permission / base filter ──
        if (!isSuperAdmin)
        {
            var perms = await _db.Set<EntityPermission>()
                .Where(p => p.UserId == userId && (p.EntityName == "Invoice" || p.EntityName == "Procurement" || p.EntityName == "PO"))
                .Select(p => new { p.EntityName, p.EntityId })
                .ToListAsync();

            var invIds  = perms.Where(p => p.EntityName == "Invoice").Select(p => long.TryParse(p.EntityId, out var l) ? l : -1L).Where(l => l > 0).ToHashSet();
            var procIds = perms.Where(p => p.EntityName == "Procurement").Select(p => long.TryParse(p.EntityId, out var l) ? l : -1L).Where(l => l > 0).ToHashSet();
            var poIds   = perms.Where(p => p.EntityName == "PO").Select(p => long.TryParse(p.EntityId, out var l) ? l : -1L).Where(l => l > 0).ToHashSet();

            baseQuery = baseQuery.Where(x =>
                (userBases != null && userBases.Length > 0 && (x.ii.Invoice.Customer.Base == null || userBases.Contains(x.ii.Invoice.Customer.Base.Value))) ||
                invIds.Contains(x.ii.InvoiceId) ||
                (x.pi != null && procIds.Contains(x.pi.ProcurementId)) ||
                (x.poi != null && x.poi.POId.HasValue && poIds.Contains(x.poi.POId.Value)) ||
                (isAdmin && x.ii.Invoice.Quote.UserId == userId)); // Admins see their own invoices even if outside base
        }

        // Keep only rows that have at least one valid part number (not null, empty, or literally "-")
        baseQuery = baseQuery.Where(x =>
            (x.poi != null && x.poi.PartNumber != null && x.poi.PartNumber.Name != "-" && x.poi.PartNumber.Name != "") ||
            (x.pi  != null && x.pi.PartNumberName  != null && x.pi.PartNumberName  != "-" && x.pi.PartNumberName  != "") ||
            (x.ii.QuoteItem != null && x.ii.QuoteItem.PartNumber != null &&
             x.ii.QuoteItem.PartNumber.Name != "-" && x.ii.QuoteItem.PartNumber.Name != ""));

        // Search filtering (Invoice Number or Part Number)
        if (!string.IsNullOrWhiteSpace(page.Search))
        {
            var s = page.Search.Trim().ToLower();
            baseQuery = baseQuery.Where(x =>
                x.ii.Invoice.InvoiceNumber.Contains(s) ||
                (x.pi != null && x.pi.PartNumberName != null && x.pi.PartNumberName.ToLower().Contains(s)) ||
                (x.poi != null && x.poi.PartNumber != null && x.poi.PartNumber.Name.ToLower().Contains(s)));
        }

        // ── Column filters (server-side) ─────────────────────────────────────
        if (customers?.Count > 0)
            baseQuery = baseQuery.Where(x =>
                x.ii.Invoice.Customer != null && customers.Contains(x.ii.Invoice.Customer.CustomerCode ?? ""));

        if (invoiceNumbers?.Count > 0)
            baseQuery = baseQuery.Where(x => invoiceNumbers.Contains(x.ii.Invoice.InvoiceNumber));

        if (partNumbers?.Count > 0)
            baseQuery = baseQuery.Where(x =>
                (x.poi != null && x.poi.PartNumber != null && partNumbers.Contains(x.poi.PartNumber.Name)) ||
                (x.pi  != null && x.pi.PartNumberName  != null && partNumbers.Contains(x.pi.PartNumberName)));

        if (conditions?.Count > 0)
            baseQuery = baseQuery.Where(x =>
                (x.poi != null && x.poi.Condition != null && conditions.Contains(x.poi.Condition)) ||
                (x.pi  != null && x.pi.Condition  != null && conditions.Contains(x.pi.Condition)));

        if (poNumbers?.Count > 0)
            baseQuery = baseQuery.Where(x =>
                x.poi != null && x.poi.PurchaseOrder != null && poNumbers.Contains(x.poi.PurchaseOrder.PONumber));

        if (suppliers?.Count > 0)
            baseQuery = baseQuery.Where(x =>
                (x.poi != null && x.poi.PurchaseOrder != null && x.poi.PurchaseOrder.Supplier != null && suppliers.Contains(x.poi.PurchaseOrder.Supplier.Name)) ||
                (x.pi  != null && x.pi.SupplierName  != null && suppliers.Contains(x.pi.SupplierName)));

        if (paymentTerms?.Count > 0)
            baseQuery = baseQuery.Where(x => paymentTerms.Contains(x.ii.Invoice.Status));

        if (poStatuses?.Count > 0)
            baseQuery = baseQuery.Where(x =>
                x.poi != null && x.poi.Status != null && poStatuses.Contains(x.poi.Status));

        if (shippingStatuses?.Count > 0)
            baseQuery = baseQuery.Where(x =>
                x.poi != null && x.poi.TrackNumbers.Any(t => shippingStatuses.Contains(t.Status)));
        // ─────────────────────────────────────────────────────────────────────

        var totalCount = await baseQuery.CountAsync();

        var orderedQuery = sortBy switch
        {
            "customer"   => sortDesc ? baseQuery.OrderByDescending(x => x.ii.Invoice.Customer.Name)        : baseQuery.OrderBy(x => x.ii.Invoice.Customer.Name),
            "partNumber" => sortDesc ? baseQuery.OrderByDescending(x => x.poi != null ? x.poi.PartNumber.Name : x.pi != null ? x.pi.PartNumberName : "") : baseQuery.OrderBy(x => x.poi != null ? x.poi.PartNumber.Name : x.pi != null ? x.pi.PartNumberName : ""),
            "qty"        => sortDesc ? baseQuery.OrderByDescending(x => x.poi != null ? x.poi.Qty : 0)    : baseQuery.OrderBy(x => x.poi != null ? x.poi.Qty : 0),
            "status"     => sortDesc ? baseQuery.OrderByDescending(x => x.poi != null ? x.poi.Status : "") : baseQuery.OrderBy(x => x.poi != null ? x.poi.Status : ""),
            "invDate"    => sortDesc ? baseQuery.OrderByDescending(x => x.ii.Invoice.CreatedAt)            : baseQuery.OrderBy(x => x.ii.Invoice.CreatedAt),
            _            => baseQuery.OrderByDescending(x => x.ii.Invoice.CreatedAt).ThenBy(x => x.ii.Id),
        };

        var pageItems = await orderedQuery
            .ApplyPaging(page)
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

            // Purchasing Price & Supplier Sync (prioritize the Purchase Order — it's the source of truth
            // for what was actually ordered/paid, and is what SuperAdmin overrides via the PO price-edit feature.
            // Fall back to the Procurement worksheet snapshot only when no POItem price exists yet.)
            // purchTotal uses POItem qty because that's what was actually ordered from the supplier.
            decimal purchUnit  = poi?.UnitPrice ?? pi?.UnitPrice ?? 0m;
            int     purchQty   = poi?.Qty ?? ii.Qty;
            decimal purchTotal = purchQty * purchUnit;
            string? supplierName = pi?.SupplierName ?? poi?.PurchaseOrder?.Supplier?.Name ?? ii.QuoteItem?.ProcumentRecord?.Supplier?.Name;

            // Selling Price — InvoiceItem is the authoritative source.
            // Two update paths exist in UpdateItemsAsync:
            //   New path  : UnitPrice + Qty edited directly → TotalPrice = Qty × UnitPrice (final),
            //               Discount = (origUnitPrice - newUnitPrice) × Qty  (informational only, NOT a deduction).
            //   Legacy path: FinalPrice edited → TotalPrice stays at original quote total,
            //               Discount = TotalPrice - FinalPrice (IS a real deduction).
            // Distinguish by checking whether UnitPrice was changed from the original quote price.
            int     sellQty = ii.Qty;
            var     origQuoteUnitPrice = ii.QuoteItem?.UnitPrice;
            bool    usedNewPath    = origQuoteUnitPrice == null || ii.UnitPrice != origQuoteUnitPrice.Value;
            bool    usedLegacyPath = !usedNewPath && (ii.Discount ?? 0m) != 0m;
            decimal effectiveSellTotal = usedLegacyPath
                ? ii.TotalPrice - (ii.Discount ?? 0m)   // legacy: TotalPrice is original, subtract discount
                : ii.TotalPrice;                         // new path / no edit: TotalPrice is already final
            decimal sellUnit  = usedNewPath
                ? ii.UnitPrice                           // new path: UnitPrice was set directly
                : (sellQty > 0 ? effectiveSellTotal / sellQty : ii.UnitPrice);
            decimal sellTotal = effectiveSellTotal;
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
                PurchaseOrderId = po?.Id,
                PONumber = po?.PONumber,
                PORef = poi?.PORef,
                QuotationExpert = qExpert,
                ProcurementExpert = pExpert,
                Customer = customer?.CustomerCode,
                Supplier = supplierName,
                PartNumber = poi?.PartNumber?.Name ?? pi?.PartNumberName ?? ii.QuoteItem?.PartNumber?.Name,
                Description = poi?.PartNumber?.Description ?? pi?.PartNumberDescription ?? ii.QuoteItem?.PartNumber?.Description,
                Qty = sellQty,       // invoice qty — authoritative for the selling side
                Condition = poi?.Condition ?? pi?.Condition ?? ii.QuoteItem?.Condition,
                Priority = pi?.RfqPriority,
                Warehouse = (ii.QuoteItem?.RFQItem?.RFQ?.ExType ?? pi?.RfqExType) switch { 0 => "Warehouse", 1 => "Vendor/Customer", 2 => "Vendor/Customer", _ => null },
                SerialNumber = poi?.TrackNumbers != null && poi.TrackNumbers.Any()
                    ? string.Join(", ", poi.TrackNumbers
                        .SelectMany(t => t.ShipmentNotes)
                        .Select(snt => snt.ShipmentNote.SNNumber)
                        .Where(s => !string.IsNullOrEmpty(s))
                        .Distinct()) is string sns && sns.Length > 0 ? sns : null
                    : null,
                ShippingStatus = poi?.TrackNumbers != null && poi.TrackNumbers.Any()
                    ? string.Join(", ", poi.TrackNumbers
                        .Select(t => t.Status)
                        .Where(s => !string.IsNullOrEmpty(s))
                        .Distinct())
                    : null,
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

        // Final safety filter: strip any rows whose effective part number is still null/empty/"-"
        rows = rows.Where(r => !string.IsNullOrWhiteSpace(r.PartNumber) && r.PartNumber != "-").ToList();

        return new PagedResult<TotalPNRowResponse>
        {
            Items = rows,
            TotalCount = totalCount,
            Page = page.Page,
            PageSize = page.PageSize,
        };
    }

    public async Task<PagedResult<TotalPNRowResponse>> GetTotalOrderAsync(PageQuery page, long userId, bool isAdmin, bool isSuperAdmin = true, int[]? userBases = null)
    {
        var iiSet = _db.Set<InvoiceItem>()
            .Include(i => i.Invoice).ThenInclude(inv => inv.Customer)
            .Include(i => i.Invoice).ThenInclude(inv => inv.Quote)
            .Include(i => i.QuoteItem).ThenInclude(qi => qi!.ProcumentRecord).ThenInclude(pr => pr!.Supplier)
            .Include(i => i.QuoteItem).ThenInclude(qi => qi!.RFQItem).ThenInclude(ri => ri!.RFQ);

        var piSet = _db.Set<ProcurementItem>().Include(p => p.CurrentSupplier);

        // Only POItems that have at least one track number
        var poiSet = _db.Set<POItem>()
            .Where(x => x.ReturnedAt == null && x.TrackNumbers.Any())
            .Include(p => p.PurchaseOrder).ThenInclude(po => po!.Supplier)
            .Include(p => p.PartNumber)
            .Include(p => p.TrackNumbers).ThenInclude(t => t.Warehouse)
            .Include(p => p.TrackNumbers).ThenInclude(t => t.ShipmentNotes).ThenInclude(snt => snt.ShipmentNote);

        var baseQuery = from ii in iiSet
                        join pi in piSet on ii.Id equals pi.SourceInvoiceItemId into pij
                        from pi in pij.DefaultIfEmpty()
                        join poi in poiSet on pi.Id equals poi.SourceProcurementItemId
                        select new { ii, pi, poi };

        if (!isSuperAdmin)
        {
            var perms = await _db.Set<EntityPermission>()
                .Where(p => p.UserId == userId && (p.EntityName == "Invoice" || p.EntityName == "Procurement" || p.EntityName == "PO"))
                .Select(p => new { p.EntityName, p.EntityId })
                .ToListAsync();

            var invIds  = perms.Where(p => p.EntityName == "Invoice").Select(p => long.TryParse(p.EntityId, out var l) ? l : -1L).Where(l => l > 0).ToHashSet();
            var procIds = perms.Where(p => p.EntityName == "Procurement").Select(p => long.TryParse(p.EntityId, out var l) ? l : -1L).Where(l => l > 0).ToHashSet();
            var poIds   = perms.Where(p => p.EntityName == "PO").Select(p => long.TryParse(p.EntityId, out var l) ? l : -1L).Where(l => l > 0).ToHashSet();

            baseQuery = baseQuery.Where(x =>
                (userBases != null && userBases.Length > 0 && (x.ii.Invoice.Customer.Base == null || userBases.Contains(x.ii.Invoice.Customer.Base.Value))) ||
                invIds.Contains(x.ii.InvoiceId) ||
                (x.pi != null && procIds.Contains(x.pi.ProcurementId)) ||
                (x.poi != null && x.poi.POId.HasValue && poIds.Contains(x.poi.POId.Value)) ||
                (isAdmin && x.ii.Invoice.Quote.UserId == userId));
        }

        // Keep only rows that have at least one valid part number (not null, empty, or literally "-")
        baseQuery = baseQuery.Where(x =>
            (x.poi.PartNumber != null || x.poi.PartNumber.Name != "-" || x.poi.PartNumber.Name != "") ||
            (x.pi != null || x.pi.PartNumberName != null || x.pi.PartNumberName != "-" || x.pi.PartNumberName != ""));

        if (!string.IsNullOrWhiteSpace(page.Search))
        {
            var s = page.Search.Trim().ToLower();
            baseQuery = baseQuery.Where(x =>
                x.ii.Invoice.InvoiceNumber.Contains(s) ||
                (x.poi.PartNumber != null && x.poi.PartNumber.Name.ToLower().Contains(s)) ||
                x.poi.PurchaseOrder!.PONumber.Contains(s) ||
                x.poi.TrackNumbers.Any(t => t.TrackNumber.Contains(s)));
        }

        var totalCount = await baseQuery.CountAsync();

        var pageItems = await baseQuery
            .OrderByDescending(x => x.ii.Invoice.CreatedAt).ThenBy(x => x.ii.Id)
            .ApplyPaging(page)
            .ToListAsync();

        // Batch-load experts
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

        var rows = pageItems.Select(x =>
        {
            var ii = x.ii;
            var invoice = ii.Invoice;
            var customer = invoice.Customer;
            var pi = x.pi;
            var poi = x.poi;
            var po = poi.PurchaseOrder;

            string? qExpert = quoteMap.TryGetValue(invoice.QuoteId, out var qv) && userMap.TryGetValue(qv.UserId, out var qun) ? qun : null;
            string? supplierName = pi?.SupplierName ?? po?.Supplier?.Name ?? ii.QuoteItem?.ProcumentRecord?.Supplier?.Name;

            // Collect SN data from linked ShipmentNotes
            var sns = poi.TrackNumbers
                .SelectMany(t => t.ShipmentNotes.Select(snt => snt.ShipmentNote))
                .Where(sn => sn != null)
                .ToList();

            return new TotalPNRowResponse
            {
                Id = poi.Id,
                PurchaseOrderId = po?.Id,
                PONumber = po?.PONumber,
                PORef = poi.PORef,
                QuotationExpert = qExpert,
                Customer = customer?.CustomerCode,
                CustomerInvoiceNumber = invoice.InvoiceNumber,
                Supplier = supplierName,

                PurchasingUnitPriceUsd = poi.UnitPrice,
                PurchasingTotalPriceUsd = poi.TotalPrice,
                POAmount = po?.TotalAmount,

                PartNumber = poi.PartNumber?.Name ?? pi?.PartNumberName,
                Description = poi.PartNumber?.Description ?? pi?.PartNumberDescription,
                Qty = poi.Qty,
                Condition = poi.Condition ?? pi?.Condition,
                Priority = pi?.RfqPriority,
                Warehouse = poi.TrackNumbers.Select(t => t.Warehouse != null ? t.Warehouse.Name : null).FirstOrDefault(w => w != null),
                SerialNumber = sns.Select(sn => sn.SNNumber).Where(s => !string.IsNullOrEmpty(s)).Distinct() is var snNums
                    ? string.Join(", ", snNums) is string joined && joined.Length > 0 ? joined : null
                    : null,
                ShippingStatus = poi.TrackNumbers.Any()
                    ? string.Join(", ", poi.TrackNumbers.Select(t => t.Status).Where(s => !string.IsNullOrEmpty(s)).Distinct())
                    : null,
                TrackNumbers = string.Join(", ", poi.TrackNumbers.Select(t => t.TrackNumber)),
                TId = sns.Select(sn => sn.TId).FirstOrDefault(s => !string.IsNullOrEmpty(s)),
                SONumber = sns.Select(sn => sn.SONumber).FirstOrDefault(s => !string.IsNullOrEmpty(s)),
                AwbNumber = sns.Select(sn => sn.AWBNumber).FirstOrDefault(s => !string.IsNullOrEmpty(s)),
            };
        }).Where(r => !string.IsNullOrWhiteSpace(r.PartNumber) && r.PartNumber != "-").ToList();

        return new PagedResult<TotalPNRowResponse>
        {
            Items = rows,
            TotalCount = totalCount,
            Page = page.Page,
            PageSize = page.PageSize,
        };
    }

    public async Task<TotalPNFilterOptions> GetFilterOptionsAsync(long userId, bool isAdmin, bool isSuperAdmin, int[]? userBases)
    {
        var iiSet = _db.Set<InvoiceItem>()
            .Include(i => i.Invoice).ThenInclude(inv => inv.Customer)
            .Include(i => i.Invoice).ThenInclude(inv => inv.Quote);

        var piSet = _db.Set<ProcurementItem>();

        var poiSet = _db.Set<POItem>().Where(x => x.ReturnedAt == null)
            .Include(p => p.PurchaseOrder).ThenInclude(po => po!.Supplier)
            .Include(p => p.PartNumber)
            .Include(p => p.TrackNumbers);

        var baseQuery = from ii in iiSet
                        join pi in piSet on ii.Id equals pi.SourceInvoiceItemId into pij
                        from pi in pij.DefaultIfEmpty()
                        join poi in poiSet on pi.Id equals poi.SourceProcurementItemId into poij
                        from poi in poij.DefaultIfEmpty()
                        select new { ii, pi, poi };

        if (!isSuperAdmin)
        {
            var perms = await _db.Set<EntityPermission>()
                .Where(p => p.UserId == userId && (p.EntityName == "Invoice" || p.EntityName == "Procurement" || p.EntityName == "PO"))
                .Select(p => new { p.EntityName, p.EntityId }).ToListAsync();
            var invIds   = perms.Where(p => p.EntityName == "Invoice").Select(p => long.TryParse(p.EntityId, out var l) ? l : -1L).Where(l => l > 0).ToHashSet();
            var procIds  = perms.Where(p => p.EntityName == "Procurement").Select(p => long.TryParse(p.EntityId, out var l) ? l : -1L).Where(l => l > 0).ToHashSet();
            var poIds    = perms.Where(p => p.EntityName == "PO").Select(p => long.TryParse(p.EntityId, out var l) ? l : -1L).Where(l => l > 0).ToHashSet();
            
            baseQuery = baseQuery.Where(x =>
                (userBases != null && userBases.Length > 0 && (x.ii.Invoice.Customer.Base == null || userBases.Contains(x.ii.Invoice.Customer.Base.Value))) ||
                invIds.Contains(x.ii.InvoiceId) ||
                (x.pi != null && procIds.Contains(x.pi.ProcurementId)) ||
                (x.poi != null && x.poi.POId.HasValue && poIds.Contains(x.poi.POId.Value)) ||
                (isAdmin && x.ii.Invoice.Quote.UserId == userId));
        }

        // Keep only rows that have at least one valid part number (not null, empty, or literally "-")
        baseQuery = baseQuery.Where(x =>
            (x.poi != null && x.poi.PartNumber != null && x.poi.PartNumber.Name != "-" && x.poi.PartNumber.Name != "") ||
            (x.pi  != null && x.pi.PartNumberName  != null && x.pi.PartNumberName  != "-" && x.pi.PartNumberName  != "") ||
            (x.ii.QuoteItem != null && x.ii.QuoteItem.PartNumber != null &&
             x.ii.QuoteItem.PartNumber.Name != "-" && x.ii.QuoteItem.PartNumber.Name != ""));

        var rows = await baseQuery.ToListAsync();

        static string Sort(IEnumerable<string> src) => string.Empty; // placeholder
        List<string> Sorted(IEnumerable<string?> src) =>
            src.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!).Distinct().OrderBy(s => s).ToList();

        return new TotalPNFilterOptions
        {
            Customers      = Sorted(rows.Select(x => x.ii.Invoice.Customer?.CustomerCode)),
            InvoiceNumbers = Sorted(rows.Select(x => x.ii.Invoice.InvoiceNumber)),
            PartNumbers    = Sorted(rows.Select(x => x.poi?.PartNumber?.Name ?? x.pi?.PartNumberName)),
            Conditions     = Sorted(rows.Select(x => x.poi?.Condition ?? x.pi?.Condition)),
            PoNumbers      = Sorted(rows.Where(x => x.poi?.PurchaseOrder != null).Select(x => x.poi!.PurchaseOrder!.PONumber)),
            Suppliers      = Sorted(rows.Select(x => x.poi?.PurchaseOrder?.Supplier?.Name ?? x.pi?.SupplierName)),
            PaymentTerms   = Sorted(rows.Select(x => x.ii.Invoice.Status)),
            Statuses       = Sorted(rows.Where(x => x.poi?.Status != null).Select(x => x.poi!.Status)),
            ShippingStatuses = Sorted(rows.SelectMany(x => x.poi?.TrackNumbers?.Select(t => t.Status) ?? Enumerable.Empty<string?>())),
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

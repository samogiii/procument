using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Procument.Module.Catalog.Entities;
using Procument.Module.Identity.Entities;
using Procument.Module.Purchasing.DTOs;
using Procument.Module.Purchasing.Entities;
using Procument.Module.Purchasing.Services;
using Procument.Module.RFQ.Entities;
using Procument.Module.Sales.Entities;
using Procument.Shared.DTOs;
using Procument.Shared.Services;

namespace Procument.Module.Sales.Services;

/// <summary>Which rows the Total P/N report returns.</summary>
public static class TotalPNOrigins
{
    public const string All = "all";
    public const string Customer = "customer";
    public const string Stock = "stock";
    /// <summary>Customer label shown on Stock PO rows (and used by the customer column filter).</summary>
    public const string StockCustomerLabel = "OUR STOCK";
}

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
        List<string>? paymentTerms = null, List<string>? poStatuses = null, List<string>? shippingStatuses = null,
        string origin = TotalPNOrigins.Customer);
    Task<TotalPNFilterOptions> GetFilterOptionsAsync(long userId, bool isAdmin, bool isSuperAdmin, int[]? userBases,
        List<string>? customers = null, List<string>? invoiceNumbers = null, List<string>? partNumbers = null,
        List<string>? conditions = null, List<string>? poNumbers = null, List<string>? suppliers = null,
        List<string>? paymentTerms = null, List<string>? poStatuses = null, List<string>? shippingStatuses = null,
        string origin = TotalPNOrigins.Customer);
    Task<PagedResult<TotalPNRowResponse>> GetTotalOrderAsync(PageQuery page, long userId, bool isAdmin, bool isSuperAdmin = true, int[]? userBases = null);
    Task<bool> UpdateAsync(long poItemId, UpdatePOItemTotalPNRequest request);
}

public class TotalPNService : ITotalPNService
{
    private readonly DbContext _db;

    private readonly IStockReservationService _stock;

    public TotalPNService(DbContext db, IStockReservationService stock) { _db = db; _stock = stock; }

    /// <summary>
    /// Selling price of one invoice line in USD, as (unit, total). InvoiceItem is the
    /// authoritative source, but UpdateItemsAsync writes it through two different paths:
    ///   New path   : UnitPrice + Qty edited directly → TotalPrice = Qty × UnitPrice (already final),
    ///                Discount = (origUnitPrice - newUnitPrice) × Qty — informational only, NOT a deduction.
    ///   Legacy path: FinalPrice edited → TotalPrice stays at the original quote total,
    ///                Discount = TotalPrice - FinalPrice — this one IS a real deduction.
    /// They are told apart by whether UnitPrice still matches the source quote's price.
    /// Shared by the Total P/N and Total Order grids so the two can never disagree on a price.
    /// </summary>
    private static (decimal Unit, decimal Total) ResolveSellingPrice(InvoiceItem ii)
    {
        var origQuoteUnitPrice = ii.QuoteItem?.UnitPrice;
        bool usedNewPath    = origQuoteUnitPrice == null || ii.UnitPrice != origQuoteUnitPrice.Value;
        bool usedLegacyPath = !usedNewPath && (ii.Discount ?? 0m) != 0m;

        decimal total = usedLegacyPath
            ? ii.TotalPrice - (ii.Discount ?? 0m)   // legacy: TotalPrice is the original, subtract the discount
            : ii.TotalPrice;                        // new path / untouched: TotalPrice is already final

        decimal unit = usedNewPath
            ? ii.UnitPrice                          // new path: UnitPrice was set directly
            : (ii.Qty > 0 ? total / ii.Qty : ii.UnitPrice);

        return (unit, total);
    }

    private async Task<(Dictionary<long, List<string>> ItemExperts, Dictionary<long, List<string>> InvoiceExperts, Dictionary<long, List<string>> DefaultExperts)>
        LoadExpertAssignmentsAsync(List<long> invoiceIds, List<long> invoiceItemIds)
    {
        var invoiceIdStrings = invoiceIds.Select(id => id.ToString()).ToList();
        var invoiceItemIdStrings = invoiceItemIds.Select(id => id.ToString()).ToList();
        var sources = await _db.Set<InvoiceItem>()
            .AsNoTracking()
            .Where(item => invoiceItemIds.Contains(item.Id) && item.QuoteItem != null)
            .Select(item => new
            {
                item.InvoiceId,
                QuoteId = item.QuoteItem!.QuoteId,
                QuoteOwnerId = item.QuoteItem.Quote.UserId,
                RfqId = item.QuoteItem.Quote.RFQId,
                RfqOwnerId = item.QuoteItem.Quote.RFQ.UserId,
            })
            .ToListAsync();
        var quoteIdStrings = sources.Select(source => source.QuoteId.ToString()).Distinct().ToList();
        var rfqIdStrings = sources.Select(source => source.RfqId.ToString()).Distinct().ToList();

        var assignments = await _db.Set<EntityPermission>()
            .Include(permission => permission.User)
            .Where(permission =>
                (permission.EntityName == "Invoice" && invoiceIdStrings.Contains(permission.EntityId)) ||
                (permission.EntityName == "InvoiceItem" && invoiceItemIdStrings.Contains(permission.EntityId)) ||
                (permission.EntityName == "Quote" && quoteIdStrings.Contains(permission.EntityId)) ||
                (permission.EntityName == "RFQ" && rfqIdStrings.Contains(permission.EntityId)))
            .Select(permission => new { permission.EntityName, permission.EntityId, permission.UserId, permission.User.Name })
            .ToListAsync();

        Dictionary<long, List<string>> Build(string entityName) => assignments
            .Where(assignment => assignment.EntityName == entityName && long.TryParse(assignment.EntityId, out _))
            .GroupBy(assignment => long.Parse(assignment.EntityId))
            .ToDictionary(
                group => group.Key,
                group => group.Select(assignment => assignment.Name)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name)
                    .ToList());

        var userIds = sources.Select(source => source.QuoteOwnerId)
            .Concat(sources.Where(source => source.RfqOwnerId.HasValue).Select(source => source.RfqOwnerId!.Value))
            .Distinct()
            .ToList();
        var ownerNames = await _db.Set<User>().AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.Name);
        var defaultExperts = new Dictionary<long, List<string>>();
        foreach (var invoiceId in invoiceIds)
        {
            var invoiceSources = sources.Where(source => source.InvoiceId == invoiceId).ToList();
            var quoteIds = invoiceSources.Select(source => source.QuoteId.ToString()).ToHashSet();
            var rfqIds = invoiceSources.Select(source => source.RfqId.ToString()).ToHashSet();
            defaultExperts[invoiceId] = invoiceSources.Select(source => source.QuoteOwnerId)
                .Concat(invoiceSources.Where(source => source.RfqOwnerId.HasValue).Select(source => source.RfqOwnerId!.Value))
                .Select(id => ownerNames.GetValueOrDefault(id))
                .Concat(assignments.Where(assignment =>
                    (assignment.EntityName == "Quote" && quoteIds.Contains(assignment.EntityId))
                    || (assignment.EntityName == "RFQ" && rfqIds.Contains(assignment.EntityId)))
                    .Select(assignment => assignment.Name))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name)
                .ToList();
        }

        return (Build("InvoiceItem"), Build("Invoice"), defaultExperts);
    }

    public async Task<PagedResult<TotalPNRowResponse>> GetAsync(PageQuery page, long userId, bool isAdmin, string? sortBy = null, bool sortDesc = false, bool isSuperAdmin = true, int[]? userBases = null,
        List<string>? customers = null, List<string>? invoiceNumbers = null, List<string>? partNumbers = null,
        List<string>? conditions = null, List<string>? poNumbers = null, List<string>? suppliers = null,
        List<string>? paymentTerms = null, List<string>? poStatuses = null, List<string>? shippingStatuses = null,
        string origin = TotalPNOrigins.Customer)
    {
        var includeCustomer = origin != TotalPNOrigins.Stock;
        var includeStock = origin != TotalPNOrigins.Customer;

        // ── Base query: start from InvoiceItems (so they show up immediately on PI creation) ──
        // Left-join with ProcurementItem (the worksheet) and POItem (the purchase).
        // Applying Includes to the source sets because they can't be applied to the anonymous type after join.
        var iiSet = _db.Set<InvoiceItem>()
            // A cancelled Proforma Invoice is not an active project and must not appear in Total P/N.
            .Where(i => !i.Invoice.IsCancelled)
            .Include(i => i.Invoice).ThenInclude(inv => inv.Customer)
            .Include(i => i.Invoice).ThenInclude(inv => inv.Quote)
            .Include(i => i.QuoteItem).ThenInclude(qi => qi!.ProcumentRecord).ThenInclude(pr => pr!.Supplier)
            .Include(i => i.QuoteItem).ThenInclude(qi => qi!.PartNumber);

        var piSet = _db.Set<ProcurementItem>()
            .Include(p => p.CurrentSupplier);

        var poiSet = _db.Set<POItem>().Where(x =>
                // Returned PO items are historical only. Their procurement item is the single
                // current source of truth when it is re-approved for a new PO.
                x.ReturnedAt == null &&
                // Keep unassigned PO items, but never join lines from cancelled/returned POs.
                // This prevents those old PO rows from duplicating the re-opened procurement line.
                (!x.POId.HasValue || (x.PurchaseOrder != null &&
                    x.PurchaseOrder.Status != "Cancelled" && x.PurchaseOrder.Status != "Returned")))
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
            var directAssignments = await _db.Set<EntityPermission>()
                .Where(p => p.EntityName == "Invoice" || p.EntityName == "InvoiceItem")
                .Select(p => new { p.EntityName, p.EntityId, p.UserId })
                .ToListAsync();
            var perms = await _db.Set<EntityPermission>()
                .Where(p => p.UserId == userId && (p.EntityName == "Invoice" || p.EntityName == "InvoiceItem" || p.EntityName == "Procurement" || p.EntityName == "PO"))
                .Select(p => new { p.EntityName, p.EntityId })
                .ToListAsync();

            var invIds  = perms.Where(p => p.EntityName == "Invoice").Select(p => long.TryParse(p.EntityId, out var l) ? l : -1L).Where(l => l > 0).ToHashSet();
            var invoiceItemIds = perms.Where(p => p.EntityName == "InvoiceItem").Select(p => long.TryParse(p.EntityId, out var l) ? l : -1L).Where(l => l > 0).ToHashSet();
            var procIds = perms.Where(p => p.EntityName == "Procurement").Select(p => long.TryParse(p.EntityId, out var l) ? l : -1L).Where(l => l > 0).ToHashSet();
            var poIds   = perms.Where(p => p.EntityName == "PO").Select(p => long.TryParse(p.EntityId, out var l) ? l : -1L).Where(l => l > 0).ToHashSet();
            var assignedInvIds = directAssignments.Where(p => p.EntityName == "Invoice").Select(p => long.TryParse(p.EntityId, out var l) ? l : -1L).Where(l => l > 0).ToHashSet();
            var assignedItemIds = directAssignments.Where(p => p.EntityName == "InvoiceItem").Select(p => long.TryParse(p.EntityId, out var l) ? l : -1L).Where(l => l > 0).ToHashSet();

            baseQuery = baseQuery.Where(x =>
                (assignedItemIds.Contains(x.ii.Id)
                    ? invoiceItemIds.Contains(x.ii.Id)
                    : assignedInvIds.Contains(x.ii.InvoiceId)
                        ? invIds.Contains(x.ii.InvoiceId)
                        : (x.ii.QuoteItem != null && x.ii.QuoteItem.Quote.RFQ.UserId == userId)
                            || (x.pi != null && procIds.Contains(x.pi.ProcurementId))
                            || (x.poi != null && x.poi.POId.HasValue && poIds.Contains(x.poi.POId.Value))));
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

        var customerCount = includeCustomer ? await baseQuery.CountAsync() : 0;
        var skip = page.PageSize == -1 ? 0 : (page.Page - 1) * page.PageSize;
        var take = page.PageSize == -1 ? int.MaxValue : page.PageSize;
        var customerTake = Math.Max(0, Math.Min(take, customerCount - skip));

        var orderedQuery = sortBy switch
        {
            "customer"   => sortDesc ? baseQuery.OrderByDescending(x => x.ii.Invoice.Customer.Name)        : baseQuery.OrderBy(x => x.ii.Invoice.Customer.Name),
            "partNumber" => sortDesc ? baseQuery.OrderByDescending(x => x.poi != null ? x.poi.PartNumber.Name : x.pi != null ? x.pi.PartNumberName : "") : baseQuery.OrderBy(x => x.poi != null ? x.poi.PartNumber.Name : x.pi != null ? x.pi.PartNumberName : ""),
            "qty"        => sortDesc ? baseQuery.OrderByDescending(x => x.poi != null ? x.poi.Qty : 0)    : baseQuery.OrderBy(x => x.poi != null ? x.poi.Qty : 0),
            "status"     => sortDesc ? baseQuery.OrderByDescending(x => x.poi != null ? x.poi.Status : "") : baseQuery.OrderBy(x => x.poi != null ? x.poi.Status : ""),
            "invDate"    => sortDesc ? baseQuery.OrderByDescending(x => x.ii.Invoice.CreatedAt)            : baseQuery.OrderBy(x => x.ii.Invoice.CreatedAt),
            _            => baseQuery.OrderByDescending(x => x.ii.Invoice.CreatedAt).ThenBy(x => x.ii.Id),
        };

        // Customer rows come first; Stock PO rows (Our Inventory) follow them in the same paging.
        var pageItems = customerTake > 0
            ? await orderedQuery.Skip(skip).Take(customerTake).ToListAsync()
            : (await orderedQuery.Take(0).ToListAsync());

        // Proforma Invoice assignments are authoritative. RFQ assignments are the fallback
        // for an invoice that has not yet had any experts assigned directly to it.
        var invoiceIdsInPage = pageItems.Select(x => x.ii.InvoiceId).Distinct().ToList();
        var invoiceItemIdsInPage = pageItems.Select(x => x.ii.Id).Distinct().ToList();
        var (itemExperts, invoiceExperts, defaultExperts) = await LoadExpertAssignmentsAsync(invoiceIdsInPage, invoiceItemIdsInPage);
        var poItemSupplierIds = pageItems.Where(x => x.poi?.SupplierId != null)
            .Select(x => x.poi!.SupplierId!.Value).Distinct().ToList();
        var poItemSupplierNames = poItemSupplierIds.Count > 0
            ? await _db.Set<Supplier>().Where(supplier => poItemSupplierIds.Contains(supplier.Id))
                .ToDictionaryAsync(supplier => supplier.Id, supplier => supplier.Name)
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

            var experts = itemExperts.TryGetValue(ii.Id, out var assignedItemExperts) && assignedItemExperts.Count > 0
                ? assignedItemExperts
                : invoiceExperts.TryGetValue(invoice.Id, out var assignedInvoiceExperts) && assignedInvoiceExperts.Count > 0
                    ? assignedInvoiceExperts
                    : defaultExperts.GetValueOrDefault(invoice.Id) ?? [];

            // Purchasing Price, Qty & Supplier Sync
            // Prioritize the Purchase Order (POItem) — it represents the final business commitment.
            // Edits made at the PO level (even if the Edit tab is now restricted) must be the authoritative 
            // source for reporting to ensure consistency with what was actually ordered/paid.
            // Fall back to the Procurement worksheet snapshot only when no POItem exists yet.
            decimal purchUnit    = poi?.UnitPrice ?? pi?.UnitPrice ?? 0m;
            int     purchQty     = poi?.Qty ?? pi?.Qty ?? ii.Qty;
            decimal purchTotal   = purchQty * purchUnit;
            string? supplierName = poi?.PurchaseOrder?.Supplier?.Name
                ?? (poi?.SupplierId is long poItemSupplierId && poItemSupplierNames.TryGetValue(poItemSupplierId, out var poItemSupplierName)
                    ? poItemSupplierName
                    : null)
                ?? pi?.CurrentSupplier?.Name
                ?? pi?.SupplierName
                ?? ii.QuoteItem?.ProcumentRecord?.Supplier?.Name;

            var (sellUnit, sellTotal) = ResolveSellingPrice(ii);
            decimal rate = (customer?.CurrencyType == "Yuan" || customer?.CurrencyType == "Both") ? 7m : 1m;

            // POItem/PI line status is authoritative. Automatic workflow events write it once;
            // this report only formats the In Shop countdown instead of re-deriving a different state.
            var status = poi != null
                ? PurchaseOrderStatusFlow.DisplayStatus(poi)
                : ii.Status ?? PurchaseOrderStatusFlow.NotStarted;

            // Payment / Invoice meta
            FinalInvoice? fi = finalInvoiceMap.TryGetValue(invoice.Id, out var fiv) ? fiv : null;
            decimal? recvTotal = paymentAgg.TryGetValue(invoice.Id, out var agg) ? agg.Total : null;
            DateTime? recvDate = paymentAgg.TryGetValue(invoice.Id, out var agg2) ? agg2.LastDate : null;

            return new TotalPNRowResponse
            {
                Id = poi?.Id ?? -ii.Id, // Use negative InvoiceItemID as stable ID for unassigned rows
                PurchaseOrderId = po?.Id,
                PurchaseOrderStatus = po?.Status,
                ProcurementId = pi?.ProcurementId,
                ProcurementItemId = pi?.Id,
                PONumber = po?.PONumber,
                PORef = poi?.PORef,
                Experts = experts,
                Customer = customer?.CustomerCode,
                Supplier = supplierName,
                PartNumber = poi?.PartNumber?.Name ?? pi?.PartNumberName ?? ii.QuoteItem?.PartNumber?.Name,
                Description = poi?.PartNumber?.Description ?? pi?.PartNumberDescription ?? ii.QuoteItem?.PartNumber?.Description,
                Qty = purchQty,      // Prioritize PO qty over Invoice qty for consistent purchasing vs selling tracking
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
                InvoiceId = invoice.Id,
                InvoiceItemId = ii.Id,
                CustomerInvoiceNumber = invoice.InvoiceNumber,
                ProformaInvoiceStatus = invoice.Status,
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
                PaymentTerm = invoice.PaymentStatus,
                CustomerDeliveryTime = invoice.DueDate,
                Rate = rate,
                TrackNumbers = poi?.TrackNumbers != null ? string.Join(", ", poi.TrackNumbers.Select(t => t.TrackNumber)) : null,
                ShippingCost = pi?.ShippingCost != null ? (decimal)pi.ShippingCost.Value : null,
                Note = poi?.Note,
            };
        }).ToList();

        // Final safety filter: strip any rows whose effective part number is still null/empty/"-"
        rows = rows.Where(r => !string.IsNullOrWhiteSpace(r.PartNumber) && r.PartNumber != "-").ToList();

        // Lines served from Our Stock have no POItem: cost comes from the stock issue, status from the reservation.
        var stockLineIds = pageItems.Where(x => x.pi != null && x.pi.FromStock && x.poi == null).Select(x => x.pi!.Id).ToHashSet();
        foreach (var row in rows.Where(r => r.ProcurementItemId.HasValue && stockLineIds.Contains(r.ProcurementItemId.Value)))
        {
            var stock = await _stock.GetLineStatusAsync(row.InvoiceItemId);
            if (stock.IssueCost is decimal cost)
            {
                row.PurchasingUnitPriceUsd = cost;
                row.PurchasingTotalPriceUsd = cost * row.Qty;
            }
            row.ShippingStatus = stock.Issued > 0
                ? $"Issued {stock.Issued:0.##} from Our Stock"
                : $"Reserved {stock.Reserved:0.##} in Our Stock";
        }

        var stockCount = 0;
        if (includeStock)
        {
            var stockQuery = StockPoLineQuery(page.Search, customers, invoiceNumbers, partNumbers, conditions, poNumbers,
                suppliers, paymentTerms, poStatuses, shippingStatuses);
            stockCount = await stockQuery.CountAsync();
            var stockSkip = Math.Max(0, skip - customerCount);
            var stockTake = take == int.MaxValue ? int.MaxValue : take - customerTake;
            if (stockTake > 0 && stockSkip < stockCount)
                rows.AddRange(await BuildStockPoRowsAsync(stockQuery, sortBy, sortDesc, stockSkip, stockTake));
        }

        return new PagedResult<TotalPNRowResponse>
        {
            Items = rows,
            TotalCount = customerCount + stockCount,
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
                .Where(p => p.UserId == userId && (p.EntityName == "Invoice" || p.EntityName == "InvoiceItem" || p.EntityName == "Procurement" || p.EntityName == "PO"))
                .Select(p => new { p.EntityName, p.EntityId })
                .ToListAsync();

            var invIds  = perms.Where(p => p.EntityName == "Invoice").Select(p => long.TryParse(p.EntityId, out var l) ? l : -1L).Where(l => l > 0).ToHashSet();
            var invoiceItemIds = perms.Where(p => p.EntityName == "InvoiceItem").Select(p => long.TryParse(p.EntityId, out var l) ? l : -1L).Where(l => l > 0).ToHashSet();
            var procIds = perms.Where(p => p.EntityName == "Procurement").Select(p => long.TryParse(p.EntityId, out var l) ? l : -1L).Where(l => l > 0).ToHashSet();
            var poIds   = perms.Where(p => p.EntityName == "PO").Select(p => long.TryParse(p.EntityId, out var l) ? l : -1L).Where(l => l > 0).ToHashSet();

            baseQuery = baseQuery.Where(x =>
                (userBases != null && userBases.Length > 0 && (x.ii.Invoice.Customer.Base == null || userBases.Contains(x.ii.Invoice.Customer.Base.Value))) ||
                invIds.Contains(x.ii.InvoiceId) ||
                invoiceItemIds.Contains(x.ii.Id) ||
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

        var rows = pageItems.Select(x =>
        {
            var ii = x.ii;
            var invoice = ii.Invoice;
            var customer = invoice.Customer;
            var pi = x.pi;
            var poi = x.poi;
            var po = poi.PurchaseOrder;

            string? supplierName = po?.Supplier?.Name ?? pi?.SupplierName ?? ii.QuoteItem?.ProcumentRecord?.Supplier?.Name;

            // Buy price comes off the POItem below; sell price off the invoice line, through the
            // same rule the Total P/N grid uses.
            var (sellUnit, sellTotal) = ResolveSellingPrice(ii);

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
                Customer = customer?.CustomerCode,
                InvoiceId = invoice.Id,
                CustomerInvoiceNumber = invoice.InvoiceNumber,
                Supplier = supplierName,

                PurchasingUnitPriceUsd = poi.UnitPrice,
                PurchasingTotalPriceUsd = poi.TotalPrice,
                SellingUnitPriceUsd = sellUnit,
                SellingTotalPriceUsd = sellTotal,
                POAmount = po?.TotalAmount,

                PartNumber = poi.PartNumber?.Name ?? pi?.PartNumberName,
                Description = poi.PartNumber?.Description ?? pi?.PartNumberDescription,
                Qty = poi.Qty,
                Condition = poi.Condition ?? pi?.Condition,
                Priority = pi?.RfqPriority,
                Warehouse = BuildWarehouseChain(poi.TrackNumbers),
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

    /// <summary>
    /// Where the part is now, then each warehouse it moved from: "Dubai ← Istanbul ← Shanghai".
    ///
    /// Only a warehouse-to-warehouse transfer is a move, and each one links its destination leg
    /// back to the source through <see cref="POItemTrackNumber.ParentTrackNumberId"/>, so the
    /// chain is built by following those links — never by ordering legs on date. A part can hold
    /// several legs for reasons that are not movement at all (a split delivery, or a second track
    /// covering a shortfall); those are separate arrivals and are listed comma-separated, because
    /// joining them with arrows would claim a move that never happened.
    /// </summary>
    private static string? BuildWarehouseChain(IEnumerable<POItemTrackNumber> tracks)
    {
        var legs = tracks
            .Where(t => t.Warehouse != null && !string.IsNullOrWhiteSpace(t.Warehouse.Name))
            .ToList();

        if (legs.Count == 0) return null;

        var byId = legs.ToDictionary(t => t.Id);
        var used = new HashSet<long>();
        var chain = new List<string>();

        // Newest transfer arrival is the part's current home; walk its parent links back
        // through every hop it made.
        var newestHop = legs
            .Where(t => t.SourceTransferId.HasValue)
            .OrderByDescending(t => t.CreatedAt)
            .ThenByDescending(t => t.Id)
            .FirstOrDefault();

        for (var cur = newestHop; cur != null; )
        {
            if (!used.Add(cur.Id)) break;   // guard against a cycle in the links
            chain.Add(cur.Warehouse!.Name);

            cur = cur.ParentTrackNumberId.HasValue && byId.TryGetValue(cur.ParentTrackNumberId.Value, out var parent)
                ? parent
                : null;
        }

        // Legs that were never part of that journey — independent arrivals for this part.
        var separate = legs
            .Where(t => !used.Contains(t.Id))
            .OrderBy(t => t.CreatedAt)
            .Select(t => t.Warehouse!.Name)
            .Distinct()
            .Where(n => !chain.Contains(n))
            .ToList();

        var moved = string.Join(" ← ", chain);
        if (separate.Count == 0) return moved.Length > 0 ? moved : null;

        var arrivals = string.Join(", ", separate);
        return moved.Length > 0 ? $"{moved}, {arrivals}" : arrivals;
    }

    public async Task<TotalPNFilterOptions> GetFilterOptionsAsync(long userId, bool isAdmin, bool isSuperAdmin, int[]? userBases,
        List<string>? customers = null, List<string>? invoiceNumbers = null, List<string>? partNumbers = null,
        List<string>? conditions = null, List<string>? poNumbers = null, List<string>? suppliers = null,
        List<string>? paymentTerms = null, List<string>? poStatuses = null, List<string>? shippingStatuses = null,
        string origin = TotalPNOrigins.Customer)
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
            var directAssignments = await _db.Set<EntityPermission>()
                .Where(p => p.EntityName == "Invoice" || p.EntityName == "InvoiceItem")
                .Select(p => new { p.EntityName, p.EntityId, p.UserId })
                .ToListAsync();
            var perms = await _db.Set<EntityPermission>()
                .Where(p => p.UserId == userId && (p.EntityName == "Invoice" || p.EntityName == "InvoiceItem" || p.EntityName == "Procurement" || p.EntityName == "PO"))
                .Select(p => new { p.EntityName, p.EntityId }).ToListAsync();
            var invIds   = perms.Where(p => p.EntityName == "Invoice").Select(p => long.TryParse(p.EntityId, out var l) ? l : -1L).Where(l => l > 0).ToHashSet();
            var invoiceItemIds = perms.Where(p => p.EntityName == "InvoiceItem").Select(p => long.TryParse(p.EntityId, out var l) ? l : -1L).Where(l => l > 0).ToHashSet();
            var procIds  = perms.Where(p => p.EntityName == "Procurement").Select(p => long.TryParse(p.EntityId, out var l) ? l : -1L).Where(l => l > 0).ToHashSet();
            var poIds    = perms.Where(p => p.EntityName == "PO").Select(p => long.TryParse(p.EntityId, out var l) ? l : -1L).Where(l => l > 0).ToHashSet();
            var assignedInvIds = directAssignments.Where(p => p.EntityName == "Invoice").Select(p => long.TryParse(p.EntityId, out var l) ? l : -1L).Where(l => l > 0).ToHashSet();
            var assignedItemIds = directAssignments.Where(p => p.EntityName == "InvoiceItem").Select(p => long.TryParse(p.EntityId, out var l) ? l : -1L).Where(l => l > 0).ToHashSet();
            
            baseQuery = baseQuery.Where(x =>
                (assignedItemIds.Contains(x.ii.Id)
                    ? invoiceItemIds.Contains(x.ii.Id)
                    : assignedInvIds.Contains(x.ii.InvoiceId)
                        ? invIds.Contains(x.ii.InvoiceId)
                        : (x.ii.QuoteItem != null && x.ii.QuoteItem.Quote.RFQ.UserId == userId)
                            || (x.pi != null && procIds.Contains(x.pi.ProcurementId))
                            || (x.poi != null && x.poi.POId.HasValue && poIds.Contains(x.poi.POId.Value))));
        }

        // Keep only rows that have at least one valid part number (not null, empty, or literally "-")
        baseQuery = baseQuery.Where(x =>
            (x.poi != null && x.poi.PartNumber != null && x.poi.PartNumber.Name != "-" && x.poi.PartNumber.Name != "") ||
            (x.pi  != null && x.pi.PartNumberName  != null && x.pi.PartNumberName  != "-" && x.pi.PartNumberName  != "") ||
            (x.ii.QuoteItem != null && x.ii.QuoteItem.PartNumber != null &&
             x.ii.QuoteItem.PartNumber.Name != "-" && x.ii.QuoteItem.PartNumber.Name != ""));

        var rows = origin == TotalPNOrigins.Stock ? (await baseQuery.Take(0).ToListAsync()) : await baseQuery.ToListAsync();

        List<string> Sorted(IEnumerable<string?> src) =>
            src.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!).Distinct().OrderBy(s => s).ToList();

        var filterRows = rows.Select(x => new TotalPNFilterRow
        {
            Customer = x.ii.Invoice.Customer?.CustomerCode,
            InvoiceNumber = x.ii.Invoice.InvoiceNumber,
            PartNumber = x.poi?.PartNumber?.Name ?? x.pi?.PartNumberName,
            Condition = x.poi?.Condition ?? x.pi?.Condition,
            PoNumber = x.poi?.PurchaseOrder?.PONumber,
            Supplier = x.poi?.PurchaseOrder?.Supplier?.Name ?? x.pi?.SupplierName,
            PaymentTerm = x.ii.Invoice.Status,
            Status = x.poi?.Status,
            ShippingStatuses = x.poi == null
                ? []
                : x.poi.TrackNumbers
                    .Select(t => t.Status)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList()
        }).ToList();

        if (origin != TotalPNOrigins.Customer)
        {
            var stockLines = await StockPoLineQuery(null, null, null, null, null, null, null, null, null, null)
                .Select(i => new
                {
                    PartNumber = i.PartNumber != null ? i.PartNumber.Name : null,
                    i.Condition, i.PurchaseOrder!.PONumber, Supplier = i.PurchaseOrder.Supplier.Name, i.Status,
                    Tracks = i.TrackNumbers.Select(t => t.Status).ToList(),
                })
                .ToListAsync();
            filterRows.AddRange(stockLines.Select(x => new TotalPNFilterRow
            {
                Customer = TotalPNOrigins.StockCustomerLabel, PartNumber = x.PartNumber, Condition = x.Condition,
                PoNumber = x.PONumber, Supplier = x.Supplier, Status = x.Status,
                ShippingStatuses = x.Tracks.Where(s => !string.IsNullOrWhiteSpace(s)).ToList(),
            }));
        }

        IEnumerable<TotalPNFilterRow> Build(string excludedColumn)
        {
            IEnumerable<TotalPNFilterRow> query = filterRows;
            if (excludedColumn != "customer" && customers?.Count > 0)
                query = query.Where(x => customers.Contains(x.Customer ?? ""));
            if (excludedColumn != "customerInvoiceNumber" && invoiceNumbers?.Count > 0)
                query = query.Where(x => invoiceNumbers.Contains(x.InvoiceNumber ?? ""));
            if (excludedColumn != "partNumber" && partNumbers?.Count > 0)
                query = query.Where(x => partNumbers.Contains(x.PartNumber ?? ""));
            if (excludedColumn != "condition" && conditions?.Count > 0)
                query = query.Where(x => conditions.Contains(x.Condition ?? ""));
            if (excludedColumn != "poNumber" && poNumbers?.Count > 0)
                query = query.Where(x => poNumbers.Contains(x.PoNumber ?? ""));
            if (excludedColumn != "supplier" && suppliers?.Count > 0)
                query = query.Where(x => suppliers.Contains(x.Supplier ?? ""));
            if (excludedColumn != "paymentTerm" && paymentTerms?.Count > 0)
                query = query.Where(x => paymentTerms.Contains(x.PaymentTerm ?? ""));
            if (excludedColumn != "status" && poStatuses?.Count > 0)
                query = query.Where(x => poStatuses.Contains(x.Status ?? ""));
            if (excludedColumn != "shippingStatus" && shippingStatuses?.Count > 0)
                query = query.Where(x => x.ShippingStatuses.Any(shippingStatuses.Contains));
            return query;
        }

        return new TotalPNFilterOptions
        {
            Customers = Sorted(Build("customer").Select(x => x.Customer)),
            InvoiceNumbers = Sorted(Build("customerInvoiceNumber").Select(x => x.InvoiceNumber)),
            PartNumbers = Sorted(Build("partNumber").Select(x => x.PartNumber)),
            Conditions = Sorted(Build("condition").Select(x => x.Condition)),
            PoNumbers = Sorted(Build("poNumber").Select(x => x.PoNumber)),
            Suppliers = Sorted(Build("supplier").Select(x => x.Supplier)),
            PaymentTerms = Sorted(Build("paymentTerm").Select(x => x.PaymentTerm)),
            Statuses = Sorted(Build("status").Select(x => x.Status)),
            ShippingStatuses = Sorted(Build("shippingStatus").SelectMany(x => x.ShippingStatuses)),
        };
    }

    /// <summary>
    /// Stock PO lines (Our Inventory) for the report. They have no Sales Order, so PI-only filters
    /// (PI number, payment term, or a customer other than "OUR STOCK") exclude them.
    /// </summary>
    private IQueryable<POItem> StockPoLineQuery(string? search, List<string>? customers, List<string>? invoiceNumbers,
        List<string>? partNumbers, List<string>? conditions, List<string>? poNumbers, List<string>? suppliers,
        List<string>? paymentTerms, List<string>? poStatuses, List<string>? shippingStatuses)
    {
        var q = _db.Set<POItem>().AsNoTracking()
            .Where(i => i.ReturnedAt == null && i.PurchaseOrder != null && i.PurchaseOrder.Origin == "Stock"
                && i.PurchaseOrder.Status != "Cancelled" && i.PurchaseOrder.Status != "Returned");

        if ((customers?.Count > 0 && !customers.Contains(TotalPNOrigins.StockCustomerLabel))
            || invoiceNumbers?.Count > 0 || paymentTerms?.Count > 0)
            return q.Where(_ => false);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(i => i.PurchaseOrder!.PONumber.ToLower().Contains(s)
                || (i.PartNumber != null && i.PartNumber.Name.ToLower().Contains(s)));
        }
        if (partNumbers?.Count > 0) q = q.Where(i => i.PartNumber != null && partNumbers.Contains(i.PartNumber.Name));
        if (conditions?.Count > 0) q = q.Where(i => i.Condition != null && conditions.Contains(i.Condition));
        if (poNumbers?.Count > 0) q = q.Where(i => poNumbers.Contains(i.PurchaseOrder!.PONumber));
        if (suppliers?.Count > 0) q = q.Where(i => suppliers.Contains(i.PurchaseOrder!.Supplier.Name));
        if (poStatuses?.Count > 0) q = q.Where(i => i.Status != null && poStatuses.Contains(i.Status));
        if (shippingStatuses?.Count > 0) q = q.Where(i => i.TrackNumbers.Any(t => shippingStatuses.Contains(t.Status)));
        return q;
    }

    private async Task<List<TotalPNRowResponse>> BuildStockPoRowsAsync(IQueryable<POItem> query, string? sortBy, bool sortDesc, int skip, int take)
    {
        var ordered = sortBy switch
        {
            "partNumber" => sortDesc ? query.OrderByDescending(i => i.PartNumber!.Name) : query.OrderBy(i => i.PartNumber!.Name),
            "qty" => sortDesc ? query.OrderByDescending(i => i.Qty) : query.OrderBy(i => i.Qty),
            "status" => sortDesc ? query.OrderByDescending(i => i.Status) : query.OrderBy(i => i.Status),
            _ => query.OrderByDescending(i => i.PurchaseOrder!.CreatedAt).ThenBy(i => i.PORef),
        };
        var lines = await ordered.Skip(skip).Take(take)
            .Include(i => i.PartNumber)
            .Include(i => i.PurchaseOrder!).ThenInclude(po => po.Supplier)
            .Include(i => i.PurchaseOrder!).ThenInclude(po => po.DestinationWarehouse)
            .Include(i => i.TrackNumbers)
            .ToListAsync();
        if (lines.Count == 0) return [];

        var received = await _stock.GetReceivedByPoItemAsync(lines.Select(l => l.Id).ToList());
        var poIds = lines.Select(l => l.POId!.Value).Distinct().Select(id => id.ToString()).ToList();
        var assigned = await (
                from permission in _db.Set<EntityPermission>().AsNoTracking()
                join user in _db.Set<User>().AsNoTracking() on permission.UserId equals user.Id
                where permission.EntityName == "PO" && poIds.Contains(permission.EntityId)
                select new { permission.EntityId, user.Name })
            .ToListAsync();

        return lines.Select(line =>
        {
            var po = line.PurchaseOrder!;
            var got = received.GetValueOrDefault(line.Id);
            var tracks = line.TrackNumbers.Select(t => t.Status).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
            if (got > 0 || po.AdminApproval == "Approved") tracks.Add($"Received {got:0.##}/{line.Qty} into Our Stock");
            return new TotalPNRowResponse
            {
                Id = line.Id,
                PurchaseOrderId = po.Id,
                PurchaseOrderStatus = po.Status,
                PONumber = po.PONumber,
                PORef = line.PORef,
                Experts = assigned.Where(a => a.EntityId == po.Id.ToString()).Select(a => a.Name)
                    .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n).ToList(),
                Customer = TotalPNOrigins.StockCustomerLabel,
                Supplier = po.Supplier?.Name,
                PartNumber = line.PartNumber?.Name,
                Description = line.PartNumber?.Description,
                Qty = line.Qty,
                Condition = line.Condition,
                Warehouse = po.DestinationWarehouse?.DisplayName ?? po.DestinationWarehouse?.Name,
                ShippingStatus = string.Join(", ", tracks),
                InvoiceId = null,
                PurchasingUnitPriceUsd = line.UnitPrice,
                PurchasingTotalPriceUsd = line.TotalPrice,
                POAmount = po.TotalAmount,
                SupplierDeliveryTime = po.ExpectedDeliveryDate?.ToString("yyyy-MM-dd"),
                Status = PurchaseOrderStatusFlow.DisplayStatus(line),
                PODate = po.CreatedAt,
                Rate = 1m,
                TrackNumbers = line.TrackNumbers.Count > 0 ? string.Join(", ", line.TrackNumbers.Select(t => t.TrackNumber)) : null,
                ShippingCost = po.Shipping,
                Note = line.Note,
            };
        }).ToList();
    }

    private sealed class TotalPNFilterRow
    {
        public string? Customer { get; init; }
        public string? InvoiceNumber { get; init; }
        public string? PartNumber { get; init; }
        public string? Condition { get; init; }
        public string? PoNumber { get; init; }
        public string? Supplier { get; init; }
        public string? PaymentTerm { get; init; }
        public string? Status { get; init; }
        public List<string> ShippingStatuses { get; init; } = [];
    }

    public async Task<bool> UpdateAsync(long poItemId, UpdatePOItemTotalPNRequest request)
    {
        var item = await _db.Set<POItem>().FindAsync(poItemId);
        if (item == null) return false;
        if (request.Status != null)
        {
            item.Status = request.Status;
            await PurchaseOrderStatusFlow.SyncPiItemsAsync(_db, new[] { item }, request.Status);
        }
        if (request.Note != null) item.Note = request.Note;
        await _db.SaveChangesAsync();
        return true;
    }
}

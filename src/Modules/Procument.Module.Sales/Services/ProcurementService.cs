using Microsoft.EntityFrameworkCore;
using Procument.Module.Catalog.Entities;
using Procument.Module.Identity.Entities;
using Procument.Module.Purchasing.DTOs;
using Procument.Module.Purchasing.Entities;
using Procument.Module.Purchasing.Services;
using Procument.Module.Sales.Entities;
using Procument.Shared.DTOs;
using Procument.Shared.Entities;

namespace Procument.Module.Sales.Services;

public class ProcurementService : IProcurementService
{
    private readonly DbContext _db;

    public ProcurementService(DbContext db)
    {
        _db = db;
    }

    // ────────────────────────────────────────────────────────────────
    // Create from accepted invoice (called by InvoiceService)
    // ────────────────────────────────────────────────────────────────
    public async Task<ProcurementResponse> CreateFromAcceptedInvoiceAsync(long invoiceId, long userId, bool autoFinalize = false)
    {
        // Idempotent: return existing Procurement if already created for this invoice
        var existing = await _db.Set<Procurement>()
            .Include(p => p.Items).ThenInclude(i => i.SupplierQuotes)
            .FirstOrDefaultAsync(p => p.InvoiceId == invoiceId);

        if (existing != null)
        {
            if (autoFinalize && existing.Status != "Finalized" && existing.Status != "Cancelled")
            {
                await AutoFinalizeInternalAsync(existing, userId);
            }
            return (await GetByIdInternalAsync(existing.Id, userId, true))!;
        }

        var invoice = await _db.Set<Invoice>()
            .Include(i => i.Customer)
            .Include(i => i.Quote)
            .Include(i => i.InvoiceItems)
                .ThenInclude(ii => ii.QuoteItem)
                    .ThenInclude(qi => qi!.PartNumber)
            .Include(i => i.InvoiceItems)
                .ThenInclude(ii => ii.QuoteItem)
                    .ThenInclude(qi => qi!.ProcumentRecord)
                        .ThenInclude(pr => pr!.Supplier)
            .Include(i => i.InvoiceItems)
                .ThenInclude(ii => ii.QuoteItem)
                    .ThenInclude(qi => qi!.RFQItem)
                        .ThenInclude(ri => ri!.RFQ)
            .FirstOrDefaultAsync(i => i.Id == invoiceId);

        if (invoice == null) throw new KeyNotFoundException($"Invoice {invoiceId} not found");

        string? quoteNumber = invoice.Quote?.QuoteNumber;

        var proc = new Procurement
        {
            ProcurementNumber = "", // set after SaveChanges
            Status = "Open",
            InvoiceId = invoice.Id,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = userId > 0 ? userId : null,
        };
        _db.Set<Procurement>().Add(proc);
        await _db.SaveChangesAsync();

        proc.ProcurementNumber = $"PROC-{proc.Id}";

        // Pre-load all ProcumentRecords tied to each RFQItem for supplier-quote cloning
        var rfqItemIds = invoice.InvoiceItems
            .Select(ii => ii.QuoteItem?.RFQItemId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var procRecords = rfqItemIds.Count > 0
            ? await _db.Set<ProcumentRecord>()
                .Include(pr => pr.Supplier)
                .Where(pr => rfqItemIds.Contains(pr.RFQItemId))
                .ToListAsync()
            : new List<ProcumentRecord>();

        int sortOrder = 0;
        foreach (var ii in invoice.InvoiceItems.OrderBy(x => x.Id))
        {
            sortOrder++;
            var qi = ii.QuoteItem;
            var rfqItem = qi?.RFQItem;
            var rfq = rfqItem?.RFQ;
            var selectedProcRecord = qi?.ProcumentRecord;

            var item = new ProcurementItem
            {
                ProcurementId = proc.Id,
                SortOrder = sortOrder,
                CreatedAt = DateTime.UtcNow,

                // RFQ snapshot
                SourceRfqId = rfq?.Id,
                SourceRfqItemId = rfqItem?.Id,
                RfqName = rfq?.Name,
                RfqExType = rfq?.ExType,
                PartNumberId = qi?.PartNumberId ?? rfqItem?.PartNumberId,
                PartNumberName = qi?.PartNumber?.Name ?? rfqItem?.PartNumber?.Name,
                PartNumberDescription = qi?.PartNumber?.Description ?? rfqItem?.PartNumber?.Description,
                RfqQty = rfqItem?.Qty,
                RfqCondition = rfqItem?.Condition,
                RfqUnit = rfqItem?.Unit,
                RfqPriority = rfqItem?.Priority,
                RfqAlt = rfqItem?.Alt,
                RfqNote = rfqItem?.Note,

                // Quote snapshot
                SourceQuoteId = qi?.QuoteId,
                SourceQuoteItemId = qi?.Id,
                QuoteNumber = quoteNumber,
                QuoteUnitPrice = qi?.UnitPrice ?? 0,
                QuoteQty = qi?.Qty ?? 0,
                QuoteCondition = qi?.Condition,
                QuoteAlt = qi?.Alt,
                QuoteLeadTimeDays = qi?.LeadTimeDays,

                // Selected-supplier snapshot
                SourceProcumentRecordId = selectedProcRecord?.Id,
                SourceSupplierId = selectedProcRecord?.SupplierId,
                SupplierName = selectedProcRecord?.Supplier?.Name,
                SupplierPrice = selectedProcRecord != null ? (decimal?)selectedProcRecord.Price : null,
                SupplierLeadTime = selectedProcRecord?.LeadTime,
                SupplierCondition = selectedProcRecord?.Condition,
                SupplierCertName = selectedProcRecord?.CertName,
                ShippingCost = selectedProcRecord?.ShippingCost,

                // Invoice snapshot
                SourceInvoiceItemId = ii.Id,
                AcceptedQty = ii.Qty,
                AcceptedUnitPrice = ii.UnitPrice,

                // Editable defaults — seeded from the accepted invoice + selected supplier
                Qty = ii.Qty,
                UnitPrice = (selectedProcRecord?.Price > 0) ? (decimal)selectedProcRecord.Price : ii.UnitPrice,
                CurrentSupplierId = selectedProcRecord?.SupplierId,
                LeadTime = selectedProcRecord?.LeadTime,
                Condition = qi?.Condition,
                ExpectedDeliveryDate = ii.ExpectedDeliveryDate,
                Alt = qi?.Alt,
                Note = null,
                ItemStatus = "Open",
            };
            _db.Set<ProcurementItem>().Add(item);
            await _db.SaveChangesAsync();

            // Clone ProcumentRecords tied to this RFQItem into ProcurementSupplierQuote rows
            if (rfqItem != null)
            {
                var candidates = procRecords
                    .Where(pr => pr.RFQItemId == rfqItem.Id)
                    .OrderBy(pr => pr.SortOrder)
                    .ThenBy(pr => pr.Id)
                    .ToList();

                int sqOrder = 0;
                foreach (var pr in candidates)
                {
                    sqOrder++;
                    var sq = new ProcurementSupplierQuote
                    {
                        ProcurementItemId = item.Id,
                        SupplierId = pr.SupplierId,
                        SupplierName = pr.Supplier?.Name ?? string.Empty,
                        Price = pr.Price,
                        Qty = pr.Qty,
                        Condition = pr.Condition,
                        Unit = pr.Unit,
                        Alt = pr.Alt,
                        LeadTime = pr.LeadTime,
                        CertName = pr.CertName,
                        ShippingCost = pr.ShippingCost,
                        Note = pr.Note,
                        TagDate = pr.TagDate,
                        ShippingPoint = pr.ShippingPoint,
                        IsSelected = selectedProcRecord != null && pr.Id == selectedProcRecord.Id,
                        SourceProcumentRecordId = pr.Id,
                        SortOrder = sqOrder,
                        CreatedAt = DateTime.UtcNow,
                        AddedByUserId = userId > 0 ? userId : null,
                    };
                    _db.Set<ProcurementSupplierQuote>().Add(sq);
                }
            }
        }

        await _db.SaveChangesAsync();

        if (autoFinalize)
        {
            // Re-load with includes for the helper
            var freshProc = await _db.Set<Procurement>()
                .Include(p => p.Items).ThenInclude(i => i.SupplierQuotes)
                .FirstAsync(p => p.Id == proc.Id);
            await AutoFinalizeInternalAsync(freshProc, userId);
        }

        return (await GetByIdInternalAsync(proc.Id, userId, true))!;
    }

    private async Task AutoFinalizeInternalAsync(Procurement proc, long userId)
    {
        bool anyPoItemCreated = false;
        foreach (var item in proc.Items)
        {
            if (item.ItemStatus == "Cancelled") continue;

            // Find selected quote
            var sq = item.SupplierQuotes.FirstOrDefault(q => q.IsSelected);
            var supplierId = sq?.SupplierId ?? item.CurrentSupplierId;

            if (supplierId == null) continue;

            // Check if already active
            var alreadyActive = await _db.Set<POItem>().AnyAsync(p =>
                p.SourceProcurementItemId == item.Id &&
                p.SupplierId == supplierId &&
                p.ReturnedAt == null);

            if (!alreadyActive)
            {
                int qty = (sq != null && sq.Qty > 0) ? (int)Math.Round(sq.Qty) : item.Qty;
                decimal price = (sq != null && sq.Price > 0) ? sq.Price : item.UnitPrice;
                var poItem = new POItem
                {
                    POId = null,
                    InvoiceItemId = item.SourceInvoiceItemId,
                    ProcumentId = item.SourceProcumentRecordId,
                    PartNumberId = item.PartNumberId,
                    SupplierId = supplierId,
                    Qty = qty,
                    UnitPrice = price,
                    TotalPrice = qty * price,
                    Condition = sq?.Condition ?? item.Condition,
                    SourceProcurementItemId = item.Id,
                };
                _db.Set<POItem>().Add(poItem);
                anyPoItemCreated = true;
            }
        }

        if (anyPoItemCreated)
        {
            await _db.SaveChangesAsync();
        }

        // Mark as finalized
        proc.Status = "Finalized";
        proc.FinalizedAt = DateTime.UtcNow;
        proc.FinalizedByUserId = userId > 0 ? userId : null;
        await _db.SaveChangesAsync();
    }

    // ────────────────────────────────────────────────────────────────
    // Query methods
    // ────────────────────────────────────────────────────────────────
    public async Task<List<ProcurementItemFlatResponse>> GetAllItemsFlatAsync(long userId, bool isAdmin)
    {
        IQueryable<ProcurementItem> query = _db.Set<ProcurementItem>()
            .Include(i => i.CurrentSupplier)
            .AsNoTracking();

        if (!isAdmin)
        {
            var permittedEntityIds = await _db.Set<EntityPermission>()
                .Where(p => p.UserId == userId && p.EntityName == "Procurement")
                .Select(p => p.EntityId)
                .ToListAsync();

            var permittedItemIds = permittedEntityIds
                .Select(s => long.TryParse(s, out var l) ? l : -1L)
                .Where(l => l > 0)
                .ToHashSet();

            if (permittedItemIds.Count == 0) return new List<ProcurementItemFlatResponse>();
            query = query.Where(i => permittedItemIds.Contains(i.Id));
        }

        var items = await query.OrderByDescending(i => i.CreatedAt).ToListAsync();
        if (items.Count == 0) return new List<ProcurementItemFlatResponse>();

        // Batch-load parent procurement status and invoice (customer) info
        var procIds = items.Select(i => i.ProcurementId).Distinct().ToList();
        var procs = await _db.Set<Procurement>()
            .AsNoTracking()
            .Where(p => procIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Status, p.InvoiceId })
            .ToListAsync();

        var invoiceIds = procs.Select(p => p.InvoiceId).Distinct().ToList();
        var invoiceMap = await _db.Set<Invoice>()
            .AsNoTracking()
            .Where(i => invoiceIds.Contains(i.Id))
            .Select(i => new { i.Id, CustomerName = i.Customer.CustomerCode })
            .ToDictionaryAsync(x => x.Id);

        var procInfoMap = procs.ToDictionary(
            p => p.Id,
            p => new
            {
                p.Status,
                CustomerName = invoiceMap.TryGetValue(p.InvoiceId, out var inv) ? inv.CustomerName : null,
            });

        // Batch-load per-item permissions (admin only — shown as chips)
        var allPerms = new List<ProcurementAssignedUser>();
        if (isAdmin)
        {
            var itemIdStrs = items.Select(i => i.Id.ToString()).ToList();
            var permRows = await _db.Set<EntityPermission>()
                .Where(p => p.EntityName == "Procurement" && itemIdStrs.Contains(p.EntityId))
                .Join(_db.Set<User>(), p => p.UserId, u => u.Id, (p, u) => new { p, u })
                .ToListAsync();

            allPerms = permRows.Select(r => new ProcurementAssignedUser
            {
                Id = r.p.Id,
                UserId = r.p.UserId,
                UserName = r.u.Name ?? r.u.Email ?? $"User #{r.u.Id}",
                UserEmail = r.u.Email,
                EntityName = r.p.EntityName,
                EntityId = r.p.EntityId,
                Permission = r.p.Permission,
                CreatedAt = r.p.CreatedAt,
            }).ToList();
        }

        return items.Select(i =>
        {
            procInfoMap.TryGetValue(i.ProcurementId, out var procInfo);
            var itemIdStr = i.Id.ToString();
            return new ProcurementItemFlatResponse
            {
                Id = i.Id,
                ProcurementId = i.ProcurementId,
                ProcurementStatus = procInfo?.Status ?? "Open",
                CustomerName = procInfo?.CustomerName,
                PartNumberName = i.PartNumberName,
                PartNumberDescription = i.PartNumberDescription,
                Qty = i.Qty,
                Condition = i.Condition,
                Alt = i.Alt,
                Note = i.Note,
                ItemStatus = i.ItemStatus,
                CurrentSupplierName = i.CurrentSupplier?.Name ?? i.SupplierName,
                UnitPrice = i.UnitPrice,
                LeadTime = i.LeadTime,
                CreatedAt = i.CreatedAt,
                AssignedUsers = isAdmin
                    ? allPerms.Where(p => p.EntityId == itemIdStr).ToList()
                    : new List<ProcurementAssignedUser>(),
            };
        }).ToList();
    }

    public async Task<PagedResult<ProcurementResponse>> GetAllAsync(PageQuery page, long userId, bool isAdmin)
    {
        IQueryable<Procurement> baseQ = _db.Set<Procurement>()
            .Include(p => p.Items);

        HashSet<long>? explicitHeaderIds = null;
        HashSet<string>? permittedItemIds = null;

        if (!isAdmin)
        {
            // Gather permitted Procurement header ids, plus any Procurement whose item(s) the user
            // has EntityName=Procurement permission on.
            var permitted = await _db.Set<EntityPermission>()
                .Where(p => p.UserId == userId && p.EntityName == "Procurement")
                .Select(p => p.EntityId)
                .ToListAsync();

            explicitHeaderIds = permitted
                .Select(s => long.TryParse(s, out var l) ? l : -1L)
                .Where(l => l > 0)
                .ToHashSet();

            permittedItemIds = permitted.ToHashSet();

            var permittedItemLongIds = permitted
                .Select(s => long.TryParse(s, out var l) ? l : -1L)
                .Where(l => l > 0)
                .ToList();

            var procsFromItems = permittedItemLongIds.Count > 0
                ? await _db.Set<ProcurementItem>()
                    .Where(pi => permittedItemLongIds.Contains(pi.Id))
                    .Select(pi => pi.ProcurementId)
                    .Distinct()
                    .ToListAsync()
                : new List<long>();

            var allAccessibleProcIds = new HashSet<long>(explicitHeaderIds);
            foreach (var pid in procsFromItems) allAccessibleProcIds.Add(pid);

            var idList = allAccessibleProcIds.ToList();
            baseQ = baseQ.Where(p => idList.Contains(p.Id));
        }

        // Text search: join with Invoice lookup in a subquery via InvoiceId set
        if (!string.IsNullOrWhiteSpace(page.Search))
        {
            var s = page.Search.Trim();
            var matchedInvoiceIds = await _db.Set<Invoice>()
                .Where(i => i.InvoiceNumber.Contains(s) || i.Customer.Name.Contains(s))
                .Select(i => i.Id)
                .ToListAsync();
            baseQ = baseQ.Where(p => p.ProcurementNumber.Contains(s) || matchedInvoiceIds.Contains(p.InvoiceId));
        }

        var query = baseQ.OrderByDescending(p => p.CreatedAt);
        var total = await query.CountAsync();
        var rows = await query
            .Skip((page.Page - 1) * page.PageSize)
            .Take(page.PageSize)
            .ToListAsync();

        // Batch-load permissions for the page (both header and item level)
        var rowIds = rows.Select(r => r.Id).ToList();
        var rowIdStrs = rowIds.Select(id => id.ToString()).ToList();
        var allItemIds = rows.SelectMany(r => r.Items).Select(i => i.Id.ToString()).ToList();
        
        var pagePerms = await _db.Set<EntityPermission>()
            .Where(p => p.EntityName == "Procurement" && (rowIdStrs.Contains(p.EntityId) || allItemIds.Contains(p.EntityId)))
            .Join(_db.Set<User>(), p => p.UserId, u => u.Id, (p, u) => new { p, u })
            .ToListAsync();
        
        var permsByProcId = new Dictionary<long, List<ProcurementAssignedUser>>();
        foreach (var r in rows)
        {
            var headerIdStr = r.Id.ToString();
            var itemIds = r.Items.Select(i => i.Id.ToString()).ToHashSet();
            
            var matched = pagePerms
                .Where(x => x.p.EntityId == headerIdStr || itemIds.Contains(x.p.EntityId))
                .Select(x => new ProcurementAssignedUser
                {
                    Id = x.p.Id,
                    UserId = x.p.UserId,
                    UserName = x.u.Name ?? x.u.Email ?? $"User #{x.u.Id}",
                    UserEmail = x.u.Email,
                    EntityName = x.p.EntityName,
                    EntityId = x.p.EntityId,
                    Permission = x.p.Permission,
                    CreatedAt = x.p.CreatedAt,
                }).ToList();
            
            permsByProcId[r.Id] = matched;
        }

        // Batch-load Invoice info for the page
        var invoiceIds = rows.Select(r => r.InvoiceId).Distinct().ToList();
        var invoiceMap = invoiceIds.Count > 0
            ? await _db.Set<Invoice>()
                .AsNoTracking()
                .Where(i => invoiceIds.Contains(i.Id))
                .Select(i => new { i.Id, i.InvoiceNumber, i.CustomerId, CustomerName = i.Customer.CustomerCode })
                .ToDictionaryAsync(x => x.Id)
            : new();

        return new PagedResult<ProcurementResponse>
        {
            Items = rows.Select(r =>
            {
                var h = MapHeader(r);
                if (invoiceMap.TryGetValue(r.InvoiceId, out var inv))
                {
                    h.InvoiceNumber = inv.InvoiceNumber;
                    h.CustomerId = inv.CustomerId;
                    h.CustomerName = inv.CustomerName;
                }

                h.AssignedUsers = permsByProcId.GetValueOrDefault(r.Id) ?? new();

                // If non-admin doesn't have header-level permission, ItemCount should reflect only assigned items
                if (!isAdmin && explicitHeaderIds != null && permittedItemIds != null)
                {
                    bool hasHeader = explicitHeaderIds.Contains(r.Id);
                    if (!hasHeader)
                    {
                        h.ItemCount = r.Items.Count(i => permittedItemIds.Contains(i.Id.ToString()));
                    }
                }

                return h;
            }).ToList(),
            TotalCount = total,
            Page = page.Page,
            PageSize = page.PageSize,
        };
    }

    public async Task<ProcurementResponse?> GetByIdAsync(long id, long userId, bool isAdmin)
    {
        if (!await UserCanAccessAsync(id, userId, isAdmin)) return null;
        return await GetByIdInternalAsync(id, userId, isAdmin);
    }

    public async Task<bool> UserCanAccessAsync(long procurementId, long userId, bool isAdmin)
    {
        if (isAdmin) return true;

        // Direct Procurement-header permission
        var hasHeader = await _db.Set<EntityPermission>()
            .AnyAsync(p => p.UserId == userId && p.EntityName == "Procurement" && p.EntityId == procurementId.ToString());
        if (hasHeader) return true;

        // Any item-level permission scoped to items within this Procurement
        var itemIds = await _db.Set<ProcurementItem>()
            .Where(pi => pi.ProcurementId == procurementId)
            .Select(pi => pi.Id.ToString())
            .ToListAsync();
        if (itemIds.Count == 0) return false;

        return await _db.Set<EntityPermission>()
            .AnyAsync(p => p.UserId == userId && p.EntityName == "Procurement" && itemIds.Contains(p.EntityId));
    }

    public async Task<bool> UserCanAccessItemAsync(long procurementId, long itemId, long userId, bool isAdmin)
    {
        if (isAdmin) return true;

        // Header permission
        var hasHeader = await _db.Set<EntityPermission>()
            .AnyAsync(p => p.UserId == userId && p.EntityName == "Procurement" && p.EntityId == procurementId.ToString());
        if (hasHeader) return true;

        // Specific item permission
        return await _db.Set<EntityPermission>()
            .AnyAsync(p => p.UserId == userId && p.EntityName == "Procurement" && p.EntityId == itemId.ToString());
    }

    private async Task<ProcurementResponse?> GetByIdInternalAsync(long id, long userId = 0, bool isAdmin = true)
    {
        var proc = await _db.Set<Procurement>()
            .AsNoTracking()
            .Include(p => p.Items).ThenInclude(i => i.PartNumber)
            .Include(p => p.Items).ThenInclude(i => i.CurrentSupplier)
            .Include(p => p.Items).ThenInclude(i => i.SupplierQuotes)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (proc == null) return null;

        var resp = MapHeader(proc);

        // Load associated Invoice info
        var inv = await _db.Set<Invoice>()
            .AsNoTracking()
            .Where(i => i.Id == proc.InvoiceId)
            .Select(i => new { i.InvoiceNumber, i.CustomerId, CustomerName = i.Customer.CustomerCode })
            .FirstOrDefaultAsync();
        if (inv != null)
        {
            resp.InvoiceNumber = inv.InvoiceNumber;
            resp.CustomerId = inv.CustomerId;
            resp.CustomerName = inv.CustomerName;
        }

        // ── Per-item permission filtering ──
        var items = proc.Items.OrderBy(i => i.SortOrder).ThenBy(i => i.Id).ToList();
        if (!isAdmin)
        {
            var hasHeaderPerm = await _db.Set<EntityPermission>()
                .AnyAsync(p => p.UserId == userId && p.EntityName == "Procurement" && p.EntityId == id.ToString());
            
            if (!hasHeaderPerm)
            {
                var permittedIds = await _db.Set<EntityPermission>()
                    .Where(p => p.UserId == userId && p.EntityName == "Procurement")
                    .Select(p => p.EntityId)
                    .ToListAsync();
                var idSet = permittedIds.ToHashSet();
                items = items.Where(i => idSet.Contains(i.Id.ToString())).ToList();
            }
        }

        resp.Items = items.Select(MapItem).ToList();
        resp.ItemCount = resp.Items.Count; // Adjust count to match visible items

        // Stamp HasActivePOItem at both item-level and per-quote level (single DB round-trip).
        // Item-level: true when ALL selected quotes have active POItems.
        // Quote-level: true when that specific supplier's POItem is active (not returned).
        if (resp.Items.Count > 0)
        {
            var itemIdList = resp.Items.Select(i => i.Id).ToList();

            // Fetch active POItems projected to (SourceProcurementItemId, SupplierId)
            var activePairs = await _db.Set<POItem>()
                .Where(p => p.SourceProcurementItemId.HasValue
                         && itemIdList.Contains(p.SourceProcurementItemId!.Value)
                         && p.ReturnedAt == null)
                .Select(p => new { ItemId = p.SourceProcurementItemId!.Value, p.SupplierId })
                .ToListAsync();

            // Build: itemId → active count
            var activeCountByItemId = activePairs
                .GroupBy(p => p.ItemId)
                .ToDictionary(g => g.Key, g => g.Count());

            // Build: (itemId, supplierId) set for per-quote lookup
            var activeQuoteKeys = activePairs
                .Select(p => (p.ItemId, p.SupplierId))
                .ToHashSet();

            foreach (var ri in resp.Items)
            {
                activeCountByItemId.TryGetValue(ri.Id, out var activeCount);
                var selectedQuoteCount = ri.SupplierQuotes.Count(q => q.IsSelected);
                var expectedCount = selectedQuoteCount >= 2 ? selectedQuoteCount : 1;
                ri.HasActivePOItem = activeCount >= expectedCount;

                // Per-quote: mark which selected rows already have a live POItem (admin approved)
                foreach (var sq in ri.SupplierQuotes)
                    sq.HasActivePOItem = sq.IsSelected && activeQuoteKeys.Contains((ri.Id, sq.SupplierId));
            }
        }

        // Load assigned users (Procurement scope) — both header and per-item
        var itemIdStrs = proc.Items.Select(i => i.Id.ToString()).ToList();
        var headerIdStr = proc.Id.ToString();

        var permRows = await _db.Set<EntityPermission>()
            .Where(p => p.EntityName == "Procurement"
                     && (p.EntityId == headerIdStr || itemIdStrs.Contains(p.EntityId)))
            .Join(_db.Set<User>(),
                p => p.UserId,
                u => u.Id,
                (p, u) => new { p, u })
            .ToListAsync();

        resp.AssignedUsers = permRows.Select(r => new ProcurementAssignedUser
        {
            Id = r.p.Id,
            UserId = r.p.UserId,
            UserName = r.u.Name ?? r.u.Email ?? $"User #{r.u.Id}",
            UserEmail = r.u.Email,
            EntityName = r.p.EntityName,
            EntityId = r.p.EntityId,
            Permission = r.p.Permission,
            CreatedAt = r.p.CreatedAt,
        }).ToList();

        // Source-assigned users (RFQ + Quote + Invoice scoped permissions)
        var sourceRfqIds = proc.Items.Select(i => i.SourceRfqId).Where(x => x.HasValue).Select(x => x!.Value.ToString()).Distinct().ToList();
        var sourceQuoteIds = proc.Items.Select(i => i.SourceQuoteId).Where(x => x.HasValue).Select(x => x!.Value.ToString()).Distinct().ToList();
        var sourceInvoiceId = proc.InvoiceId.ToString();

        var sourcePerms = await _db.Set<EntityPermission>()
            .Where(p => (p.EntityName == "RFQ" && sourceRfqIds.Contains(p.EntityId))
                     || (p.EntityName == "Quote" && sourceQuoteIds.Contains(p.EntityId))
                     || (p.EntityName == "Invoice" && p.EntityId == sourceInvoiceId))
            .Join(_db.Set<User>(),
                p => p.UserId,
                u => u.Id,
                (p, u) => new { p, u })
            .ToListAsync();

        resp.SourceAssignedUsers = sourcePerms.Select(r => new ProcurementAssignedUser
        {
            Id = r.p.Id,
            UserId = r.p.UserId,
            UserName = r.u.Name ?? r.u.Email ?? $"User #{r.u.Id}",
            UserEmail = r.u.Email,
            EntityName = r.p.EntityName,
            EntityId = r.p.EntityId,
            Permission = r.p.Permission,
            CreatedAt = r.p.CreatedAt,
        }).ToList();

        return resp;
    }

    // ────────────────────────────────────────────────────────────────
    // Edit item (only the editable slice)
    // ────────────────────────────────────────────────────────────────
    public async Task<bool> UpdateItemAsync(long procurementId, long itemId, UpdateProcurementItemRequest request)
    {
        var proc = await _db.Set<Procurement>().FindAsync(procurementId);
        if (proc == null) return false;
        if (proc.Status is "Finalized" or "Cancelled") return false;

        var item = await _db.Set<ProcurementItem>()
            .FirstOrDefaultAsync(i => i.Id == itemId && i.ProcurementId == procurementId);
        if (item == null) return false;

        if (request.Qty.HasValue && request.Qty.Value >= 1) item.Qty = request.Qty.Value;
        if (request.UnitPrice.HasValue && request.UnitPrice.Value >= 0) item.UnitPrice = request.UnitPrice.Value;
        if (request.CurrentSupplierId.HasValue) item.CurrentSupplierId = request.CurrentSupplierId.Value;
        if (request.LeadTime != null) item.LeadTime = request.LeadTime;
        if (request.Condition != null) item.Condition = request.Condition;
        if (request.ExpectedDeliveryDate.HasValue) item.ExpectedDeliveryDate = request.ExpectedDeliveryDate.Value;
        if (request.Alt != null) item.Alt = request.Alt;
        if (request.Note != null) item.Note = request.Note;
        if (!string.IsNullOrWhiteSpace(request.ItemStatus))
        {
            var allowed = new[] { "Open", "Sourcing", "Ready", "Cancelled" };
            if (allowed.Contains(request.ItemStatus)) item.ItemStatus = request.ItemStatus;
        }

        item.UpdatedAt = DateTime.UtcNow;

        if (proc.Status == "Open") proc.Status = "Sourcing";

        await _db.SaveChangesAsync();
        return true;
    }

    // ────────────────────────────────────────────────────────────────
    // Supplier quote upsert / delete / select
    // ────────────────────────────────────────────────────────────────
    public async Task<ProcurementSupplierQuoteResponse?> UpsertSupplierQuoteAsync(long procurementId, long itemId, UpsertSupplierQuoteRequest request, long userId)
    {
        var proc = await _db.Set<Procurement>().FindAsync(procurementId);
        if (proc == null) return null;
        if (proc.Status is "Finalized" or "Cancelled") return null;

        var item = await _db.Set<ProcurementItem>()
            .FirstOrDefaultAsync(i => i.Id == itemId && i.ProcurementId == procurementId);
        if (item == null) return null;

        // Resolve supplier by id or name — auto-create a Supplier row when the name is new
        // so downstream SelectSupplierQuote / Finalize / PO creation always have a real FK to work with.
        long? supplierId = request.SupplierId;
        string supplierName = (request.SupplierName ?? string.Empty).Trim();
        if (supplierId.HasValue)
        {
            var s = await _db.Set<Supplier>().FindAsync(supplierId.Value);
            if (s != null && string.IsNullOrWhiteSpace(supplierName)) supplierName = s.Name;
        }
        else if (!string.IsNullOrWhiteSpace(supplierName))
        {
            var lower = supplierName.ToLower();
            var existing = await _db.Set<Supplier>().FirstOrDefaultAsync(x => x.Name.ToLower() == lower);

            // Caller role — Admin/SuperAdmin auto-approves, otherwise Pending review
            var caller = userId > 0 ? await _db.Set<User>().FindAsync(userId) : null;
            bool isAdmin = caller?.Role == "Admin" || caller?.Role == "SuperAdmin";

            if (existing != null)
            {
                // Reactivate a soft-deleted supplier if we stumble on one
                if (!existing.IsActive)
                {
                    existing.IsActive = true;
                    if (existing.Status == "Disabled") existing.Status = isAdmin ? "Approved" : "Pending";
                    await _db.SaveChangesAsync();
                }
                supplierId = existing.Id;
                supplierName = existing.Name; // canonical casing
            }
            else
            {
                var created = new Supplier
                {
                    Name = supplierName,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true,
                    Status = isAdmin ? "Approved" : "Pending",
                    RequestedByUserId = userId > 0 ? userId : null,
                };
                _db.Set<Supplier>().Add(created);
                await _db.SaveChangesAsync();
                supplierId = created.Id;
            }
        }

        ProcurementSupplierQuote sq;
        if (request.Id.HasValue)
        {
            var existing = await _db.Set<ProcurementSupplierQuote>()
                .FirstOrDefaultAsync(q => q.Id == request.Id.Value && q.ProcurementItemId == itemId);
            if (existing == null) return null;
            sq = existing;
        }
        else
        {
            var maxSort = await _db.Set<ProcurementSupplierQuote>()
                .Where(q => q.ProcurementItemId == itemId)
                .Select(q => (int?)q.SortOrder)
                .MaxAsync() ?? 0;

            sq = new ProcurementSupplierQuote
            {
                ProcurementItemId = itemId,
                SortOrder = maxSort + 1,
                CreatedAt = DateTime.UtcNow,
                AddedByUserId = userId > 0 ? userId : null,
            };
            _db.Set<ProcurementSupplierQuote>().Add(sq);
        }

        sq.SupplierId = supplierId;
        sq.SupplierName = supplierName;
        sq.Price = request.Price;
        sq.Qty = request.Qty;
        sq.Condition = request.Condition;
        sq.Unit = request.Unit;
        sq.Alt = request.Alt;
        sq.LeadTime = request.LeadTime;
        sq.CertName = request.CertName;
        sq.ShippingCost = request.ShippingCost;
        sq.Note = request.Note;
        sq.TagDate = request.TagDate;
        sq.ShippingPoint = request.ShippingPoint;

        if (proc.Status == "Open") proc.Status = "Sourcing";
        await _db.SaveChangesAsync();

        return MapSupplierQuote(sq);
    }

    public async Task<bool> DeleteSupplierQuoteAsync(long procurementId, long itemId, long supplierQuoteId)
    {
        var proc = await _db.Set<Procurement>().FindAsync(procurementId);
        if (proc == null || proc.Status is "Finalized" or "Cancelled") return false;

        var sq = await _db.Set<ProcurementSupplierQuote>()
            .FirstOrDefaultAsync(q => q.Id == supplierQuoteId && q.ProcurementItemId == itemId);
        if (sq == null) return false;

        // If the deleted quote was selected, clear CurrentSupplierId on the item
        if (sq.IsSelected)
        {
            var item = await _db.Set<ProcurementItem>().FindAsync(itemId);
            if (item != null) item.CurrentSupplierId = null;
        }

        _db.Set<ProcurementSupplierQuote>().Remove(sq);
        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Toggle the IsSelected flag on a single supplier quote. Multiple quotes can now be
    /// selected for the same ProcurementItem — Finalize will split that item into one
    /// POItem per selected quote, using each quote's own Qty / Price / Supplier / Condition.
    ///
    /// Side effects on the parent ProcurementItem (display fields only):
    ///   - 0 selected → CurrentSupplierId = null, SupplierName = "" (no item-level price sync)
    ///   - 1 selected → CurrentSupplierId/SupplierName/UnitPrice/LeadTime synced from that quote
    ///   - 2+ selected → CurrentSupplierId/SupplierName show the *first* selected quote
    ///                   (purely informational header — actual finalize uses per-quote values).
    ///                   item.UnitPrice and LeadTime are NOT mutated in multi-select mode
    ///                   because there is no single value that represents the split.
    /// </summary>
    public async Task<bool> SelectSupplierQuoteAsync(long procurementId, long itemId, long supplierQuoteId)
    {
        var proc = await _db.Set<Procurement>().FindAsync(procurementId);
        if (proc == null || proc.Status is "Finalized" or "Cancelled") return false;

        var item = await _db.Set<ProcurementItem>()
            .Include(i => i.SupplierQuotes)
            .FirstOrDefaultAsync(i => i.Id == itemId && i.ProcurementId == procurementId);
        if (item == null) return false;
        var target = item.SupplierQuotes.FirstOrDefault(q => q.Id == supplierQuoteId);
        if (target == null) return false;

        // Toggle just this one quote
        target.IsSelected = !target.IsSelected;

        // Recompute display fields based on the new selection set
        var selected = item.SupplierQuotes.Where(q => q.IsSelected).ToList();
        if (selected.Count == 0)
        {
            item.CurrentSupplierId = null;
            item.SupplierName = string.Empty;
        }
        else if (selected.Count == 1)
        {
            var only = selected[0];
            var sup = only.SupplierId.HasValue
                ? await _db.Set<Supplier>().FirstOrDefaultAsync(x => x.Id == only.SupplierId.Value)
                : null;
            item.CurrentSupplierId = only.SupplierId;
            item.SupplierName = sup?.Name ?? only.SupplierName ?? string.Empty;
            if (only.Price > 0) item.UnitPrice = only.Price;
            if (!string.IsNullOrWhiteSpace(only.LeadTime)) item.LeadTime = only.LeadTime;
        }
        else
        {
            // Multi-select: pick the first selected quote as the visual "primary"
            var primary = selected[0];
            var sup = primary.SupplierId.HasValue
                ? await _db.Set<Supplier>().FirstOrDefaultAsync(x => x.Id == primary.SupplierId.Value)
                : null;
            item.CurrentSupplierId = primary.SupplierId;
            item.SupplierName = sup?.Name ?? primary.SupplierName ?? string.Empty;
            // Do NOT mutate item.UnitPrice / item.LeadTime — they have no single meaningful value when split.
        }

        item.UpdatedAt = DateTime.UtcNow;
        if (proc.Status == "Open") proc.Status = "Sourcing";
        await _db.SaveChangesAsync();
        return true;
    }

    // ────────────────────────────────────────────────────────────────
    // Finalize → create POItems
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Shared helper: materializes one ProcurementItem into POItem(s).
    /// Returns the IDs of the newly created POItems (empty if all supplier rows are already active).
    ///
    /// Multi-supplier split (2+ selected quotes):
    ///   Each selected supplier quote is checked INDEPENDENTLY. If that specific supplier's
    ///   POItem was returned, a new one is created for it — without touching the other
    ///   supplier's still-active POItem.
    ///
    /// Single-supplier (0 or 1 selected quote):
    ///   Standard guard — skip if any active POItem already exists for this item.
    /// </summary>
    private async Task<List<long>> MaterializeItemAsync(ProcurementItem pi)
    {
        var created = new List<long>();

        if (pi.ItemStatus == "Cancelled") return created;

        var selectedQuotes = pi.SupplierQuotes.Where(q => q.IsSelected).ToList();

        if (selectedQuotes.Count >= 2)
        {
            // ── Multi-supplier split ──
            // Guard is per-supplier: only skip a specific supplier if its POItem is still active.
            // This allows re-finalization of a single returned supplier row without blocking
            // the other suppliers that are still in their POs.
            foreach (var sq in selectedQuotes)
            {
                var supplierId = sq.SupplierId ?? pi.CurrentSupplierId;

                // Skip only if THIS supplier already has an active (non-returned) POItem for this item
                var alreadyActive = await _db.Set<POItem>().AnyAsync(p =>
                    p.SourceProcurementItemId == pi.Id &&
                    p.SupplierId == supplierId &&
                    p.ReturnedAt == null);
                if (alreadyActive) continue;

                int qty = sq.Qty > 0 ? (int)Math.Round(sq.Qty) : pi.Qty;
                decimal price = sq.Price > 0 ? sq.Price : pi.UnitPrice;
                var splitItem = new POItem
                {
                    POId = null,
                    InvoiceItemId = pi.SourceInvoiceItemId,
                    ProcumentId = pi.SourceProcumentRecordId,
                    PartNumberId = pi.PartNumberId,
                    SupplierId = supplierId,
                    Qty = qty,
                    UnitPrice = price,
                    TotalPrice = qty * price,
                    Condition = sq.Condition ?? pi.Condition,
                    SourceProcurementItemId = pi.Id,
                };
                _db.Set<POItem>().Add(splitItem);
                await _db.SaveChangesAsync();
                created.Add(splitItem.Id);
            }
            return created;
        }

        // ── Single-supplier path (0 or 1 selected) ──
        // Standard guard: skip if any active POItem already exists for this procurement item
        var already = await _db.Set<POItem>().AnyAsync(p =>
            p.SourceProcurementItemId == pi.Id && p.ReturnedAt == null);
        if (already) return created;

        var poItem = new POItem
        {
            POId = null, // unassigned — admin will group & create POs from /purchase-orders
            InvoiceItemId = pi.SourceInvoiceItemId,
            ProcumentId = pi.SourceProcumentRecordId,
            PartNumberId = pi.PartNumberId,
            SupplierId = pi.CurrentSupplierId,
            Qty = pi.Qty,
            UnitPrice = pi.UnitPrice,
            TotalPrice = pi.Qty * pi.UnitPrice,
            Condition = pi.Condition,
            SourceProcurementItemId = pi.Id,
        };
        _db.Set<POItem>().Add(poItem);
        await _db.SaveChangesAsync();
        created.Add(poItem.Id);
        return created;
    }

    /// <summary>Finalize the entire procurement — creates POItems for ALL non-finalized items at once.</summary>
    public async Task<FinalizeProcurementResponse?> FinalizeAsync(long procurementId, long userId, FinalizeProcurementRequest? request = null)
    {
        var proc = await _db.Set<Procurement>()
            .Include(p => p.Items)
                .ThenInclude(i => i.SupplierQuotes)
            .FirstOrDefaultAsync(p => p.Id == procurementId);
        if (proc == null) return null;
        if (proc.Status is "Finalized" or "Cancelled") return null;

        var createdItemIds = new List<long>();
        foreach (var pi in proc.Items)
        {
            var ids = await MaterializeItemAsync(pi);
            createdItemIds.AddRange(ids);
        }

        proc.Status = "Finalized";
        proc.FinalizedAt = DateTime.UtcNow;
        proc.FinalizedByUserId = userId > 0 ? userId : null;
        if (!string.IsNullOrWhiteSpace(request?.Notes)) proc.Notes = request.Notes;

        await _db.SaveChangesAsync();

        return new FinalizeProcurementResponse
        {
            ProcurementId = proc.Id,
            CreatedPOIds = new List<long>(), // POs not created here — admin triages unassigned items on /purchase-orders
            CreatedPOItemIds = createdItemIds,
        };
    }

    /// <summary>
    /// Finalize a SINGLE ProcurementItem (one supplier row) independently.
    /// Creates POItem(s) for just that item. If all items in the procurement are now
    /// materialized, the procurement status is automatically set to Finalized.
    /// </summary>
    public async Task<FinalizeProcurementItemResponse?> FinalizeItemAsync(long procurementId, long itemId, long userId)
    {
        var proc = await _db.Set<Procurement>()
            .Include(p => p.Items)
                .ThenInclude(i => i.SupplierQuotes)
            .FirstOrDefaultAsync(p => p.Id == procurementId);
        if (proc == null) return null;
        if (proc.Status is "Cancelled") return null;

        var pi = proc.Items.FirstOrDefault(i => i.Id == itemId);
        if (pi == null) return null;

        var createdIds = await MaterializeItemAsync(pi);

        // Check if all non-cancelled items are now fully materialized → auto-finalize the procurement.
        // For multi-supplier splits, every selected supplier must have an active POItem.
        bool allDone = true;
        foreach (var item in proc.Items)
        {
            if (item.ItemStatus == "Cancelled") continue;

            var selectedQuotes = item.SupplierQuotes.Where(q => q.IsSelected).ToList();

            if (selectedQuotes.Count >= 2)
            {
                // Multi-split: each selected supplier must have an active POItem
                foreach (var sq in selectedQuotes)
                {
                    var supplierId = sq.SupplierId ?? item.CurrentSupplierId;
                    var hasActive = await _db.Set<POItem>().AnyAsync(p =>
                        p.SourceProcurementItemId == item.Id &&
                        p.SupplierId == supplierId &&
                        p.ReturnedAt == null);
                    if (!hasActive) { allDone = false; break; }
                }
            }
            else
            {
                // Single-supplier: just need any active POItem
                var hasPOItem = await _db.Set<POItem>().AnyAsync(p =>
                    p.SourceProcurementItemId == item.Id && p.ReturnedAt == null);
                if (!hasPOItem) { allDone = false; }
            }

            if (!allDone) break;
        }

        bool fullyFinalized = false;
        if (allDone && proc.Status != "Finalized")
        {
            proc.Status = "Finalized";
            proc.FinalizedAt = DateTime.UtcNow;
            proc.FinalizedByUserId = userId > 0 ? userId : null;
            await _db.SaveChangesAsync();
            fullyFinalized = true;
        }

        return new FinalizeProcurementItemResponse
        {
            ProcurementId = proc.Id,
            ProcurementItemId = itemId,
            CreatedPOItemIds = createdIds,
            ProcurementFullyFinalized = fullyFinalized,
        };
    }

    /// <summary>
    /// Admin approves a single selected supplier quote row — creates exactly ONE POItem from that quote.
    /// If all supplier quotes across all items now have active POItems, the procurement is auto-finalized.
    /// Returns null when the procurement/item/quote is not found or procurement is cancelled.
    /// </summary>
    public async Task<FinalizeProcurementItemResponse?> FinalizeSupplierQuoteAsync(
        long procurementId, long itemId, long supplierQuoteId, long userId)
    {
        var proc = await _db.Set<Procurement>()
            .Include(p => p.Items)
                .ThenInclude(i => i.SupplierQuotes)
            .FirstOrDefaultAsync(p => p.Id == procurementId);
        if (proc == null) return null;
        if (proc.Status is "Cancelled") return null;

        var pi = proc.Items.FirstOrDefault(i => i.Id == itemId);
        if (pi == null) return null;

        var sq = pi.SupplierQuotes.FirstOrDefault(q => q.Id == supplierQuoteId);
        if (sq == null || !sq.IsSelected) return null; // must be selected first

        var supplierId = sq.SupplierId ?? pi.CurrentSupplierId;

        // Guard: don't create a duplicate if this supplier's POItem is already active
        var alreadyActive = await _db.Set<POItem>().AnyAsync(p =>
            p.SourceProcurementItemId == pi.Id &&
            p.SupplierId == supplierId &&
            p.ReturnedAt == null);

        var createdIds = new List<long>();
        if (!alreadyActive)
        {
            int qty = sq.Qty > 0 ? (int)Math.Round(sq.Qty) : pi.Qty;
            decimal price = sq.Price > 0 ? sq.Price : pi.UnitPrice;
            var poItem = new POItem
            {
                POId = null,
                InvoiceItemId = pi.SourceInvoiceItemId,
                ProcumentId = pi.SourceProcumentRecordId,
                PartNumberId = pi.PartNumberId,
                SupplierId = supplierId,
                Qty = qty,
                UnitPrice = price,
                TotalPrice = qty * price,
                Condition = sq.Condition ?? pi.Condition,
                SourceProcurementItemId = pi.Id,
            };
            _db.Set<POItem>().Add(poItem);
            await _db.SaveChangesAsync();
            createdIds.Add(poItem.Id);
        }

        // Auto-finalize the procurement when every selected quote across all items has an active POItem
        bool fullyFinalized = false;
        bool allDone = true;
        foreach (var item in proc.Items)
        {
            if (item.ItemStatus == "Cancelled") continue;
            var selectedQuotes = item.SupplierQuotes.Where(q => q.IsSelected).ToList();
            var quoteList = selectedQuotes.Count > 0 ? selectedQuotes : new List<ProcurementSupplierQuote>();

            if (quoteList.Count == 0)
            {
                // No quotes selected — check for any active POItem (legacy / single-supplier)
                var has = await _db.Set<POItem>().AnyAsync(p =>
                    p.SourceProcurementItemId == item.Id && p.ReturnedAt == null);
                if (!has) { allDone = false; break; }
            }
            else
            {
                foreach (var q in quoteList)
                {
                    var sid = q.SupplierId ?? item.CurrentSupplierId;
                    var has = await _db.Set<POItem>().AnyAsync(p =>
                        p.SourceProcurementItemId == item.Id &&
                        p.SupplierId == sid &&
                        p.ReturnedAt == null);
                    if (!has) { allDone = false; break; }
                }
            }
            if (!allDone) break;
        }

        if (allDone && proc.Status != "Finalized")
        {
            proc.Status = "Finalized";
            proc.FinalizedAt = DateTime.UtcNow;
            proc.FinalizedByUserId = userId > 0 ? userId : null;
            await _db.SaveChangesAsync();
            fullyFinalized = true;
        }

        return new FinalizeProcurementItemResponse
        {
            ProcurementId = proc.Id,
            ProcurementItemId = itemId,
            CreatedPOItemIds = createdIds,
            ProcurementFullyFinalized = fullyFinalized,
        };
    }

    public async Task<bool> CancelAsync(long procurementId, long userId)
    {
        var proc = await _db.Set<Procurement>().FindAsync(procurementId);
        if (proc == null) return false;
        if (proc.Status == "Finalized") return false;

        proc.Status = "Cancelled";
        proc.FinalizedAt = DateTime.UtcNow;
        proc.FinalizedByUserId = userId > 0 ? userId : null;
        await _db.SaveChangesAsync();
        return true;
    }

    // ────────────────────────────────────────────────────────────────
    // Mapping helpers
    // ────────────────────────────────────────────────────────────────
    private static ProcurementResponse MapHeader(Procurement p) => new()
    {
        Id = p.Id,
        ProcurementNumber = p.ProcurementNumber,
        Status = p.Status,
        CreatedAt = p.CreatedAt,
        FinalizedAt = p.FinalizedAt,
        CreatedByUserId = p.CreatedByUserId,
        FinalizedByUserId = p.FinalizedByUserId,
        Notes = p.Notes,
        InvoiceId = p.InvoiceId,
        ItemCount = p.Items?.Count ?? 0,
    };

    private static ProcurementItemResponse MapItem(ProcurementItem i) => new()
    {
        Id = i.Id,
        ProcurementId = i.ProcurementId,
        SortOrder = i.SortOrder,

        SourceRfqId = i.SourceRfqId,
        SourceRfqItemId = i.SourceRfqItemId,
        RfqName = i.RfqName,
        RfqExType = i.RfqExType,
        PartNumberId = i.PartNumberId,
        PartNumberName = i.PartNumberName ?? i.PartNumber?.Name,
        PartNumberDescription = i.PartNumberDescription ?? i.PartNumber?.Description,
        RfqQty = i.RfqQty,
        RfqCondition = i.RfqCondition,
        RfqUnit = i.RfqUnit,
        RfqPriority = i.RfqPriority,
        RfqAlt = i.RfqAlt,
        RfqNote = i.RfqNote,

        SourceQuoteId = i.SourceQuoteId,
        SourceQuoteItemId = i.SourceQuoteItemId,
        QuoteNumber = i.QuoteNumber,
        QuoteUnitPrice = i.QuoteUnitPrice,
        QuoteQty = i.QuoteQty,
        QuoteCondition = i.QuoteCondition,
        QuoteAlt = i.QuoteAlt,
        QuoteLeadTimeDays = i.QuoteLeadTimeDays,

        SourceProcumentRecordId = i.SourceProcumentRecordId,
        SourceSupplierId = i.SourceSupplierId,
        SupplierName = i.SupplierName,
        SupplierPrice = i.SupplierPrice,
        SupplierLeadTime = i.SupplierLeadTime,
        SupplierCondition = i.SupplierCondition,
        SupplierCertName = i.SupplierCertName,
        ShippingCost = i.ShippingCost,

        SourceInvoiceItemId = i.SourceInvoiceItemId,
        AcceptedQty = i.AcceptedQty,
        AcceptedUnitPrice = i.AcceptedUnitPrice,

        Qty = i.Qty,
        UnitPrice = i.UnitPrice,
        CurrentSupplierId = i.CurrentSupplierId,
        CurrentSupplierName = i.CurrentSupplier?.Name,
        LeadTime = i.LeadTime,
        Condition = i.Condition,
        ExpectedDeliveryDate = i.ExpectedDeliveryDate,
        Alt = i.Alt,
        Note = i.Note,
        ItemStatus = i.ItemStatus,

        LoopCount = i.LoopCount,
        LastReturnReason = i.LastReturnReason,
        LastReturnedAt = i.LastReturnedAt,
        FulfilledByPOItemId = i.FulfilledByPOItemId,

        CreatedAt = i.CreatedAt,
        UpdatedAt = i.UpdatedAt,

        SupplierQuotes = (i.SupplierQuotes ?? new List<ProcurementSupplierQuote>())
            .OrderBy(q => q.SortOrder).ThenBy(q => q.Id)
            .Select(MapSupplierQuote)
            .ToList(),
    };

    // ────────────────────────────────────────────────────────────────
    // Loop-back: recycle returned POItems into the Procurement layer
    // ────────────────────────────────────────────────────────────────
    /// <summary>
    /// Recycle the given POItems back into the Procurement layer. For each POItem:
    /// soft-delete (POId=null, ReturnedAt=now), flip its source ProcurementItem.ItemStatus → Open,
    /// increment LoopCount (cap 5 — items past the cap are skipped and warned), stamp
    /// LastReturnReason / LastReturnedAt, then transition parents from Finalized → Reopened.
    /// </summary>
    public async Task<(List<long> reopenedProcurementIds, List<long> skippedPOItemIds, List<string> warnings)> RecyclePOItemsAsync(
        IEnumerable<long> poItemIds, long poId, string reason, long userId)
    {
        var reopened = new HashSet<long>();
        var skipped = new List<long>();
        var warnings = new List<string>();

        var ids = poItemIds?.Distinct().ToList() ?? new List<long>();
        if (ids.Count == 0) return (reopened.ToList(), skipped, warnings);

        // Load target POItems (ignore already-returned ones)
        var poItems = await _db.Set<POItem>()
            .Where(p => ids.Contains(p.Id) && p.ReturnedAt == null)
            .ToListAsync();

        // Load all source ProcurementItems in one shot
        var sourceIds = poItems
            .Where(p => p.SourceProcurementItemId.HasValue)
            .Select(p => p.SourceProcurementItemId!.Value)
            .Distinct()
            .ToList();

        var sourceItems = sourceIds.Count > 0
            ? await _db.Set<ProcurementItem>()
                .Where(pi => sourceIds.Contains(pi.Id))
                .ToListAsync()
            : new List<ProcurementItem>();

        var sourceMap = sourceItems.ToDictionary(pi => pi.Id);
        var now = DateTime.UtcNow;

        foreach (var poItem in poItems)
        {
            // Untraceable: no source ProcurementItem — can't recycle (stays orphan on the PO)
            if (!poItem.SourceProcurementItemId.HasValue
                || !sourceMap.TryGetValue(poItem.SourceProcurementItemId.Value, out var src))
            {
                skipped.Add(poItem.Id);
                warnings.Add($"POItem {poItem.Id} has no Procurement source — cannot recycle.");
                continue;
            }

            // Loop cap — hard block at 5
            if (src.LoopCount >= 50)
            {
                skipped.Add(poItem.Id);
                warnings.Add($"ProcurementItem {src.Id} (PartNumber #{src.PartNumberId}) reached the 5-loop cap — cancel or skip.");
                continue;
            }

            // Soft-delete the POItem
            poItem.ReturnedAt = now;
            poItem.ReturnedFromPOId = poId;
            poItem.ReturnReason = reason;
            poItem.POId = null;

            // Flip the source ProcurementItem back into play
            src.ItemStatus = "Open";
            src.LoopCount += 1;
            src.LastReturnReason = reason;
            src.LastReturnedAt = now;
            src.UpdatedAt = now;

            reopened.Add(src.ProcurementId);
        }

        // Transition parent Procurements: Finalized → Reopened (don't touch Cancelled)
        if (reopened.Count > 0)
        {
            var parentIds = reopened.ToList();
            var parents = await _db.Set<Procurement>()
                .Where(p => parentIds.Contains(p.Id))
                .ToListAsync();
            foreach (var parent in parents)
            {
                if (parent.Status == "Finalized") parent.Status = "Reopened";
            }
        }

        await _db.SaveChangesAsync();

        return (reopened.ToList(), skipped, warnings);
    }

    /// <summary>
    /// Called when a PO is marked Completed — stamps FulfilledByPOItemId on the source
    /// ProcurementItems of every non-returned POItem on that PO. Idempotent.
    /// </summary>
    public async Task MarkFulfilledByPOAsync(long poId)
    {
        var items = await _db.Set<POItem>()
            .Where(p => p.POId == poId && p.ReturnedAt == null && p.SourceProcurementItemId != null)
            .ToListAsync();

        if (items.Count == 0) return;

        var sourceIds = items.Select(p => p.SourceProcurementItemId!.Value).Distinct().ToList();
        var sources = await _db.Set<ProcurementItem>()
            .Where(pi => sourceIds.Contains(pi.Id))
            .ToListAsync();
        var sourceMap = sources.ToDictionary(pi => pi.Id);

        var now = DateTime.UtcNow;
        foreach (var p in items)
        {
            if (!sourceMap.TryGetValue(p.SourceProcurementItemId!.Value, out var src)) continue;
            src.FulfilledByPOItemId = p.Id;
            src.UpdatedAt = now;
        }

        await _db.SaveChangesAsync();
    }

    // ────────────────────────────────────────────────────────────────
    // Supplier-quote mapping helper
    // ────────────────────────────────────────────────────────────────
    private static ProcurementSupplierQuoteResponse MapSupplierQuote(ProcurementSupplierQuote q) => new()
    {
        Id = q.Id,
        ProcurementItemId = q.ProcurementItemId,
        SupplierId = q.SupplierId,
        SupplierName = q.SupplierName,
        Price = q.Price,
        Qty = q.Qty,
        Condition = q.Condition,
        Unit = q.Unit,
        Alt = q.Alt,
        LeadTime = q.LeadTime,
        CertName = q.CertName,
        ShippingCost = q.ShippingCost,
        Note = q.Note,
        TagDate = q.TagDate,
        ShippingPoint = q.ShippingPoint,
        IsSelected = q.IsSelected,
        SourceProcumentRecordId = q.SourceProcumentRecordId,
        SortOrder = q.SortOrder,
        CreatedAt = q.CreatedAt,
        AddedByUserId = q.AddedByUserId,
    };
}

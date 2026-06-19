using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Procument.Module.Catalog.Entities;
using Procument.Module.Identity.Entities;
using Procument.Module.Purchasing.DTOs;
using Procument.Module.Purchasing.Entities;
using Procument.Module.RFQ.Entities;
using Procument.Shared.DTOs;
using Procument.Shared.Services;

namespace Procument.Module.Purchasing.Services;

public interface IPurchaseOrderService
{
    Task<PagedResult<POResponse>> GetAllAsync(PageQuery page, long userId, bool isAdmin, bool isSuperAdmin = true, int[]? userBases = null);
    Task<POResponse?> GetByIdAsync(long id);
    Task<bool> UserCanAccessAsync(long poId, long userId, bool isAdmin, bool isSuperAdmin = true, int[]? userBases = null);
    /// <summary>
    /// Get all unassigned POItems (POId is null, not returned). Admins see every live item;
    /// non-admins only see items whose SourceProcurementItem is assigned to them via
    /// EntityPermission(EntityName="Procurement").
    /// </summary>
    Task<List<UnassignedPOItemResponse>> GetUnassignedItemsAsync(long userId, bool isAdmin, bool isSuperAdmin = true, int[]? userBases = null);
    Task<POResponse> CreateAsync(CreatePORequest request);
    Task<bool> UpdateStatusAsync(long id, string newStatus, bool isAdmin, bool isSuperAdmin, string? rejectionNote = null);
    Task<bool> UpdateItemAsync(UpdatePOItemRequest request);
    /// <summary>SuperAdmin-only: override the UnitPrice of a POItem and recalculate TotalPrice + PO.TotalAmount.</summary>
    Task<bool> UpdateItemPriceAsync(long poItemId, decimal unitPrice);
    Task<bool> DeleteAsync(long id);

    /// <summary>
    /// Return a PO (or a subset of its items) back into the Procurement layer. If ItemIds is
    /// null/empty, returns the ENTIRE PO (full return → PO.Status = Returned). Partial returns
    /// leave the remaining items on the PO untouched.
    /// </summary>
    Task<ReturnPOResponse?> ReturnAsync(long poId, ReturnPORequest request, long userId);
}

public class PurchaseOrderService : IPurchaseOrderService
{
    private readonly DbContext _db;
    private readonly IDocumentStorageService _documentStorage;
    private readonly IProcurementService _procurementService;

    public PurchaseOrderService(DbContext db, IDocumentStorageService documentStorage, IProcurementService procurementService)
    {
        _db = db;
        _documentStorage = documentStorage;
        _procurementService = procurementService;
    }

    /// <summary>Get all unassigned POItems (POId is null) — enriched with ExType, supplier, customer.</summary>
    public async Task<List<UnassignedPOItemResponse>> GetUnassignedItemsAsync(long userId, bool isAdmin, bool isSuperAdmin = true, int[]? userBases = null)
    {
        IQueryable<POItem> q = _db.Set<POItem>()
            .Where(i => i.POId == null && i.ReturnedAt == null);

        if (!isSuperAdmin)
        {
            // 1. Resolve all explicit permissions for this user
            var perms = await _db.Set<EntityPermission>()
                .Where(p => p.UserId == userId && (p.EntityName == "Invoice" || p.EntityName == "Procurement" || p.EntityName == "PO"))
                .Select(p => new { p.EntityName, p.EntityId })
                .ToListAsync();

            var invIds   = perms.Where(p => p.EntityName == "Invoice").Select(p => long.TryParse(p.EntityId, out var l) ? l : -1L).Where(l => l > 0).ToHashSet();
            var procIds  = perms.Where(p => p.EntityName == "Procurement").Select(p => long.TryParse(p.EntityId, out var l) ? l : -1L).Where(l => l > 0).ToHashSet();
            var poIds    = perms.Where(p => p.EntityName == "PO").Select(p => long.TryParse(p.EntityId, out var l) ? l : -1L).Where(l => l > 0).ToHashSet();

            // 2. Resolve allowed InvoiceItem IDs by base
            List<long> allowedInvoiceItemIds = [];
            if (userBases != null && userBases.Length > 0)
            {
                allowedInvoiceItemIds = await ResolveInvoiceItemIdsByBase(userBases);
            }

            // 3. Apply Filter: (In Base) OR (Assigned via Procurement) OR (Assigned via Invoice)
            q = q.Where(i =>
                (userBases != null && userBases.Length > 0 && i.InvoiceItemId != null && allowedInvoiceItemIds.Contains(i.InvoiceItemId.Value)) ||
                (i.SourceProcurementItem != null && procIds.Contains(i.SourceProcurementItem.Id)) ||
                (i.InvoiceItemId != null && invIds.Contains(i.InvoiceItemId.Value)) ||
                (i.POId != null && poIds.Contains(i.POId.Value)) ||
                (isAdmin && i.InvoiceItemId == null) || // Admins see internal items
                (isAdmin && i.InvoiceItemId != null && i.ProcumentRecord != null && i.ProcumentRecord.RFQItem != null && i.ProcumentRecord.RFQItem.RFQ.UserId == userId)); // Admins see items from their own RFQs
        }

        var items = await q
            .Include(i => i.PartNumber)
            .Include(i => i.SourceProcurementItem)
                .ThenInclude(pi => pi!.CurrentSupplier)
            .Include(i => i.ProcumentRecord)
                .ThenInclude(pr => pr!.Supplier)
            .Include(i => i.ProcumentRecord)
                .ThenInclude(pr => pr!.RFQItem)
                    .ThenInclude(ri => ri.RFQ)
                        .ThenInclude(r => r.Customer)
            .OrderBy(i => i.Id)
            .ToListAsync();

        // Batch-load all overridden suppliers to avoid N+1
        var overriddenSupplierIds = items
            .Where(i => i.SupplierId.HasValue)
            .Select(i => i.SupplierId!.Value)
            .Distinct()
            .ToList();
        var overriddenSuppliers = overriddenSupplierIds.Count > 0
            ? await _db.Set<Supplier>().Where(s => overriddenSupplierIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id)
            : new Dictionary<long, Supplier>();

        // Resolve InvoiceId from InvoiceItemId via raw lookup
        var invoiceItemIds = items.Where(i => i.InvoiceItemId.HasValue).Select(i => i.InvoiceItemId!.Value).Distinct().ToList();
        var invoiceItemMap = await ResolveInvoiceFromItemIds(invoiceItemIds);

        return items.Select(i =>
        {
            var proc = i.ProcumentRecord;
            var rfqItem = proc?.RFQItem;
            var rfq = rfqItem?.RFQ;
            var srcProcItem = i.SourceProcurementItem;

            // Resolve supplier name priority:
            // 1. POItem.SupplierId (explicit override)
            // 2. SourceProcurementItem.CurrentSupplier (the selection from the new Procurement layer)
            // 3. ProcumentRecord.Supplier (the original RFQ selection)
            string? supplierName = null;
            if (i.SupplierId.HasValue && overriddenSuppliers.TryGetValue(i.SupplierId.Value, out var ovSup))
                supplierName = ovSup.Name;
            
            supplierName ??= srcProcItem?.CurrentSupplier?.Name;
            supplierName ??= proc?.Supplier?.Name;

            var invoiceInfo = i.InvoiceItemId.HasValue && invoiceItemMap.ContainsKey(i.InvoiceItemId.Value)
                ? invoiceItemMap[i.InvoiceItemId.Value] : (Id: (long?)null, Number: (string?)null);

            return new UnassignedPOItemResponse
            {
                Id = i.Id,
                Qty = i.Qty,
                UnitPrice = i.UnitPrice,
                TotalPrice = i.TotalPrice,
                Condition = i.Condition,
                SupplierId = i.SupplierId ?? srcProcItem?.CurrentSupplierId ?? proc?.SupplierId,
                SupplierName = supplierName ?? "Unknown Supplier",
                PartNumberId = i.PartNumberId,
                PartNumberName = i.PartNumber?.Name ?? "",
                Alt = proc?.Alt,
                ProcumentId = i.ProcumentId,
                InvoiceItemId = i.InvoiceItemId,
                // ExType priority:
                //  1. Live RFQ (if ProcumentRecord chain survived)
                //  2. ProcurementItem.RfqExType snapshot — always populated during CreateFromAcceptedInvoice,
                //     which is the reliable fallback for items that went through the Procurement→PO path
                //     (including loop-recycled items where the ProcumentRecord linkage can be missing).
                ExType = rfq?.ExType ?? srcProcItem?.RfqExType,
                CustomerName = rfq?.Customer?.Name,
                InvoiceId = invoiceInfo.Id,
                InvoiceNumber = invoiceInfo.Number,
            };
        }).ToList();
    }

    public async Task<PagedResult<POResponse>> GetAllAsync(PageQuery page, long userId, bool isAdmin, bool isSuperAdmin = true, int[]? userBases = null)
    {
        IQueryable<PurchaseOrder> baseQ = _db.Set<PurchaseOrder>().AsNoTracking();

        if (!isSuperAdmin)
        {
            // 1. Resolve assigned PO IDs
            var permittedIdStrings = await _db.Set<EntityPermission>()
                .Where(p => p.UserId == userId && p.EntityName == "PO")
                .Select(p => p.EntityId)
                .ToListAsync();
            var permittedIds = permittedIdStrings
                .Select(s => long.TryParse(s, out var l) ? l : -1L)
                .Where(l => l > 0)
                .ToList();

            // 2. Resolve allowed Invoice IDs by base
            List<long> allowedInvoiceIds = [];
            if (userBases != null && userBases.Length > 0)
            {
                allowedInvoiceIds = await ResolveInvoiceIdsByBase(userBases);
            }

            // 3. Apply Filter: (Assigned) OR (In Base)
            if (allowedInvoiceIds.Count > 0)
            {
                baseQ = baseQ.Where(po =>
                    permittedIds.Contains(po.Id) ||
                    (po.InvoiceId != null && allowedInvoiceIds.Contains(po.InvoiceId.Value)) ||
                    (po.InvoiceId == null && isAdmin)); // Admins see internal POs (no invoice) if they have bases
            }
            else
            {
                // If user has no bases, they ONLY see explicitly assigned POs
                baseQ = baseQ.Where(po => permittedIds.Contains(po.Id));
            }
        }

        var query = baseQ.OrderByDescending(po => po.CreatedAt);

        var total = await query.CountAsync();
        var pos = await query
            .ApplyPaging(page)
            .Include(po => po.Supplier)
            .Include(po => po.POItems)
                .ThenInclude(i => i.PartNumber)
            .Include(po => po.POItems)
                .ThenInclude(i => i.ProcumentRecord)
                    .ThenInclude(pr => pr!.Supplier)
            .Include(po => po.POItems)
                .ThenInclude(i => i.TrackNumbers)
                    .ThenInclude(t => t.Items)
            .ToListAsync();

        var invoiceMap = await ResolveInvoiceNumbers(pos.Where(p => p.InvoiceId.HasValue).Select(p => p.InvoiceId!.Value).Distinct().ToList());

        // Batch-load any overridden suppliers across all items on the page
        var overriddenSupplierIds = pos.SelectMany(po => po.POItems)
            .Where(i => i.SupplierId.HasValue)
            .Select(i => i.SupplierId!.Value)
            .Distinct()
            .ToList();
        var overriddenSuppliers = overriddenSupplierIds.Count > 0
            ? await _db.Set<Supplier>().Where(s => overriddenSupplierIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name)
            : new Dictionary<long, string>();

        var items = pos.Select(po => MapToResponse(po, po.InvoiceId.HasValue && invoiceMap.ContainsKey(po.InvoiceId.Value) ? invoiceMap[po.InvoiceId.Value] : null, overriddenSuppliers)).ToList();

        return new PagedResult<POResponse> { Items = items, TotalCount = total, Page = page.Page, PageSize = page.PageSize };
    }

    public async Task<bool> UserCanAccessAsync(long poId, long userId, bool isAdmin, bool isSuperAdmin = true, int[]? userBases = null)
    {
        if (isSuperAdmin) return true;

        // 1. Check explicit assignment
        var assigned = await _db.Set<EntityPermission>()
            .AnyAsync(p => p.UserId == userId && p.EntityName == "PO" && p.EntityId == poId.ToString());
        if (assigned) return true;

        // 2. Check base
        if (userBases != null && userBases.Length > 0)
        {
            var po = await _db.Set<PurchaseOrder>().FindAsync(poId);
            if (po == null) return false;
            
            if (po.InvoiceId == null) return isAdmin; // Admins see internal POs if they have bases

            return await CheckInvoiceBase(po.InvoiceId.Value, userBases);
        }

        return false;
    }

    private async Task<bool> CheckInvoiceBase(long invoiceId, int[] userBases)
    {
        if (userBases == null || userBases.Length == 0) return false;
        var conn = _db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        var baseList = string.Join(",", userBases);
        cmd.CommandText = $"SELECT COUNT(1) FROM Invoices i JOIN Customers c ON c.Id = i.CustomerId WHERE i.Id = @invId AND (c.[Base] IS NULL OR c.[Base] IN ({baseList}))";
        var p = cmd.CreateParameter();
        p.ParameterName = "@invId";
        p.Value = invoiceId;
        cmd.Parameters.Add(p);

        return (int)(await cmd.ExecuteScalarAsync() ?? 0) > 0;
    }

    public async Task<POResponse?> GetByIdAsync(long id)
    {
        var po = await _db.Set<PurchaseOrder>()
            .Include(po => po.Supplier)
            .Include(po => po.POItems)
                .ThenInclude(i => i.PartNumber)
            .Include(po => po.POItems)
                .ThenInclude(i => i.ProcumentRecord)
                    .ThenInclude(pr => pr!.Supplier)
            .Include(po => po.POItems)
                .ThenInclude(i => i.TrackNumbers)
            .FirstOrDefaultAsync(po => po.Id == id);

        if (po == null) return null;

        // Resolve any overridden supplier names for individual items
        var overriddenSupplierIds = po.POItems
            .Where(i => i.SupplierId.HasValue)
            .Select(i => i.SupplierId!.Value)
            .Distinct()
            .ToList();
        var overriddenSuppliers = overriddenSupplierIds.Count > 0
            ? await _db.Set<Supplier>().Where(s => overriddenSupplierIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name)
            : new Dictionary<long, string>();

        string? invoiceNumber = null;
        if (po.InvoiceId.HasValue)
        {
            var map = await ResolveInvoiceNumbers(new List<long> { po.InvoiceId.Value });
            invoiceNumber = map.GetValueOrDefault(po.InvoiceId.Value);
        }

        return MapToResponse(po, invoiceNumber, overriddenSuppliers);
    }

    /// <summary>Create PO and assign existing unassigned POItems to it.</summary>
    public async Task<POResponse> CreateAsync(CreatePORequest request)
    {
        var po = new PurchaseOrder
        {
            PONumber = "", // Will be set after getting Id
            SupplierId = request.SupplierId,
            InvoiceId = request.InvoiceId,
            Status = "Waiting For Admin Approval",
            CreatedAt = DateTime.UtcNow,
            PreferredWalletId = request.PreferredWalletId,
        };

        _db.Set<PurchaseOrder>().Add(po);
        await _db.SaveChangesAsync();

        // Set PO number using the actual Id
        po.PONumber = $"PO-{po.Id}";
        await _db.SaveChangesAsync();

        // Assign selected POItems to this PO
        var poItems = await _db.Set<POItem>()
            .Where(i => request.POItemIds.Contains(i.Id) && i.POId == null)
            .ToListAsync();

        decimal totalAmount = 0;
        // Preserve the order the caller selected the items in so PORef is stable & predictable.
        var orderedItems = request.POItemIds
            .Select(id => poItems.FirstOrDefault(p => p.Id == id))
            .Where(p => p != null)
            .ToList()!;
        int lineNo = 1;
        foreach (var item in orderedItems)
        {
            item!.POId = po.Id;
            item.PORef = lineNo++;
            // Default workflow status for newly-created PO lines so the Total P/N grid renders something.
            if (string.IsNullOrWhiteSpace(item.Status))
                item.Status = "Not Started";
            totalAmount += item.TotalPrice;
        }

        po.TotalAmount = totalAmount;
        await _db.SaveChangesAsync();

        // ── Auto-assign users from source RFQs/Procurements to this new PO ──
        try
        {
            var sourceRfqIds = orderedItems
                .Select(i => i.ProcumentRecord?.RFQItem?.RFQId)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct().ToList();

            var sourceProcItemIds = orderedItems
                .Select(i => i.SourceProcurementItemId)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct().ToList();

            var usersToAssign = new Dictionary<long, string>(); // userId -> permission (Edit wins over View)

            // 1. Gather from RFQs
            foreach (var rfqId in sourceRfqIds)
            {
                var rfq = await _db.Set<RFQHeader>().FindAsync(rfqId);
                if (rfq?.UserId != null) usersToAssign[rfq.UserId.Value] = "Edit";

                var rfqPerms = await _db.Set<EntityPermission>()
                    .Where(p => p.EntityName == "RFQ" && p.EntityId == rfqId.ToString())
                    .ToListAsync();
                foreach (var p in rfqPerms)
                {
                    if (!usersToAssign.ContainsKey(p.UserId) || p.Permission == "Edit")
                        usersToAssign[p.UserId] = p.Permission;
                }
            }

            // 2. Gather from Procurement items
            foreach (var procItemId in sourceProcItemIds)
            {
                var procPerms = await _db.Set<EntityPermission>()
                    .Where(p => p.EntityName == "Procurement" && p.EntityId == procItemId.ToString())
                    .ToListAsync();
                foreach (var p in procPerms)
                {
                    if (!usersToAssign.ContainsKey(p.UserId) || p.Permission == "Edit")
                        usersToAssign[p.UserId] = p.Permission;
                }
            }

            // 3. Apply to PO
            foreach (var kv in usersToAssign)
            {
                var already = await _db.Set<EntityPermission>()
                    .AnyAsync(p => p.UserId == kv.Key && p.EntityName == "PO" && p.EntityId == po.Id.ToString());
                if (!already)
                {
                    _db.Set<EntityPermission>().Add(new EntityPermission
                    {
                        UserId = kv.Key,
                        EntityName = "PO",
                        EntityId = po.Id.ToString(),
                        Permission = kv.Value,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
            await _db.SaveChangesAsync();
        }
        catch { /* auto-assignment is best-effort */ }

        // Create supplier subfolders inside each related Proforma Invoice folder
        try { await CreateSupplierFoldersForPoAsync(po, poItems); }
        catch { /* folder creation must not fail PO creation */ }

        return (await GetByIdAsync(po.Id))!;
    }

    /// <summary>
    /// For every Proforma Invoice linked (directly or via POItems) to this PO,
    /// create a supplier-named subfolder. Uses the PO's supplier name.
    /// </summary>
    private async Task CreateSupplierFoldersForPoAsync(PurchaseOrder po, List<POItem> poItems)
    {
        // Resolve supplier name (from PO)
        var supplier = await _db.Set<Supplier>().FindAsync(po.SupplierId);
        var supplierName = supplier?.Name ?? "Unknown Supplier";

        // Collect invoice numbers: from po.InvoiceId (direct) + from POItem -> InvoiceItem -> Invoice
        var invoiceNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (po.InvoiceId.HasValue)
        {
            var direct = await ResolveInvoiceNumbers(new List<long> { po.InvoiceId.Value });
            if (direct.TryGetValue(po.InvoiceId.Value, out var n) && !string.IsNullOrWhiteSpace(n))
                invoiceNumbers.Add(n);
        }

        var invoiceItemIds = poItems.Where(i => i.InvoiceItemId.HasValue).Select(i => i.InvoiceItemId!.Value).Distinct().ToList();
        if (invoiceItemIds.Count > 0)
        {
            var map = await ResolveInvoiceFromItemIds(invoiceItemIds);
            foreach (var kv in map)
            {
                if (!string.IsNullOrWhiteSpace(kv.Value.Number))
                    invoiceNumbers.Add(kv.Value.Number!);
            }
        }

        foreach (var invoiceNumber in invoiceNumbers)
        {
            _documentStorage.EnsureSupplierSubfolder(invoiceNumber, supplierName);
        }
    }

    public async Task<bool> UpdateStatusAsync(long id, string newStatus, bool isAdmin, bool isSuperAdmin, string? rejectionNote = null)
    {
        var po = await _db.Set<PurchaseOrder>().FindAsync(id);
        if (po == null) return false;

        // Already in a terminal state — no further transitions allowed
        if (po.Status == "Completed" || po.Status == "Cancelled" || po.Status == "Returned") return false;

        // Allowed Statuses
        var allowed = new[] {
            "Draft", "Waiting For Admin Approval", "Waiting For Documents", "Waiting For Payment",
            "PO Sent", "Document Added", "Payment Done", "Waiting For Shipment",
            "Ship To Warehouse 1", "Ship To Warehouse 2",
            "Ship To Warehouse 3", "Ship To Customer", "Completed", "Cancelled", "Issue"
        };
        if (!allowed.Contains(newStatus)) return false;

        // ── Cancellation: always allowed regardless of current state or role ──
        if (newStatus == "Cancelled")
        {
            po.Status = "Cancelled";

            // 1. Soft-delete all live POItems on this PO so they disappear from Order Items entirely.
            //    Setting ReturnedAt removes them cleanly — no ghost items floating in Order Items.
            //    When the user re-approves from a new procurement, fresh POItems are created with no duplicates.
            //    Also sweep loose partial-return fragments that originated from this PO (POId == null,
            //    ReturnedFromPOId == this PO) — otherwise they survive the reset and collide with the
            //    fresh re-approved POItem, producing duplicates in the Vendor/Customer, Warehouse and Total P/N views.
            var liveItems = await _db.Set<POItem>()
                .Where(i => i.ReturnedAt == null &&
                            (i.POId == po.Id ||
                             (i.POId == null && i.ReturnedFromPOId == po.Id)))
                .ToListAsync();

            var now = DateTime.UtcNow;
            foreach (var item in liveItems)
                item.ReturnedAt = now;

            await _db.SaveChangesAsync();

            // 3. Cancel all Procurements linked to this Invoice so users can start fresh.
            //    A cancelled PO means the procurement cycle needs to restart completely.
            if (po.InvoiceId.HasValue)
            {
                try
                {
                    var linkedProcurements = await _db.Set<Procurement>()
                        .Where(pr => pr.InvoiceId == po.InvoiceId.Value)
                        .ToListAsync();
                    foreach (var pr in linkedProcurements)
                        pr.Status = "Open";
                    if (linkedProcurements.Count > 0)
                        await _db.SaveChangesAsync();
                }
                catch { /* non-fatal */ }
            }

            // 4. Flip the linked Invoice status to "PO Cancelled" (raw SQL — avoids circular module dep)
            if (po.InvoiceId.HasValue)
            {
                try
                {
                    var conn = _db.Database.GetDbConnection();
                    if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "UPDATE Invoices SET Status = 'PO Cancelled' WHERE Id = @id";
                    var p = cmd.CreateParameter(); p.ParameterName = "@id"; p.Value = po.InvoiceId.Value;
                    cmd.Parameters.Add(p);
                    await cmd.ExecuteNonQueryAsync();
                }
                catch { /* non-fatal — invoice update is cosmetic */ }
            }

            return true;
        }

        // Restriction: Only Admin or SuperAdmin can manually set back to "Waiting For Admin Approval"
        if (!isAdmin && newStatus == "Waiting For Admin Approval")
        {
            return false;
        }

        // Block manual status changes during approval/payment workflow
        // 1. If waiting for Admin approval
        if (po.AdminApproval != "Approved" && po.Status == "Waiting For Admin Approval")
        {
            // Only Admin or SuperAdmin can override or correct status at this stage
            if (!isAdmin) return false;
        }

        // 2. If waiting for Documents
        if (po.Status == "Waiting For Documents" && newStatus != "Waiting For Payment" && newStatus != "Cancelled")
        {
            // Must go to Waiting For Payment next, or be cancelled
            if (!isAdmin) return false;
        }

        // 3. If waiting for Payment (Admin approved but payment not yet submitted)
        if (po.AdminApproval == "Approved" && po.Status == "Waiting For Payment" && po.PaymentStatus != "Submitted")
        {
            // Even Admin/SuperAdmin shouldn't skip the payment submission step manually via status change
            if (!isAdmin && newStatus != "Waiting For Documents") return false;
        }

        po.Status = newStatus;
        await _db.SaveChangesAsync();

        // Completed → close the loop on source ProcurementItems
        if (newStatus == "Completed")
        {
            try { await _procurementService.MarkFulfilledByPOAsync(po.Id); }
            catch { /* never break the status transition on a downstream stamp */ }
        }

        return true;
    }

    /// <summary>
    /// Return a PO (or a subset of its items) back into Procurement. Full return (empty ItemIds)
    /// sets PO.Status = "Returned"; partial returns leave the PO open with its surviving items.
    /// ItemQtys (POItemId → qty) enables partial-quantity returns: the original item shrinks and
    /// a new unassigned POItem for the returned qty surfaces in the procurement pool.
    /// </summary>
    public async Task<ReturnPOResponse?> ReturnAsync(long poId, ReturnPORequest request, long userId)
    {
        var po = await _db.Set<PurchaseOrder>()
            .Include(p => p.POItems)
            .FirstOrDefaultAsync(p => p.Id == poId);
        if (po == null) return null;

        // Guard terminal states
        if (po.Status == "Completed" || po.Status == "Cancelled" || po.Status == "Returned") return null;

        // Live items on this PO (exclude anything already returned)
        var liveItems = po.POItems.Where(i => i.ReturnedAt == null).ToList();
        if (liveItems.Count == 0) return null;

        var reason = string.IsNullOrWhiteSpace(request.Reason) ? "(no reason provided)" : request.Reason.Trim();
        var now = DateTime.UtcNow;

        List<long> fullReturnIds;
        bool fullReturn;

        if (request.ItemQtys != null && request.ItemQtys.Count > 0)
        {
            // ── Qty-based path ──────────────────────────────────────────────────
            // Build a map of live item → clamped return qty (1..item.Qty)
            var itemQtysMap = liveItems
                .Where(i => request.ItemQtys.ContainsKey(i.Id) && request.ItemQtys[i.Id] > 0)
                .Select(i => new { Item = i, ReturnQty = Math.Min(request.ItemQtys[i.Id], i.Qty) })
                .ToList();

            if (itemQtysMap.Count == 0) return null;

            // Split: partial qty returns vs. full item returns
            var partials = itemQtysMap.Where(x => x.ReturnQty < x.Item.Qty).ToList();
            fullReturnIds = itemQtysMap.Where(x => x.ReturnQty >= x.Item.Qty).Select(x => x.Item.Id).ToList();

            // Handle partial-qty returns: the line stays on the PO at reduced qty, and the returned
            // units go BACK INTO the Procurement layer (re-sourceable) — not straight into the PO pool.
            foreach (var p in partials)
            {
                var poItem = p.Item;
                var returnQty = p.ReturnQty;

                // Shrink the line that stays on the PO
                poItem.Qty -= returnQty;
                poItem.TotalPrice = poItem.Qty * poItem.UnitPrice;

                // Send the returned units back into Procurement as a new "Open" item so the user can
                // change supplier / qty and re-approve. (This also gives the returned qty its OWN
                // ProcurementItem id, so a later re-approval can never collide with the source line.)
                bool split = false;
                if (poItem.SourceProcurementItemId.HasValue)
                    split = await _procurementService.SplitReturnedQtyToProcurementAsync(
                        poItem.SourceProcurementItemId.Value, returnQty, reason, userId);

                // Fallback for legacy / untraceable items (no Procurement source): keep the returned
                // qty as an unassigned POItem so it is never lost. These rare strays are swept on
                // PO cancel / full return.
                if (!split)
                {
                    var newItem = new POItem
                    {
                        Qty             = returnQty,
                        UnitPrice       = poItem.UnitPrice,
                        TotalPrice      = returnQty * poItem.UnitPrice,
                        Condition       = poItem.Condition,
                        SupplierId      = poItem.SupplierId,
                        PartNumberId    = poItem.PartNumberId,
                        SourceProcurementItemId = poItem.SourceProcurementItemId,
                        ProcumentId     = poItem.ProcumentId,
                        InvoiceItemId   = poItem.InvoiceItemId,
                        Note            = poItem.Note,
                        ReturnedFromPOId = poId,
                        ReturnReason     = reason,
                    };
                    _db.Set<POItem>().Add(newItem);
                }
            }

            // Full return = every live item is being fully returned (no partials, all full)
            fullReturn = partials.Count == 0 && fullReturnIds.Count == liveItems.Count;
        }
        else
        {
            // ── Legacy ItemIds path (all-or-nothing per item) ───────────────────
            fullReturnIds = (request.ItemIds == null || request.ItemIds.Count == 0)
                ? liveItems.Select(i => i.Id).ToList()
                : liveItems.Where(i => request.ItemIds.Contains(i.Id)).Select(i => i.Id).ToList();

            if (fullReturnIds.Count == 0) return null;

            fullReturn = fullReturnIds.Count == liveItems.Count;
        }

        // Recycle full-return items through procurement (soft-delete + reopen ProcurementItem)
        List<long> reopened = [];
        List<long> skipped = [];
        List<string> warnings = [];
        if (fullReturnIds.Count > 0)
            (reopened, skipped, warnings) = await _procurementService.RecyclePOItemsAsync(fullReturnIds, po.Id, reason, userId);

        // Recompute PO total from whatever remains live
        var remaining = await _db.Set<POItem>()
            .Where(i => i.POId == po.Id && i.ReturnedAt == null)
            .ToListAsync();
        po.TotalAmount = remaining.Sum(i => i.TotalPrice);

        // Flip PO status on full return (partial returns keep the PO in its current workflow state)
        if (fullReturn)
        {
            // Sweep leftover partial-return fragments from PRIOR partial returns on this PO.
            // fullReturn implies no new partials were created in this same call, so this never
            // deletes a clone being created right now — only stale strays from earlier actions.
            var strays = await _db.Set<POItem>()
                .Where(i => i.POId == null && i.ReturnedAt == null && i.ReturnedFromPOId == po.Id)
                .ToListAsync();
            foreach (var s in strays) s.ReturnedAt = now;

            po.Status = "Returned";
            po.ReturnReason = reason;
            po.ReturnedAt = now;
            po.ReturnedByUserId = userId > 0 ? userId : null;
        }
        else if (po.ReturnedAt == null)
        {
            // Stamp first-return audit on the PO for partial returns too (non-destructive)
            po.ReturnReason = reason;
            po.ReturnedAt = now;
            po.ReturnedByUserId = userId > 0 ? userId : null;
        }

        await _db.SaveChangesAsync();

        return new ReturnPOResponse
        {
            POId = po.Id,
            FullReturn = fullReturn,
            POStatus = po.Status,
            ReturnedPOItemIds = fullReturnIds.Except(skipped).ToList(),
            ReopenedProcurementIds = reopened,
            SkippedPOItemIds = skipped,
            Warnings = warnings,
        };
    }

    /// <summary>Update a POItem's supplier, qty, and unitPrice.</summary>
    public async Task<bool> UpdateItemAsync(UpdatePOItemRequest request)
    {
        var item = await _db.Set<POItem>().FindAsync(request.Id);
        if (item == null) return false;

        item.Qty = request.Qty;
        item.UnitPrice = request.UnitPrice;
        item.TotalPrice = request.Qty * request.UnitPrice;

        // Resolve supplier by ID or name
        if (request.SupplierId.HasValue)
        {
            item.SupplierId = request.SupplierId.Value;
        }
        else if (!string.IsNullOrWhiteSpace(request.SupplierName))
        {
            var supplier = await _db.Set<Supplier>()
                .FirstOrDefaultAsync(s => s.Name == request.SupplierName.Trim());
            if (supplier != null)
            {
                item.SupplierId = supplier.Id;
            }
        }

        await _db.SaveChangesAsync();

        // Recalculate PO total if item is assigned to a PO
        if (item.POId.HasValue)
        {
            var poItems = await _db.Set<POItem>().Where(i => i.POId == item.POId).ToListAsync();
            var po = await _db.Set<PurchaseOrder>().FindAsync(item.POId.Value);
            if (po != null)
            {
                po.TotalAmount = poItems.Sum(i => i.TotalPrice);
                await _db.SaveChangesAsync();
            }
        }

        // ── SYNC: Propagate price back to ProcurementItem ──
        if (item.SourceProcurementItemId.HasValue)
        {
            var pi = await _db.Set<ProcurementItem>().FindAsync(item.SourceProcurementItemId.Value);
            if (pi != null)
            {
                pi.UnitPrice = item.UnitPrice;
                pi.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }
        }

        return true;
    }

    public async Task<bool> UpdateItemPriceAsync(long poItemId, decimal unitPrice)
    {
        var item = await _db.Set<POItem>().FindAsync(poItemId);
        if (item == null) return false;

        item.UnitPrice = unitPrice;
        item.TotalPrice = item.Qty * unitPrice;
        await _db.SaveChangesAsync();

        // Recalculate PO total
        if (item.POId.HasValue)
        {
            var poItems = await _db.Set<POItem>().Where(i => i.POId == item.POId).ToListAsync();
            var po = await _db.Set<PurchaseOrder>().FindAsync(item.POId.Value);
            if (po != null)
            {
                po.TotalAmount = poItems.Sum(i => i.TotalPrice);
                await _db.SaveChangesAsync();
            }
        }

        // ── SYNC: Propagate price back to ProcurementItem ──
        if (item.SourceProcurementItemId.HasValue)
        {
            var pi = await _db.Set<ProcurementItem>().FindAsync(item.SourceProcurementItemId.Value);
            if (pi != null)
            {
                pi.UnitPrice = item.UnitPrice;
                pi.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }
        }

        return true;
    }

    public async Task<bool> DeleteAsync(long id)
    {
        var po = await _db.Set<PurchaseOrder>()
            .Include(p => p.POItems)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (po == null) return false;

        // Unassign POItems (set POId to null) instead of deleting them
        foreach (var item in po.POItems)
        {
            item.POId = null;
        }

        _db.Set<PurchaseOrder>().Remove(po);
        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>Resolve InvoiceId + InvoiceNumber from InvoiceItemIds via ADO.NET (avoids circular module dep).</summary>
    private async Task<Dictionary<long, (long? Id, string? Number)>> ResolveInvoiceFromItemIds(List<long> invoiceItemIds)
    {
        if (invoiceItemIds.Count == 0) return new();

        var result = new Dictionary<long, (long? Id, string? Number)>();
        var conn = _db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        var ids = string.Join(",", invoiceItemIds);
        cmd.CommandText = $"SELECT ii.Id AS InvoiceItemId, i.Id AS InvoiceId, i.InvoiceNumber FROM InvoiceItems ii INNER JOIN Invoices i ON ii.InvoiceId = i.Id WHERE ii.Id IN ({ids})";

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var iiId = reader.GetInt64(0);
            var invId = reader.GetInt64(1);
            var invNum = reader.GetString(2);
            result[iiId] = (invId, invNum);
        }

        return result;
    }

    /// <summary>Resolve InvoiceNumber from InvoiceIds via ADO.NET.</summary>
    private async Task<List<long>> ResolveInvoiceIdsByBase(int[] userBases)
    {
        if (userBases.Length == 0) return [];
        var result = new List<long>();
        var conn = _db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        var baseList = string.Join(",", userBases);
        cmd.CommandText = $"SELECT i.Id FROM Invoices i JOIN Customers c ON c.Id = i.CustomerId WHERE c.[Base] IS NULL OR c.[Base] IN ({baseList})";
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) result.Add(reader.GetInt64(0));
        return result;
    }

    private async Task<List<long>> ResolveInvoiceItemIdsByBase(int[] userBases)
    {
        if (userBases.Length == 0) return [];
        var result = new List<long>();
        var conn = _db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        var baseList = string.Join(",", userBases);
        cmd.CommandText = $"SELECT ii.Id FROM InvoiceItems ii JOIN Invoices i ON i.Id = ii.InvoiceId JOIN Customers c ON c.Id = i.CustomerId WHERE c.[Base] IS NULL OR c.[Base] IN ({baseList})";
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) result.Add(reader.GetInt64(0));
        return result;
    }

    private async Task<Dictionary<long, string>> ResolveInvoiceNumbers(List<long> invoiceIds)
    {
        if (invoiceIds.Count == 0) return new();

        var result = new Dictionary<long, string>();
        var conn = _db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        var ids = string.Join(",", invoiceIds);
        cmd.CommandText = $"SELECT Id, InvoiceNumber FROM Invoices WHERE Id IN ({ids})";

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result[reader.GetInt64(0)] = reader.GetString(1);
        }

        return result;
    }

    private static POResponse MapToResponse(PurchaseOrder po, string? invoiceNumber = null, Dictionary<long, string>? overriddenSuppliers = null) => new()
    {
        Id = po.Id,
        PONumber = po.PONumber,
        PODate = po.PODate,
        TotalAmount = po.TotalAmount,
        Status = po.Status,
        CreatedAt = po.CreatedAt,
        SupplierId = po.SupplierId,
        SupplierName = po.Supplier?.Name ?? "",
        InvoiceId = po.InvoiceId,
        InvoiceNumber = invoiceNumber,
        RejectionNote = po.RejectionNote,
        Subject = po.Subject,
        AdminApproval = po.AdminApproval,
        AdminApprovalNote = po.AdminApprovalNote,
        AdminApprovalAt = po.AdminApprovalAt,
        PaymentStatus = po.PaymentStatus,
        PaymentSubmittedAt = po.PaymentSubmittedAt,
        PaymentApproval = po.PaymentApproval,
        PaymentApprovalNote = po.PaymentApprovalNote,
        PaymentApprovalAt = po.PaymentApprovalAt,
        ProcessingFee = po.ProcessingFee,
        Shipping = po.Shipping,
        Tax = po.Tax,
        Items = po.POItems.Select(i => {
            string? sName = null;
            if (i.SupplierId.HasValue && overriddenSuppliers != null && overriddenSuppliers.TryGetValue(i.SupplierId.Value, out var name))
                sName = name;

            sName ??= i.ProcumentRecord?.Supplier?.Name;
            sName ??= po.Supplier?.Name;

            return new POItemResponse
            {
                Id = i.Id,
                POId = i.POId,
                ProcumentId = i.ProcumentId,
                PartNumberId = i.PartNumberId,
                PartNumberName = i.PartNumber?.Name ?? "",
                Description = i.PartNumber?.Description,
                Qty = i.Qty,
                UnitPrice = i.UnitPrice,
                TotalPrice = i.TotalPrice,
                Condition = i.Condition,
                SupplierId = i.SupplierId,
                SupplierName = sName ?? "Unknown Supplier",
                TrackNumbers = (i.TrackNumbers ?? new List<POItemTrackNumber>()).Select(t => new TrackNumberResponse
                {
                    Id = t.Id,
                    POItemId = t.POItemId,
                    TrackNumber = t.TrackNumber,
                    Carrier = t.Carrier,
                    Notes = t.Notes,
                    CreatedAt = t.CreatedAt,
                }).ToList(),
            };
        }).ToList(),
        AcceptedTrackItems = po.POItems
            .SelectMany(i => i.TrackNumbers ?? [])
            .SelectMany(t => t.Items ?? [])
            .Count(item => item.Status == "Accepted"),
        TotalTrackItems = po.POItems
            .SelectMany(i => i.TrackNumbers ?? [])
            .SelectMany(t => t.Items ?? [])
            .Count(),
    };
}


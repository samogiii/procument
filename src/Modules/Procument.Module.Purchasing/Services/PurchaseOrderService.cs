using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Procument.Module.Catalog.Entities;
using Procument.Module.Identity.Entities;
using Procument.Module.Purchasing.DTOs;
using Procument.Module.Purchasing.Entities;
using Procument.Shared.DTOs;
using Procument.Shared.Services;

namespace Procument.Module.Purchasing.Services;

public interface IPurchaseOrderService
{
    Task<PagedResult<POResponse>> GetAllAsync(PageQuery page, long userId, bool isAdmin);
    Task<POResponse?> GetByIdAsync(long id);
    Task<bool> UserCanAccessAsync(long poId, long userId, bool isAdmin);
    /// <summary>
    /// Get all unassigned POItems (POId is null, not returned). Admins see every live item;
    /// non-admins only see items whose SourceProcurementItem is assigned to them via
    /// EntityPermission(EntityName="Procurement").
    /// </summary>
    Task<List<UnassignedPOItemResponse>> GetUnassignedItemsAsync(long userId, bool isAdmin);
    Task<POResponse> CreateAsync(CreatePORequest request);
    Task<bool> UpdateStatusAsync(long id, string newStatus, bool isAdmin, bool isSuperAdmin, string? rejectionNote = null);
    Task<bool> UpdateItemAsync(UpdatePOItemRequest request);
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
    public async Task<List<UnassignedPOItemResponse>> GetUnassignedItemsAsync(long userId, bool isAdmin)
    {
        IQueryable<POItem> q = _db.Set<POItem>()
            .Where(i => i.POId == null && i.ReturnedAt == null);

        // Non-admins only see items that trace back to a ProcurementItem they're assigned to.
        // The Procurement→PO loop preserves SourceProcurementItemId on every finalized POItem,
        // so this is the canonical link between a user's Procurement assignment and the
        // resulting unassigned PO line.
        if (!isAdmin)
        {
            var permittedIdStrings = await _db.Set<EntityPermission>()
                .Where(p => p.UserId == userId && p.EntityName == "Procurement")
                .Select(p => p.EntityId)
                .ToListAsync();
            var permittedProcItemIds = permittedIdStrings
                .Select(s => long.TryParse(s, out var l) ? l : -1L)
                .Where(l => l > 0)
                .ToHashSet();

            if (permittedProcItemIds.Count == 0)
                return new List<UnassignedPOItemResponse>();

            q = q.Where(i => i.SourceProcurementItemId.HasValue
                             && permittedProcItemIds.Contains(i.SourceProcurementItemId.Value));
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

    public async Task<PagedResult<POResponse>> GetAllAsync(PageQuery page, long userId, bool isAdmin)
    {
        IQueryable<PurchaseOrder> baseQ = _db.Set<PurchaseOrder>();

        // Permission filter — non-admins see only POs they have an EntityPermission for
        if (!isAdmin)
        {
            var permittedIdStrings = await _db.Set<EntityPermission>()
                .Where(p => p.UserId == userId && p.EntityName == "PO")
                .Select(p => p.EntityId)
                .ToListAsync();
            var permittedIds = permittedIdStrings
                .Select(s => long.TryParse(s, out var l) ? l : -1L)
                .Where(l => l > 0)
                .ToList();
            baseQ = baseQ.Where(po => permittedIds.Contains(po.Id));
        }

        var query = baseQ.OrderByDescending(po => po.CreatedAt);

        var total = await query.CountAsync();
        var pos = await query
            .Skip((page.Page - 1) * page.PageSize)
            .Take(page.PageSize)
            .Include(po => po.Supplier)
            .Include(po => po.POItems)
                .ThenInclude(i => i.PartNumber)
            .Include(po => po.POItems)
                .ThenInclude(i => i.ProcumentRecord)
                    .ThenInclude(pr => pr!.Supplier)
            .Include(po => po.POItems)
                .ThenInclude(i => i.TrackNumbers)
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

    public async Task<bool> UserCanAccessAsync(long poId, long userId, bool isAdmin)
    {
        if (isAdmin) return true;
        return await _db.Set<EntityPermission>()
            .AnyAsync(p => p.UserId == userId && p.EntityName == "PO" && p.EntityId == poId.ToString());
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
        foreach (var item in poItems)
        {
            item.POId = po.Id;
            totalAmount += item.TotalPrice;
        }

        po.TotalAmount = totalAmount;
        await _db.SaveChangesAsync();

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

        // Allowed Statuses
        var allowed = new[] { 
            "Waiting For Admin Approval", "Waiting For Documents", "Waiting For Payment", 
            "Payment Done", "Ship To Warehouse 1", "Ship To Warehouse 2", 
            "Ship To Warehouse 3", "Ship To Customer", "Completed", "Cancelled" 
        };
        if (!allowed.Contains(newStatus)) return false;

        // Restriction: Only SuperAdmin can manually set back to "Waiting For Admin Approval"
        if (!isSuperAdmin && newStatus == "Waiting For Admin Approval")
        {
            return false;
        }

        // Block manual status changes during approval/payment workflow
        // 1. If waiting for Admin approval
        if (po.AdminApproval != "Approved" && po.Status == "Waiting For Admin Approval")
        {
            // Only SuperAdmin can override or correct status at this stage
            if (!isSuperAdmin) return false;
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
            // Even SuperAdmin shouldn't skip the payment submission step manually via status change
            if (!isSuperAdmin && newStatus != "Waiting For Documents") return false;
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

        // Determine target set: null/empty → full return
        var targetIds = (request.ItemIds == null || request.ItemIds.Count == 0)
            ? liveItems.Select(i => i.Id).ToList()
            : liveItems.Where(i => request.ItemIds.Contains(i.Id)).Select(i => i.Id).ToList();

        if (targetIds.Count == 0) return null;

        var reason = string.IsNullOrWhiteSpace(request.Reason) ? "(no reason provided)" : request.Reason.Trim();
        bool fullReturn = targetIds.Count == liveItems.Count;

        var (reopened, skipped, warnings) = await _procurementService.RecyclePOItemsAsync(targetIds, po.Id, reason, userId);

        // Recompute PO total from whatever remains live
        var remaining = await _db.Set<POItem>()
            .Where(i => i.POId == po.Id && i.ReturnedAt == null)
            .ToListAsync();
        po.TotalAmount = remaining.Sum(i => i.TotalPrice);

        // Flip PO status on full return (partial returns keep the PO in its current workflow state)
        var now = DateTime.UtcNow;
        if (fullReturn)
        {
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
            ReturnedPOItemIds = targetIds.Except(skipped).ToList(),
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
        TotalAmount = po.TotalAmount,
        Status = po.Status,
        CreatedAt = po.CreatedAt,
        SupplierId = po.SupplierId,
        SupplierName = po.Supplier?.Name ?? "",
        InvoiceId = po.InvoiceId,
        InvoiceNumber = invoiceNumber,
        RejectionNote = po.RejectionNote,
        AdminApproval = po.AdminApproval,
        AdminApprovalNote = po.AdminApprovalNote,
        AdminApprovalAt = po.AdminApprovalAt,
        PaymentStatus = po.PaymentStatus,
        PaymentSubmittedAt = po.PaymentSubmittedAt,
        PaymentApproval = po.PaymentApproval,
        PaymentApprovalNote = po.PaymentApprovalNote,
        PaymentApprovalAt = po.PaymentApprovalAt,
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
        }).ToList()
    };
}


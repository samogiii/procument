using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Procument.Module.Catalog.Entities;
using Procument.Module.Purchasing.DTOs;
using Procument.Module.Purchasing.Entities;

namespace Procument.Module.Purchasing.Services;

public interface IPurchaseOrderService
{
    Task<List<POResponse>> GetAllAsync();
    Task<POResponse?> GetByIdAsync(long id);
    Task<List<UnassignedPOItemResponse>> GetUnassignedItemsAsync();
    Task<POResponse> CreateAsync(CreatePORequest request);
    Task<bool> UpdateStatusAsync(long id, string newStatus, bool isAdmin, string? rejectionNote = null);
    Task<bool> UpdateItemAsync(UpdatePOItemRequest request);
    Task<bool> DeleteAsync(long id);
}

public class PurchaseOrderService : IPurchaseOrderService
{
    private readonly DbContext _db;

    public PurchaseOrderService(DbContext db)
    {
        _db = db;
    }

    /// <summary>Get all unassigned POItems (POId is null) — enriched with ExType, supplier, customer.</summary>
    public async Task<List<UnassignedPOItemResponse>> GetUnassignedItemsAsync()
    {
        var items = await _db.Set<POItem>()
            .Where(i => i.POId == null)
            .Include(i => i.PartNumber)
            .Include(i => i.ProcumentRecord)
                .ThenInclude(pr => pr!.Supplier)
            .Include(i => i.ProcumentRecord)
                .ThenInclude(pr => pr!.RFQItem)
                    .ThenInclude(ri => ri.RFQ)
                        .ThenInclude(r => r.Customer)
            .OrderBy(i => i.Id)
            .ToListAsync();

        // Resolve InvoiceId from InvoiceItemId via raw lookup
        var invoiceItemIds = items.Where(i => i.InvoiceItemId.HasValue).Select(i => i.InvoiceItemId!.Value).Distinct().ToList();
        var invoiceItemMap = await ResolveInvoiceFromItemIds(invoiceItemIds);

        return items.Select(i =>
        {
            var proc = i.ProcumentRecord;
            var rfqItem = proc?.RFQItem;
            var rfq = rfqItem?.RFQ;
            // Resolve supplier: use POItem.SupplierId first, fallback to ProcumentRecord.Supplier
            string? supplierName = null;
            if (i.SupplierId.HasValue)
            {
                // If SupplierId was overridden on the POItem, resolve it
                var supplier = _db.Set<Supplier>().Find(i.SupplierId.Value);
                supplierName = supplier?.Name;
            }
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
                SupplierId = i.SupplierId ?? proc?.SupplierId,
                SupplierName = supplierName ?? "Unknown Supplier",
                PartNumberId = i.PartNumberId,
                PartNumberName = i.PartNumber?.Name ?? "",
                Alt = proc?.Alt,
                ProcumentId = i.ProcumentId,
                InvoiceItemId = i.InvoiceItemId,
                ExType = rfq?.ExType,
                CustomerName = rfq?.Customer?.Name,
                InvoiceId = invoiceInfo.Id,
                InvoiceNumber = invoiceInfo.Number,
            };
        }).ToList();
    }

    public async Task<List<POResponse>> GetAllAsync()
    {
        var pos = await _db.Set<PurchaseOrder>()
            .Include(po => po.Supplier)
            .Include(po => po.POItems)
                .ThenInclude(i => i.PartNumber)
            .Include(po => po.POItems)
                .ThenInclude(i => i.ProcumentRecord)
                    .ThenInclude(pr => pr!.Supplier)
            .Include(po => po.POItems)
                .ThenInclude(i => i.TrackNumbers)
            .OrderByDescending(po => po.CreatedAt)
            .ToListAsync();

        // Resolve invoice numbers for all POs
        var invoiceMap = await ResolveInvoiceNumbers(pos.Where(p => p.InvoiceId.HasValue).Select(p => p.InvoiceId!.Value).Distinct().ToList());

        return pos.Select(po => MapToResponse(po, po.InvoiceId.HasValue && invoiceMap.ContainsKey(po.InvoiceId.Value) ? invoiceMap[po.InvoiceId.Value] : null)).ToList();
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

        string? invoiceNumber = null;
        if (po.InvoiceId.HasValue)
        {
            var map = await ResolveInvoiceNumbers(new List<long> { po.InvoiceId.Value });
            invoiceNumber = map.GetValueOrDefault(po.InvoiceId.Value);
        }

        return MapToResponse(po, invoiceNumber);
    }

    /// <summary>Create PO and assign existing unassigned POItems to it.</summary>
    public async Task<POResponse> CreateAsync(CreatePORequest request)
    {
        // Generate PO number
        var count = await _db.Set<PurchaseOrder>().CountAsync();
        var poNumber = $"PO-{(count + 1).ToString().PadLeft(5, '0')}";

        var po = new PurchaseOrder
        {
            PONumber = poNumber,
            SupplierId = request.SupplierId,
            InvoiceId = request.InvoiceId,
            Status = "Sent",
            CreatedAt = DateTime.UtcNow,
        };

        _db.Set<PurchaseOrder>().Add(po);
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

        return (await GetByIdAsync(po.Id))!;
    }

    public async Task<bool> UpdateStatusAsync(long id, string newStatus, bool isAdmin, string? rejectionNote = null)
    {
        var po = await _db.Set<PurchaseOrder>().FindAsync(id);
        if (po == null) return false;

        po.Status = newStatus;
        await _db.SaveChangesAsync();
        return true;
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

    private static POResponse MapToResponse(PurchaseOrder po, string? invoiceNumber = null) => new()
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
        Items = po.POItems.Select(i => new POItemResponse
        {
            Id = i.Id,
            POId = i.POId,
            ProcumentId = i.ProcumentId,
            PartNumberId = i.PartNumberId,
            PartNumberName = i.PartNumber?.Name ?? "",
            Qty = i.Qty,
            UnitPrice = i.UnitPrice,
            TotalPrice = i.TotalPrice,
            Condition = i.Condition,
            SupplierId = i.SupplierId,
            SupplierName = i.ProcumentRecord?.Supplier?.Name ?? po.Supplier?.Name ?? "",
            TrackNumbers = (i.TrackNumbers ?? new List<POItemTrackNumber>()).Select(t => new TrackNumberResponse
            {
                Id = t.Id,
                POItemId = t.POItemId,
                TrackNumber = t.TrackNumber,
                Carrier = t.Carrier,
                Notes = t.Notes,
                CreatedAt = t.CreatedAt,
            }).ToList(),
        }).ToList()
    };
}


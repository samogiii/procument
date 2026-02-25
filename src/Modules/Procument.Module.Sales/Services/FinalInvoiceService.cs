using Microsoft.EntityFrameworkCore;
using Procument.Module.Catalog.Entities;
using Procument.Module.Purchasing.Entities;
using Procument.Module.Sales.DTOs;
using Procument.Module.Sales.Entities;

namespace Procument.Module.Sales.Services;

public interface IFinalInvoiceService
{
    Task<List<FinalInvoiceResponse>> GetAllAsync();
    Task<FinalInvoiceResponse?> GetByIdAsync(long id);
    Task<FinalInvoiceResponse> CreateFromProformaAsync(long proformaInvoiceId);
    Task<bool> UpdateStatusAsync(long id, string status);
    Task<bool> UpdateAsync(long id, UpdateFinalInvoiceRequest request);
    Task<bool> CanCreateFinalInvoice(long proformaInvoiceId);
}

public class FinalInvoiceService : IFinalInvoiceService
{
    private readonly DbContext _db;

    public FinalInvoiceService(DbContext db)
    {
        _db = db;
    }

    public async Task<List<FinalInvoiceResponse>> GetAllAsync()
    {
        var invoices = await _db.Set<FinalInvoice>()
            .Include(fi => fi.Customer)
            .Include(fi => fi.ProformaInvoice)
            .Include(fi => fi.Items).ThenInclude(i => i.PartNumber)
            .OrderByDescending(fi => fi.CreatedAt)
            .ToListAsync();

        return invoices.Select(MapToResponse).ToList();
    }

    public async Task<FinalInvoiceResponse?> GetByIdAsync(long id)
    {
        var fi = await _db.Set<FinalInvoice>()
            .Include(f => f.Customer)
            .Include(f => f.ProformaInvoice)
            .Include(f => f.Items).ThenInclude(i => i.PartNumber)
            .FirstOrDefaultAsync(f => f.Id == id);

        return fi == null ? null : MapToResponse(fi);
    }

    /// <summary>Check if all POs for this proforma invoice are 'Completed'.</summary>
    public async Task<bool> CanCreateFinalInvoice(long proformaInvoiceId)
    {
        // Check if a final invoice already exists for this proforma
        var exists = await _db.Set<FinalInvoice>()
            .AnyAsync(fi => fi.ProformaInvoiceId == proformaInvoiceId);
        if (exists) return false;

        // Get all POs linked to this proforma invoice
        var pos = await _db.Set<PurchaseOrder>()
            .Where(po => po.InvoiceId == proformaInvoiceId)
            .ToListAsync();

        // Must have at least one PO
        if (pos.Count == 0) return false;

        // All POs must be 'Completed'
        return pos.All(po => po.Status == "Completed");
    }

    /// <summary>Create a Final Invoice from a proforma invoice, pulling in items and track numbers from POs.</summary>
    public async Task<FinalInvoiceResponse> CreateFromProformaAsync(long proformaInvoiceId)
    {
        // Load the proforma invoice with items
        var proforma = await _db.Set<Invoice>()
            .Include(i => i.InvoiceItems)
                .ThenInclude(ii => ii.QuoteItem)
                    .ThenInclude(qi => qi!.ProcumentRecord)
            .Include(i => i.InvoiceItems)
                .ThenInclude(ii => ii.QuoteItem)
                    .ThenInclude(qi => qi!.PartNumber)
            .Include(i => i.Customer)
            .FirstOrDefaultAsync(i => i.Id == proformaInvoiceId);

        if (proforma == null)
            throw new InvalidOperationException("Proforma invoice not found.");

        // Generate invoice number
        var count = await _db.Set<FinalInvoice>().CountAsync();
        var invoiceNumber = $"FINV-{(count + 1).ToString().PadLeft(5, '0')}";

        // Collect POItem track numbers mapped by InvoiceItemId
        var invoiceItemIds = proforma.InvoiceItems.Select(ii => ii.Id).ToList();
        var poItems = await _db.Set<POItem>()
            .Where(pi => pi.InvoiceItemId.HasValue && invoiceItemIds.Contains(pi.InvoiceItemId.Value))
            .Include(pi => pi.TrackNumbers)
            .ToListAsync();

        var trackMap = poItems.ToDictionary(
            pi => pi.InvoiceItemId!.Value,
            pi => new
            {
                TrackNumbers = string.Join(", ", (pi.TrackNumbers ?? new List<POItemTrackNumber>()).Select(t => t.TrackNumber)),
                Carrier = (pi.TrackNumbers ?? new List<POItemTrackNumber>()).FirstOrDefault()?.Carrier ?? ""
            }
        );

        var finalInvoice = new FinalInvoice
        {
            InvoiceNumber = invoiceNumber,
            TotalAmount = proforma.TotalAmount,
            Status = "Draft",
            ProformaInvoiceId = proforma.Id,
            CustomerId = proforma.CustomerId,
            CreatedAt = DateTime.UtcNow,
        };

        _db.Set<FinalInvoice>().Add(finalInvoice);
        await _db.SaveChangesAsync(); // Get the ID

        // Create items from proforma items
        foreach (var ii in proforma.InvoiceItems)
        {
            var quoteItem = ii.QuoteItem;
            var proc = quoteItem?.ProcumentRecord;
            var track = trackMap.ContainsKey(ii.Id) ? trackMap[ii.Id] : null;

            var item = new FinalInvoiceItem
            {
                FinalInvoiceId = finalInvoice.Id,
                PartNumberId = quoteItem?.PartNumberId,
                InvoiceItemId = ii.Id,
                Qty = ii.Qty,
                UnitPrice = ii.UnitPrice,   // Sell price from proforma
                TotalPrice = ii.TotalPrice,
                Condition = quoteItem?.Condition,
                CertName = proc?.CertName,
                TrackNumber = track?.TrackNumbers ?? "",
                Carrier = track?.Carrier ?? "",
            };

            _db.Set<FinalInvoiceItem>().Add(item);
        }

        await _db.SaveChangesAsync();

        return (await GetByIdAsync(finalInvoice.Id))!;
    }

    public async Task<bool> UpdateStatusAsync(long id, string status)
    {
        var fi = await _db.Set<FinalInvoice>().FindAsync(id);
        if (fi == null) return false;

        fi.Status = status;
        if (status == "Paid") fi.PaidDate = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateAsync(long id, UpdateFinalInvoiceRequest request)
    {
        var fi = await _db.Set<FinalInvoice>().FindAsync(id);
        if (fi == null) return false;

        if (request.ShippingCost.HasValue) fi.ShippingCost = request.ShippingCost.Value;
        if (request.ShippingMethod != null) fi.ShippingMethod = request.ShippingMethod;
        if (request.Notes != null) fi.Notes = request.Notes;
        if (request.DueDate.HasValue) fi.DueDate = request.DueDate.Value;

        await _db.SaveChangesAsync();
        return true;
    }

    private static FinalInvoiceResponse MapToResponse(FinalInvoice fi) => new()
    {
        Id = fi.Id,
        InvoiceNumber = fi.InvoiceNumber,
        TotalAmount = fi.TotalAmount,
        Status = fi.Status,
        ShippingMethod = fi.ShippingMethod,
        ShippingCost = fi.ShippingCost,
        Notes = fi.Notes,
        DueDate = fi.DueDate,
        PaidDate = fi.PaidDate,
        CreatedAt = fi.CreatedAt,
        ProformaInvoiceId = fi.ProformaInvoiceId,
        ProformaInvoiceNumber = fi.ProformaInvoice?.InvoiceNumber ?? "",
        CustomerId = fi.CustomerId,
        CustomerName = fi.Customer?.Name ?? "",
        CustomerBillTo = fi.Customer?.BillTo,
        CustomerShipTo = fi.Customer?.ShipTo,
        Items = fi.Items.Select(i => new FinalInvoiceItemResponse
        {
            Id = i.Id,
            Qty = i.Qty,
            UnitPrice = i.UnitPrice,
            TotalPrice = i.TotalPrice,
            Condition = i.Condition,
            CertName = i.CertName,
            TrackNumber = i.TrackNumber,
            Carrier = i.Carrier,
            PartNumberId = i.PartNumberId,
            PartNumberName = i.PartNumber?.Name ?? "",
            Description = i.PartNumber?.Description,
        }).ToList()
    };
}

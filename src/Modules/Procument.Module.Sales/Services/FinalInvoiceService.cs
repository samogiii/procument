using Microsoft.EntityFrameworkCore;
using Procument.Shared.DTOs;
using Procument.Module.Catalog.Entities;
using Procument.Module.Purchasing.Entities;
using Procument.Module.Sales.DTOs;
using Procument.Module.Sales.Entities;

namespace Procument.Module.Sales.Services;

public interface IFinalInvoiceService
{
    Task<PagedResult<FinalInvoiceListItem>> GetAllAsync(PageQuery page);
    Task<FinalInvoiceResponse?> GetByIdAsync(long id);
    Task<FinalInvoiceResponse> CreateFromProformaAsync(long proformaInvoiceId);
    Task<bool> UpdateStatusAsync(long id, string status);
    Task<bool> UpdateAsync(long id, UpdateFinalInvoiceRequest request);
    Task<bool> CanCreateFinalInvoice(long proformaInvoiceId);
    Task<List<EligibleProformaResponse>> GetEligibleProformasAsync();
}

public class FinalInvoiceService : IFinalInvoiceService
{
    private readonly DbContext _db;

    public FinalInvoiceService(DbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// List endpoint — paginated and projected to a flat DTO so the query is a single
    /// SELECT with only the columns the list page actually displays. Avoids the 5-level
    /// Include/ThenInclude chain used by <see cref="GetByIdAsync"/>.
    /// </summary>
    public async Task<PagedResult<FinalInvoiceListItem>> GetAllAsync(PageQuery page)
    {
        var q = _db.Set<FinalInvoice>().AsQueryable();

        if (!string.IsNullOrWhiteSpace(page.Search))
        {
            var s = page.Search.Trim();
            q = q.Where(fi =>
                fi.InvoiceNumber.Contains(s)
             || (fi.Customer != null && fi.Customer.Name.Contains(s))
             || (fi.ProformaInvoice != null && fi.ProformaInvoice.InvoiceNumber.Contains(s)));
        }

        var projected = q
            .OrderByDescending(fi => fi.CreatedAt)
            .Select(fi => new FinalInvoiceListItem
            {
                Id = fi.Id,
                InvoiceNumber = fi.InvoiceNumber,
                TotalAmount = fi.TotalAmount,
                Status = fi.Status,
                CreatedAt = fi.CreatedAt,
                DueDate = fi.DueDate,
                PaidDate = fi.PaidDate,
                ProformaInvoiceId = fi.ProformaInvoiceId,
                ProformaInvoiceNumber = fi.ProformaInvoice != null ? fi.ProformaInvoice.InvoiceNumber : "",
                CustomerId = fi.CustomerId,
                CustomerName = fi.Customer != null ? fi.Customer.Name : "",
                CustomerCode = fi.Customer != null ? fi.Customer.CustomerCode : null,
                ItemCount = fi.Items.Count()
            });

        return await projected.ToPagedResultAsync(page);
    }

    public async Task<FinalInvoiceResponse?> GetByIdAsync(long id)
    {
        var fi = await _db.Set<FinalInvoice>()
            .Include(f => f.Customer)
            .Include(f => f.ProformaInvoice)
            .Include(f => f.Items).ThenInclude(i => i.PartNumber)
            .Include(f => f.Items).ThenInclude(i => i.InvoiceItem)
                .ThenInclude(ii => ii!.QuoteItem)
                    .ThenInclude(qi => qi!.RFQItem)
                        .ThenInclude(ri => ri!.RFQ)
                            .ThenInclude(r => r!.RFQItems)
            .FirstOrDefaultAsync(f => f.Id == id);

        return fi == null ? null : MapToResponse(fi);
    }

    public async Task<List<EligibleProformaResponse>> GetEligibleProformasAsync()
    {
        var finalInvoiceProformaIds = await _db.Set<FinalInvoice>().Select(f => f.ProformaInvoiceId).ToListAsync();

        var eligibleProformas = await _db.Set<Invoice>()
            .Include(i => i.Customer)
            .Where(i => !finalInvoiceProformaIds.Contains(i.Id))
            .Where(i => _db.Set<PurchaseOrder>().Any(po => po.InvoiceId == i.Id && po.Status == "Completed"))
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();

        return eligibleProformas.Select(i => new EligibleProformaResponse
        {
            Id = i.Id,
            InvoiceNumber = i.InvoiceNumber,
            CustomerName = i.Customer?.Name ?? "",
            CustomerCode = i.Customer?.CustomerCode,
            TotalAmount = i.TotalAmount
        }).ToList();
    }

    /// <summary>Check if proforma has at least one Completed PO and no existing final invoice.</summary>
    public async Task<bool> CanCreateFinalInvoice(long proformaInvoiceId)
    {
        var exists = await _db.Set<FinalInvoice>()
            .AnyAsync(fi => fi.ProformaInvoiceId == proformaInvoiceId);
        if (exists) return false;

        var hasCompletedPo = await _db.Set<PurchaseOrder>()
            .AnyAsync(po => po.InvoiceId == proformaInvoiceId && po.Status == "Completed");

        return hasCompletedPo;
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
        //var count = await _db.Set<FinalInvoice>().CountAsync();
        //var invoiceNumber = $"INV-{(count + 1).ToString().PadLeft(5, '0')}";

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
            InvoiceNumber = "",
            TotalAmount = proforma.TotalAmount,
            Status = "Draft",
            ProformaInvoiceId = proforma.Id,
            CustomerId = proforma.CustomerId,
            CreatedAt = DateTime.UtcNow,
        };

        _db.Set<FinalInvoice>().Add(finalInvoice);
        await _db.SaveChangesAsync(); // Get the ID


        // Set PO number using the actual Id
        finalInvoice.InvoiceNumber = $"INV-{finalInvoice.Id}";
        await _db.SaveChangesAsync();

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
                Discount = ii.Discount,     // Copy discount from proforma item
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

    private static FinalInvoiceResponse MapToResponse(FinalInvoice fi)
    {
        var rfqItemRank = fi.Items
            .Select(i => i.InvoiceItem?.QuoteItem?.RFQItem?.RFQ)
            .Where(r => r != null)
            .SelectMany(r => r!.RFQItems)
            .DistinctBy(ri => ri.Id)
            .OrderBy(ri => ri.Id)
            .Select((ri, idx) => new { ri.Id, rank = idx + 1 })
            .ToDictionary(x => x.Id, x => x.rank);

        return new()
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
            CustomerPONumber = fi.ProformaInvoice?.CustomerPONumber,
            CustomerId = fi.CustomerId,
            CustomerName = fi.Customer?.Name ?? "",
            CustomerCode = fi.Customer?.CustomerCode,
            CustomerContactPerson = fi.Customer?.ContactPerson,
            CustomerBillTo = fi.Customer?.BillTo,
            CustomerBillToEmail = fi.Customer?.Email,
            CustomerBillToPhone = fi.Customer?.Phone,
            CustomerTermsAndConditions = fi.Customer?.TermsAndConditions,
            CustomerCurrencyType = fi.Customer?.CurrencyType,
            //CustomerBillToContactPerson = fi.Customer?.ContactPerson,
            CustomerShipTo = fi.Customer?.ShipTo,
            CustomerShipToContactPerson = fi.Customer?.ContactPerson,
            CustomerShipToEmail = fi.Customer?.Email,
            CustomerShipToPhone = fi.Customer?.Phone,
            CustomerShipToAccount = fi.Customer?.ShippingAccount,
            Items = fi.Items.Select(i => new FinalInvoiceItemResponse
            {
                Id = i.Id,
                Qty = i.Qty,
                UnitPrice = i.UnitPrice,
                TotalPrice = i.TotalPrice,
                Discount = i.Discount,
                Condition = i.Condition,
                CertName = i.CertName,
                TrackNumber = i.TrackNumber,
                Carrier = i.Carrier,
                PartNumberId = i.PartNumberId,
                RFQReference = i.InvoiceItem?.QuoteItem?.RFQItemId.HasValue == true &&
                               rfqItemRank.TryGetValue(i.InvoiceItem.QuoteItem.RFQItemId!.Value, out var rank)
                               ? rank.ToString() : null,
                PartNumberName = i.PartNumber?.Name ?? "",
                Description = i.PartNumber?.Description,
            }).ToList()
        };
    }
}

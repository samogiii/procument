using Microsoft.EntityFrameworkCore;
using Procument.Module.Catalog.Entities;
using Procument.Module.Purchasing.DTOs;
using Procument.Module.Purchasing.Entities;

namespace Procument.Module.Purchasing.Services;

public interface IILSProformaService
{
    Task<List<ILSProformaResponse>> GetAllAsync();
    Task<ILSProformaResponse?> GetByIdAsync(long id);
    Task<ILSProformaResponse> CreateAsync(CreateILSProformaRequest request);
    Task<ILSProformaResponse> UpdateStatusAsync(long id, string status);
    Task<bool> DeleteAsync(long id);
}

public class ILSProformaService : IILSProformaService
{
    private readonly DbContext _db;

    public ILSProformaService(DbContext db) => _db = db;

    public async Task<List<ILSProformaResponse>> GetAllAsync()
    {
        var pis = await _db.Set<ILSProformaInvoice>()
            .Include(p => p.ILSCustomer)
            .Include(p => p.Items).ThenInclude(i => i.PartNumber)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return pis.Select(MapToResponse).ToList();
    }

    public async Task<ILSProformaResponse?> GetByIdAsync(long id)
    {
        var pi = await _db.Set<ILSProformaInvoice>()
            .Include(p => p.ILSCustomer)
            .Include(p => p.Items).ThenInclude(i => i.PartNumber)
            .FirstOrDefaultAsync(p => p.Id == id);

        return pi == null ? null : MapToResponse(pi);
    }

    public async Task<ILSProformaResponse> CreateAsync(CreateILSProformaRequest request)
    {
        var pi = new ILSProformaInvoice
        {
            PINumber = "DRAFT",
            Status = "Open",
            ILSCustomerId = request.ILSCustomerId,
            BillTo = request.BillTo,
            ShipTo = request.ShipTo,
            Subject = request.Subject,
            CustomerPONumber = request.CustomerPONumber,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow,
        };

        _db.Set<ILSProformaInvoice>().Add(pi);
        await _db.SaveChangesAsync();

        pi.PINumber = $"ILSPI-{pi.Id}";

        foreach (var item in request.Items)
        {
            var partNumberId = await ResolvePartNumberIdAsync(item.PartNumberId, item.PartNumberName);
            _db.Set<ILSProformaInvoiceItem>().Add(new ILSProformaInvoiceItem
            {
                ILSProformaInvoiceId = pi.Id,
                PartNumberId = partNumberId,
                AltPartNumber = item.AltPartNumber,
                Condition = item.Condition,
                CertName = item.CertName,
                Qty = item.Qty,
                SellPrice = item.SellPrice,
                TotalPrice = item.TotalPrice,
                LeadTime = item.LeadTime,
                Notes = item.Notes,
                ILSItemSerialId = item.ILSItemSerialId,
                SerialNumber = item.SerialNumber,
                ILSItemId = item.ILSItemId,
                SourceQuoteId = item.SourceQuoteId,
                SourceQuoteItemId = item.SourceQuoteItemId,
            });
        }

        pi.TotalAmount = request.Items.Sum(i => i.TotalPrice);

        // Mark the source quotes as Invoiced
        var quoteIds = request.SourceQuoteIds.Distinct().ToList();
        if (quoteIds.Count > 0)
        {
            var quotes = await _db.Set<ILSQuote>().Where(q => quoteIds.Contains(q.Id)).ToListAsync();
            foreach (var q in quotes) q.Status = "Invoiced";
        }

        await _db.SaveChangesAsync();

        return (await GetByIdAsync(pi.Id))!;
    }

    public async Task<ILSProformaResponse> UpdateStatusAsync(long id, string status)
    {
        var pi = await _db.Set<ILSProformaInvoice>().FindAsync(id)
            ?? throw new KeyNotFoundException($"ILS Proforma Invoice {id} not found.");
        pi.Status = status;
        await _db.SaveChangesAsync();
        return (await GetByIdAsync(id))!;
    }

    public async Task<bool> DeleteAsync(long id)
    {
        var pi = await _db.Set<ILSProformaInvoice>()
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (pi == null) return false;
        _db.Set<ILSProformaInvoiceItem>().RemoveRange(pi.Items);
        _db.Set<ILSProformaInvoice>().Remove(pi);
        await _db.SaveChangesAsync();
        return true;
    }

    private async Task<long> ResolvePartNumberIdAsync(long partNumberId, string? partNumberName)
    {
        if (partNumberId > 0) return partNumberId;

        if (string.IsNullOrWhiteSpace(partNumberName))
            throw new ArgumentException("PartNumberId or PartNumberName is required");

        var trimmed = partNumberName.Trim();
        var existing = await _db.Set<PartNumber>().FirstOrDefaultAsync(p => p.Name == trimmed);
        if (existing != null) return existing.Id;

        var newPn = new PartNumber { Name = trimmed, CreatedAt = DateTime.UtcNow };
        _db.Set<PartNumber>().Add(newPn);
        await _db.SaveChangesAsync();
        return newPn.Id;
    }

    private static ILSProformaResponse MapToResponse(ILSProformaInvoice p) => new()
    {
        Id = p.Id,
        PINumber = p.PINumber,
        Status = p.Status,
        ILSCustomerId = p.ILSCustomerId,
        ILSCustomerName = p.ILSCustomer.Name,
        ILSCustomerCode = p.ILSCustomer.CustomerCode,
        BillTo = p.BillTo ?? p.ILSCustomer.BillTo,
        ShipTo = p.ShipTo ?? p.ILSCustomer.ShipTo,
        Subject = p.Subject,
        CustomerPONumber = p.CustomerPONumber,
        Notes = p.Notes,
        CustomerTermsAndConditions = p.ILSCustomer.TermsAndConditions,
        TotalAmount = p.TotalAmount,
        CreatedAt = p.CreatedAt,
        Items = p.Items.Select(i => new ILSProformaItemResponse
        {
            Id = i.Id,
            ILSProformaInvoiceId = i.ILSProformaInvoiceId,
            PartNumberId = i.PartNumberId,
            PartNumberName = i.PartNumber.Name,
            AltPartNumber = i.AltPartNumber,
            Condition = i.Condition,
            CertName = i.CertName,
            Qty = i.Qty,
            SellPrice = i.SellPrice,
            TotalPrice = i.TotalPrice,
            LeadTime = i.LeadTime,
            Notes = i.Notes,
            ILSItemSerialId = i.ILSItemSerialId,
            SerialNumber = i.SerialNumber,
            ILSItemId = i.ILSItemId,
            SourceQuoteId = i.SourceQuoteId,
            SourceQuoteItemId = i.SourceQuoteItemId,
        }).ToList()
    };
}

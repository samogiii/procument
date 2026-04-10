using Microsoft.EntityFrameworkCore;
using Procument.Module.Catalog.Entities;
using Procument.Module.Purchasing.DTOs;
using Procument.Module.Purchasing.Entities;

namespace Procument.Module.Purchasing.Services;

public interface IILSQuoteService
{
    Task<List<ILSCustomerResponse>> GetAllCustomersAsync();
    Task<ILSCustomerResponse> SaveCustomerAsync(long? id, ILSCustomerDto dto);
    Task<bool> DeleteCustomerAsync(long id);

    Task<List<ILSQuoteResponse>> GetAllQuotesAsync();
    Task<ILSQuoteResponse?> GetQuoteByIdAsync(long id);
    Task<ILSQuoteResponse> CreateQuoteAsync(CreateILSQuoteRequest request);
    Task<ILSQuoteResponse> UpdateQuoteAsync(long id, CreateILSQuoteRequest request);
    Task<ILSQuoteResponse> UpdateStatusAsync(long id, string status);
    Task<bool> DeleteQuoteAsync(long id);
}

public class ILSQuoteService : IILSQuoteService
{
    private readonly DbContext _db;

    public ILSQuoteService(DbContext db) => _db = db;

    // ─── ILS Customers ───

    public async Task<List<ILSCustomerResponse>> GetAllCustomersAsync()
    {
        return await _db.Set<ILSCustomer>()
            .OrderBy(c => c.Name)
            .Select(c => new ILSCustomerResponse
            {
                Id = c.Id,
                Name = c.Name,
                CustomerCode = c.CustomerCode,
                Email = c.Email,
                Phone = c.Phone,
                ContactPerson = c.ContactPerson,
                Address = c.Address,
                Description = c.Description,
                IsActive = c.IsActive,
                CreatedAt = c.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<ILSCustomerResponse> SaveCustomerAsync(long? id, ILSCustomerDto dto)
    {
        ILSCustomer customer;

        if (id.HasValue && id > 0)
        {
            customer = await _db.Set<ILSCustomer>().FindAsync(id.Value)
                ?? throw new KeyNotFoundException($"ILS Customer {id} not found.");
            customer.Name = dto.Name;
            customer.CustomerCode = dto.CustomerCode;
            customer.Email = dto.Email;
            customer.Phone = dto.Phone;
            customer.ContactPerson = dto.ContactPerson;
            customer.Address = dto.Address;
            customer.Description = dto.Description;
            customer.ModifyAt = DateTime.UtcNow;
        }
        else
        {
            customer = new ILSCustomer
            {
                Name = dto.Name,
                CustomerCode = dto.CustomerCode,
                Email = dto.Email,
                Phone = dto.Phone,
                ContactPerson = dto.ContactPerson,
                Address = dto.Address,
                Description = dto.Description,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
            _db.Set<ILSCustomer>().Add(customer);
        }

        await _db.SaveChangesAsync();

        return new ILSCustomerResponse
        {
            Id = customer.Id,
            Name = customer.Name,
            CustomerCode = customer.CustomerCode,
            Email = customer.Email,
            Phone = customer.Phone,
            ContactPerson = customer.ContactPerson,
            Address = customer.Address,
            Description = customer.Description,
            IsActive = customer.IsActive,
            CreatedAt = customer.CreatedAt
        };
    }

    public async Task<bool> DeleteCustomerAsync(long id)
    {
        var customer = await _db.Set<ILSCustomer>().FindAsync(id);
        if (customer == null) return false;
        _db.Set<ILSCustomer>().Remove(customer);
        await _db.SaveChangesAsync();
        return true;
    }

    // ─── ILS Quotes ───

    public async Task<List<ILSQuoteResponse>> GetAllQuotesAsync()
    {
        var quotes = await _db.Set<ILSQuote>()
            .Include(q => q.ILSCustomer)
            .Include(q => q.Items).ThenInclude(i => i.PartNumber)
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync();

        return quotes.Select(MapToResponse).ToList();
    }

    public async Task<ILSQuoteResponse?> GetQuoteByIdAsync(long id)
    {
        var quote = await _db.Set<ILSQuote>()
            .Include(q => q.ILSCustomer)
            .Include(q => q.Items).ThenInclude(i => i.PartNumber)
            .FirstOrDefaultAsync(q => q.Id == id);

        return quote == null ? null : MapToResponse(quote);
    }

    public async Task<ILSQuoteResponse> CreateQuoteAsync(CreateILSQuoteRequest request)
    {
        var quote = new ILSQuote
        {
            QuoteNumber = "DRAFT",
            Status = "Draft",
            ILSCustomerId = request.ILSCustomerId,
            RfqReference = request.RfqReference,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow,
        };

        _db.Set<ILSQuote>().Add(quote);
        await _db.SaveChangesAsync();

        quote.QuoteNumber = $"ILSQ-{quote.Id}";

        foreach (var item in request.Items)
        {
            var partNumberId = await ResolvePartNumberIdAsync(item.PartNumberId, item.PartNumberName);
            var quoteItem = new ILSQuoteItem
            {
                ILSQuoteId = quote.Id,
                PartNumberId = partNumberId,
                AltPartNumber = item.AltPartNumber,
                Condition = item.Condition,
                CertName = item.CertName,
                Qty = item.Qty,
                SellPrice = item.SellPrice,
                TotalPrice = item.TotalPrice,
                LeadTime = item.LeadTime,
                Notes = item.Notes,
                ILSItemId = item.ILSItemId
            };
            _db.Set<ILSQuoteItem>().Add(quoteItem);
        }

        quote.TotalAmount = request.Items.Sum(i => i.TotalPrice);
        await _db.SaveChangesAsync();

        return (await GetQuoteByIdAsync(quote.Id))!;
    }

    public async Task<ILSQuoteResponse> UpdateQuoteAsync(long id, CreateILSQuoteRequest request)
    {
        var quote = await _db.Set<ILSQuote>()
            .Include(q => q.Items)
            .FirstOrDefaultAsync(q => q.Id == id)
            ?? throw new KeyNotFoundException($"ILS Quote {id} not found.");

        quote.ILSCustomerId = request.ILSCustomerId;
        quote.RfqReference = request.RfqReference;
        quote.Notes = request.Notes;
        quote.Status = "Draft";

        // Remove old items
        _db.Set<ILSQuoteItem>().RemoveRange(quote.Items);

        // Add new items
        var newItems = new List<ILSQuoteItem>();
        foreach (var item in request.Items)
        {
            var partNumberId = await ResolvePartNumberIdAsync(item.PartNumberId, item.PartNumberName);
            newItems.Add(new ILSQuoteItem
            {
                ILSQuoteId = quote.Id,
                PartNumberId = partNumberId,
                AltPartNumber = item.AltPartNumber,
                Condition = item.Condition,
                CertName = item.CertName,
                Qty = item.Qty,
                SellPrice = item.SellPrice,
                TotalPrice = item.TotalPrice,
                LeadTime = item.LeadTime,
                Notes = item.Notes,
                ILSItemId = item.ILSItemId
            });
        }

        _db.Set<ILSQuoteItem>().AddRange(newItems);
        quote.TotalAmount = request.Items.Sum(i => i.TotalPrice);
        await _db.SaveChangesAsync();

        return (await GetQuoteByIdAsync(quote.Id))!;
    }

    public async Task<ILSQuoteResponse> UpdateStatusAsync(long id, string status)
    {
        var quote = await _db.Set<ILSQuote>().FindAsync(id)
            ?? throw new KeyNotFoundException($"ILS Quote {id} not found.");

        quote.Status = status;
        await _db.SaveChangesAsync();

        return (await GetQuoteByIdAsync(id))!;
    }

    public async Task<bool> DeleteQuoteAsync(long id)
    {
        var quote = await _db.Set<ILSQuote>()
            .Include(q => q.Items)
            .FirstOrDefaultAsync(q => q.Id == id);
        if (quote == null) return false;
        _db.Set<ILSQuoteItem>().RemoveRange(quote.Items);
        _db.Set<ILSQuote>().Remove(quote);
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

    private static ILSQuoteResponse MapToResponse(ILSQuote q) => new()
    {
        Id = q.Id,
        QuoteNumber = q.QuoteNumber,
        Status = q.Status,
        ILSCustomerId = q.ILSCustomerId,
        ILSCustomerName = q.ILSCustomer.Name,
        ILSCustomerCode = q.ILSCustomer.CustomerCode,
        RfqReference = q.RfqReference,
        TotalAmount = q.TotalAmount,
        Notes = q.Notes,
        CreatedAt = q.CreatedAt,
        Items = q.Items.Select(i => new ILSQuoteItemResponse
        {
            Id = i.Id,
            ILSQuoteId = i.ILSQuoteId,
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
            ILSItemId = i.ILSItemId
        }).ToList()
    };
}

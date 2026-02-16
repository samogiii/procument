using Microsoft.EntityFrameworkCore;
using Procument.Module.Catalog.Entities;
using Procument.Module.Identity.Entities;
using Procument.Module.RFQ.DTOs;
using Procument.Module.RFQ.Entities;

namespace Procument.Module.RFQ.Services;

public interface IRFQService
{
    Task<RFQResponse> CreateAsync(CreateRFQRequest request);
    Task<RFQResponse?> GetByIdAsync(long id);
    Task<List<RFQResponse>> GetAllAsync();
    Task<RFQItemResponse?> UpdateItemAsync(long itemId, UpdateRFQItemRequest request);
}

public class RFQService : IRFQService
{
    private readonly DbContext _db;

    public RFQService(DbContext db)
    {
        _db = db;
    }

    public async Task<RFQResponse> CreateAsync(CreateRFQRequest request)
    {
        // ── 1. Resolve or create Customer ──
        var customer = await _db.Set<Customer>()
            .FirstOrDefaultAsync(c => c.Name == request.CustomerName);

        if (customer == null)
        {
            customer = new Customer
            {
                Name = request.CustomerName,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
            _db.Set<Customer>().Add(customer);
            await _db.SaveChangesAsync(); // get the ID
        }

        // ── 2. Resolve or create PartNumbers ──
        var partNumberEntities = new List<PartNumber>();

        foreach (var pnName in request.PartNumbers)
        {
            var trimmed = pnName.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            var existing = await _db.Set<PartNumber>()
                .FirstOrDefaultAsync(p => p.Name == trimmed);

            if (existing != null)
            {
                partNumberEntities.Add(existing);
            }
            else
            {
                var newPn = new PartNumber
                {
                    Name = trimmed,
                    CreatedAt = DateTime.UtcNow
                };
                _db.Set<PartNumber>().Add(newPn);
                await _db.SaveChangesAsync(); // get the ID
                partNumberEntities.Add(newPn);
            }
        }

        // ── 3. Create RFQ Header ──
        var rfq = new RFQHeader
        {
            Name = request.Name,
            LeadTime = request.LeadTime,
            CustomerId = customer.Id,
            UserId = request.UserId,
            CreatedAt = DateTime.UtcNow
        };

        _db.Set<RFQHeader>().Add(rfq);
        await _db.SaveChangesAsync();

        // ── 4. Create RFQ Items (one per part number) ──
        foreach (var pn in partNumberEntities)
        {
            var item = new RFQItem
            {
                RFQId = rfq.Id,
                PartNumberId = pn.Id,
                Qty = 1
            };
            _db.Set<RFQItem>().Add(item);
        }

        await _db.SaveChangesAsync();

        return await GetByIdAsync(rfq.Id) ?? throw new Exception("Failed to load created RFQ.");
    }

    public async Task<RFQResponse?> GetByIdAsync(long id)
    {
        var rfq = await _db.Set<RFQHeader>()
            .Include(r => r.Customer)
            .Include(r => r.User)
            .Include(r => r.RFQItems)
                .ThenInclude(i => i.PartNumber)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (rfq == null) return null;

        return MapToResponse(rfq);
    }

    public async Task<List<RFQResponse>> GetAllAsync()
    {
        var rfqs = await _db.Set<RFQHeader>()
            .Include(r => r.Customer)
            .Include(r => r.User)
            .Include(r => r.RFQItems)
                .ThenInclude(i => i.PartNumber)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return rfqs.Select(MapToResponse).ToList();
    }

    // ──── Update Item ────

    public async Task<RFQItemResponse?> UpdateItemAsync(long itemId, UpdateRFQItemRequest request)
    {
        var item = await _db.Set<RFQItem>()
            .Include(i => i.PartNumber)
            .FirstOrDefaultAsync(i => i.Id == itemId);

        if (item == null) return null;

        item.Alt = request.Alt;
        item.Qty = request.Qty;
        item.Condition = request.Condition;

        await _db.SaveChangesAsync();

        return new RFQItemResponse
        {
            Id = item.Id,
            PartNumberName = item.PartNumber.Name,
            PartNumberId = item.PartNumberId,
            Description = item.PartNumber.Description,
            Alt = item.Alt,
            Qty = item.Qty,
            
            Condition = item.Condition
        };
    }

    // ──── Mapping ────

    private static RFQResponse MapToResponse(RFQHeader rfq) => new()
    {
        Id = rfq.Id,
        Name = rfq.Name,
        LeadTime = rfq.LeadTime,
        CreatedAt = rfq.CreatedAt,
        CustomerName = rfq.Customer.Name,
        CustomerId = rfq.CustomerId,
        UserName = rfq.User?.Name,
        UserId = rfq.UserId,
        Items = rfq.RFQItems.Select(i => new RFQItemResponse
        {
            Id = i.Id,
            PartNumberName = i.PartNumber.Name,
            PartNumberId = i.PartNumberId,
            Description = i.PartNumber.Description,
            Alt = i.Alt,
            Qty = i.Qty,
            Condition = i.Condition
        }).ToList()
    };
}

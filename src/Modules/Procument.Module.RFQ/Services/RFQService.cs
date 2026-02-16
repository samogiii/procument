using Microsoft.EntityFrameworkCore;

using Procument.Module.Catalog.Entities;
using Procument.Module.Identity.DTOs;
using Procument.Module.Identity.Entities;
using Procument.Module.Identity.Services;
using Procument.Module.RFQ.DTOs;
using Procument.Module.RFQ.Entities;

namespace Procument.Module.RFQ.Services;

public interface IRFQService
{
    Task<RFQResponse> CreateAsync(CreateRFQRequest request);
    Task<RFQResponse?> GetByIdAsync(long id, long userId, bool isAdmin);
    Task<List<RFQResponse>> GetAllAsync(long userId, bool isAdmin);
    Task<RFQItemResponse?> UpdateItemAsync(long itemId, UpdateRFQItemRequest request);
}

public class RFQService : IRFQService
{
    private readonly DbContext _db;
    private readonly IPermissionService _permissionService;

    public RFQService(DbContext db, IPermissionService permissionService)
    {
        _db = db;
        _permissionService = permissionService;
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
            CreatedAt = request.CreatedAt
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

        return await GetByIdAsync(rfq.Id, request.UserId, true) ?? throw new Exception("Failed to load created RFQ.");
    }

    public async Task<RFQResponse?> GetByIdAsync(long id, long userId, bool isAdmin)
    {
        var rfq = await _db.Set<RFQHeader>()
            .Include(r => r.Customer)
            .Include(r => r.User)
            .Include(r => r.RFQItems)
                .ThenInclude(ri => ri.PartNumber)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (rfq == null) return null;

        // Permission Check
        if (!isAdmin && rfq.UserId != userId)
        {
            var hasPermission = await _db.Set<EntityPermission>()
                .AnyAsync(p => p.UserId == userId && p.EntityName == "RFQ" && p.EntityId == id.ToString());

            if (!hasPermission) return null; // Or throw UnauthorizedAccessException? returning null behaves like 404 which is safer
        }

        var response = MapToResponse(rfq);

        // Populate Permissions
        var permissions = await _permissionService.GetPermissionsForEntityAsync("RFQ", id.ToString());

        response.Checkers = permissions
            .Where(p => p.Permission == "Checker")
            .Select(p => new UserResponse
            {
                Id = p.User.Id,
                Name = p.User.Name,
                Email = p.User.Email,
                Role = p.User.Role,
                IsActive = p.User.IsActive,
                CreatedAt = p.User.CreatedAt
            })
            .ToList();

        response.Procurers = permissions
            .Where(p => p.Permission == "Procurer")
            .Select(p => new UserResponse
            {
                Id = p.User.Id,
                Name = p.User.Name,
                Email = p.User.Email,
                Role = p.User.Role,
                IsActive = p.User.IsActive,
                CreatedAt = p.User.CreatedAt
            })
            .ToList();

        return response;
    }

    public async Task<List<RFQResponse>> GetAllAsync(long userId, bool isAdmin)
    {
        IQueryable<RFQHeader> query = _db.Set<RFQHeader>()
            .Include(r => r.Customer)
            .Include(r => r.User)
            .Include(r => r.RFQItems)
                .ThenInclude(i => i.PartNumber);

        if (!isAdmin)
        {
            // Filter: Owner OR HasPermission
            // Note: EF Core translation for Any with local variables might differ based on version, but 10.0 should be fine.
            // Using a subquery approach for permissions join might be more efficient but this is cleaner.
            // Since EntityId is string, we need to convert r.Id to string or use client eval? 
            // EF Core might not translate `r.Id.ToString()`. 
            // Better to perform a join or separate query for IDs.

            // Let's get list of RFQ IDs user has permission to first.
            var permittedRfqIdsStr = await _db.Set<EntityPermission>()
                .Where(p => p.UserId == userId && p.EntityName == "RFQ")
                .Select(p => p.EntityId)
                .ToListAsync();

            var permittedRfqIds = permittedRfqIdsStr
                .Select(id => long.TryParse(id, out var l) ? l : -1)
                .ToList();

            query = query.Where(r => r.UserId == userId || permittedRfqIds.Contains(r.Id));
        }

        var rfqs = await query
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

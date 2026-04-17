using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

using Procument.Module.Catalog.Entities;
using Procument.Module.Identity.DTOs;
using Procument.Module.Identity.Entities;
using Procument.Module.Identity.Services;
using Procument.Module.RFQ.DTOs;
using Procument.Module.RFQ.Entities;
using Procument.Shared.DTOs;
using Procument.Shared.Entities;

namespace Procument.Module.RFQ.Services;

public interface IRFQService
{
    Task<RFQResponse> CreateAsync(CreateRFQRequest request);
    Task<RFQResponse?> GetByIdAsync(long id, long userId, bool isAdmin);
    Task<PagedResult<RFQListItem>> GetAllAsync(long userId, bool isAdmin, PageQuery page, string[]? statuses = null, string? pnSearch = null, long[]? userIds = null, string? customerSearch = null);
    Task<RFQItemResponse?> UpdateItemAsync(long itemId, UpdateRFQItemRequest request);
    Task<RFQItemResponse?> AddItemAsync(long rfqId, AddRFQItemRequest request);
    Task<bool> UpdateExTypeAsync(long rfqId, int? exType);
    Task<bool> UpdateStatusAsync(long rfqId, string status, string? noQuoteReason = null, string? rejectionNote = null);
    Task<bool> UpdateNotesAsync(long rfqId, string? notes);
    Task<bool> UpdateNameAsync(long rfqId, string name);
    Task<bool> UpdateLeadTimeAsync(long rfqId, DateTime leadTime);
    Task<string> DeleteRFQItem(long id);
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
            ReceivedDate = request.ReceivedDate,
            CreatedAt = DateTime.UtcNow,
            Notes = request.Notes,
            ExType = request.ExType,
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
                Qty = 1,
                
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
                    .ThenInclude(pn => pn.Alternatives)
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

        // Populate IsUnread
        var readRecord = await _db.Set<RFQUserRead>()
            .FirstOrDefaultAsync(r => r.RFQId == id && r.UserId == userId);
        response.IsUnread = readRecord != null && !readRecord.IsRead;

        // Populate Permissions
        var permissions = await _permissionService.GetPermissionsForEntityAsync("RFQ", id.ToString());

        response.Views = permissions
            .Where(p => p.Permission == "View")
            .Select(p => new UserResponse
            {
                Id = p.User.Id,
                Name = p.User.Name,
                Email = p.User.Email,
                Role = p.User.Role,
                IsActive = p.User.IsActive,
                CreatedAt = p.User.CreatedAt,
                AssignedAt = p.CreatedAt
            })
            .ToList();

        response.Edits = permissions
            .Where(p => p.Permission == "Edit")
            .Select(p => new UserResponse
            {
                Id = p.User.Id,
                Name = p.User.Name,
                Email = p.User.Email,
                Role = p.User.Role,
                IsActive = p.User.IsActive,
                CreatedAt = p.User.CreatedAt,
                AssignedAt = p.CreatedAt
            })
            .ToList();

        return response;
    }

    public async Task<PagedResult<RFQListItem>> GetAllAsync(long userId, bool isAdmin, PageQuery page, string[]? statuses = null, string? pnSearch = null, long[]? userIds = null, string? customerSearch = null)
    {
        IQueryable<RFQHeader> query = _db.Set<RFQHeader>();

        // ── 1. Permission filter ──
        if (!isAdmin)
        {
            var permittedIdsStr = await _db.Set<EntityPermission>()
                .Where(p => p.UserId == userId && p.EntityName == "RFQ")
                .Select(p => p.EntityId)
                .ToListAsync();

            var permittedIds = permittedIdsStr
                .Select(id => long.TryParse(id, out var l) ? l : -1L)
                .Where(l => l > 0)
                .ToList();

            query = query.Where(r => r.UserId == userId || permittedIds.Contains(r.Id));
        }

        // ── 2. Client-requested filters ──
        if (!string.IsNullOrWhiteSpace(page.Search))
        {
            var s = page.Search.Trim();
            query = query.Where(r => r.Name.Contains(s) || r.Customer.Name.Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(pnSearch))
        {
            var pn = pnSearch.Trim();
            query = query.Where(r => r.RFQItems.Any(i => i.PartNumber.Name.Contains(pn)));
        }

        if (statuses != null && statuses.Length > 0)
            query = query.Where(r => statuses.Contains(r.Status));

        if (userIds != null && userIds.Length > 0)
        {
            var userIdList = userIds.ToList();
            query = query.Where(r => r.UserId != null && userIdList.Contains(r.UserId.Value));
        }

        if (!string.IsNullOrWhiteSpace(customerSearch))
        {
            var cs = customerSearch.Trim();
            query = query.Where(r => r.Customer.Name.Contains(cs) || (r.Customer.CustomerCode != null && r.Customer.CustomerCode.Contains(cs)));
        }

        // ── 3. Sort + paginate (flat projection — no Alternatives loaded) ──
        query = query.OrderByDescending(r => r.Id);

        var total = await query.CountAsync();
        var rows = await query
            .Skip((page.Page - 1) * page.PageSize)
            .Take(page.PageSize)
            .Select(r => new
            {
                r.Id,
                r.Name,
                r.Status,
                r.LeadTime,
                r.ReceivedDate,
                r.CreatedAt,
                r.NoQuoteReason,
                r.RejectionNote,
                r.ExType,
                r.UserId,
                UserName = r.User != null ? r.User.Name : null,
                CustomerName = r.Customer != null ? r.Customer.Name : "",
                CustomerCode = r.Customer != null ? r.Customer.CustomerCode : null,
                CustomerId = r.CustomerId,
                ItemCount = r.RFQItems.Count()
            })
            .ToListAsync();

        // ── 4. Batch-load permissions and unread status for this page only ──
        var rfqIds = rows.Select(r => r.Id).ToList();
        var rfqIdStrings = rfqIds.Select(id => id.ToString()).ToList();

        var permissions = await _db.Set<EntityPermission>()
            .Include(p => p.User)
            .Where(p => p.EntityName == "RFQ" && rfqIdStrings.Contains(p.EntityId))
            .ToListAsync();

        var unreadIds = await _db.Set<RFQUserRead>()
            .Where(r => r.UserId == userId && rfqIds.Contains(r.RFQId) && !r.IsRead)
            .Select(r => r.RFQId)
            .ToListAsync();
        var unreadSet = new HashSet<long>(unreadIds);

        // ── 5. Build response ──
        var items = rows.Select(r =>
        {
            var perms = permissions.Where(p => p.EntityId == r.Id.ToString()).ToList();
            return new RFQListItem
            {
                Id = r.Id,
                Name = r.Name,
                Status = r.Status,
                LeadTime = r.LeadTime,
                ReceivedDate = r.ReceivedDate,
                CreatedAt = r.CreatedAt,
                NoQuoteReason = r.NoQuoteReason,
                RejectionNote = r.RejectionNote,
                ExType = r.ExType,
                UserId = r.UserId ?? 0,
                UserName = r.UserName,
                CustomerName = r.CustomerName,
                CustomerCode = r.CustomerCode,
                CustomerId = r.CustomerId,
                ItemCount = r.ItemCount,
                IsUnread = unreadSet.Contains(r.Id),
                Views = perms
                    .Where(p => p.Permission == "View")
                    .Select(p => new RFQListUserRef { Id = p.User.Id, Name = p.User.Name })
                    .ToList(),
                Edits = perms
                    .Where(p => p.Permission == "Edit")
                    .Select(p => new RFQListUserRef { Id = p.User.Id, Name = p.User.Name })
                    .ToList()
            };
        }).ToList();

        return new PagedResult<RFQListItem>
        {
            Items = items,
            TotalCount = total,
            Page = page.Page,
            PageSize = page.PageSize
        };
    }

    // ──── Update Item ────

    public async Task<RFQItemResponse?> UpdateItemAsync(long itemId, UpdateRFQItemRequest request)
    {
        var item = await _db.Set<RFQItem>()
            .Include(i => i.PartNumber)
                .ThenInclude(pn => pn.Alternatives)
            .FirstOrDefaultAsync(i => i.Id == itemId);

        if (item == null) return null;

        item.Alt = request.Alt;
        item.Qty = request.Qty;
        item.Priority = request.Priority;
        item.Note = request.Note;
        item.Condition = request.Condition;
        item.Unit = request.Unit;
        item.IsHighlighted = request.IsHighlighted;

        await _db.SaveChangesAsync();

        return new RFQItemResponse
        {
            Id = item.Id,
            PartNumberName = item.PartNumber.Name,
            PartNumberId = item.PartNumberId,
            Description = item.PartNumber.Description,
            Alt = item.Alt,
            Qty = item.Qty,
            Priority = item.Priority,
            Note = item.Note,
            Unit = item.Unit,
            Condition = item.Condition,
            IsHighlighted = item.IsHighlighted,
            Remark = item.PartNumber.Remark,
            Alternatives = item.PartNumber.Alternatives.Select(a => new AlternativeResponse { Id = a.Id, Name = a.Name }).ToList()
        };
    }

    public async Task<RFQItemResponse?> AddItemAsync(long rfqId, AddRFQItemRequest request)
    {
        // Verify RFQ exists
        var rfq = await _db.Set<RFQHeader>().FindAsync(rfqId);
        if (rfq == null) return null;

        // Resolve or create PartNumber
        var trimmedName = request.PartNumberName.Trim();
        var partNumber = await _db.Set<PartNumber>()
            .Include(p => p.Alternatives)
            .FirstOrDefaultAsync(p => p.Name == trimmedName);

        if (partNumber == null)
        {
            partNumber = new PartNumber
            {
                Name = trimmedName,
                Description = request.Description,
                CreatedAt = DateTime.UtcNow
            };
            _db.Set<PartNumber>().Add(partNumber);
            await _db.SaveChangesAsync();
        }
        else
        {
            // Update description if provided
            if (!string.IsNullOrWhiteSpace(request.Description))
            {
                partNumber.Description = request.Description;
            }
        }

        // Add new alternatives that don't already exist
        foreach (var altName in request.Alternatives)
        {
            var trimmedAlt = altName.Trim();
            if (string.IsNullOrWhiteSpace(trimmedAlt)) continue;

            var exists = partNumber.Alternatives.Any(a => a.Name == trimmedAlt);
            if (!exists)
            {
                var alt = new Alternative
                {
                    Name = trimmedAlt,
                    PartNumberId = partNumber.Id,
                    CreatedAt = DateTime.UtcNow
                };
                _db.Set<Alternative>().Add(alt);
                partNumber.Alternatives.Add(alt);
            }
        }

        // Create RFQ Item
        var item = new RFQItem
        {
            RFQId = rfqId,
            PartNumberId = partNumber.Id,
            Qty = request.Qty,
            Condition = request.Condition,
            Alt = request.Alt,
            Note = request.Note,
            Unit = request.Unit,
        };
        _db.Set<RFQItem>().Add(item);
        await _db.SaveChangesAsync();

        // Reload alternatives
        await _db.Entry(partNumber).Collection(p => p.Alternatives).LoadAsync();

        return new RFQItemResponse
        {
            Id = item.Id,
            PartNumberName = partNumber.Name,
            PartNumberId = partNumber.Id,
            Description = partNumber.Description,
            Alt = item.Alt,
            Qty = item.Qty,
            Note = item.Note,
            Unit = item.Unit,
            Priority = item.Priority,
            Condition = item.Condition,
            IsHighlighted = item.IsHighlighted,
            Remark = partNumber.Remark,
            Alternatives = partNumber.Alternatives.Select(a => new AlternativeResponse { Id = a.Id, Name = a.Name }).ToList()
        };
    }

    public async Task<bool> UpdateExTypeAsync(long rfqId, int? exType)
    {
        var rfq = await _db.Set<RFQHeader>().FindAsync(rfqId);
        if (rfq == null) return false;
        rfq.ExType = exType;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateStatusAsync(long rfqId, string status, string? noQuoteReason = null, string? rejectionNote = null)
    {
        var rfq = await _db.Set<RFQHeader>().FindAsync(rfqId);
        if (rfq == null) return false;
        rfq.Status = status;

        // Keep existing reason if not provided in this call, 
        // but only clear it if we are moving away from No Quote/Waiting states
        if (noQuoteReason != null)
        {
            rfq.NoQuoteReason = noQuoteReason;
        }
        else if (status != "No Quote" && status != "Waiting For Admin")
        {
            rfq.NoQuoteReason = null;
        }

        // Handle rejection note when reverting from No Quote
        if (rejectionNote != null)
        {
            rfq.RejectionNote = rejectionNote;
        }
        // Don't clear rejectionNote when reverting to In Progress - keep it for user reference

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateNotesAsync(long rfqId, string? notes)
    {
        var rfq = await _db.Set<RFQHeader>().FindAsync(rfqId);
        if (rfq == null) return false;
        rfq.Notes = notes;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateNameAsync(long rfqId, string name)
    {
        var rfq = await _db.Set<RFQHeader>().FindAsync(rfqId);
        if (rfq == null) return false;
        rfq.Name = name;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateLeadTimeAsync(long rfqId, DateTime leadTime)
    {
        var rfq = await _db.Set<RFQHeader>().FindAsync(rfqId);
        if (rfq == null) return false;
        rfq.LeadTime = leadTime;
        await _db.SaveChangesAsync();
        return true;
    }

    // ──── Mapping ────

    private static RFQResponse MapToResponse(RFQHeader rfq) => new()
    {
        Id = rfq.Id,
        Name = rfq.Name,
        Status = rfq.Status,
        LeadTime = rfq.LeadTime,
        ReceivedDate = rfq.ReceivedDate,
        CreatedAt = rfq.CreatedAt,
        CustomerName = rfq.Customer.Name,
        CustomerCode = rfq.Customer.CustomerCode,
        CustomerBase = rfq.Customer.Base,
        CustomerCurrencyType = rfq.Customer.CurrencyType,
        CustomerId = rfq.CustomerId,
        UserName = rfq.User?.Name,
        UserId = rfq.UserId,
        Notes = rfq.Notes,
        NoQuoteReason = rfq.NoQuoteReason,
        RejectionNote = rfq.RejectionNote,
        ExType = rfq.ExType,
        Items = rfq.RFQItems.Select(i => new RFQItemResponse
        {
            Id = i.Id,
            PartNumberName = i.PartNumber.Name,
            PartNumberId = i.PartNumberId,
            Description = i.PartNumber.Description,
            Alt = i.Alt,
            Qty = i.Qty,
            Priority = i.Priority,
            Note = i.Note,
            Unit = i.Unit,
            Condition = i.Condition,
            IsHighlighted = i.IsHighlighted,
            Remark = i.PartNumber.Remark,
            Alternatives = i.PartNumber.Alternatives.Select(a => new AlternativeResponse { Id = a.Id, Name = a.Name }).ToList()
        }).ToList()
    };

    public async Task<string> DeleteRFQItem(long id)
    {
        var item = await _db.Set<RFQItem>().FindAsync(id);
        if (item == null) throw new Exception("Not found");
        var hasQuotes = await _db.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM QuoteItems WHERE RFQItemId = {id}")
            .SingleAsync();

        if (hasQuotes > 0)
            throw new Exception( "Cannot delete this item because it has linked quote items.");

        _db.Set<RFQItem>().Remove(item);
        await _db.SaveChangesAsync();


        return "Done";

    }
}

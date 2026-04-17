using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procument.Module.Catalog.Entities;
using Procument.Shared.DTOs;

namespace Procument.Module.Catalog.Controllers;

// ════════════════════════════════════════════════════════════
//  CUSTOMERS
// ════════════════════════════════════════════════════════════

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Expert")]
public class CustomersController : ControllerBase
{
    private readonly DbContext _db;
    public CustomersController(DbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult> GetAll(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 200, [FromQuery] string? search = null)
    {
        var query = _db.Set<Customer>().OrderBy(c => c.Name);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = (IOrderedQueryable<Customer>)query.Where(c => c.Name.Contains(s) || (c.CustomerCode != null && c.CustomerCode.Contains(s)));
        }
        var pq = new PageQuery { Page = page, PageSize = pageSize };
        var total = await query.CountAsync();
        var items = await query
            .Skip((pq.Page - 1) * pq.PageSize)
            .Take(pq.PageSize)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.CustomerCode,
                c.Email,
                c.Phone,
                c.ContactPerson,
                c.ShipTo,
                c.BillTo,
                c.ShippingAccount,
                c.Description,
                c.Base,
                c.TermsAndConditions,
                c.CurrencyType,
                c.IsActive,
                c.CreatedAt
            })
            .ToListAsync();

        return Ok(new PagedResult<object> { Items = items.Cast<object>().ToList(), TotalCount = total, Page = pq.Page, PageSize = pq.PageSize });
    }

    [HttpGet("search")]
    public async Task<ActionResult> Search([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 1)
            return Ok(Array.Empty<object>());

        var results = await _db.Set<Customer>()
            .Where(c => c.Name.Contains(q) && c.IsActive)
            .OrderBy(c => c.Name)
            .Take(10)
            .Select(c => new { c.Id, c.Name })
            .ToListAsync();

        return Ok(results);
    }

    [HttpPost]
    public async Task<ActionResult> Create([FromBody] CustomerDto dto)
    {
        var entity = new Customer
        {
            Name = dto.Name,
            CustomerCode = dto.CustomerCode,
            Email = dto.Email,
            Phone = dto.Phone,
            ContactPerson = dto.ContactPerson,
            ShipTo = dto.ShipTo,
            BillTo = dto.BillTo,
            ShippingAccount = dto.ShippingAccount,
            Description = dto.Description,
            Base = dto.Base,
            TermsAndConditions = dto.TermsAndConditions,
            CurrencyType = dto.CurrencyType,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
        _db.Set<Customer>().Add(entity);
        await _db.SaveChangesAsync();
        return Ok(new { entity.Id, entity.Name });
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult> Update(long id, [FromBody] CustomerDto dto)
    {
        var entity = await _db.Set<Customer>().FindAsync(id);
        if (entity == null) return NotFound();

        entity.Name = dto.Name;
        entity.CustomerCode = dto.CustomerCode;
        entity.Email = dto.Email;
        entity.Phone = dto.Phone;
        entity.ContactPerson = dto.ContactPerson;
        entity.ShipTo = dto.ShipTo;
        entity.BillTo = dto.BillTo;
        entity.ShippingAccount = dto.ShippingAccount;
        entity.Description = dto.Description;
        entity.Base = dto.Base;
        entity.TermsAndConditions = dto.TermsAndConditions;
        entity.CurrencyType = dto.CurrencyType;

        await _db.SaveChangesAsync();
        return Ok(new { entity.Id, entity.Name });
    }

    [HttpDelete("{id:long}")]
    public async Task<ActionResult> Delete(long id)
    {
        var entity = await _db.Set<Customer>().FindAsync(id);
        if (entity == null) return NotFound();
        _db.Set<Customer>().Remove(entity);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

public class CustomerDto
{
    public string Name { get; set; } = string.Empty;
    public string? CustomerCode { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? ContactPerson { get; set; }
    public string? ShipTo { get; set; }
    public string? BillTo { get; set; }
    public string? ShippingAccount { get; set; }
    public string? Description { get; set; }
    public int? Base { get; set; }
    public string? TermsAndConditions { get; set; }
    public string? CurrencyType { get; set; }
}

// ════════════════════════════════════════════════════════════
//  SUPPLIERS
// ════════════════════════════════════════════════════════════

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Expert")]
public class SuppliersController : ControllerBase
{
    private readonly DbContext _db;
    public SuppliersController(DbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult> GetAll(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 200, [FromQuery] string? search = null)
    {
        var pq = new PageQuery { Page = page, PageSize = pageSize };
        IQueryable<Supplier> query = _db.Set<Supplier>().Where(s => s.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(x => x.Name.Contains(s) || (x.Username != null && x.Username.Contains(s)));
        }
        query = query.OrderBy(x => x.Name);
        var total = await query.CountAsync();
        var items = await query
            .Skip((pq.Page - 1) * pq.PageSize)
            .Take(pq.PageSize)
            .Select(s => new
            {
                s.Id,
                s.Name,
                s.Username,
                s.Description,
                s.Dependency,
                s.Email,
                s.Phone,
                s.Address,
                s.IsActive,
                s.CreatedAt,
                s.Status,
                s.RequestedByUserId
            })
            .ToListAsync();

        return Ok(new PagedResult<object> { Items = items.Cast<object>().ToList(), TotalCount = total, Page = pq.Page, PageSize = pq.PageSize });
    }

    /// <summary>Search suppliers — returns Approved and Pending suppliers.</summary>
    [HttpGet("search")]
    public async Task<ActionResult> Search([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 1)
            return Ok(Array.Empty<object>());

        var results = await _db.Set<Supplier>()
            .Where(s => (s.Name.Contains(q) || (s.Username != null && s.Username.Contains(q)))
                        && s.IsActive && (s.Status == "Approved" || s.Status == "Pending"))
            .OrderBy(s => s.Name)
            .Take(10)
            .Select(s => new { s.Id, s.Name, s.Username, s.Status })
            .ToListAsync();

        return Ok(results);
    }

    /// <summary>Get all pending suppliers (admin review page).</summary>
    [HttpGet("pending")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> GetPending()
    {
        var items = await _db.Set<Supplier>()
            .Where(s => s.IsActive && (s.Status == "Pending" || s.Status == "Rejected"))
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new
            {
                s.Id,
                s.Name,
                s.Username,
                s.Email,
                s.Phone,
                s.Status,
                s.CreatedAt,
                s.RequestedByUserId
            })
            .ToListAsync();

        return Ok(items);
    }

    /// <summary>Approve a pending supplier.</summary>
    [HttpPost("{id:long}/approve")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> Approve(long id)
    {
        var entity = await _db.Set<Supplier>().FindAsync(id);
        if (entity == null) return NotFound();
        entity.Status = "Approved";
        entity.IsActive = true;
        await _db.SaveChangesAsync();
        return Ok(new { entity.Id, entity.Name, entity.Status });
    }

    /// <summary>Reject a pending supplier — user must correct the name.</summary>
    [HttpPost("{id:long}/reject")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> Reject(long id)
    {
        var entity = await _db.Set<Supplier>().FindAsync(id);
        if (entity == null) return NotFound();
        entity.Status = "Rejected";
        await _db.SaveChangesAsync();
        return Ok(new { entity.Id, entity.Name, entity.Status });
    }

    /// <summary>User updates a rejected supplier name and resubmits for approval.</summary>
    [HttpPost("{id:long}/resubmit")]
    public async Task<ActionResult> Resubmit(long id, [FromBody] ResubmitSupplierDto dto)
    {
        var entity = await _db.Set<Supplier>().FindAsync(id);
        if (entity == null) return NotFound();
        if (entity.Status != "Rejected") return BadRequest("Supplier is not in Rejected status.");

        var trimmedName = dto.Name.Trim();

        // Check if an Approved supplier with this name already exists
        var existing = await _db.Set<Supplier>()
            .FirstOrDefaultAsync(s => s.Id != id && s.Status == "Approved" &&
                                      s.IsActive &&
                                      s.Name.ToLower() == trimmedName.ToLower());

        if (existing != null)
        {
            // Re-point all ProcumentRecords from the rejected supplier to the existing approved one
            await _db.Database.ExecuteSqlRawAsync(
                "UPDATE Procument SET SupplierId = {0} WHERE SupplierId = {1}",
                existing.Id, id);

            // Delete the temp rejected supplier
            _db.Set<Supplier>().Remove(entity);
            await _db.SaveChangesAsync();

            return Ok(new { existing.Id, existing.Name, existing.Status });
        }

        // No match — rename and set back to Pending for admin review
        entity.Name = trimmedName;
        entity.Status = "Pending";
        entity.IsActive = true;
        await _db.SaveChangesAsync();
        return Ok(new { entity.Id, entity.Name, entity.Status });
    }

    [HttpPost]
    public async Task<ActionResult> Create([FromBody] SupplierDto dto)
    {
        var entity = new Supplier
        {
            Name = dto.Name,
            Username = dto.Username,
            Description = dto.Description,
            Dependency = dto.Dependency,
            Email = dto.Email,
            Phone = dto.Phone,
            Address = dto.Address,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            Status = "Approved" // Admin creates via catalog = always approved
        };
        _db.Set<Supplier>().Add(entity);
        await _db.SaveChangesAsync();
        return Ok(new { entity.Id, entity.Name });
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult> Update(long id, [FromBody] SupplierDto dto)
    {
        var entity = await _db.Set<Supplier>().FindAsync(id);
        if (entity == null) return NotFound();

        entity.Name = dto.Name;
        entity.Username = dto.Username;
        entity.Description = dto.Description;
        entity.Dependency = dto.Dependency;
        entity.Email = dto.Email;
        entity.Phone = dto.Phone;
        entity.Address = dto.Address;

        await _db.SaveChangesAsync();
        return Ok(new { entity.Id, entity.Name });
    }

    [HttpDelete("{id:long}")]
    public async Task<ActionResult> Delete(long id)
    {
        var entity = await _db.Set<Supplier>().FindAsync(id);
        if (entity == null) return NotFound();
        
        // Soft delete: set IsActive to false and status to "Disabled"
        entity.IsActive = false;
        entity.Status = "Disabled";
        
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

public class SupplierDto
{
    public string Name { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? Description { get; set; }
    public string? Dependency { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
}

public class ResubmitSupplierDto
{
    public string Name { get; set; } = string.Empty;
}

// ════════════════════════════════════════════════════════════
//  PART NUMBERS
// ════════════════════════════════════════════════════════════

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Expert")]
public class PartNumbersController : ControllerBase
{
    private readonly DbContext _db;
    public PartNumbersController(DbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult> GetAll(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 200, [FromQuery] string? search = null)
    {
        var pq = new PageQuery { Page = page, PageSize = pageSize };
        IQueryable<PartNumber> query = _db.Set<PartNumber>();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(p => p.Name.Contains(s) || p.Alternatives.Any(a => a.Name.Contains(s)));
        }
        query = query.OrderBy(p => p.Name);
        var total = await query.CountAsync();
        var items = await query
            .Skip((pq.Page - 1) * pq.PageSize)
            .Take(pq.PageSize)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Description,
                p.Remark,
                p.IsFavorite,
                p.CreatedAt,
                SupplierName = p.Supplier != null ? p.Supplier.Name : null,
                Alternatives = p.Alternatives.Select(a => new { a.Id, a.Name }).ToList()
            })
            .ToListAsync();

        return Ok(new PagedResult<object> { Items = items.Cast<object>().ToList(), TotalCount = total, Page = pq.Page, PageSize = pq.PageSize });
    }

    [HttpGet("search")]
    public async Task<ActionResult> Search([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 1)
            return Ok(Array.Empty<object>());

        var results = await _db.Set<PartNumber>()
            .Include(p => p.Alternatives)
            .Where(p => p.Name.Contains(q) || p.Alternatives.Any(a => a.Name.Contains(q)))
            .OrderBy(p => p.Name)
            .Take(10)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Description,
                p.Remark,
                p.IsFavorite,
                Alternatives = p.Alternatives.Select(a => new { a.Id, a.Name }).ToList()
            })
            .ToListAsync();

        return Ok(results);
    }

    [HttpPost]
    public async Task<ActionResult> Create([FromBody] PartNumberDto dto)
    {
        var entity = new PartNumber
        {
            Name = dto.Name,
            Description = dto.Description,
            Remark = dto.Remark,
            IsFavorite = dto.IsFavorite,
            SupplierId = dto.SupplierId,
            CreatedAt = DateTime.UtcNow
        };
        _db.Set<PartNumber>().Add(entity);
        await _db.SaveChangesAsync();
        return Ok(new { entity.Id, entity.Name });
    }

    /// <summary>Bulk create part numbers with alternatives.</summary>
    [HttpPost("bulk")]
    public async Task<ActionResult> BulkCreate([FromBody] BulkPartNumberRequest request)
    {
        var created = 0;
        var skipped = 0;

        foreach (var item in request.Parts)
        {
            var trimmedName = item.Name.Trim();
            if (string.IsNullOrWhiteSpace(trimmedName)) { skipped++; continue; }

            var existing = await _db.Set<PartNumber>()
                .Include(p => p.Alternatives)
                .FirstOrDefaultAsync(p => p.Name == trimmedName);

            if (existing != null)
            {
                // Update fields if provided
                if (!string.IsNullOrWhiteSpace(item.Description)) existing.Description = item.Description;
                if (!string.IsNullOrWhiteSpace(item.Remark)) existing.Remark = item.Remark;

                // Add new alternatives
                foreach (var altName in item.Alternatives ?? [])
                {
                    var alt = altName.Trim();
                    if (string.IsNullOrWhiteSpace(alt)) continue;
                    if (!existing.Alternatives.Any(a => a.Name == alt))
                    {
                        _db.Set<Alternative>().Add(new Alternative
                        {
                            Name = alt,
                            PartNumberId = existing.Id,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }
                skipped++;
            }
            else
            {
                var entity = new PartNumber
                {
                    Name = trimmedName,
                    Description = item.Description,
                    Remark = item.Remark,
                    CreatedAt = DateTime.UtcNow
                };
                _db.Set<PartNumber>().Add(entity);
                await _db.SaveChangesAsync();

                foreach (var altName in item.Alternatives ?? [])
                {
                    var alt = altName.Trim();
                    if (string.IsNullOrWhiteSpace(alt)) continue;
                    _db.Set<Alternative>().Add(new Alternative
                    {
                        Name = alt,
                        PartNumberId = entity.Id,
                        CreatedAt = DateTime.UtcNow
                    });
                }
                created++;
            }
        }

        await _db.SaveChangesAsync();
        return Ok(new { created, skipped, total = request.Parts.Count });
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult> Update(long id, [FromBody] PartNumberDto dto)
    {
        var entity = await _db.Set<PartNumber>().FindAsync(id);
        if (entity == null) return NotFound();

        entity.Name = dto.Name;
        entity.Description = dto.Description;
        entity.Remark = dto.Remark;
        entity.IsFavorite = dto.IsFavorite;
        entity.SupplierId = dto.SupplierId;

        await _db.SaveChangesAsync();
        return Ok(new { entity.Id, entity.Name });
    }

    [HttpPost("{id:long}/toggle-favorite")]
    public async Task<ActionResult> ToggleFavorite(long id)
    {
        var entity = await _db.Set<PartNumber>().FindAsync(id);
        if (entity == null) return NotFound();

        entity.IsFavorite = !entity.IsFavorite;
        await _db.SaveChangesAsync();
        return Ok(new { entity.Id, entity.IsFavorite });
    }

    /// <summary>Add an alternative part number to an existing part.</summary>
    [HttpPost("{id:long}/alternatives")]
    public async Task<ActionResult> AddAlternative(long id, [FromBody] AlternativeDto dto)
    {
        var part = await _db.Set<PartNumber>().FindAsync(id);
        if (part == null) return NotFound();

        var alt = new Alternative
        {
            Name = dto.Name,
            PartNumberId = id,
            CreatedAt = DateTime.UtcNow
        };
        _db.Set<Alternative>().Add(alt);
        await _db.SaveChangesAsync();
        return Ok(new { alt.Id, alt.Name });
    }

    /// <summary>Remove an alternative from a part number.</summary>
    [HttpDelete("{id:long}/alternatives/{altId:long}")]
    public async Task<ActionResult> RemoveAlternative(long id, long altId)
    {
        var alt = await _db.Set<Alternative>()
            .FirstOrDefaultAsync(a => a.Id == altId && a.PartNumberId == id);
        if (alt == null) return NotFound();
        _db.Set<Alternative>().Remove(alt);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Get all suppliers linked to a part number — only returns active ones.</summary>
    [HttpGet("{id:long}/suppliers")]
    public async Task<ActionResult> GetSuppliers(long id)
    {
        var suppliers = await _db.Set<PartNumberSupplier>()
            .Where(ps => ps.PartNumberId == id && ps.Supplier.IsActive)
            .Include(ps => ps.Supplier)
            .OrderBy(ps => ps.Supplier.Name)
            .Select(ps => new { ps.Supplier.Id, ps.Supplier.Name })
            .ToListAsync();

        return Ok(suppliers);
    }

    [HttpDelete("{id:long}")]
    public async Task<ActionResult> Delete(long id)
    {
        var entity = await _db.Set<PartNumber>().FindAsync(id);
        if (entity == null) return NotFound();
        _db.Set<PartNumber>().Remove(entity);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

public class PartNumberDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Priority { get; set; }
    public string? Remark { get; set; }
    public bool IsFavorite { get; set; }
    public long? SupplierId { get; set; }
}

public class AlternativeDto
{
    public string Name { get; set; } = string.Empty;
}

public class BulkPartNumberRequest
{
    public List<BulkPartNumberItem> Parts { get; set; } = new();
}

public class BulkPartNumberItem
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Priority { get; set; }
    public string? Remark { get; set; }
    public List<string> Alternatives { get; set; } = new();
}

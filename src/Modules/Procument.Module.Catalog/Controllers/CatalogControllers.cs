using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procument.Module.Catalog.Entities;

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
    public async Task<ActionResult> GetAll()
    {
        var items = await _db.Set<Customer>()
            .OrderBy(c => c.Name)
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
                c.IsActive,
                c.CreatedAt
            })
            .ToListAsync();

        return Ok(items);
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
    public async Task<ActionResult> GetAll()
    {
        var items = await _db.Set<Supplier>()
            .OrderBy(s => s.Name)
            .Select(s => new
            {
                s.Id,
                s.Name,
                s.Email,
                s.Phone,
                s.Address,
                s.IsActive,
                s.CreatedAt
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("search")]
    public async Task<ActionResult> Search([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 1)
            return Ok(Array.Empty<object>());

        var results = await _db.Set<Supplier>()
            .Where(s => s.Name.Contains(q) && s.IsActive)
            .OrderBy(s => s.Name)
            .Take(10)
            .Select(s => new { s.Id, s.Name })
            .ToListAsync();

        return Ok(results);
    }

    [HttpPost]
    public async Task<ActionResult> Create([FromBody] SupplierDto dto)
    {
        var entity = new Supplier
        {
            Name = dto.Name,
            Email = dto.Email,
            Phone = dto.Phone,
            Address = dto.Address,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
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
        _db.Set<Supplier>().Remove(entity);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

public class SupplierDto
{
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
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
    public async Task<ActionResult> GetAll()
    {
        var items = await _db.Set<PartNumber>()
            .Include(p => p.Supplier)
            .Include(p => p.Alternatives)
            .OrderBy(p => p.Name)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Description,
                p.Remark,
                p.CreatedAt,
                SupplierName = p.Supplier != null ? p.Supplier.Name : null,
                Alternatives = p.Alternatives.Select(a => new { a.Id, a.Name }).ToList()
            })
            .ToListAsync();

        return Ok(items);
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
        entity.SupplierId = dto.SupplierId;

        await _db.SaveChangesAsync();
        return Ok(new { entity.Id, entity.Name });
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

    /// <summary>Get all suppliers linked to a part number.</summary>
    [HttpGet("{id:long}/suppliers")]
    public async Task<ActionResult> GetSuppliers(long id)
    {
        var suppliers = await _db.Set<PartNumberSupplier>()
            .Where(ps => ps.PartNumberId == id)
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

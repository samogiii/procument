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
                c.Email,
                c.Phone,
                c.ShipTo,
                c.BillTo,
                c.IsActive,
                c.CreatedAt
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("search")]
    public async Task<ActionResult> Search([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 3)
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
            Email = dto.Email,
            Phone = dto.Phone,
            ShipTo = dto.ShipTo,
            BillTo = dto.BillTo,
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
        entity.Email = dto.Email;
        entity.Phone = dto.Phone;
        entity.ShipTo = dto.ShipTo;
        entity.BillTo = dto.BillTo;

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
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? ShipTo { get; set; }
    public string? BillTo { get; set; }
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
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
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
        if (string.IsNullOrWhiteSpace(q) || q.Length < 3)
            return Ok(Array.Empty<object>());

        var results = await _db.Set<PartNumber>()
            .Where(p => p.Name.Contains(q))
            .OrderBy(p => p.Name)
            .Take(10)
            .Select(p => new { p.Id, p.Name })
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
            SupplierId = dto.SupplierId,
            CreatedAt = DateTime.UtcNow
        };
        _db.Set<PartNumber>().Add(entity);
        await _db.SaveChangesAsync();
        return Ok(new { entity.Id, entity.Name });
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult> Update(long id, [FromBody] PartNumberDto dto)
    {
        var entity = await _db.Set<PartNumber>().FindAsync(id);
        if (entity == null) return NotFound();

        entity.Name = dto.Name;
        entity.Description = dto.Description;
        entity.SupplierId = dto.SupplierId;

        await _db.SaveChangesAsync();
        return Ok(new { entity.Id, entity.Name });
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
    public long? SupplierId { get; set; }
}

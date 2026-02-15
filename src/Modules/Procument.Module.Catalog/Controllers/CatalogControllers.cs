using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procument.Module.Catalog.Entities;

namespace Procument.Module.Catalog.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Expert")]
public class CustomersController : ControllerBase
{
    private readonly DbContext _db;

    public CustomersController(DbContext db)
    {
        _db = db;
    }

    /// <summary>Search customers by name (min 3 chars). Returns top 10 matches.</summary>
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
}

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Expert")]
public class PartNumbersController : ControllerBase
{
    private readonly DbContext _db;

    public PartNumbersController(DbContext db)
    {
        _db = db;
    }

    /// <summary>Search part numbers by name (min 3 chars). Returns top 10 matches.</summary>
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
}

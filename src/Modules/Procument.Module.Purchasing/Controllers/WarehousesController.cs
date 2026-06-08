using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Procument.Module.Purchasing.DTOs;
using Procument.Module.Purchasing.Services;

namespace Procument.Module.Purchasing.Controllers;

[ApiController]
[Route("api/warehouses")]
[Authorize(Roles = "Admin,SuperAdmin,Inventory,Expert")]
public class WarehousesController : ControllerBase
{
    private readonly IWarehouseService _service;

    public WarehousesController(IWarehouseService service) => _service = service;

    /// <summary>List all active warehouses.</summary>
    [HttpGet]
    public async Task<ActionResult<List<WarehouseResponse>>> GetAll()
        => Ok(await _service.GetAllAsync());

    /// <summary>Get a single warehouse by ID.</summary>
    [HttpGet("{id:long}")]
    public async Task<ActionResult<WarehouseResponse>> GetById(long id)
    {
        var w = await _service.GetByIdAsync(id);
        return w == null ? NotFound() : Ok(w);
    }

    /// <summary>Create a warehouse (Admin only).</summary>
    [HttpPost]
    [Authorize(Roles = "Admin,SuperAdmin,Expert")]
    public async Task<ActionResult<WarehouseResponse>> Create([FromBody] SaveWarehouseRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest("Name is required.");
        var result = await _service.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>Update a warehouse (Admin only).</summary>
    [HttpPut("{id:long}")]
    [Authorize(Roles = "Admin,SuperAdmin,Expert")]
    public async Task<ActionResult<WarehouseResponse>> Update(long id, [FromBody] SaveWarehouseRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest("Name is required.");
        var result = await _service.UpdateAsync(id, request);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>Soft-delete a warehouse (Admin only).</summary>
    [HttpDelete("{id:long}")]
    [Authorize(Roles = "Admin,SuperAdmin,Expert")]
    public async Task<IActionResult> Delete(long id)
    {
        var ok = await _service.DeleteAsync(id);
        return ok ? NoContent() : NotFound();
    }

    // ── User assignments ──────────────────────────────────────────────────

    /// <summary>Get users assigned to a warehouse.</summary>
    [HttpGet("{id:long}/users")]
    [Authorize(Roles = "Admin,SuperAdmin,Expert")]
    public async Task<ActionResult<List<WarehouseUserResponse>>> GetUsers(long id)
        => Ok(await _service.GetUsersAsync(id));

    /// <summary>Assign an Inventory user to a warehouse.</summary>
    [HttpPost("{id:long}/users/{userId:long}")]
    [Authorize(Roles = "Admin,SuperAdmin,Expert")]
    public async Task<IActionResult> AssignUser(long id, long userId)
    {
        await _service.AssignUserAsync(id, userId);
        return NoContent();
    }

    /// <summary>Remove a user from a warehouse.</summary>
    [HttpDelete("{id:long}/users/{userId:long}")]
    [Authorize(Roles = "Admin,SuperAdmin,Expert")]
    public async Task<IActionResult> UnassignUser(long id, long userId)
    {
        var ok = await _service.UnassignUserAsync(id, userId);
        return ok ? NoContent() : NotFound();
    }
}

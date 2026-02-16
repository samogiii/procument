using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Procument.Module.RFQ.DTOs;
using Procument.Module.RFQ.Services;

namespace Procument.Module.RFQ.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Expert")]
public class RFQsController : ControllerBase
{
    private readonly IRFQService _rfqService;

    public RFQsController(IRFQService rfqService)
    {
        _rfqService = rfqService;
    }

    /// <summary>Create a new RFQ. Auto-creates customer and part numbers if they don't exist.</summary>
    [HttpPost]
    public async Task<ActionResult<RFQResponse>> Create([FromBody] CreateRFQRequest request)
    {
        var result = await _rfqService.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>Get all RFQs.</summary>
    [HttpGet]
    public async Task<ActionResult<List<RFQResponse>>> GetAll()
    {
        var (userId, isAdmin) = GetUserContext();
        var result = await _rfqService.GetAllAsync(userId, isAdmin);
        return Ok(result);
    }

    /// <summary>Get RFQ by ID.</summary>
    [HttpGet("{id:long}")]
    public async Task<ActionResult<RFQResponse>> GetById(long id)
    {
        var (userId, isAdmin) = GetUserContext();
        var result = await _rfqService.GetByIdAsync(id, userId, isAdmin);

        if (result == null)
        {
            // If service returns null, it could be 404 or 403 (filtered out). 
            // Returning NotFound covers both securely.
            return NotFound();
        }

        return Ok(result);
    }

    private (long userId, bool isAdmin) GetUserContext()
    {
        var idClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        long userId = 0;
        if (idClaim != null && long.TryParse(idClaim.Value, out var id))
        {
            userId = id;
        }
        bool isAdmin = User.IsInRole("Admin");
        return (userId, isAdmin);
    }

    /// <summary>Update an RFQ item's fields (Alt, Qty, Condition).</summary>
    [HttpPut("items/{itemId:long}")]
    public async Task<ActionResult<RFQItemResponse>> UpdateItem(long itemId, [FromBody] UpdateRFQItemRequest request)
    {
        var result = await _rfqService.UpdateItemAsync(itemId, request);
        return result == null ? NotFound() : Ok(result);
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Procument.Module.Sales.DTOs;
using Procument.Module.Sales.Services;
using System.Security.Claims;

using Procument.Shared.Audit;

namespace Procument.Module.Sales.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Expert")]
public class QuotesController : ControllerBase
{
    private readonly IQuoteService _quoteService;

    public QuotesController(IQuoteService quoteService)
    {
        _quoteService = quoteService;
    }

    /// <summary>Get all quotes (paginated).</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<QuoteResponse>>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? status = null)
    {
        var (userId, isAdmin) = GetUserContext();
        var result = await _quoteService.GetAllAsync(page, pageSize, userId, isAdmin, status);
        return Ok(result);
    }

    /// <summary>Get all quotes for an RFQ.</summary>
    [HttpGet("by-rfq/{rfqId:long}")]
    public async Task<ActionResult<List<QuoteResponse>>> GetByRFQ(long rfqId)
    {
        var (userId, isAdmin) = GetUserContext();
        var result = await _quoteService.GetByRFQIdAsync(rfqId, userId, isAdmin);
        return Ok(result);
    }

    /// <summary>Get a quote by ID.</summary>
    [HttpGet("{id:long}")]
    public async Task<ActionResult<QuoteResponse>> GetById(long id)
    {
        var (userId, isAdmin) = GetUserContext();
        var result = await _quoteService.GetByIdAsync(id, userId, isAdmin);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>Create a new quote from selected procurement records.</summary>
    [HttpPost]
    [Auditable("Quote", "Create", CaptureBody = true)]
    public async Task<ActionResult<QuoteResponse>> Create([FromBody] CreateQuoteRequest request)
    {
        var (userId, isAdmin) = GetUserContext();
        if (userId == 0) return Unauthorized("User ID not found in token.");

        var result = await _quoteService.CreateAsync(request, userId);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>Update quote status.</summary>
    [HttpPatch("{id:long}/status")]
    [Auditable("Quote", "UpdateStatus", CaptureBody = true)]
    public async Task<IActionResult> UpdateStatus(long id, [FromBody] UpdateQuoteStatusRequest request)
    {
        var (userId, isAdmin) = GetUserContext();
        var success = await _quoteService.UpdateStatusAsync(id, request.Status, userId, isAdmin);
        return success ? Ok() : NotFound();
    }

    /// <summary>Update quote items (re-select procurement records).</summary>
    [HttpPut("{id:long}")]
    [Auditable("Quote", "Update", CaptureBody = true)]
    public async Task<ActionResult<QuoteResponse>> Update(long id, [FromBody] CreateQuoteRequest request)
    {
        var (userId, isAdmin) = GetUserContext();
        var result = await _quoteService.UpdateAsync(id, request, userId, isAdmin);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>Update quote Type.</summary>
    [HttpPatch("{id:long}/types")]
    [Auditable("Quote", "UpdateStatus", CaptureBody = true)]
    public async Task<IActionResult> UpdateQuoteType(long id, [FromBody] QuoteTypeDTO request)
    {
        var (userId, isAdmin) = GetUserContext();
        var success = await _quoteService.UpdateQuoteTypeAsync(id, request.QuoteType,request.TypeAdditional, userId, isAdmin);
        return success ? Ok() : NotFound();
    }

    /// <summary>Delete a quote.</summary>
    [HttpDelete("{id:long}")]
    [Auditable("Quote", "Delete")]
    public async Task<IActionResult> Delete(long id)
    {
        var deleted = await _quoteService.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
    }

    private (long userId, bool isAdmin) GetUserContext()
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        long userId = 0;
        if (idClaim != null && long.TryParse(idClaim.Value, out var id))
        {
            userId = id;
        }
        bool isAdmin = User.IsInRole("Admin");
        return (userId, isAdmin);
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Procument.Module.Purchasing.DTOs;
using Procument.Module.Purchasing.Services;
using Procument.Shared.DTOs;

namespace Procument.Module.Purchasing.Controllers;

[ApiController]
[Route("api/procument-page")]
[Authorize(Roles = "Admin,SuperAdmin,Expert")]
public class ProcumentPageController : ControllerBase
{
    private readonly IProcumentPageService _service;

    public ProcumentPageController(IProcumentPageService service)
    {
        _service = service;
    }

    /// <summary>Get all RFQ items with their supplier quotes for the Procument page (paginated).</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<ProcumentPageItemResponse>>> GetAll(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 200, [FromQuery] string? search = null)
    {
        var pq = new PageQuery { Page = page, PageSize = pageSize, Search = search };
        var (userId, isAdmin) = GetUserContext();
        var result = await _service.GetAllItemsAsync(userId, isAdmin, pq);
        return Ok(result);
    }

    /// <summary>Get supplier suggestions for a part number based on history.</summary>
    [HttpGet("suggestions")]
    public async Task<ActionResult<SupplierSuggestionsResponse>> GetSuggestions(
        [FromQuery] long partNumberId, [FromQuery] long rfqId)
    {
        var result = await _service.GetSuggestionsAsync(partNumberId, rfqId);
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
        bool isAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");
        return (userId, isAdmin);
    }
}

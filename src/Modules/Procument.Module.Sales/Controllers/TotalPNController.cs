using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Procument.Module.Purchasing.DTOs;
using Procument.Module.Sales.Services;
using Procument.Shared.Audit;
using Procument.Shared.DTOs;

namespace Procument.Module.Sales.Controllers;

/// <summary>
/// Total P/N (TPP) report — flat denormalized view of every POItem in the system,
/// joined across PO + Procurement + Invoice + Quote + FinalInvoice + CustomerPayments.
/// Mirrors the columns of the user's totalpn.xlsx.
/// </summary>
[ApiController]
[Route("api/po-items/total-pn")]
[Authorize(Roles = "Admin,SuperAdmin,Expert,Payment")]
public class TotalPNController : ControllerBase
{
    private readonly ITotalPNService _service;
    public TotalPNController(ITotalPNService service) { _service = service; }

    private (long userId, bool isAdmin) GetCurrentUser()
    {
        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        long.TryParse(userIdStr, out var userId);
        var isAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");
        return (userId, isAdmin);
    }

    /// <summary>Paged Total P/N rows. Non-admins see only items they have access to (Procurement OR PO permission).</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<TotalPNRowResponse>>> Get(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 200)
    {
        var (userId, isAdmin) = GetCurrentUser();
        var pq = new PageQuery { Page = page, PageSize = pageSize };
        var result = await _service.GetAsync(pq, userId, isAdmin);
        return Ok(result);
    }

    /// <summary>Inline edit — sets POItem.Status and/or POItem.Note from the Total P/N grid.</summary>
    [HttpPatch("{poItemId:long}")]
    [Auditable("POItem", "UpdateTotalPN", CaptureBody = true)]
    public async Task<IActionResult> Update(long poItemId, [FromBody] UpdatePOItemTotalPNRequest request)
    {
        var ok = await _service.UpdateAsync(poItemId, request);
        return ok ? NoContent() : NotFound();
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Procument.Module.Purchasing.DTOs;
using Procument.Module.Sales.Services;
using Procument.Shared.Audit;
using Procument.Shared.DTOs;
using System.Security.Claims;

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

    private (long userId, bool isAdmin, bool isSuperAdmin, int[] userBases) GetCurrentUser()
    {
        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        long.TryParse(userIdStr, out var userId);
        var isAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");
        var isSuperAdmin = User.IsInRole("SuperAdmin");
        var basesClaim = User.FindFirst("bases")?.Value ?? "";
        int[] userBases = basesClaim.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s, out var b) ? b : -1)
            .Where(b => b > 0).ToArray();
        return (userId, isAdmin, isSuperAdmin, userBases);
    }

    /// <summary>Paged Total P/N rows. Non-admins see only items they have access to (Procurement OR PO permission).</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<TotalPNRowResponse>>> Get(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 200,
        [FromQuery] string? sortBy = null, [FromQuery] bool sortDesc = false,
        [FromQuery] List<string>? customers = null,
        [FromQuery] List<string>? invoiceNumbers = null,
        [FromQuery] List<string>? partNumbers = null,
        [FromQuery] List<string>? conditions = null,
        [FromQuery] List<string>? poNumbers = null,
        [FromQuery] List<string>? suppliers = null,
        [FromQuery] List<string>? paymentTerms = null,
        [FromQuery] List<string>? poStatuses = null,
        [FromQuery] List<string>? shippingStatuses = null)
    {
        var (userId, isAdmin, isSuperAdmin, userBases) = GetCurrentUser();
        var pq = new PageQuery { Page = page, PageSize = pageSize };
        var result = await _service.GetAsync(pq, userId, isAdmin, sortBy, sortDesc, isSuperAdmin, userBases,
            customers, invoiceNumbers, partNumbers, conditions, poNumbers, suppliers, paymentTerms, poStatuses, shippingStatuses);
        return Ok(result);
    }

    /// <summary>Returns unique values for each filterable column — used to populate filter dropdowns.</summary>
    [HttpGet("filter-options")]
    public async Task<ActionResult<TotalPNFilterOptions>> GetFilterOptions()
    {
        var (userId, isAdmin, isSuperAdmin, userBases) = GetCurrentUser();
        var result = await _service.GetFilterOptionsAsync(userId, isAdmin, isSuperAdmin, userBases);
        return Ok(result);
    }

    /// <summary>Total Order view — one row per POItem that has at least one Track Number, with SN/TID/AWB data.</summary>
    [HttpGet("/api/po-items/total-order")]
    public async Task<ActionResult<PagedResult<TotalPNRowResponse>>> GetTotalOrder(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 200, [FromQuery] string? search = null)
    {
        var (userId, isAdmin, isSuperAdmin, userBases) = GetCurrentUser();
        var pq = new PageQuery { Page = page, PageSize = pageSize, Search = search };
        var result = await _service.GetTotalOrderAsync(pq, userId, isAdmin, isSuperAdmin, userBases);
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

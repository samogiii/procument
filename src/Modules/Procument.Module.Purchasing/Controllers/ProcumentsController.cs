using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Procument.Module.Purchasing.DTOs;
using Procument.Module.Purchasing.Services;

using Procument.Shared.Audit;

namespace Procument.Module.Purchasing.Controllers;

[ApiController]
[Route("api/rfqs/{rfqId:long}/supplier-quotes")]
[Authorize(Roles = "Admin,SuperAdmin,Expert")]
public class SupplierQuotesController : ControllerBase
{
    private readonly ISupplierQuoteService _procumentService;

    public SupplierQuotesController(ISupplierQuoteService procumentService)
    {
        _procumentService = procumentService;
    }

    /// <summary>Get all supplier quotes for an RFQ.</summary>
    [HttpGet]
    public async Task<ActionResult<List<SupplierQuoteResponse>>> GetByRFQ(long rfqId)
    {
        var (userId, isAdmin) = GetUserContext();
        var result = await _procumentService.GetByRFQIdAsync(rfqId, userId, isAdmin);
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

    private long GetUserId() => GetUserContext().userId;

    /// <summary>Create or update a single supplier quote.</summary>
    [HttpPost]
    [Auditable("ProcumentRecord", "Save", CaptureBody = true)]
    public async Task<ActionResult<SupplierQuoteResponse>> Save(long rfqId, [FromBody] SaveSupplierQuoteRequest request)
    {
        var result = await _procumentService.SaveAsync(request, GetUserId());
        return Ok(result);
    }

    /// <summary>Bulk save supplier quotes.</summary>
    [HttpPost("bulk")]
    [Auditable("ProcumentRecord", "BulkSave", CaptureBody = true)]
    public async Task<ActionResult<List<SupplierQuoteResponse>>> BulkSave(long rfqId, [FromBody] BulkSaveQuotesRequest request)
    {
        var result = await _procumentService.BulkSaveAsync(rfqId, request, GetUserId());
        return Ok(result);
    }

    /// <summary>Delete a supplier quote.</summary>
    [HttpDelete("{id:long}")]
    [Auditable("ProcumentRecord", "Delete")]
    public async Task<IActionResult> Delete(long rfqId, long id)
    {
        var deleted = await _procumentService.DeleteAsync(id, GetUserId());
        return deleted ? NoContent() : NotFound();
    }

    /// <summary>Update sort order of supplier quotes for an RFQ.</summary>
    [HttpPatch("order")]
    [Auditable("ProcumentRecord", "UpdateOrder", CaptureBody = true)]
    public async Task<IActionResult> UpdateOrder(long rfqId, [FromBody] UpdateSupplierQuotesOrderRequest request)
    {
        var (userId, isAdmin) = GetUserContext();
        var ok = await _procumentService.UpdateOrderAsync(rfqId, request.Items, userId, isAdmin);
        return ok ? Ok() : NotFound();
    }
}

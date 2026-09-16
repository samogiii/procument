using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
        try
        {
            var result = await _procumentService.SaveAsync(request, GetUserId());
            return Ok(result);
        }
        catch (SupplierNotFoundException ex)
        {
            return BadRequest(new { message = ex.Message, missingSuppliers = ex.MissingSuppliers });
        }
    }

    /// <summary>Bulk save supplier quotes.</summary>
    [HttpPost("bulk")]
    [Auditable("ProcumentRecord", "BulkSave", CaptureBody = true)]
    public async Task<ActionResult<List<SupplierQuoteResponse>>> BulkSave(long rfqId, [FromBody] BulkSaveQuotesRequest request)
    {
        try
        {
            var result = await _procumentService.BulkSaveAsync(rfqId, request, GetUserId());
            return Ok(result);
        }
        catch (SupplierNotFoundException ex)
        {
            return BadRequest(new { message = ex.Message, missingSuppliers = ex.MissingSuppliers });
        }
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

    /// <summary>List all supplier certificate PDFs for an RFQ, enriched with part and supplier names.</summary>
    [HttpGet("certificates")]
    public async Task<ActionResult<List<SupplierQuoteCertificateResponse>>> GetCertificatesForRfq(long rfqId)
    {
        var (userId, isAdmin) = GetUserContext();
        return Ok(await _procumentService.GetCertificatesForRfqAsync(rfqId, userId, isAdmin));
    }

    /// <summary>List certificate PDFs attached to one supplier-part quote.</summary>
    [HttpGet("{quoteId:long}/certificates")]
    public async Task<ActionResult<List<SupplierQuoteCertificateResponse>>> GetCertificates(long rfqId, long quoteId)
    {
        var (userId, isAdmin) = GetUserContext();
        return Ok(await _procumentService.GetCertificatesAsync(rfqId, quoteId, userId, isAdmin));
    }

    /// <summary>Upload one or more PDF certificates for one saved supplier-part quote.</summary>
    [HttpPost("{quoteId:long}/certificates")]
    [Auditable("ProcumentRecord", "UploadCertificate")]
    public async Task<ActionResult<List<SupplierQuoteCertificateResponse>>> UploadCertificates(
        long rfqId,
        long quoteId,
        [FromForm] List<IFormFile> files)
    {
        var result = await _procumentService.UploadCertificatesAsync(rfqId, quoteId, GetUserId(), files);
        return Ok(result);
    }

    /// <summary>Download a certificate PDF.</summary>
    [HttpGet("{quoteId:long}/certificates/{certificateId:long}/download")]
    public async Task<IActionResult> DownloadCertificate(long rfqId, long quoteId, long certificateId)
    {
        var (userId, isAdmin) = GetUserContext();
        var result = await _procumentService.DownloadCertificateAsync(rfqId, quoteId, certificateId, userId, isAdmin);
        if (result == null) return NotFound();
        var (stream, fileName, mimeType) = result.Value;
        return File(stream, mimeType, fileName);
    }
}

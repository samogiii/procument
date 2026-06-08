using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Procument.Module.Purchasing.DTOs;
using Procument.Module.Purchasing.Services;

namespace Procument.Module.Purchasing.Controllers;

[ApiController]
[Route("api/shipping")]
[Authorize]
public class ShippingController : ControllerBase
{
    private readonly IShippingService _service;

    public ShippingController(IShippingService service) => _service = service;

    private long GetUserId()
    {
        var str = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!long.TryParse(str, out var id))
            throw new UnauthorizedAccessException("Invalid user token.");
        return id;
    }

    // ── Track Numbers for the current Inventory user ─────────────────────

    /// <summary>List all track numbers assigned to the current user's warehouses.</summary>
    [HttpGet("track-numbers")]
    [Authorize(Roles = "Admin,SuperAdmin,Inventory,Expert")]
    public async Task<ActionResult<List<ShippingTrackResponse>>> GetTrackNumbers()
    {
        var isAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");
        var isSyd = User.Identity?.Name == "SYD";
        var userId = GetUserId();
        return Ok(await _service.GetTrackNumbersForUserAsync(userId, isAdmin || isSyd));
    }

    // ── Per-part items ────────────────────────────────────────────────────

    /// <summary>Submit (create or update) per-part qty/availability entries for a track number.</summary>
    [HttpPost("track-numbers/{trackId:long}/items")]
    [Authorize(Roles = "Admin,SuperAdmin,Inventory,Expert")]
    public async Task<ActionResult<List<TrackNumberItemResponse>>> SubmitItems(
        long trackId,
        [FromBody] SubmitTrackNumberItemsRequest request)
    {
        var result = await _service.SubmitItemsAsync(trackId, GetUserId(), request);
        return Ok(result);
    }

    /// <summary>Update a single part item (qty, actual qty, availability).</summary>
    [HttpPut("track-numbers/{trackId:long}/items/{itemId:long}")]
    [Authorize(Roles = "Admin,SuperAdmin,Inventory,Expert")]
    public async Task<ActionResult<TrackNumberItemResponse>> UpdateItem(
        long trackId,
        long itemId,
        [FromBody] UpdateTrackNumberItemRequest request)
    {
        var result = await _service.UpdateItemAsync(trackId, itemId, request);
        return result == null ? NotFound() : Ok(result);
    }

    // ── Reject track ──────────────────────────────────────────────────────

    /// <summary>Reject a track number → sets PO status to Issue.</summary>
    [HttpPost("track-numbers/{trackId:long}/reject")]
    [Authorize(Roles = "Admin,SuperAdmin,Inventory,Expert")]
    public async Task<IActionResult> RejectTrack(long trackId)
    {
        var ok = await _service.RejectTrackAsync(trackId, GetUserId());
        return ok ? NoContent() : NotFound();
    }

    // ── Documents ─────────────────────────────────────────────────────────

    /// <summary>Upload a track-level document (not tied to a specific part).</summary>
    [HttpPost("track-numbers/{trackId:long}/documents")]
    [Authorize(Roles = "Admin,SuperAdmin,Inventory,Expert")]
    public async Task<ActionResult<TrackNumberDocumentResponse>> UploadTrackDoc(
        long trackId,
        IFormFile file)
    {
        if (file == null || file.Length == 0) return BadRequest("No file provided.");
        var result = await _service.UploadTrackDocAsync(trackId, GetUserId(), file);
        return Ok(result);
    }

    /// <summary>Upload a part-level document for a specific PO item within a track number.</summary>
    [HttpPost("track-numbers/{trackId:long}/parts/{poItemId:long}/documents")]
    [Authorize(Roles = "Admin,SuperAdmin,Inventory,Expert")]
    public async Task<ActionResult<TrackNumberDocumentResponse>> UploadPartDoc(
        long trackId,
        long poItemId,
        IFormFile file)
    {
        if (file == null || file.Length == 0) return BadRequest("No file provided.");
        var result = await _service.UploadPartDocAsync(trackId, poItemId, GetUserId(), file);
        return Ok(result);
    }

    /// <summary>List all documents for a track number (track-level and part-level).</summary>
    [HttpGet("track-numbers/{trackId:long}/documents")]
    [Authorize(Roles = "Admin,SuperAdmin,Inventory,Expert")]
    public async Task<ActionResult<List<TrackNumberDocumentResponse>>> GetDocuments(long trackId)
        => Ok(await _service.GetDocumentsAsync(trackId));

    /// <summary>Download a specific document file.</summary>
    [HttpGet("documents/{docId:long}/file")]
    [Authorize(Roles = "Admin,SuperAdmin,Inventory,Expert")]
    public async Task<IActionResult> DownloadDoc(long docId)
    {
        var result = await _service.DownloadDocAsync(docId);
        if (result == null) return NotFound();
        var (stream, fileName, mimeType) = result.Value;
        return File(stream, mimeType, fileName);
    }

    /// <summary>Delete a document.</summary>
    [HttpDelete("documents/{docId:long}")]
    [Authorize(Roles = "Admin,SuperAdmin,Inventory,Expert")]
    public async Task<IActionResult> DeleteDoc(long docId)
    {
        var ok = await _service.DeleteDocAsync(docId, GetUserId());
        return ok ? NoContent() : NotFound();
    }

    // ── Review (Admin or originator) ──────────────────────────────────────

    /// <summary>Accept or Reject a part item's documents. Accepted items appear in the Ready-for-SN queue.</summary>
    [HttpPost("track-numbers/{trackId:long}/items/{itemId:long}/review")]
    [Authorize(Roles = "Admin,SuperAdmin,Expert")]
    public async Task<ActionResult<TrackNumberItemResponse>> ReviewItem(
        long trackId,
        long itemId,
        [FromBody] ReviewTrackNumberItemRequest request)
    {
        var result = await _service.ReviewItemAsync(trackId, itemId, GetUserId(), request);
        return result == null ? NotFound() : Ok(result);
    }

    // ── Single track review (Admin/Expert) ───────────────────────────────

    /// <summary>Get a single track number with all inventory items and documents — for Admin/Expert review on PO page.</summary>
    [HttpGet("track-numbers/{trackId:long}/review")]
    [Authorize(Roles = "Admin,SuperAdmin,Expert")]
    public async Task<ActionResult<ShippingTrackResponse>> GetTrackForReview(long trackId)
    {
        var result = await _service.GetTrackForReviewAsync(trackId);
        return result == null ? NotFound() : Ok(result);
    }

    // ── Track Number Boxes ─────────────────────────────────────────────────

    /// <summary>List boxes for a track number.</summary>
    [HttpGet("track-numbers/{trackId:long}/boxes")]
    [Authorize(Roles = "Admin,SuperAdmin,Inventory,Expert")]
    public async Task<ActionResult<List<TrackBoxResponse>>> GetTrackBoxes(long trackId)
        => Ok(await _service.GetTrackBoxesAsync(trackId));

    /// <summary>Add a box to a track number.</summary>
    [HttpPost("track-numbers/{trackId:long}/boxes")]
    [Authorize(Roles = "Admin,SuperAdmin,Inventory,Expert")]
    public async Task<ActionResult<TrackBoxResponse>> AddTrackBox(
        long trackId,
        [FromBody] SaveTrackBoxRequest request)
    {
        var result = await _service.AddTrackBoxAsync(trackId, request);
        return Ok(result);
    }

    /// <summary>Update a box on a track number.</summary>
    [HttpPut("track-numbers/{trackId:long}/boxes/{boxId:long}")]
    [Authorize(Roles = "Admin,SuperAdmin,Inventory,Expert")]
    public async Task<ActionResult<TrackBoxResponse>> UpdateTrackBox(
        long trackId,
        long boxId,
        [FromBody] SaveTrackBoxRequest request)
    {
        var result = await _service.UpdateTrackBoxAsync(trackId, boxId, request);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>Delete a box from a track number.</summary>
    [HttpDelete("track-numbers/{trackId:long}/boxes/{boxId:long}")]
    [Authorize(Roles = "Admin,SuperAdmin,Inventory,Expert")]
    public async Task<IActionResult> DeleteTrackBox(long trackId, long boxId)
    {
        var ok = await _service.DeleteTrackBoxAsync(trackId, boxId);
        return ok ? NoContent() : NotFound();
    }

    // ── Ready for SN ──────────────────────────────────────────────────────

    /// <summary>Get all accepted track number items (optionally filtered by warehouse) ready for SN# creation.</summary>
    [HttpGet("ready-for-sn")]
    [Authorize(Roles = "Admin,SuperAdmin,Expert")]
    public async Task<ActionResult<List<ReadyForSnItemResponse>>> GetReadyForSn(
        [FromQuery] long? warehouseId = null)
        => Ok(await _service.GetReadyForSnAsync(warehouseId));
}

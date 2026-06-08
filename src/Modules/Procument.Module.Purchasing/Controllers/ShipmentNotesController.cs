using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Procument.Module.Purchasing.DTOs;
using Procument.Module.Purchasing.Services;

namespace Procument.Module.Purchasing.Controllers;

[ApiController]
[Route("api/shipment-notes")]
[Authorize(Roles = "Admin,SuperAdmin,Inventory,Expert")]
public class ShipmentNotesController : ControllerBase
{
    private readonly IShipmentNoteService _service;
    private readonly IWarehouseService _warehouseService;

    public ShipmentNotesController(IShipmentNoteService service, IWarehouseService warehouseService)
    {
        _service = service;
        _warehouseService = warehouseService;
    }

    private long GetUserId()
    {
        var str = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!long.TryParse(str, out var id))
            throw new UnauthorizedAccessException("Invalid user token.");
        return id;
    }

    /// <summary>List SN#s. Inventory restricted to their warehouses. Expert sees only Ship To USA and beyond.</summary>
    [HttpGet]
    public async Task<ActionResult<List<ShipmentNoteResponse>>> GetAll(
        [FromQuery] long? warehouseId = null)
    {
        if (User.IsInRole("Inventory"))
        {
            var userId = GetUserId();
            var warehouseIds = await _warehouseService.GetWarehouseIdsForUserAsync(userId);
            return Ok(await _service.GetAllAsync(allowedWarehouseIds: warehouseIds));
        }
        if (User.IsInRole("Expert"))
        {
            var isSyd = User.Identity?.Name == "SYD";
            return Ok(await _service.GetAllAsync(expertView: !isSyd));
        }

        return Ok(await _service.GetAllAsync(warehouseId));
    }

    /// <summary>Get a single SN# with full detail.</summary>
    [HttpGet("{id:long}")]
    public async Task<ActionResult<ShipmentNoteResponse>> GetById(long id)
    {
        var result = await _service.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>Create a new SN# with selected accepted parts and linked track numbers.</summary>
    [HttpPost]
    [Authorize(Roles = "Admin,SuperAdmin,Expert")]
    public async Task<ActionResult<ShipmentNoteResponse>> Create([FromBody] CreateShipmentNoteRequest request)
    {
        if (request.WarehouseId == 0) return BadRequest("WarehouseId is required.");
        var result = await _service.CreateAsync(GetUserId(), request);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>Update SN# fields (TId, AWBNumber).</summary>
    [HttpPut("{id:long}")]
    [Authorize(Roles = "Admin,SuperAdmin,Expert")]
    public async Task<ActionResult<ShipmentNoteResponse>> Update(
        long id,
        [FromBody] UpdateShipmentNoteRequest request)
    {
        var result = await _service.UpdateAsync(id, request);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>Upload the SN# PDF document.</summary>
    [HttpPost("{id:long}/upload-pdf")]
    [Authorize(Roles = "Admin,SuperAdmin,Expert")]
    public async Task<ActionResult<ShipmentNoteResponse>> UploadPdf(long id, IFormFile file)
    {
        if (file == null || file.Length == 0) return BadRequest("No file provided.");
        var result = await _service.UploadPdfAsync(id, file);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>Download the SN# PDF.</summary>
    [HttpGet("{id:long}/pdf-file")]
    public async Task<IActionResult> DownloadPdf(long id)
    {
        var result = await _service.DownloadPdfAsync(id);
        if (result == null) return NotFound();
        var (stream, fileName, mimeType) = result.Value;
        return File(stream, mimeType, fileName);
    }

    /// <summary>Inventory users submit the AWB / carrier tracking number for a SN#.</summary>
    [HttpPatch("{id:long}/awb")]
    [Authorize(Roles = "Admin,SuperAdmin,Inventory")]
    public async Task<ActionResult<ShipmentNoteResponse>> UpdateAwb(
        long id,
        [FromBody] UpdateAwbRequest request)
    {
        var result = await _service.UpdateAwbAsync(id, request);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>Confirm the SN# (sets status to Confirmed).</summary>
    [HttpPost("{id:long}/confirm")]
    [Authorize(Roles = "Admin,SuperAdmin,Expert")]
    public async Task<IActionResult> Confirm(long id)
    {
        var ok = await _service.ConfirmAsync(id);
        return ok ? NoContent() : NotFound();
    }

    /// <summary>Add a track number to an existing SN#.</summary>
    [HttpPost("{id:long}/track-numbers/{trackNumberId:long}")]
    [Authorize(Roles = "Admin,SuperAdmin,Inventory,Expert")]
    public async Task<ActionResult<ShipmentNoteResponse>> AddTrackNumber(long id, long trackNumberId)
    {
        var result = await _service.AddTrackNumberAsync(id, trackNumberId);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>Remove a track number from a SN# (Admin only).</summary>
    [HttpDelete("{id:long}/track-numbers/{trackNumberId:long}")]
    [Authorize(Roles = "Admin,SuperAdmin,Expert")]
    public async Task<IActionResult> RemoveTrackNumber(long id, long trackNumberId)
    {
        var ok = await _service.RemoveTrackNumberAsync(id, trackNumberId);
        return ok ? NoContent() : NotFound();
    }

    // ── Boxes ──────────────────────────────────────────────────────────────

    /// <summary>Add a packing box to a SN#.</summary>
    [HttpPost("{id:long}/boxes")]
    [Authorize(Roles = "Admin,SuperAdmin,Inventory,Expert")]
    public async Task<ActionResult<SnBoxResponse>> AddBox(long id, [FromBody] SaveSnBoxRequest request)
    {
        var result = await _service.AddBoxAsync(id, request);
        return Ok(result);
    }

    /// <summary>Update a packing box on a SN#.</summary>
    [HttpPut("{id:long}/boxes/{boxId:long}")]
    [Authorize(Roles = "Admin,SuperAdmin,Inventory,Expert")]
    public async Task<ActionResult<SnBoxResponse>> UpdateBox(long id, long boxId, [FromBody] SaveSnBoxRequest request)
    {
        var result = await _service.UpdateBoxAsync(id, boxId, request);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>Delete a packing box from a SN#.</summary>
    [HttpDelete("{id:long}/boxes/{boxId:long}")]
    [Authorize(Roles = "Admin,SuperAdmin,Inventory,Expert")]
    public async Task<IActionResult> DeleteBox(long id, long boxId)
    {
        var ok = await _service.DeleteBoxAsync(id, boxId);
        return ok ? NoContent() : NotFound();
    }

    // ── Customs file ───────────────────────────────────────────────────────

    /// <summary>Upload customs document → sets status to Clearing Customs (CPT auto-advances to Delivered to Customer).</summary>
    [HttpPost("{id:long}/customs-file")]
    [Authorize(Roles = "Admin,SuperAdmin,Expert")]
    public async Task<ActionResult<ShipmentNoteResponse>> UploadCustomsFile(long id, IFormFile file)
    {
        if (file == null || file.Length == 0) return BadRequest("No file provided.");
        var result = await _service.UploadCustomsFileAsync(id, file);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>Download the customs document.</summary>
    [HttpGet("{id:long}/customs-file")]
    public async Task<IActionResult> DownloadCustomsFile(long id)
    {
        var note = await _service.GetByIdAsync(id);
        if (note == null || string.IsNullOrEmpty(note.CustomsFileName)) return NotFound();
        var folder = Path.Combine("Documents/ShipmentNotes", id.ToString());
        var path = Path.Combine(folder, note.CustomsFileName);
        if (!System.IO.File.Exists(path)) return NotFound();
        return PhysicalFile(Path.GetFullPath(path), "application/octet-stream", note.CustomsOriginalFileName ?? note.CustomsFileName);
    }

    // ── Manual status update ───────────────────────────────────────────────

    /// <summary>Admin manually advances SN# status (e.g. Received in Office → Delivered to Customer for DDP).</summary>
    [HttpPatch("{id:long}/status")]
    [Authorize(Roles = "Admin,SuperAdmin,Expert")]
    public async Task<ActionResult<ShipmentNoteResponse>> UpdateStatus(
        long id,
        [FromBody] UpdateShipmentNoteStatusRequest request)
    {
        var result = await _service.UpdateStatusAsync(id, request.Status);
        return result == null ? NotFound() : Ok(result);
    }
}

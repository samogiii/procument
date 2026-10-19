using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procument.Module.Identity.Entities;
using Procument.Module.Sales.DTOs;
using Procument.Module.Sales.Services;
using Procument.Shared.Audit;

namespace Procument.Module.Sales.Controllers;

/// <summary>
/// Customer Follow Ups — one shared, spreadsheet-style list. Granted per user in Menu Access
/// ("customerFollowUps"); SuperAdmin always passes.
/// </summary>
[ApiController]
[Route("api/customer-follow-ups")]
[Authorize]
public class CustomerFollowUpsController : ControllerBase
{
    public const string Feature = "customerFollowUps";

    private readonly ICustomerFollowUpService _service;
    private readonly DbContext _db;

    public CustomerFollowUpsController(ICustomerFollowUpService service, DbContext db)
    {
        _service = service;
        _db = db;
    }

    private string CurrentUserName => User.Identity?.Name ?? "System";

    private async Task<bool> HasAccessAsync()
    {
        if (User.IsInRole(UserRoles.SuperAdmin)) return true;
        var userName = User.Identity?.Name;
        return !string.IsNullOrEmpty(userName) && await _db.Set<MenuPermission>()
            .AnyAsync(p => p.Feature == Feature && p.UserName == userName);
    }

    [HttpGet]
    public async Task<ActionResult<List<CustomerFollowUpResponse>>> GetAll()
    {
        if (!await HasAccessAsync()) return Forbid();
        return Ok(await _service.GetAllAsync(CurrentUserName));
    }

    /// <summary>Dropdown values: catalog customer codes, user names and the status defaults, plus every value already used on a row.</summary>
    [HttpGet("options")]
    public async Task<ActionResult<CustomerFollowUpOptionsResponse>> GetOptions()
    {
        if (!await HasAccessAsync()) return Forbid();
        return Ok(await _service.GetOptionsAsync());
    }

    [HttpPost]
    [Auditable("CustomerFollowUp", "Create", CaptureBody = true)]
    public async Task<ActionResult<CustomerFollowUpResponse>> Create([FromBody] CreateCustomerFollowUpRequest? request)
    {
        if (!await HasAccessAsync()) return Forbid();
        try
        {
            return Ok(await _service.CreateAsync(request ?? new CreateCustomerFollowUpRequest(), CurrentUserName));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPatch("{id:long}")]
    [Auditable("CustomerFollowUp", "UpdateCell", CaptureBody = true)]
    public async Task<ActionResult<CustomerFollowUpResponse>> UpdateField(long id, [FromBody] UpdateCustomerFollowUpFieldRequest request)
    {
        if (!await HasAccessAsync()) return Forbid();
        try
        {
            var row = await _service.UpdateFieldAsync(id, request, CurrentUserName);
            return row == null ? NotFound() : Ok(row);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Deletes the row together with its documents (rows and files).</summary>
    [HttpDelete("{id:long}")]
    [Auditable("CustomerFollowUp", "Delete")]
    public async Task<IActionResult> Delete(long id)
    {
        if (!await HasAccessAsync()) return Forbid();
        return await _service.DeleteAsync(id) ? NoContent() : NotFound();
    }

    // ── Documents (PDFs per row) ─────────────────────────────────────────────

    [HttpGet("{id:long}/documents")]
    public async Task<ActionResult<List<CustomerFollowUpDocumentResponse>>> GetDocuments(long id)
    {
        if (!await HasAccessAsync()) return Forbid();
        var documents = await _service.GetDocumentsAsync(id);
        return documents == null ? NotFound() : Ok(documents);
    }

    [HttpPost("{id:long}/documents")]
    [RequestSizeLimit(100_000_000)]
    [Auditable("CustomerFollowUp", "UploadDocument")]
    public async Task<ActionResult<List<CustomerFollowUpDocumentResponse>>> UploadDocuments(long id, [FromForm] List<IFormFile> files)
    {
        if (!await HasAccessAsync()) return Forbid();
        try
        {
            var documents = await _service.UploadDocumentsAsync(id, files, CurrentUserName);
            return documents == null ? NotFound() : Ok(documents);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id:long}/documents/{documentId:long}/download")]
    public async Task<IActionResult> DownloadDocument(long id, long documentId)
    {
        if (!await HasAccessAsync()) return Forbid();
        var result = await _service.OpenDocumentAsync(id, documentId);
        if (result == null) return NotFound();
        var (stream, fileName, mimeType) = result.Value;
        return File(stream, mimeType, fileName);
    }

    [HttpDelete("{id:long}/documents/{documentId:long}")]
    [Auditable("CustomerFollowUp", "DeleteDocument")]
    public async Task<IActionResult> DeleteDocument(long id, long documentId)
    {
        if (!await HasAccessAsync()) return Forbid();
        return await _service.DeleteDocumentAsync(id, documentId) ? NoContent() : NotFound();
    }

    // ── Messages (chat per row) ──────────────────────────────────────────────

    [HttpGet("{id:long}/messages")]
    public async Task<ActionResult<List<CustomerFollowUpMessageResponse>>> GetMessages(long id)
    {
        if (!await HasAccessAsync()) return Forbid();
        var messages = await _service.GetMessagesAsync(id);
        return messages == null ? NotFound() : Ok(messages);
    }

    /// <summary>Multipart: optional "text" and optional PDF / photo "files" (saved as row documents).</summary>
    [HttpPost("{id:long}/messages")]
    [RequestSizeLimit(100_000_000)]
    [Auditable("CustomerFollowUp", "SendMessage")]
    public async Task<ActionResult<List<CustomerFollowUpMessageResponse>>> SendMessage(long id, [FromForm] string? text, [FromForm] List<IFormFile>? files)
    {
        if (!await HasAccessAsync()) return Forbid();
        try
        {
            var messages = await _service.SendMessageAsync(id, text, files ?? [], CurrentUserName);
            return messages == null ? NotFound() : Ok(messages);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Message count + unread flag (for the current user) of every row with a chat — polled by the page.</summary>
    [HttpGet("message-status")]
    public async Task<ActionResult<List<CustomerFollowUpMessageStatusResponse>>> GetMessageStatuses()
    {
        if (!await HasAccessAsync()) return Forbid();
        return Ok(await _service.GetMessageStatusesAsync(CurrentUserName));
    }

    /// <summary>The current user has seen the row's chat.</summary>
    [HttpPut("{id:long}/messages/read")]
    public async Task<IActionResult> MarkMessagesRead(long id)
    {
        if (!await HasAccessAsync()) return Forbid();
        return await _service.MarkMessagesReadAsync(id, CurrentUserName) ? NoContent() : NotFound();
    }

    /// <summary>Flags the row's chat unread for the current user only.</summary>
    [HttpPut("{id:long}/messages/unread")]
    public async Task<IActionResult> MarkMessagesUnread(long id)
    {
        if (!await HasAccessAsync()) return Forbid();
        return await _service.MarkMessagesUnreadAsync(id, CurrentUserName) ? NoContent() : NotFound();
    }

    /// <summary>Only the sender can delete a message; its document goes with it.</summary>
    [HttpDelete("{id:long}/messages/{messageId:long}")]
    [Auditable("CustomerFollowUp", "DeleteMessage")]
    public async Task<IActionResult> DeleteMessage(long id, long messageId)
    {
        if (!await HasAccessAsync()) return Forbid();
        return await _service.DeleteMessageAsync(id, messageId, CurrentUserName) switch
        {
            DeleteMessageResult.Deleted => NoContent(),
            DeleteMessageResult.NotSender => StatusCode(StatusCodes.Status403Forbidden, new { message = "Only the sender can delete this message." }),
            _ => NotFound(),
        };
    }
}

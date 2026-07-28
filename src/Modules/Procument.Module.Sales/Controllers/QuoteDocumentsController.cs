using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Procument.Module.Sales.Entities;
using Procument.Shared.Services;

namespace Procument.Module.Sales.Controllers;

[ApiController]
[Route("api/quotes/{quoteId:long}/documents")]
[Authorize(Roles = "Admin,SuperAdmin,Expert")]
public class QuoteDocumentsController : ControllerBase
{
    private readonly DbContext _db;
    private readonly IDocumentStorageService _storage;

    // Maps category key → folder name
    private static readonly Dictionary<string, string> CategoryFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        ["pdf"] = "PDF",
        ["excel"] = "Excel",
        ["uploaded"] = "Uploaded",
    };

    public QuoteDocumentsController(DbContext db, IDocumentStorageService storage)
    {
        _db = db;
        _storage = storage;
    }

    [HttpGet]
    public async Task<IActionResult> List(long quoteId)
    {
        var quote = await _db.Set<Quote>().FirstOrDefaultAsync(q => q.Id == quoteId);
        if (quote == null) return NotFound();

        var categoryPairs = CategoryFolders.Select(kv => (kv.Key, kv.Value));
        var files = _storage.ListFilesInQuoteCategories(quote.QuoteNumber, categoryPairs)
            .OrderByDescending(f => f.ModifiedAt)
            .ToList();

        return Ok(new { quoteId, quoteNumber = quote.QuoteNumber, files });
    }

    /// <summary>Persist a copy of a generated (PDF/Excel) or manually-uploaded file for this quote.
    /// Category is inferred from the file extension when not explicitly provided.</summary>
    [HttpPost("upload")]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> Upload(long quoteId, [FromForm] IFormFile file, [FromForm] string? category = null)
    {
        if (file == null || file.Length == 0) return BadRequest("No file uploaded.");
        var quote = await _db.Set<Quote>().FirstOrDefaultAsync(q => q.Id == quoteId);
        if (quote == null) return NotFound();

        var resolvedCategory = ResolveCategory(category, file.FileName);
        if (!CategoryFolders.TryGetValue(resolvedCategory, out var categoryFolder))
            return BadRequest("Invalid category. Valid values: " + string.Join(", ", CategoryFolders.Keys));

        using var stream = file.OpenReadStream();
        var savedName = _storage.SaveFileInQuoteCategory(quote.QuoteNumber, categoryFolder, file.FileName, stream);

        return Ok(new { fileName = savedName, category = resolvedCategory });
    }

    [HttpGet("file")]
    public async Task<IActionResult> Download(long quoteId, [FromQuery] string name, [FromQuery] string category)
    {
        var quote = await _db.Set<Quote>().FirstOrDefaultAsync(q => q.Id == quoteId);
        if (quote == null) return NotFound();
        if (!CategoryFolders.TryGetValue(category, out var categoryFolder)) return BadRequest("Invalid category.");

        var result = _storage.OpenFileInQuoteCategory(quote.QuoteNumber, categoryFolder, name);
        if (result == null) return NotFound();
        return File(result.Value.Stream, GetContentType(result.Value.AbsolutePath), name);
    }

    [HttpDelete("file")]
    public async Task<IActionResult> Delete(long quoteId, [FromQuery] string name, [FromQuery] string category)
    {
        var quote = await _db.Set<Quote>().FirstOrDefaultAsync(q => q.Id == quoteId);
        if (quote == null) return NotFound();
        if (!CategoryFolders.TryGetValue(category, out var categoryFolder)) return BadRequest("Invalid category.");

        var ok = _storage.DeleteFileInQuoteCategory(quote.QuoteNumber, categoryFolder, name);
        return ok ? Ok() : NotFound();
    }

    private static string ResolveCategory(string? category, string fileName)
    {
        if (!string.IsNullOrWhiteSpace(category)) return category;
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".pdf" => "pdf",
            ".xlsx" or ".xls" => "excel",
            _ => "uploaded",
        };
    }

    private static string GetContentType(string path)
    {
        var provider = new FileExtensionContentTypeProvider();
        if (!provider.TryGetContentType(path, out var contentType))
            contentType = "application/octet-stream";
        return contentType;
    }
}

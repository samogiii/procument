using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Procument.Module.Sales.DTOs;
using Procument.Module.Sales.Services;

namespace Procument.Module.Sales.Controllers;

[ApiController]
[Route("api/wallet-transfers")]
[Authorize(Roles = "SuperAdmin,Admin,Payment")]
public class WalletTransfersController : ControllerBase
{
    private readonly IWalletTransferService _service;
    private readonly string _storageRoot;

    public WalletTransfersController(IWalletTransferService service, IConfiguration config)
    {
        _service = service;
        var configured = config["DocumentStorage:ProformaInvoicesRoot"];
        var base64Dir = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(AppContext.BaseDirectory, "Documents")
            : Path.GetDirectoryName(
                Path.IsPathRooted(configured)
                    ? configured
                    : Path.Combine(AppContext.BaseDirectory, configured))!;
        _storageRoot = base64Dir;
    }

    [HttpGet]
    public async Task<ActionResult<List<WalletTransferPendingResponse>>> GetAll()
        => Ok(await _service.GetAllAsync());

    [HttpPost]
    public async Task<ActionResult<WalletTransferPendingResponse>> Create([FromBody] CreateWalletTransferPendingRequest req)
    {
        var userId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        return Ok(await _service.CreateAsync(req, userId));
    }

    [HttpPatch("{id:long}/review")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> Review(long id, [FromBody] ReviewWalletTransferRequest req)
    {
        var userId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        if (!await _service.ReviewAsync(id, req.Decision, req.Note, userId)) return NotFound();
        return Ok();
    }

    [HttpPost("{id:long}/upload-pop")]
    public async Task<IActionResult> UploadPop(long id, IFormFile file)
    {
        var userId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        if (!await _service.UploadPopAndExecuteAsync(id, file, userId, _storageRoot))
            return BadRequest(new { message = "Transfer not found or not in Accepted state." });
        return Ok();
    }

    [HttpGet("{id:long}/pop-file")]
    public async Task<IActionResult> GetPopFile(long id)
    {
        var result = await _service.GetPopFileAsync(id, _storageRoot);
        if (result == null) return NotFound();
        return File(result.Value.Stream, result.Value.MimeType, result.Value.FileName);
    }
}

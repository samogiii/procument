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
    private readonly PopReaderSignal _reader;
    private readonly string _storageRoot;

    public WalletTransfersController(IWalletTransferService service, PopReaderSignal reader, IConfiguration config)
    {
        _service = service;
        _reader = reader;
        _storageRoot = WalletTransferService.StorageRoot(config);
    }

    private long UserId => long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

    [HttpGet]
    public async Task<ActionResult<List<WalletTransferPendingResponse>>> GetAll()
        => Ok(await _service.GetAllAsync());

    [HttpPost]
    public async Task<ActionResult<WalletTransferPendingResponse>> Create([FromBody] CreateWalletTransferPendingRequest req)
    {
        try { return Ok(await _service.CreateAsync(req, UserId)); }
        catch (InvalidOperationException e) { return BadRequest(new { message = e.Message }); }
    }

    /// <summary>POP uploaded here instead of on the phone: it is read and waits for Accept like a warehouse one.</summary>
    [HttpPost("{id:long}/upload-pop")]
    public async Task<IActionResult> UploadPop(long id, IFormFile file)
    {
        if (!await _service.UploadPopAsync(id, file, UserId, _storageRoot))
            return BadRequest(new { message = "Transfer not found or already completed." });
        _reader.Poke();
        return Ok();
    }

    /// <summary>The office's final numbers (the POP reader's proposal, maybe changed): moves the money.</summary>
    [HttpPost("{id:long}/accept")]
    public async Task<IActionResult> Accept(long id, [FromBody] AcceptWalletTransferRequest req)
    {
        var error = await _service.AcceptAsync(id, req, UserId);
        return error == null ? Ok(await _service.GetByIdAsync(id)) : BadRequest(new { message = error });
    }

    [HttpGet("{id:long}/pop-file")]
    public async Task<IActionResult> GetPopFile(long id)
    {
        var result = await _service.GetPopFileAsync(id, _storageRoot);
        if (result == null) return NotFound();
        return File(result.Value.Stream, result.Value.MimeType, result.Value.FileName);
    }
}

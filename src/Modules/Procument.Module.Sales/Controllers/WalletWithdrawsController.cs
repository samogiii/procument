using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;
using Procument.Module.Sales.DTOs;
using Procument.Module.Sales.Services;

namespace Procument.Module.Sales.Controllers;

/// <summary>Wallets → Ready To Withdraw: withdraws the warehouse pays (see WalletWithdrawService).</summary>
[ApiController]
[Route("api/wallet-withdraws")]
[Authorize(Roles = "SuperAdmin,Admin,Payment")]
public class WalletWithdrawsController : ControllerBase
{
    private readonly IWalletWithdrawService _service;
    private readonly string _storageRoot;

    public WalletWithdrawsController(IWalletWithdrawService service, IConfiguration config)
    {
        _service = service;
        _storageRoot = WalletTransferService.StorageRoot(config);
    }

    [HttpGet]
    public async Task<ActionResult<List<WalletWithdrawResponse>>> GetAll() => Ok(await _service.GetAllAsync());

    /// <summary>Same rights as adding a wallet transaction.</summary>
    [HttpPost]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<ActionResult<WalletWithdrawResponse>> Create([FromBody] CreateWalletWithdrawRequest req)
    {
        var userId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        try { return Ok(await _service.CreateAsync(req, userId)); }
        catch (InvalidOperationException e) { return BadRequest(new { message = e.Message }); }
    }

    [HttpDelete("{id:long}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Delete(long id)
        => await _service.DeleteAsync(id) ? Ok() : BadRequest(new { message = "Not found, or the warehouse already paid it." });

    /// <summary>The office's final numbers (the POP reader's proposal, maybe changed): records the withdraw and its fees.</summary>
    [HttpPost("{id:long}/accept")]
    public async Task<IActionResult> Accept(long id, [FromBody] AcceptWalletWithdrawRequest req)
    {
        var error = await _service.AcceptAsync(id, req);
        return error == null ? Ok((await _service.GetAllAsync()).FirstOrDefault(w => w.Id == id)) : BadRequest(new { message = error });
    }

    [HttpGet("{id:long}/pop-file")]
    public async Task<IActionResult> GetPopFile(long id)
    {
        var result = await _service.GetPopFileAsync(id, _storageRoot);
        if (result == null) return NotFound();
        var mime = new FileExtensionContentTypeProvider().TryGetContentType(result.Value.FileName, out var m) ? m : "application/octet-stream";
        return File(result.Value.Stream, mime, result.Value.FileName);
    }
}

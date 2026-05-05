using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procument.API.Services;
using Procument.Data;
using Procument.Shared.Entities;

namespace Procument.API.Controllers;

[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("api/[controller]")]
public class SyncController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ISyncService _syncService;

    public SyncController(AppDbContext db, ISyncService syncService)
    {
        _db = db;
        _syncService = syncService;
    }

    [HttpGet("nodes")]
    public async Task<IActionResult> GetNodes()
    {
        var nodes = await _db.SatelliteNodes.ToListAsync();
        return Ok(nodes);
    }

    [HttpPost("nodes")]
    public async Task<IActionResult> CreateNode(SatelliteNode node)
    {
        _db.SatelliteNodes.Add(node);
        await _db.SaveChangesAsync();
        return Ok(node);
    }

    [HttpPut("nodes/{id}")]
    public async Task<IActionResult> UpdateNode(long id, SatelliteNode node)
    {
        if (id != node.Id) return BadRequest("ID mismatch");
        
        var existing = await _db.SatelliteNodes.FindAsync(id);
        if (existing == null) return NotFound();

        existing.Name = node.Name;
        existing.BaseNumber = node.BaseNumber;
        existing.EndpointUrl = node.EndpointUrl;
        existing.PublicKey = node.PublicKey;
        existing.ModifyAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(existing);
    }

    [HttpDelete("nodes/{id}")]
    public async Task<IActionResult> DeleteNode(long id)
    {
        var node = await _db.SatelliteNodes.FindAsync(id);
        if (node == null) return NotFound();
        _db.SatelliteNodes.Remove(node);
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("trigger/{nodeId}")]
    public async Task<IActionResult> TriggerSync(long nodeId)
    {
        try
        {
            var result = await _syncService.SyncWithSatelliteAsync(nodeId);
            return Ok(new { message = result });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("trigger-all")]
    public async Task<IActionResult> TriggerSyncAll()
    {
        try
        {
            var results = await _syncService.SyncAllAsync();
            return Ok(results);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("generate-keys")]
    public async Task<IActionResult> GenerateKeys()
    {
        var keys = await _syncService.GenerateMainKeysAsync();
        return Ok(new { privateKey = keys.PrivateKey, publicKey = keys.PublicKey });
    }
}

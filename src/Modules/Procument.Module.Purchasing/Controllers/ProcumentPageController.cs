using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Procument.Module.Purchasing.DTOs;
using Procument.Module.Purchasing.Services;

namespace Procument.Module.Purchasing.Controllers;

[ApiController]
[Route("api/procument-page")]
[Authorize(Roles = "Admin,Expert")]
public class ProcumentPageController : ControllerBase
{
    private readonly IProcumentPageService _service;

    public ProcumentPageController(IProcumentPageService service)
    {
        _service = service;
    }

    /// <summary>Get all RFQ items with their supplier quotes for the Procument page.</summary>
    [HttpGet]
    public async Task<ActionResult<List<ProcumentPageItemResponse>>> GetAll()
    {
        var (userId, isAdmin) = GetUserContext();
        var result = await _service.GetAllItemsAsync(userId, isAdmin);
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
        bool isAdmin = User.IsInRole("Admin");
        return (userId, isAdmin);
    }
}

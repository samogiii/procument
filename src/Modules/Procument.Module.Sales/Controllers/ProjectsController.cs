using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Procument.Module.Sales.Services;
using Procument.Shared.DTOs;

namespace Procument.Module.Sales.Controllers;

[ApiController]
[Route("api/projects")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class ProjectsController : ControllerBase
{
    private readonly IProjectsService _service;

    public ProjectsController(IProjectsService service) { _service = service; }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        [FromQuery] string? status = null,
        [FromQuery] string? customer = null,
        [FromQuery] string? search = null)
    {
        var pq = new PageQuery { Page = page, PageSize = pageSize, Search = search };
        var result = await _service.GetAsync(pq, status, customer);
        return Ok(result);
    }
}

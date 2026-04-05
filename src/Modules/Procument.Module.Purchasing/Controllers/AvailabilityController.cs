using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Procument.Module.Purchasing.DTOs;
using Procument.Module.Purchasing.Services;

namespace Procument.Module.Purchasing.Controllers;

[ApiController]
[Route("api/availability")]
[Authorize]
public class AvailabilityController : ControllerBase
{
    private readonly IAvailabilityService _availabilityService;

    public AvailabilityController(IAvailabilityService availabilityService)
    {
        _availabilityService = availabilityService;
    }

    [HttpPost("parts")]
    public async Task<IActionResult> GetPartAvailability([FromBody] PartAvailabilityRequest request)
    {
        var result = await _availabilityService.GetPartAvailabilityAsync(request.PartNumberIds);
        return Ok(result);
    }
}

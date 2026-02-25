using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Procument.API.Services;

namespace Procument.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpGet]
    public async Task<ActionResult<List<NotificationDto>>> GetAll()
    {
        var userId = GetUserId();
        var notifications = await _notificationService.GetForUserAsync(userId);
        return Ok(notifications);
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<int>> GetUnreadCount()
    {
        var userId = GetUserId();
        var count = await _notificationService.GetUnreadCountAsync(userId);
        return Ok(count);
    }

    [HttpGet("rejections")]
    public async Task<ActionResult<List<NotificationDto>>> GetUndismissedRejections()
    {
        var userId = GetUserId();
        var rejections = await _notificationService.GetUndismissedRejectionsAsync(userId);
        return Ok(rejections);
    }

    [HttpPatch("{id:long}/read")]
    public async Task<IActionResult> MarkRead(long id)
    {
        var userId = GetUserId();
        await _notificationService.MarkReadAsync(userId, id);
        return Ok();
    }

    [HttpPatch("{id:long}/dismiss")]
    public async Task<IActionResult> Dismiss(long id)
    {
        var userId = GetUserId();
        await _notificationService.DismissAsync(userId, id);
        return Ok();
    }

    [HttpPost("dismiss-rejections")]
    public async Task<IActionResult> DismissAllRejections()
    {
        var userId = GetUserId();
        await _notificationService.DismissAllRejectionsAsync(userId);
        return Ok();
    }

    private long GetUserId()
    {
        var idClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        return idClaim != null && long.TryParse(idClaim.Value, out var id) ? id : 0;
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Procument.API.Services;

namespace Procument.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly NotificationService _notificationService;

    public NotificationsController(NotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpGet]
    public async Task<ActionResult<List<NotificationDto>>> GetAll()
    {
        var userId = GetUserId();
        return Ok(await _notificationService.GetForUserAsync(userId));
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<int>> GetUnreadCount()
    {
        return Ok(await _notificationService.GetUnreadCountAsync(GetUserId()));
    }

    [HttpGet("rejections")]
    public async Task<ActionResult<List<NotificationDto>>> GetUndismissedRejections()
    {
        return Ok(await _notificationService.GetUndismissedRejectionsAsync(GetUserId()));
    }

    [HttpPatch("{id:long}/read")]
    public async Task<IActionResult> MarkRead(long id)
    {
        await _notificationService.MarkReadAsync(GetUserId(), id);
        return Ok();
    }

    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        await _notificationService.MarkAllReadAsync(GetUserId());
        return Ok();
    }

    [HttpPatch("{id:long}/dismiss")]
    public async Task<IActionResult> Dismiss(long id)
    {
        await _notificationService.DismissAsync(GetUserId(), id);
        return Ok();
    }

    [HttpPost("dismiss-rejections")]
    public async Task<IActionResult> DismissAllRejections()
    {
        await _notificationService.DismissAllRejectionsAsync(GetUserId());
        return Ok();
    }

    // ── Browser Push ──────────────────────────────────────────────────────────

    [HttpGet("vapid-public-key")]
    public IActionResult GetVapidPublicKey()
    {
        var key = _notificationService.GetVapidPublicKey();
        if (key == null) return NotFound("VAPID not configured");
        return Ok(new { publicKey = key });
    }

    [HttpPost("push-subscribe")]
    public async Task<IActionResult> Subscribe([FromBody] PushSubscribeRequest req)
    {
        await _notificationService.SubscribePushAsync(GetUserId(), req.Endpoint, req.P256dh, req.Auth);
        return Ok();
    }

    [HttpPost("push-unsubscribe")]
    public async Task<IActionResult> Unsubscribe([FromBody] PushUnsubscribeRequest req)
    {
        await _notificationService.UnsubscribePushAsync(GetUserId(), req.Endpoint);
        return Ok();
    }

    private long GetUserId()
    {
        var idClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        return idClaim != null && long.TryParse(idClaim.Value, out var id) ? id : 0;
    }
}

public record PushSubscribeRequest(string Endpoint, string P256dh, string Auth);
public record PushUnsubscribeRequest(string Endpoint);

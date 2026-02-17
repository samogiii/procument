using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procument.Data;

namespace Procument.API.Controllers;

/// <summary>
/// Dashboard controller — returns aggregated stats for the main overview page.
/// Admins get recent audit activity; regular users get stats only.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _db;

    public DashboardController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<DashboardResponse>> Get()
    {
        var isAdmin = User.IsInRole("Admin");

        // Aggregate counts in parallel
        var totalRfqs = await _db.RFQs.CountAsync();
        var totalQuotes = await _db.Quotes.CountAsync();
        var pendingQuotes = await _db.Quotes.CountAsync(q => q.Status == "Draft");
        var totalUsers = await _db.Users.CountAsync();

        // "Pending RFQs" = RFQs that have zero linked quotes
        var pendingRfqs = await _db.RFQs
            .Where(r => !_db.Quotes.Any(q => q.RFQId == r.Id))
            .CountAsync();

        var response = new DashboardResponse
        {
            TotalRfqs = totalRfqs,
            TotalQuotes = totalQuotes,
            PendingRfqs = pendingRfqs,
            PendingQuotes = pendingQuotes,
            TotalUsers = totalUsers,
        };

        // Only admins get recent audit activity
        if (isAdmin)
        {
            response.RecentActivity = await _db.AuditLogs
                .OrderByDescending(a => a.Timestamp)
                .Take(20)
                .Select(a => new AuditActivityItem
                {
                    Id = a.Id,
                    Action = a.Action,
                    EntityName = a.EntityName,
                    EntityId = a.EntityId,
                    Details = a.Details,
                    UserName = a.UserName,
                    Timestamp = a.Timestamp,
                })
                .ToListAsync();
        }

        return Ok(response);
    }
}

// ─── Response DTOs ──────────────────────────────────────────

public class DashboardResponse
{
    public int TotalRfqs { get; set; }
    public int TotalQuotes { get; set; }
    public int PendingRfqs { get; set; }
    public int PendingQuotes { get; set; }
    public int TotalUsers { get; set; }
    public List<AuditActivityItem>? RecentActivity { get; set; }
}

public class AuditActivityItem
{
    public long Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string? UserName { get; set; }
    public DateTime Timestamp { get; set; }
}

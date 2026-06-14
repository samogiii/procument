using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Procument.Module.RFQ.Entities;

namespace Procument.Module.RFQ.Services;

/// <summary>
/// Runs once a day. Any RFQ that is still Open or In Progress but whose deadline
/// (LeadTime) passed more than 1 month ago is automatically moved to "No Quote".
/// </summary>
public class RfqAutoExpireService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<RfqAutoExpireService> _logger;

    public RfqAutoExpireService(IServiceProvider services, ILogger<RfqAutoExpireService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExpireOverdueRfqsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RfqAutoExpireService failed");
            }

            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }

    private async Task ExpireOverdueRfqsAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbContext>();

        var cutoff = DateTime.UtcNow.AddMonths(-1);

        var expired = await db.Set<RFQHeader>()
            .Where(r => (r.Status == "Open" || r.Status == "In Progress")
                     && r.LeadTime < cutoff)
            .ToListAsync(ct);

        if (expired.Count == 0) return;

        foreach (var rfq in expired)
        {
            rfq.Status = "No Quote";
            rfq.NoQuoteReason = "Auto-expired: deadline was over 1 month ago";
            rfq.ModifyAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("RfqAutoExpireService: marked {Count} RFQ(s) as No Quote", expired.Count);
    }
}

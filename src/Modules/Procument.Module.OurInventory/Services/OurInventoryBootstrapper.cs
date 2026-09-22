using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Procument.Module.Catalog.Entities;

namespace Procument.Module.OurInventory.Services;

/// <summary>
/// Creates catalog records required before the stock workflow is available.
/// This is intentionally idempotent so it is safe to run at every API startup.
/// </summary>
public sealed class OurInventoryBootstrapper : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public OurInventoryBootstrapper(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DbContext>();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<OurInventoryOptions>>().Value;
        var supplierName = options.OurStockSupplierName.Trim();
        var normalizedName = supplierName.ToUpper();

        var exists = await db.Set<Supplier>()
            .AnyAsync(s => s.Name.ToUpper() == normalizedName, cancellationToken);

        if (exists)
            return;

        db.Set<Supplier>().Add(new Supplier
        {
            Name = supplierName,
            Description = "Internal supplier used for quotes fulfilled from our own stock.",
            Status = "Approved",
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Procument.Data;
using Procument.Module.Catalog.Entities;
using Procument.Module.Identity.Entities;
using Procument.Module.OurInventory;
using Procument.Module.OurInventory.DTOs;
using Procument.Module.OurInventory.Services;
using Procument.Module.Purchasing;
using Procument.Module.Purchasing.Entities;
using Procument.Module.Purchasing.Services;
using Procument.Shared.Services;
using Xunit;

namespace Procument.OurInventory.Tests;

/// <summary>
/// Creates a throwaway SQL Server database from all migrations, wires the real module services
/// (with EnableRetryOnFailure, as in production) and drops the database afterwards.
///
/// Set <c>PROCUMENT_TEST_SQL</c> to a SQL Server connection string whose login may create databases,
/// e.g. <c>Data Source=.;User ID=sa;Password=…;TrustServerCertificate=True</c>. Without it every test is skipped.
/// </summary>
public sealed class StockDatabaseFixture : IAsyncLifetime
{
    public const string EnvVar = "PROCUMENT_TEST_SQL";

    public bool Available { get; private set; }
    public ServiceProvider Services { get; private set; } = null!;
    public long UserId { get; private set; }
    public long SupplierId { get; private set; }
    public long PresetId { get; private set; }
    public long MainWarehouseId { get; private set; }
    public long SecondWarehouseId { get; private set; }

    private string? _databaseName;

    public async Task InitializeAsync()
    {
        var baseCs = Environment.GetEnvironmentVariable(EnvVar);
        if (string.IsNullOrWhiteSpace(baseCs)) return;

        _databaseName = $"ProcumentTest_{Guid.NewGuid():N}";
        var cs = new SqlConnectionStringBuilder(baseCs) { InitialCatalog = _databaseName }.ConnectionString;

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddDbContext<AppDbContext>(o => o.UseSqlServer(cs, s =>
        {
            s.MigrationsAssembly("Procument.Data");
            s.EnableRetryOnFailure(3);
        }));
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<INotificationService, NoopNotifications>();
        services.AddSingleton<IDocumentStorageService, DocumentStorageService>();
        Procument.Module.Identity.IdentityModule.AddIdentityModule(services);
        services.AddPurchasingModule();
        Procument.Module.Sales.SalesModule.AddSalesModule(services);
        services.AddOurInventoryModule(new ConfigurationBuilder().Build());
        Services = services.BuildServiceProvider();

        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        var user = new User { Name = "Test User", Email = "test@ourinventory.local", Password = "x.x", Role = "Admin", IsActive = true };
        var supplier = new Supplier { Name = "Test Supplier", Status = "Approved", IsActive = true };
        var preset = new CompanyPreset { Name = "Test Company", IsActive = true };
        var main = new Warehouse { Name = "Main", IsActive = true };
        var second = new Warehouse { Name = "Second", IsActive = true };
        db.AddRange(user, supplier, preset, main, second);
        await db.SaveChangesAsync();
        (UserId, SupplierId, PresetId, MainWarehouseId, SecondWarehouseId) = (user.Id, supplier.Id, preset.Id, main.Id, second.Id);
        Available = true;
    }

    public async Task DisposeAsync()
    {
        if (!Available || _databaseName is null) return;
        await using (var scope = Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
        await Services.DisposeAsync();
    }

    public AsyncServiceScope Scope() => Services.CreateAsyncScope();

    public void RequireDatabase() => Skip.IfNot(Available, $"Set {EnvVar} to run the Our Inventory database tests.");

    /// <summary>A part number unique to the calling test, so tests sharing the database never collide.</summary>
    public async Task<PartNumber> NewPartAsync(string? name = null)
    {
        await using var scope = Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var part = new PartNumber { Name = name ?? $"PN-{Guid.NewGuid():N}"[..14], Description = "Test part" };
        db.Add(part);
        await db.SaveChangesAsync();
        return part;
    }

    /// <summary>Creates an approved Stock PO through the real service, then approves it.</summary>
    public async Task<StockPurchaseOrderResponse> CreateApprovedStockPoAsync(long warehouseId, params (long PartId, int Qty, decimal Price, string Condition)[] lines)
    {
        await using var scope = Scope();
        var service = scope.ServiceProvider.GetRequiredService<IStockPurchaseOrderService>();
        var po = await service.CreateAsync(new SaveStockPurchaseOrderRequest
        {
            SupplierId = SupplierId, CompanyPresetId = PresetId, DestinationWarehouseId = warehouseId,
            Items = lines.Select(l => new StockPurchaseOrderLineRequest { PartNumberId = l.PartId, Qty = l.Qty, UnitPrice = l.Price, Condition = l.Condition }).ToList(),
        });
        await service.SubmitAsync(po.Id);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ChangeTracker.Clear();
        var entity = await db.PurchaseOrders.FirstAsync(p => p.Id == po.Id);
        entity.AdminApproval = "Approved";
        entity.Status = PurchaseOrderStatusFlow.PaymentDone;
        await db.SaveChangesAsync();
        return (await service.GetByIdAsync(po.Id))!;
    }

    /// <summary>A supplier track for one PO line, at the given warehouse.</summary>
    public async Task<long> NewTrackAsync(long poItemId, long warehouseId)
    {
        await using var scope = Scope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var track = new POItemTrackNumber { TrackNumber = $"TRK-{Guid.NewGuid():N}"[..14], POItemId = poItemId, WarehouseId = warehouseId, Status = "Ship to Warehouse" };
        db.Add(track);
        await db.SaveChangesAsync();
        return track.Id;
    }

    public async Task<(decimal OnHand, decimal Reserved, decimal Avg)> LotAsync(long partId, long warehouseId)
    {
        await using var scope = Scope();
        var lot = await scope.ServiceProvider.GetRequiredService<AppDbContext>().OurStockItems.AsNoTracking()
            .FirstOrDefaultAsync(l => l.PartNumberId == partId && l.WarehouseId == warehouseId);
        return lot is null ? (0, 0, 0) : (lot.QtyOnHand, lot.QtyReserved, lot.AvgUnitCost);
    }

    public async Task<long> LotIdAsync(long partId, long warehouseId)
    {
        await using var scope = Scope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().OurStockItems
            .Where(l => l.PartNumberId == partId && l.WarehouseId == warehouseId).Select(l => l.Id).FirstAsync();
    }

    private sealed class NoopNotifications : INotificationService
    {
        public Task CreateAsync(long userId, string type, string entityName, long entityId, string entityNumber, string message, long? triggeredByUserId = null, string? triggeredByUserName = null) => Task.CompletedTask;
        public Task CreateForUsersAsync(IEnumerable<long> userIds, string type, string entityName, long entityId, string entityNumber, string message, long? triggeredByUserId = null, string? triggeredByUserName = null) => Task.CompletedTask;
        public Task CreateForAllAdminsAsync(string type, string entityName, long entityId, string entityNumber, string message, long? triggeredByUserId = null, string? triggeredByUserName = null) => Task.CompletedTask;
    }
}

[CollectionDefinition(Name)]
public sealed class StockDatabaseCollection : ICollectionFixture<StockDatabaseFixture>
{
    public const string Name = "Our Inventory database";
}

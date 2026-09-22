using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Procument.Module.OurInventory.Services;
using Procument.Module.Purchasing.Services;
using Procument.Shared.Services;

namespace Procument.Module.OurInventory;

public static class OurInventoryModule
{
    public static IServiceCollection AddOurInventoryModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<OurInventoryOptions>()
            .Bind(configuration.GetSection(OurInventoryOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.StockPoNumberPrefix),
                "OurInventory:StockPoNumberPrefix is required.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.OurStockSupplierName),
                "OurInventory:OurStockSupplierName is required.")
            .Validate(o => o.QuoteReservationDays > 0,
                "OurInventory:QuoteReservationDays must be greater than zero.")
            .ValidateOnStart();

        services.AddHostedService<OurInventoryBootstrapper>();
        services.AddScoped<IPartAvailabilitySource, StockAvailabilitySource>();
        services.AddScoped<IStockLedgerService, StockLedgerService>();
        services.AddScoped<StockReceiptService>();
        services.AddScoped<IStockReceiptService>(sp => sp.GetRequiredService<StockReceiptService>());
        services.AddScoped<IStockReceiptHandler>(sp => sp.GetRequiredService<StockReceiptService>());
        services.AddScoped<IStockReservationService, StockReservationService>();
        services.AddScoped<IStockPurchaseOrderService, StockPurchaseOrderService>();
        services.AddScoped<IStockQueryService, StockQueryService>();

        return services;
    }
}

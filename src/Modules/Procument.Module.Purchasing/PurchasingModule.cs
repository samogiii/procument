using Microsoft.Extensions.DependencyInjection;
using Procument.Module.Purchasing.Services;

namespace Procument.Module.Purchasing;

public static class PurchasingModule
{
    public static IServiceCollection AddPurchasingModule(this IServiceCollection services)
    {
        services.AddScoped<ISupplierQuoteService, SupplierQuoteService>();
        services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
        return services;
    }
}

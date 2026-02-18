using Microsoft.Extensions.DependencyInjection;
using Procument.Module.Sales.Services;

namespace Procument.Module.Sales;

public static class SalesModule
{
    public static IServiceCollection AddSalesModule(this IServiceCollection services)
    {
        services.AddScoped<IQuoteService, QuoteService>();
        services.AddScoped<IInvoiceService, InvoiceService>();
        return services;
    }
}

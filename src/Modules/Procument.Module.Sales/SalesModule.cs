using Microsoft.Extensions.DependencyInjection;
using Procument.Module.Purchasing.Services;
using Procument.Module.Sales.Services;

namespace Procument.Module.Sales;

public static class SalesModule
{
    public static IServiceCollection AddSalesModule(this IServiceCollection services)
    {
        services.AddScoped<IQuoteService, QuoteService>();
        services.AddScoped<IInvoiceService, InvoiceService>();
        services.AddScoped<IFinalInvoiceService, FinalInvoiceService>();
        // ProcurementService lives in Sales (can see both Sales + Purchasing types); interface is in Purchasing.
        services.AddScoped<IProcurementService, ProcurementService>();
        return services;
    }
}

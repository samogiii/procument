using Microsoft.Extensions.DependencyInjection;
using Procument.Module.Purchasing.Services;
using Procument.Module.Sales.Services;
using Procument.Shared.Services;

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
        services.AddScoped<ITotalPNService, TotalPNService>();
        services.AddScoped<PaymentBoxService>();
        services.AddScoped<IPaymentBoxService>(sp => sp.GetRequiredService<PaymentBoxService>());
        services.AddScoped<IPaymentLedgerService>(sp => sp.GetRequiredService<PaymentBoxService>());
        services.AddScoped<IWalletTransferService, WalletTransferService>();
        return services;
    }
}

using Microsoft.Extensions.DependencyInjection;
using Procument.Module.Purchasing.Services;

namespace Procument.Module.Purchasing;

public static class PurchasingModule
{
    public static IServiceCollection AddPurchasingModule(this IServiceCollection services)
    {
        services.AddScoped<ISupplierQuoteService, SupplierQuoteService>();
        services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
        services.AddScoped<IProcumentPageService, ProcumentPageService>();
        services.AddScoped<IILSService, ILSService>();
        services.AddScoped<IILSQuoteService, ILSQuoteService>();
        services.AddScoped<IILSProformaService, ILSProformaService>();
        services.AddScoped<ICapListService, CapListService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IAvailabilityService, AvailabilityService>();
        services.AddScoped<IPaymentRequestService, PaymentRequestService>();
        services.AddScoped<IPurchaseOrderCompanyService, PurchaseOrderCompanyService>();
        services.AddScoped<IWarehouseService, WarehouseService>();
        services.AddScoped<IShippingService, ShippingService>();
        services.AddScoped<IWarehouseTransferService, WarehouseTransferService>();
        services.AddScoped<IShipmentNoteService, ShipmentNoteService>();
        services.AddScoped<IPendingSupplierQuoteService, PendingSupplierQuoteService>();
        services.AddScoped<Procument.Shared.Services.IRfqItemsCreatedHandler, PendingSupplierQuoteService>();
        // After the auto-fill above (handlers run in this order): active commissions reach the new RFQs' quotes.
        services.AddScoped<Procument.Shared.Services.IRfqItemsCreatedHandler, CommissionAutoApplyHandler>();
        services.AddScoped<IProcurementPlusService, ProcurementPlusService>();
        // New Procurement Plus page (PPE parts) — same class, it reuses the part / file / price helpers.
        services.AddScoped<IPpeService, ProcurementPlusService>();
        services.AddScoped<IPendingRfqService, PendingRfqService>();
        // Every 3 min: ask MailFlow for new quote + RFQ emails (MailFlow:AutoFetchSeconds, 0 = off).
        services.AddHostedService<MailFlowAutoFetchService>();
        return services;
    }
}

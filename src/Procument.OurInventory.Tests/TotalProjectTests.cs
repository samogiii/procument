using Microsoft.Extensions.DependencyInjection;
using Procument.Module.OurInventory.DTOs;
using Procument.Module.OurInventory.Services;
using Procument.Module.Sales.Services;
using Procument.Shared.DTOs;
using Xunit;

namespace Procument.OurInventory.Tests;

/// <summary>Stock PO lines appear in Total Project (after customer rows) and respect its filters.</summary>
[Collection(StockDatabaseCollection.Name)]
public sealed class TotalProjectTests(StockDatabaseFixture fx)
{
    private Task<PagedResult<Procument.Module.Purchasing.DTOs.TotalPNRowResponse>> GetAsync(ITotalPNService service, string origin,
        List<string>? poNumbers = null, List<string>? customers = null, List<string>? invoiceNumbers = null, int page = 1, int pageSize = -1)
        => service.GetAsync(new PageQuery { Page = page, PageSize = pageSize }, fx.UserId, isAdmin: true, isSuperAdmin: true,
            customers: customers, invoiceNumbers: invoiceNumbers, poNumbers: poNumbers, origin: origin);

    [SkippableFact]
    public async Task Stock_po_lines_are_listed_with_received_progress_and_filtered_like_other_rows()
    {
        fx.RequireDatabase();
        var partA = await fx.NewPartAsync();
        var partB = await fx.NewPartAsync();
        var po = await fx.CreateApprovedStockPoAsync(fx.MainWarehouseId, (partA.Id, 4, 10m, "NE"), (partB.Id, 2, 99m, "OH"));
        await using var s = fx.Scope();
        await s.ServiceProvider.GetRequiredService<IStockReceiptService>().ReceiveManualAsync(po.Id,
            new ManualStockReceiptRequest { Lines = [new() { POItemId = po.Items.Single(i => i.PartNumberId == partA.Id).Id, Qty = 3 }] }, fx.UserId);

        var service = s.ServiceProvider.GetRequiredService<ITotalPNService>();
        List<string> only = [po.PONumber];

        var stock = await GetAsync(service, TotalPNOrigins.Stock, only);
        Assert.Equal(2, stock.TotalCount);
        var rowA = Assert.Single(stock.Items, r => r.PartNumber == partA.Name);
        Assert.Equal(("OUR STOCK", "Test Supplier", 4, 10m, 40m, "Main"), (rowA.Customer, rowA.Supplier, rowA.Qty, rowA.PurchasingUnitPriceUsd, rowA.PurchasingTotalPriceUsd, rowA.Warehouse));
        Assert.Null(rowA.InvoiceId);
        Assert.Contains("Received 3/4 into Our Stock", rowA.ShippingStatus);

        // Customer-only view never includes them; PI-only filters exclude them; "OUR STOCK" customer filter keeps them.
        Assert.Equal(0, (await GetAsync(service, TotalPNOrigins.Customer, only)).TotalCount);
        Assert.Equal(0, (await GetAsync(service, TotalPNOrigins.All, only, invoiceNumbers: ["SO-X"])).TotalCount);
        Assert.Equal(2, (await GetAsync(service, TotalPNOrigins.All, only, customers: ["OUR STOCK"])).TotalCount);
        Assert.Equal(0, (await GetAsync(service, TotalPNOrigins.All, only, customers: ["SOME CUSTOMER"])).TotalCount);

        var options = await service.GetFilterOptionsAsync(fx.UserId, true, true, null, origin: TotalPNOrigins.All);
        Assert.Contains("OUR STOCK", options.Customers);
        Assert.Contains(po.PONumber, options.PoNumbers);
    }

    [SkippableFact]
    public async Task All_view_pages_through_customer_rows_then_stock_rows_without_gaps()
    {
        fx.RequireDatabase();
        var part = await fx.NewPartAsync();
        await fx.CreateApprovedStockPoAsync(fx.MainWarehouseId, (part.Id, 1, 1m, "NE"));
        await using var s = fx.Scope();
        var service = s.ServiceProvider.GetRequiredService<ITotalPNService>();

        var everything = await GetAsync(service, TotalPNOrigins.All);
        var customerCount = (await GetAsync(service, TotalPNOrigins.Customer)).TotalCount;
        var stockCount = (await GetAsync(service, TotalPNOrigins.Stock)).TotalCount;
        Assert.Equal(customerCount + stockCount, everything.TotalCount);

        // Walk the "all" view in pages of 3: every row appears once, customer rows before stock rows.
        var seen = new List<string?>();
        for (var p = 1; (p - 1) * 3 < everything.TotalCount; p++)
            seen.AddRange((await GetAsync(service, TotalPNOrigins.All, page: p, pageSize: 3)).Items.Select(r => r.Customer));
        Assert.Equal(everything.Items.Count, seen.Count);
        var firstStock = seen.IndexOf("OUR STOCK");
        Assert.True(firstStock >= 0);
        Assert.All(seen.Skip(firstStock), c => Assert.Equal("OUR STOCK", c));
    }
}

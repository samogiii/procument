using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Procument.Data;
using Procument.Module.Catalog.Entities;
using Procument.Module.OurInventory.DTOs;
using Procument.Module.OurInventory.Services;
using Procument.Module.Purchasing.Entities;
using Procument.Module.Purchasing.Services;
using Procument.Module.RFQ.Entities;
using Procument.Module.Sales.Entities;
using Procument.Module.Sales.Services;
using Procument.Shared.DTOs;
using Procument.Shared.Services;
using Xunit;

namespace Procument.OurInventory.Tests;

/// <summary>Stock PO lines appear in Total Project (after customer rows) and respect its filters.</summary>
[Collection(StockDatabaseCollection.Name)]
public sealed class TotalProjectTests(StockDatabaseFixture fx)
{
    private async Task<(long PoItemId, long InvoiceItemId, long ProcurementItemId, string PoNumber, long PartId, string PartNumber)> CustomerLineAsync(
        int qty, bool fromStock = false)
    {
        var part = await fx.NewPartAsync();
        await using var s = fx.Scope();
        var db = s.ServiceProvider.GetRequiredService<AppDbContext>();
        var customer = new Customer { Name = $"Cust-{Guid.NewGuid():N}"[..12], CustomerCode = $"C-{Guid.NewGuid():N}"[..8] };
        var rfq = new RFQHeader { Name = $"RFQ-{Guid.NewGuid():N}"[..12], Customer = customer, UserId = fx.UserId, LeadTime = DateTime.UtcNow.AddDays(5), ReceivedDate = DateTime.UtcNow };
        var rfqItem = new RFQItem { RFQ = rfq, PartNumberId = part.Id, Qty = qty, Condition = "NE" };
        var record = new ProcumentRecord { RFQItem = rfqItem, SupplierId = fx.SupplierId, Qty = qty, Price = 10, Condition = "NE" };
        var quote = new Quote { QuoteNumber = $"Q-{Guid.NewGuid():N}"[..10], RFQ = rfq, Customer = customer, UserId = fx.UserId, Status = "Accepted" };
        var quoteItem = new QuoteItem { Quote = quote, RFQItem = rfqItem, PartNumberId = part.Id, ProcumentRecord = record, Qty = qty, UnitPrice = 13, TotalPrice = qty * 13, Condition = "NE" };
        var invoice = new Invoice { InvoiceNumber = $"SO-{Guid.NewGuid():N}"[..10], Quote = quote, Customer = customer, Status = "Running", TotalAmount = quoteItem.TotalPrice };
        var invoiceItem = new InvoiceItem { Invoice = invoice, QuoteItem = quoteItem, Qty = qty, UnitPrice = 13, TotalPrice = qty * 13, Condition = "NE" };
        db.Add(invoiceItem);
        await db.SaveChangesAsync();

        var procurement = new Procurement { ProcurementNumber = $"PROC-{Guid.NewGuid():N}"[..14], InvoiceId = invoice.Id };
        var procurementItem = new ProcurementItem
        {
            Procurement = procurement, SourceInvoiceItemId = invoiceItem.Id, PartNumberId = part.Id,
            PartNumberName = part.Name, Qty = qty, AcceptedQty = qty, UnitPrice = 10, AcceptedUnitPrice = 13,
            CurrentSupplierId = fx.SupplierId, SupplierName = "Test Supplier", Condition = "NE", FromStock = fromStock,
        };
        db.Add(procurementItem);
        await db.SaveChangesAsync();
        if (fromStock) return (0, invoiceItem.Id, procurementItem.Id, string.Empty, part.Id, part.Name);

        var po = new PurchaseOrder
        {
            Origin = "Customer", PONumber = $"PO-{Guid.NewGuid():N}"[..11], SupplierId = fx.SupplierId,
            InvoiceId = invoice.Id, Status = PurchaseOrderStatusFlow.PaymentDone, AdminApproval = "Approved",
        };
        var poItem = new POItem
        {
            PurchaseOrder = po, SourceProcurementItem = procurementItem, InvoiceItemId = invoiceItem.Id,
            PartNumberId = part.Id, SupplierId = fx.SupplierId, Qty = qty, UnitPrice = 10, TotalPrice = qty * 10,
            Condition = "NE", Status = PurchaseOrderStatusFlow.ShipToWarehouse, PORef = 1,
        };
        db.Add(poItem);
        await db.SaveChangesAsync();
        return (poItem.Id, invoiceItem.Id, procurementItem.Id, po.PONumber, part.Id, part.Name);
    }

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
        Assert.Null(rowA.InTransitQty);
        Assert.Equal((3, 1), (rowA.ReceivedQty, rowA.RemainingQty));
        Assert.Null(rowA.InWarehouseQty);

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
    public async Task Customer_receipt_progress_excludes_rejections_and_packed_tracks_from_warehouse()
    {
        fx.RequireDatabase();
        var line = await CustomerLineAsync(10);
        long trackId;
        await using (var s = fx.Scope())
        {
            var db = s.ServiceProvider.GetRequiredService<AppDbContext>();
            var track = new POItemTrackNumber { POItemId = line.PoItemId, TrackNumber = $"TRK-{Guid.NewGuid():N}"[..14], WarehouseId = fx.MainWarehouseId };
            db.Add(track);
            await db.SaveChangesAsync();
            trackId = track.Id;
            db.AddRange(
                new TrackNumberItem { TrackNumberId = track.Id, POItemId = line.PoItemId, ExpectedQty = 3, ActualQty = 3, Status = "Accepted" },
                new TrackNumberItem { TrackNumberId = track.Id, POItemId = line.PoItemId, ExpectedQty = 2, ActualQty = 2, Status = "Rejected" },
                new TrackNumberItem { TrackNumberId = track.Id, POItemId = line.PoItemId, ExpectedQty = 2, Status = "Pending" });
            await db.SaveChangesAsync();
        }

        await using (var s = fx.Scope())
        {
            var row = Assert.Single((await GetAsync(s.ServiceProvider.GetRequiredService<ITotalPNService>(), TotalPNOrigins.Customer, [line.PoNumber])).Items);
            Assert.Equal((2, 3, 3, 7), (row.InTransitQty, row.ReceivedQty, row.InWarehouseQty, row.RemainingQty));
            var totalOrder = Assert.Single((await s.ServiceProvider.GetRequiredService<ITotalPNService>()
                .GetTotalOrderAsync(new PageQuery { Page = 1, PageSize = -1 }, fx.UserId, true)).Items, r => r.Id == line.PoItemId);
            Assert.Equal((2, 3, 3, 7), (totalOrder.InTransitQty, totalOrder.ReceivedQty, totalOrder.InWarehouseQty, totalOrder.RemainingQty));
        }

        await using (var s = fx.Scope())
        {
            var db = s.ServiceProvider.GetRequiredService<AppDbContext>();
            var note = new ShipmentNote { SNNumber = $"SN-{Guid.NewGuid():N}"[..13], WarehouseId = fx.MainWarehouseId, CreatedByUserId = fx.UserId };
            db.Add(note);
            await db.SaveChangesAsync();
            db.Add(new ShipmentNoteTrackNumber { ShipmentNoteId = note.Id, TrackNumberId = trackId });
            await db.SaveChangesAsync();
        }
        await using (var s = fx.Scope())
        {
            var row = Assert.Single((await GetAsync(s.ServiceProvider.GetRequiredService<ITotalPNService>(), TotalPNOrigins.Customer, [line.PoNumber])).Items);
            Assert.Equal(3, row.ReceivedQty);
            Assert.Equal(0, row.InWarehouseQty);
        }
    }

    [SkippableFact]
    public async Task Warehouse_transfer_counts_only_the_destination_stock()
    {
        fx.RequireDatabase();
        var line = await CustomerLineAsync(5);
        await using (var s = fx.Scope())
        {
            var db = s.ServiceProvider.GetRequiredService<AppDbContext>();
            var source = new POItemTrackNumber { POItemId = line.PoItemId, TrackNumber = $"SRC-{Guid.NewGuid():N}"[..14], WarehouseId = fx.MainWarehouseId };
            db.Add(source);
            await db.SaveChangesAsync();
            var destination = new POItemTrackNumber { POItemId = line.PoItemId, TrackNumber = $"DST-{Guid.NewGuid():N}"[..14], WarehouseId = fx.SecondWarehouseId, Origin = "Transfer", ParentTrackNumberId = source.Id };
            db.Add(destination);
            await db.SaveChangesAsync();
            db.AddRange(
                new TrackNumberItem { TrackNumberId = source.Id, POItemId = line.PoItemId, ExpectedQty = 5, ActualQty = 5, TransferredOutQty = 5, Status = "Accepted" },
                new TrackNumberItem { TrackNumberId = destination.Id, POItemId = line.PoItemId, ExpectedQty = 5, ActualQty = 5, Status = "Accepted" });
            await db.SaveChangesAsync();
        }
        await using var scope = fx.Scope();
        var row = Assert.Single((await GetAsync(scope.ServiceProvider.GetRequiredService<ITotalPNService>(), TotalPNOrigins.Customer, [line.PoNumber])).Items);
        Assert.Equal(5, row.ReceivedQty);
        Assert.Equal(5, row.InWarehouseQty);
        Assert.Equal(0, row.RemainingQty);
    }

    [SkippableFact]
    public async Task From_stock_row_uses_issued_reserved_and_remaining_quantities()
    {
        fx.RequireDatabase();
        var line = await CustomerLineAsync(5, fromStock: true);
        await using var s = fx.Scope();
        await s.ServiceProvider.GetRequiredService<IStockManagementService>().ImportOpeningAsync(new OpeningStockRequest
        {
            Lines = [new() { PartNumberId = line.PartId, WarehouseId = fx.MainWarehouseId, CompanyPresetId = fx.PresetId, Condition = "NE", Qty = 5, UnitCost = 10 }],
        }, fx.UserId);
        var lotId = await fx.LotIdAsync(line.PartId, fx.MainWarehouseId);
        var reservations = s.ServiceProvider.GetRequiredService<IStockReservationService>();
        await reservations.ReserveAsync(lotId, 4, line.InvoiceItemId, fx.UserId);
        await reservations.ConsumeAsync(line.InvoiceItemId, 2, fx.UserId);

        var row = Assert.Single((await GetAsync(s.ServiceProvider.GetRequiredService<ITotalPNService>(), TotalPNOrigins.Customer)).Items,
            r => r.InvoiceItemId == line.InvoiceItemId && r.ProcurementItemId == line.ProcurementItemId);
        Assert.Null(row.InTransitQty);
        Assert.Equal((2, 2, 3), (row.ReceivedQty, row.InWarehouseQty, row.RemainingQty));
    }

    [SkippableFact]
    public async Task Quantity_progress_query_count_is_constant_for_one_or_many_rows()
    {
        fx.RequireDatabase();
        var first = await CustomerLineAsync(2);
        var second = await CustomerLineAsync(3);
        await using var s = fx.Scope();
        var service = s.ServiceProvider.GetRequiredService<ITotalPNService>();

        fx.QueryCounter.Reset();
        await GetAsync(service, TotalPNOrigins.Customer, [first.PoNumber]);
        var oneRowQueries = fx.QueryCounter.Count;

        fx.QueryCounter.Reset();
        await GetAsync(service, TotalPNOrigins.Customer, [first.PoNumber, second.PoNumber]);
        var twoRowQueries = fx.QueryCounter.Count;

        Assert.Equal(oneRowQueries, twoRowQueries);
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

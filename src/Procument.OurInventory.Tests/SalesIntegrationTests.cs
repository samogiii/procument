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
using Procument.Shared.Services;
using Xunit;

namespace Procument.OurInventory.Tests;

/// <summary>Epic 7: RFQ availability → quote from Our Stock → Sales Order reserves → no PO → issue / release.</summary>
[Collection(StockDatabaseCollection.Name)]
public sealed class SalesIntegrationTests(StockDatabaseFixture fx)
{
    private async Task<(long PartId, long LotId)> StockAsync(decimal qty, decimal cost, string condition = "NE")
    {
        var part = await fx.NewPartAsync();
        await using var s = fx.Scope();
        await s.ServiceProvider.GetRequiredService<IStockManagementService>().ImportOpeningAsync(new OpeningStockRequest
        {
            Lines = [new() { PartNumberId = part.Id, WarehouseId = fx.MainWarehouseId, CompanyPresetId = fx.PresetId, Condition = condition, Qty = qty, UnitCost = cost }],
        }, fx.UserId);
        return (part.Id, await fx.LotIdAsync(part.Id, fx.MainWarehouseId));
    }

    private async Task<long> StockSupplierIdAsync()
    {
        await using var s = fx.Scope();
        var db = s.ServiceProvider.GetRequiredService<AppDbContext>();
        var existing = await db.Suppliers.Where(x => x.Name == "OUR STOCK").Select(x => (long?)x.Id).FirstOrDefaultAsync();
        if (existing.HasValue) return existing.Value;
        var supplier = new Supplier { Name = "OUR STOCK", Status = "Approved", IsActive = true };
        db.Add(supplier);
        await db.SaveChangesAsync();
        return supplier.Id;
    }

    /// <summary>RFQ → supplier-quote row → quote → accepted (Running) Sales Order for one line.</summary>
    private async Task<(long InvoiceId, long InvoiceItemId)> SalesOrderAsync(long partId, int qty, long supplierId, long? stockItemId, decimal cost)
    {
        await using var s = fx.Scope();
        var db = s.ServiceProvider.GetRequiredService<AppDbContext>();
        var customer = new Customer { Name = $"Cust-{Guid.NewGuid():N}"[..12] };
        db.Add(customer);
        await db.SaveChangesAsync();
        var rfq = new RFQHeader { Name = $"RFQ-{Guid.NewGuid():N}"[..12], CustomerId = customer.Id, LeadTime = DateTime.UtcNow.AddDays(5), ReceivedDate = DateTime.UtcNow };
        db.Add(rfq);
        await db.SaveChangesAsync();
        var rfqItem = new RFQItem { RFQId = rfq.Id, PartNumberId = partId, Qty = qty, Condition = "NE" };
        db.Add(rfqItem);
        await db.SaveChangesAsync();
        var record = new ProcumentRecord { RFQItemId = rfqItem.Id, SupplierId = supplierId, Qty = qty, Price = cost, Condition = "NE", SourceStockItemId = stockItemId };
        db.Add(record);
        await db.SaveChangesAsync();
        var quote = new Quote { QuoteNumber = $"Q-{Guid.NewGuid():N}"[..10], RFQId = rfq.Id, CustomerId = customer.Id, UserId = fx.UserId, Status = "Accepted" };
        db.Add(quote);
        await db.SaveChangesAsync();
        var quoteItem = new QuoteItem
        {
            QuoteId = quote.Id, RFQItemId = rfqItem.Id, PartNumberId = partId, ProcumentRecordId = record.Id,
            SourceStockItemId = stockItemId, Qty = qty, UnitPrice = cost * 1.3m, TotalPrice = qty * cost * 1.3m, Condition = "NE",
        };
        db.Add(quoteItem);
        await db.SaveChangesAsync();
        var invoice = new Invoice { InvoiceNumber = $"SO-{Guid.NewGuid():N}"[..10], QuoteId = quote.Id, CustomerId = customer.Id, Status = "Running", TotalAmount = quoteItem.TotalPrice };
        db.Add(invoice);
        await db.SaveChangesAsync();
        var line = new InvoiceItem { InvoiceId = invoice.Id, QuoteItemId = quoteItem.Id, Qty = qty, UnitPrice = quoteItem.UnitPrice, TotalPrice = quoteItem.TotalPrice, Condition = "NE", Status = "Not Started" };
        db.Add(line);
        await db.SaveChangesAsync();
        return (invoice.Id, line.Id);
    }

    [SkippableFact]
    public async Task Rfq_availability_lists_our_stock_and_incoming_stock_po_lines()
    {
        fx.RequireDatabase();
        var (partId, lotId) = await StockAsync(4, 25);
        await fx.CreateApprovedStockPoAsync(fx.MainWarehouseId, (partId, 3, 20m, "NE"));
        await using var s = fx.Scope();
        var result = (await s.ServiceProvider.GetRequiredService<IAvailabilityService>().GetPartAvailabilityAsync([partId])).Single();
        var stock = Assert.Single(result.OurStockRecords);
        Assert.Equal((lotId, 4d, 25m), (stock.StockItemId!.Value, stock.Qty!.Value, stock.Price!.Value));
        Assert.Equal(3d, Assert.Single(result.IncomingStockRecords).Qty);
    }

    [SkippableFact]
    public async Task Accepted_sales_order_reserves_stock_splits_the_shortfall_and_never_creates_a_po()
    {
        fx.RequireDatabase();
        var supplierId = await StockSupplierIdAsync();
        var (partId, lotId) = await StockAsync(3, 10);
        var (invoiceId, lineId) = await SalesOrderAsync(partId, 5, supplierId, lotId, 10);

        await using var s = fx.Scope();
        var procurement = await s.ServiceProvider.GetRequiredService<IProcurementService>().CreateFromAcceptedInvoiceAsync(invoiceId, fx.UserId);
        var lot = await fx.LotAsync(partId, fx.MainWarehouseId);
        Assert.Equal(3, lot.Reserved);

        var db = s.ServiceProvider.GetRequiredService<AppDbContext>();
        var rows = await db.Set<ProcurementItem>().AsNoTracking().Where(i => i.ProcurementId == procurement.Id).ToListAsync();
        var stockRow = Assert.Single(rows, r => r.FromStock);
        Assert.Equal((3, "Ready"), (stockRow.Qty, stockRow.ItemStatus));
        var remainder = Assert.Single(rows, r => !r.FromStock);
        Assert.Equal((2, "No Supplier"), (remainder.Qty, remainder.SupplierName));

        var finalize = await s.ServiceProvider.GetRequiredService<IProcurementService>().FinalizeAsync(procurement.Id, fx.UserId);
        Assert.False(await db.Set<POItem>().AnyAsync(p => p.SourceProcurementItemId == stockRow.Id));
        Assert.Equal("Reserved from Stock", await db.Set<InvoiceItem>().Where(i => i.Id == lineId).Select(i => i.Status).SingleAsync());

        // Issue: stock leaves the lot at its cost, and the Sales Order line is delivered.
        var manage = s.ServiceProvider.GetRequiredService<IStockManagementService>();
        var reservationId = await db.OurStockReservations.Where(r => r.InvoiceItemId == lineId).Select(r => r.Id).SingleAsync();
        await manage.IssueReservationAsync(reservationId, new IssueReservationRequest(), fx.UserId);
        lot = await fx.LotAsync(partId, fx.MainWarehouseId);
        Assert.Equal((0m, 0m), (lot.OnHand, lot.Reserved));
        var status = await s.ServiceProvider.GetRequiredService<IStockReservationService>().GetLineStatusAsync(lineId);
        Assert.Equal((0m, 3m, 10m), (status.Reserved, status.Issued, status.IssueCost!.Value));
        db.ChangeTracker.Clear();
        Assert.Equal("Delivered to Customer", await db.Set<InvoiceItem>().Where(i => i.Id == lineId).Select(i => i.Status).SingleAsync());
    }

    [SkippableFact]
    public async Task Stock_line_without_available_stock_falls_back_to_normal_sourcing()
    {
        fx.RequireDatabase();
        var supplierId = await StockSupplierIdAsync();
        var part = await fx.NewPartAsync();
        var (invoiceId, _) = await SalesOrderAsync(part.Id, 2, supplierId, null, 10);

        await using var s = fx.Scope();
        var procurement = await s.ServiceProvider.GetRequiredService<IProcurementService>().CreateFromAcceptedInvoiceAsync(invoiceId, fx.UserId);
        var db = s.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.Set<ProcurementItem>().AsNoTracking().SingleAsync(i => i.ProcurementId == procurement.Id);
        Assert.False(row.FromStock);
        Assert.Null(row.CurrentSupplierId);
        Assert.Equal(2, row.Qty);
    }

    [SkippableFact]
    public async Task Regular_supplier_line_is_unchanged_and_becomes_a_po_item()
    {
        fx.RequireDatabase();
        var part = await fx.NewPartAsync();
        var (invoiceId, lineId) = await SalesOrderAsync(part.Id, 3, fx.SupplierId, null, 12);

        await using var s = fx.Scope();
        var procurements = s.ServiceProvider.GetRequiredService<IProcurementService>();
        var procurement = await procurements.CreateFromAcceptedInvoiceAsync(invoiceId, fx.UserId);
        var db = s.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.Set<ProcurementItem>().AsNoTracking().SingleAsync(i => i.ProcurementId == procurement.Id);
        Assert.False(row.FromStock);
        Assert.Equal(fx.SupplierId, row.CurrentSupplierId);
        Assert.False(await db.OurStockReservations.AnyAsync(r => r.InvoiceItemId == lineId));

        // Mark the cloned supplier quote as chosen, as a user would, then finalize → one POItem for the supplier.
        var quote = await db.Set<ProcurementSupplierQuote>().SingleAsync(q => q.ProcurementItemId == row.Id);
        quote.IsSelected = true;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        await procurements.FinalizeAsync(procurement.Id, fx.UserId);
        var poItem = await db.Set<POItem>().AsNoTracking().SingleAsync(p => p.SourceProcurementItemId == row.Id);
        Assert.Equal((3, fx.SupplierId), (poItem.Qty, poItem.SupplierId!.Value));
    }

    [SkippableFact]
    public async Task Cancelling_the_sales_order_releases_its_reservation()
    {
        fx.RequireDatabase();
        var supplierId = await StockSupplierIdAsync();
        var (partId, lotId) = await StockAsync(6, 5);
        var (invoiceId, _) = await SalesOrderAsync(partId, 4, supplierId, lotId, 5);

        await using (var s = fx.Scope())
            await s.ServiceProvider.GetRequiredService<IProcurementService>().CreateFromAcceptedInvoiceAsync(invoiceId, fx.UserId);
        Assert.Equal(4, (await fx.LotAsync(partId, fx.MainWarehouseId)).Reserved);

        await using (var s = fx.Scope())
            Assert.True(await s.ServiceProvider.GetRequiredService<IInvoiceService>().CancelAsync(invoiceId));
        var lot = await fx.LotAsync(partId, fx.MainWarehouseId);
        Assert.Equal((6m, 0m), (lot.OnHand, lot.Reserved));
    }
}

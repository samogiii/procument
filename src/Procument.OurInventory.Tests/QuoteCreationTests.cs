using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Procument.Data;
using Procument.Module.Catalog.Entities;
using Procument.Module.RFQ.Entities;
using Procument.Module.Sales.DTOs;
using Procument.Module.Sales.Entities;
using Procument.Module.Sales.Services;
using Xunit;

namespace Procument.OurInventory.Tests;

/// <summary>
/// Quote creation numbers the quote in one transaction. Quotes.QuoteNumber is unique, so a quote
/// left without its number used to block every later quote with a duplicate-key error.
/// </summary>
[Collection(StockDatabaseCollection.Name)]
public sealed class QuoteCreationTests(StockDatabaseFixture fx)
{
    private async Task<(long RfqId, long RfqItemId, long CustomerId)> NewRfqAsync()
    {
        var part = await fx.NewPartAsync();
        await using var s = fx.Scope();
        var db = s.ServiceProvider.GetRequiredService<AppDbContext>();
        var customer = new Customer { Name = $"Cust-{Guid.NewGuid():N}"[..12] };
        db.Add(customer);
        await db.SaveChangesAsync();
        var rfq = new RFQHeader
        {
            Name = $"RFQ-{Guid.NewGuid():N}"[..12], CustomerId = customer.Id,
            LeadTime = DateTime.UtcNow.AddDays(5), ReceivedDate = DateTime.UtcNow,
        };
        db.Add(rfq);
        await db.SaveChangesAsync();
        var item = new RFQItem { RFQId = rfq.Id, PartNumberId = part.Id, Qty = 2, Condition = "NE" };
        db.Add(item);
        await db.SaveChangesAsync();
        return (rfq.Id, item.Id, customer.Id);
    }

    private static CreateQuoteRequest Request(long rfqId, long rfqItemId) => new()
    {
        RFQId = rfqId,
        Items = [new CreateQuoteItemRequest { RFQItemId = rfqItemId, Qty = 2, UnitPrice = 25m, Condition = "NE" }],
    };

    [SkippableFact]
    public async Task Created_quote_is_numbered_and_moves_the_rfq_to_ready_to_quote()
    {
        fx.RequireDatabase();
        var (rfqId, rfqItemId, _) = await NewRfqAsync();

        await using var s = fx.Scope();
        var created = await s.ServiceProvider.GetRequiredService<IQuoteService>().CreateAsync(Request(rfqId, rfqItemId), fx.UserId);

        var db = s.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ChangeTracker.Clear();
        var quote = await db.Set<Quote>().AsNoTracking().SingleAsync(q => q.Id == created.Id);
        Assert.Equal($"QT-{quote.Id}", quote.QuoteNumber);
        Assert.Equal(50m, quote.TotalAmount);
        Assert.Equal("Ready To Quote", await db.Set<RFQHeader>().Where(r => r.Id == rfqId).Select(r => r.Status).SingleAsync());
        Assert.Equal(1, await db.Set<QuoteItem>().CountAsync(i => i.QuoteId == quote.Id));

        // No placeholder survives a successful creation.
        Assert.False(await db.Set<Quote>().AnyAsync(q => q.QuoteNumber == "" || q.QuoteNumber.StartsWith("TMP-")));
    }

    [SkippableFact]
    public async Task A_numberless_quote_left_by_an_older_failure_does_not_block_new_quotes()
    {
        fx.RequireDatabase();
        var (blockedRfqId, _, blockedCustomerId) = await NewRfqAsync();
        var (rfqId, rfqItemId, _) = await NewRfqAsync();

        // Exactly what the old two-step create left behind when it died before writing "QT-{Id}".
        await using (var seed = fx.Scope())
        {
            var db = seed.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Add(new Quote
            {
                QuoteNumber = "", RFQId = blockedRfqId, CustomerId = blockedCustomerId,
                UserId = fx.UserId, Status = "Draft", CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        await using var s = fx.Scope();
        var created = await s.ServiceProvider.GetRequiredService<IQuoteService>().CreateAsync(Request(rfqId, rfqItemId), fx.UserId);
        Assert.Equal($"QT-{created.Id}", created.QuoteNumber);
    }

    [SkippableFact]
    public async Task A_rejected_request_leaves_no_quote_and_no_rfq_status_change()
    {
        fx.RequireDatabase();
        var (rfqId, _, _) = await NewRfqAsync();

        await using var s = fx.Scope();
        var service = s.ServiceProvider.GetRequiredService<IQuoteService>();
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.CreateAsync(Request(rfqId, rfqItemId: -1), fx.UserId));

        var db = s.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ChangeTracker.Clear();
        Assert.False(await db.Set<Quote>().AnyAsync(q => q.RFQId == rfqId));
        Assert.NotEqual("Ready To Quote", await db.Set<RFQHeader>().Where(r => r.Id == rfqId).Select(r => r.Status).SingleAsync());
    }
}

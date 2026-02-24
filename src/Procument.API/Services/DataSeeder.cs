using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Procument.Module.Catalog.Entities;
using Procument.Module.Identity.Entities;
using Procument.Module.RFQ.Entities;
using Procument.Module.Purchasing.Entities;
using Procument.Module.Sales.Entities;

namespace Procument.API.Services;

public static class DataSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DataSeeder");

        await SeedUsersAsync(db, logger);
        await SeedCustomersAsync(db, logger);
        await SeedSuppliersAsync(db, logger);
        await SeedPartNumbersAsync(db, logger);
        await SeedRFQsAsync(db, logger);
        await SeedProcurementRecordsAsync(db, logger);
        await SeedQuotesAsync(db, logger);
        await SeedInvoicesAsync(db, logger);
    }

    private static async Task SeedUsersAsync(DbContext db, ILogger logger)
    {
        if (await db.Set<User>().AnyAsync()) return;

        var users = new List<User>
        {
            new User
            {
                Name = "Admin",
                Email = "admin@procument.com",
                Password = HashPassword("Admin@123"),
                Role = UserRoles.Admin,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            },
            new User
            {
                Name = "Admin Two",
                Email = "admin2@procument.com",
                Password = HashPassword("Admin@123"),
                Role = UserRoles.Admin,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            },
            new User
            {
                Name = "Expert",
                Email = "expert@procument.com",
                Password = HashPassword("Expert@123"),
                Role = UserRoles.Expert,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            }
        };

        for (int i = 1; i <= 10; i++)
        {
            users.Add(new User
            {
                Name = $"User {i}",
                Email = $"user{i}@procument.com",
                Password = HashPassword("SimplePass123!"),
                Role = UserRoles.Expert,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });
        }

        db.Set<User>().AddRange(users);
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} users.", users.Count);
    }

    private static async Task SeedCustomersAsync(DbContext db, ILogger logger)
    {
        if (await db.Set<Customer>().AnyAsync()) return;

        var customers = new List<Customer>();
        for (int i = 1; i <= 5; i++)
        {
            customers.Add(new Customer
            {
                Name = $"Customer {i} Inc.",
                Email = $"contact@customer{i}.com",
                Phone = $"555-010{i}",
                BillTo = $"10{i} Main St, City {i}",
                ShipTo = $"10{i} Warehouse Dr, City {i}",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        db.Set<Customer>().AddRange(customers);
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} customers.", customers.Count);
    }

    private static async Task SeedSuppliersAsync(DbContext db, ILogger logger)
    {
        if (await db.Set<Supplier>().AnyAsync()) return;

        var suppliers = new List<Supplier>();
        for (int i = 1; i <= 5; i++)
        {
            suppliers.Add(new Supplier
            {
                Name = $"Supplier {i} Ltd.",
                Email = $"sales@supplier{i}.com",
                Phone = $"555-020{i}",
                Address = $"20{i} Industrial Pkwy, City {i}",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        db.Set<Supplier>().AddRange(suppliers);
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} suppliers.", suppliers.Count);
    }

    private static async Task SeedPartNumbersAsync(DbContext db, ILogger logger)
    {
        if (await db.Set<PartNumber>().AnyAsync()) return;

        var suppliers = await db.Set<Supplier>().ToListAsync();
        if (!suppliers.Any()) return;

        var parts = new List<PartNumber>();
        var random = new Random(42);

        for (int i = 1; i <= 20; i++)
        {
            parts.Add(new PartNumber
            {
                Name = $"PN-{1000 + i}",
                Description = $"Part Description {i}",
                SupplierId = suppliers[random.Next(suppliers.Count)].Id,
                CreatedAt = DateTime.UtcNow
            });
        }

        db.Set<PartNumber>().AddRange(parts);
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} part numbers.", parts.Count);
    }

    private static async Task SeedRFQsAsync(DbContext db, ILogger logger)
    {
        if (await db.Set<RFQHeader>().AnyAsync()) return;

        var customers = await db.Set<Customer>().ToListAsync();
        var users = await db.Set<User>().ToListAsync();
        var parts = await db.Set<PartNumber>().ToListAsync();

        if (!customers.Any() || !users.Any() || !parts.Any()) return;

        var random = new Random(42);
        int[] exTypes = [0, 0, 0, 1, 1, 2, 2, 2, 0, 1]; // Warehouse, Vendor, Customer mix

        for (int i = 0; i < 10; i++)
        {
            var rfq = new RFQHeader
            {
                Name = $"RFQ-{DateTime.UtcNow.Year}-{100 + i + 1}",
                LeadTime = DateTime.UtcNow.AddDays(random.Next(10, 30)),
                CustomerId = customers[i % customers.Count].Id,
                UserId = users[0].Id, // Admin user
                CreatedAt = DateTime.UtcNow,
                ExType = exTypes[i],
            };

            var itemCount = random.Next(2, 5);
            var usedParts = new HashSet<long>();
            for (int j = 0; j < itemCount; j++)
            {
                long partId;
                do { partId = parts[random.Next(parts.Count)].Id; }
                while (usedParts.Contains(partId));
                usedParts.Add(partId);

                rfq.RFQItems.Add(new RFQItem
                {
                    PartNumberId = partId,
                    Qty = random.Next(1, 50),
                    Condition = j % 2 == 0 ? "NE" : "OH",
                    Alt = j % 3 == 0 ? "ALT-PN" : null,
                });
            }

            db.Set<RFQHeader>().Add(rfq);
        }

        await db.SaveChangesAsync();
        logger.LogInformation("Seeded 10 RFQs with ExType.");
    }

    private static async Task SeedProcurementRecordsAsync(DbContext db, ILogger logger)
    {
        if (await db.Set<ProcumentRecord>().AnyAsync()) return;

        var rfqItems = await db.Set<RFQItem>().ToListAsync();
        var suppliers = await db.Set<Supplier>().ToListAsync();
        var users = await db.Set<User>().ToListAsync();

        if (!rfqItems.Any() || !suppliers.Any()) return;

        var random = new Random(42);

        foreach (var item in rfqItems)
        {
            // 1-2 supplier quotes per item
            var count = random.Next(1, 3);
            var usedSuppliers = new HashSet<long>();
            for (int s = 0; s < count; s++)
            {
                long suppId;
                do { suppId = suppliers[random.Next(suppliers.Count)].Id; }
                while (usedSuppliers.Contains(suppId));
                usedSuppliers.Add(suppId);

                var price = Math.Round((decimal)(random.NextDouble() * 500 + 10), 2);
                var qty = item.Qty;

                db.Set<ProcumentRecord>().Add(new ProcumentRecord
                {
                    RFQItemId = item.Id,
                    SupplierId = suppId,
                    UserId = users[0].Id,
                    Price = price,
                    Qty = qty,
                    Condition = item.Condition ?? "NE",
                    Alt = item.Alt,
                    Unit = "EA",
                    UnitPrice = (double)price,
                    TotalPrice = (double)(price * (decimal)qty),
                    LeadTime = "14 days",
                    ShippingCost = Math.Round(random.NextDouble() * 50, 2),
                    ShippingPoint = "FOB",
                });
            }
        }

        await db.SaveChangesAsync();
        logger.LogInformation("Seeded procurement records for all RFQ items.");
    }

    private static async Task SeedQuotesAsync(DbContext db, ILogger logger)
    {
        if (await db.Set<Quote>().AnyAsync()) return;

        var rfqs = await db.Set<RFQHeader>()
            .Include(r => r.RFQItems)
            .ToListAsync();
        var users = await db.Set<User>().ToListAsync();

        if (!rfqs.Any()) return;

        int quoteCounter = 1;

        foreach (var rfq in rfqs)
        {
            // Get procurement records for this RFQ's items
            var rfqItemIds = rfq.RFQItems.Select(i => i.Id).ToList();
            var procRecords = await db.Set<ProcumentRecord>()
                .Where(p => rfqItemIds.Contains(p.RFQItemId))
                .ToListAsync();

            if (!procRecords.Any()) continue;

            var quote = new Quote
            {
                QuoteNumber = $"QT-{quoteCounter:D5}",
                RFQId = rfq.Id,
                CustomerId = rfq.CustomerId,
                UserId = users[0].Id,
                Status = "Accepted",
                Type = rfq.ExType,
                CreatedAt = DateTime.UtcNow,
                ValidUntil = DateTime.UtcNow.AddDays(30),
            };

            decimal totalAmount = 0;

            // One QuoteItem per procurement record (pick first per rfqItem)
            var grouped = procRecords.GroupBy(p => p.RFQItemId);
            foreach (var g in grouped)
            {
                var proc = g.First();
                var rfqItem = rfq.RFQItems.First(i => i.Id == g.Key);
                var unitPrice = (decimal)(proc.UnitPrice ?? (double)proc.Price);
                var qty = (int)proc.Qty;
                var total = unitPrice * qty;
                totalAmount += total;

                quote.QuoteItems.Add(new QuoteItem
                {
                    PartNumberId = rfqItem.PartNumberId,
                    RFQItemId = rfqItem.Id,
                    ProcumentRecordId = proc.Id,
                    Qty = qty,
                    UnitPrice = unitPrice,
                    TotalPrice = total,
                    Condition = proc.Condition,
                    Alt = proc.Alt,
                });
            }

            quote.TotalAmount = totalAmount;
            db.Set<Quote>().Add(quote);
            quoteCounter++;
        }

        await db.SaveChangesAsync();
        logger.LogInformation("Seeded quotes for all RFQs.");
    }

    private static async Task SeedInvoicesAsync(DbContext db, ILogger logger)
    {
        if (await db.Set<Invoice>().AnyAsync()) return;

        var quotes = await db.Set<Quote>()
            .Include(q => q.QuoteItems)
            .ToListAsync();

        if (!quotes.Any()) return;

        int invoiceCounter = 1;

        foreach (var quote in quotes)
        {
            if (!quote.QuoteItems.Any()) continue;

            var invoice = new Invoice
            {
                InvoiceNumber = $"INV-{invoiceCounter:D5}",
                QuoteId = quote.Id,
                CustomerId = quote.CustomerId,
                Status = "Paid",
                DueDate = DateTime.UtcNow.AddDays(60),
                PaidDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
            };

            decimal totalAmount = 0;
            foreach (var qi in quote.QuoteItems)
            {
                var total = qi.UnitPrice * qi.Qty;
                totalAmount += total;

                invoice.InvoiceItems.Add(new InvoiceItem
                {
                    QuoteItemId = qi.Id,
                    Qty = qi.Qty,
                    UnitPrice = qi.UnitPrice,
                    TotalPrice = total,
                    ExpectedDeliveryDate = DateTime.UtcNow.AddDays(30),
                });
            }

            invoice.TotalAmount = totalAmount;
            db.Set<Invoice>().Add(invoice);
            invoiceCounter++;
        }

        await db.SaveChangesAsync();

        // Now create POItems for all paid invoices (same logic as InvoiceService)
        var paidInvoices = await db.Set<Invoice>()
            .Include(i => i.InvoiceItems)
                .ThenInclude(ii => ii.QuoteItem)
            .Where(i => i.Status == "Paid")
            .ToListAsync();

        foreach (var inv in paidInvoices)
        {
            foreach (var ii in inv.InvoiceItems)
            {
                var exists = await db.Set<POItem>().AnyAsync(p => p.InvoiceItemId == ii.Id);
                if (exists) continue;

                var quoteItem = ii.QuoteItem;
                long? supplierId = null;
                if (quoteItem?.ProcumentRecordId != null)
                {
                    var proc = await db.Set<ProcumentRecord>().FindAsync(quoteItem.ProcumentRecordId.Value);
                    supplierId = proc?.SupplierId;
                }

                db.Set<POItem>().Add(new POItem
                {
                    POId = null,
                    InvoiceItemId = ii.Id,
                    ProcumentId = quoteItem?.ProcumentRecordId,
                    PartNumberId = quoteItem?.PartNumberId,
                    SupplierId = supplierId,
                    Qty = ii.Qty,
                    UnitPrice = ii.UnitPrice,
                    TotalPrice = ii.TotalPrice,
                    Condition = quoteItem?.Condition,
                });
            }
        }

        await db.SaveChangesAsync();
        logger.LogInformation("Seeded invoices (Paid) and POItems for all quotes.");
    }

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations: 100_000,
            HashAlgorithmName.SHA256,
            outputLength: 32);

        return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }
}

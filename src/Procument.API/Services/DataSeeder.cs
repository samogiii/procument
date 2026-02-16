using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Procument.Module.Catalog.Entities;
using Procument.Module.Identity.Entities;
using Procument.Module.RFQ.Entities;

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
    }

    private static async Task SeedUsersAsync(DbContext db, ILogger logger)
    {
        if (await db.Set<User>().AnyAsync())
        {
            return;
        }

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

        // Create 10 simple users
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
        var random = new Random();

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

        var rfqs = new List<RFQHeader>();
        var random = new Random();

        for (int i = 1; i <= 10; i++)
        {
            var rfq = new RFQHeader
            {
                Name = $"RFQ-{DateTime.UtcNow.Year}-{100 + i}",
                LeadTime = DateTime.UtcNow.AddDays(random.Next(10, 30)),
                CustomerId = customers[random.Next(customers.Count)].Id,
                UserId = users[random.Next(users.Count)].Id,
                CreatedAt = DateTime.UtcNow
            };

            // Add Items
            var itemCount = random.Next(1, 5);
            for (int j = 0; j < itemCount; j++)
            {
                rfq.RFQItems.Add(new RFQItem
                {
                    PartNumberId = parts[random.Next(parts.Count)].Id,
                    Qty = random.Next(1, 100),
                    Condition = j % 2 == 0 ? "NE" : "OH",
                    Alt = j % 3 == 0 ? "ALT-PN" : null
                });
            }

            rfqs.Add(rfq);
        }

        db.Set<RFQHeader>().AddRange(rfqs);
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} RFQs.", rfqs.Count);
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

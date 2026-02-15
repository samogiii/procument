using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Procument.Module.Identity.Entities;

namespace Procument.Module.Identity.Services;

public static class DataSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DataSeeder");

        await SeedUsersAsync(db, logger);
    }

    private static async Task SeedUsersAsync(DbContext db, ILogger logger)
    {
        if (await db.Set<User>().AnyAsync())
        {
            logger.LogInformation("Users already seeded, skipping.");
            return;
        }

        var users = new[]
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
                Name = "Expert",
                Email = "expert@procument.com",
                Password = HashPassword("Expert@123"),
                Role = UserRoles.Expert,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            }
        };

        db.Set<User>().AddRange(users);
        await db.SaveChangesAsync();

        logger.LogInformation("Seeded {Count} users: Admin (admin@procument.com / Admin@123), Expert (expert@procument.com / Expert@123)", users.Length);
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

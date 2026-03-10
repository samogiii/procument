using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Procument.Data;
using Procument.Module.Identity.Entities;

namespace Procument.API.Services;

public static class DatabaseResetter
{
    public static async Task HardResetAndSeedAdminAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseResetter");

        logger.LogWarning("STARTING DATABASE RESET. All data will be lost!");

        // 1. Completely delete the database
        await db.Database.EnsureDeletedAsync();
        logger.LogInformation("Database deleted successfully.");

        // 2. Recreate the database schema by applying all migrations
        await db.Database.MigrateAsync();
        logger.LogInformation("Database schema recreated successfully.");

        // 3. Seed exactly one Admin user
        var adminUser = new User
        {
            Name = "System Admin",
            Email = "admin@procument.com",
            Password = HashPassword("Admin@123"), // Change this to a secure initial password
            Role = "Admin", // Assuming UserRoles.Admin evaluates to the string "Admin"
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        db.Users.Add(adminUser);
        await db.SaveChangesAsync();

        logger.LogInformation("Database reset complete. Admin user created.");
    }

    // Adapted from your existing DataSeeder.cs
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
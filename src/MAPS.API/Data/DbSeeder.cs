using MAPS.API.Data.Entities;
using MAPS.Shared.Enums;

namespace MAPS.API.Data;

public static class DbSeeder
{
    /// <summary>
    /// Seeds the initial admin account and any reference data.
    /// Called on application startup automatically.
    /// </summary>
    public static async Task SeedAsync(AppDbContext context)
    {
        await context.Database.EnsureCreatedAsync();

        // Only seed if Users table is empty
        if (context.Users.Any()) return;

        // ─── Default Admin Account ────────────────────────────────────────────
        var adminUser = new AppUser
        {
            UserId       = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            FullName     = "System Administrator",
            Email        = "admin@maps.local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123!"),
            Role         = UserRole.Admin,
            IsActive     = true,
            IsApproved   = true,
            CreatedAt    = DateTime.UtcNow
        };

        context.Users.Add(adminUser);
        await context.SaveChangesAsync();

        Console.WriteLine("✅ Default admin seeded: admin@maps.local / Admin@123!");
        Console.WriteLine("⚠️  Change the default admin password immediately in production!");
    }
}

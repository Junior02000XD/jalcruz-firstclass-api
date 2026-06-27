using JalcruzFirstClass.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace JalcruzFirstClass.Api.Data;

public static class DbSeeder
{
    /// <summary>
    /// Aplica migraciones pendientes y crea un Super Admin inicial si no existe ningún usuario.
    /// Credenciales tomadas de configuración (Seed:AdminEmail / Seed:AdminPassword).
    /// </summary>
    public static async Task SeedAsync(IServiceProvider services, IConfiguration config, ILogger logger)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.MigrateAsync();

        if (await db.Users.AnyAsync())
            return;

        var email = config["Seed:AdminEmail"] ?? "admin@jalcruz.com";
        var password = config["Seed:AdminPassword"] ?? "ChangeMe123!";

        var admin = new User
        {
            Name = "Super Admin",
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
        };
        db.Users.Add(admin);
        await db.SaveChangesAsync();

        var superAdminRole = await db.Roles.FirstAsync(r => r.Name == Roles.SuperAdmin);
        db.UserRoles.Add(new UserRole { UserId = admin.Id, RoleId = superAdminRole.Id });
        await db.SaveChangesAsync();

        logger.LogInformation("Super Admin inicial creado: {Email}", email);
    }
}

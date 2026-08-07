using JalcruzFirstClass.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace JalcruzFirstClass.Api.Data;

public static class DbSeeder
{
    /// <summary>
    /// Aplica migraciones pendientes y crea las cuentas iniciales.
    /// Credenciales tomadas de configuración (sección Seed).
    /// </summary>
    public static async Task SeedAsync(IServiceProvider services, IConfiguration config, ILogger logger)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.MigrateAsync();

        await SeedSuperAdminAsync(db, config, logger);
        await SeedCrmUserAsync(db, config, logger);
        await SeedAgentAccountAsync(db, config, logger);
    }

    /// <summary>Super Admin inicial, sólo si no existe ningún usuario todavía.</summary>
    private static async Task SeedSuperAdminAsync(AppDbContext db, IConfiguration config, ILogger logger)
    {
        if (await db.Users.AnyAsync())
            return;

        var email = config["Seed:AdminEmail"] ?? "admin@jalcruz.com";
        var password = config["Seed:AdminPassword"] ?? "ChangeMe123!";

        await CreateUserAsync(db, "Super Admin", email, password, Roles.SuperAdmin);
        logger.LogInformation("Super Admin inicial creado: {Email}", email);
    }

    /// <summary>
    /// Cuenta del CRM que puede recibir un hand-off desde WhatsApp
    /// (prospects.assigned_to_user_id). Sin ella no hay a quién derivar una
    /// conversación cuando la IA no alcanza.
    ///
    /// A diferencia del Super Admin NO tiene credenciales por defecto: se crea sólo
    /// si están definidas Seed:CrmUserEmail y Seed:CrmUserPassword. Una cuenta con
    /// contraseña conocida escrita en el repo sería una puerta abierta en Railway.
    /// </summary>
    private static async Task SeedCrmUserAsync(AppDbContext db, IConfiguration config, ILogger logger)
    {
        var email = config["Seed:CrmUserEmail"];
        var password = config["Seed:CrmUserPassword"];
        var name = config["Seed:CrmUserName"] ?? "Susanne";

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return;

        if (await db.Users.AnyAsync(u => u.Email == email))
            return;

        await CreateUserAsync(db, name, email, password, Roles.CrmAdmin);
        logger.LogInformation("Usuario de CRM creado: {Email}", email);
    }

    /// <summary>
    /// Cuenta de servicio del agente de WhatsApp (la que usa n8n). Se crea con rol
    /// CRM Admin y SIN contraseña utilizable: se hashea un secreto aleatorio de 32
    /// bytes que se descarta en el acto, así que no existe en ningún lado un valor
    /// que pase por /api/login. Su único acceso es /api/service-token.
    ///
    /// A diferencia de las otras dos cuentas no necesita ninguna variable de
    /// entorno con secretos, justamente porque no hay secreto que configurar. El
    /// email es sólo un identificador; se puede cambiar con Seed:AgentUserEmail.
    ///
    /// Es idempotente por email, así que un redeploy no la duplica ni la pisa.
    /// </summary>
    private static async Task SeedAgentAccountAsync(AppDbContext db, IConfiguration config, ILogger logger)
    {
        var email = config["Seed:AgentUserEmail"] ?? "agente-whatsapp@firstclass.local";
        var name = config["Seed:AgentUserName"] ?? "Agente WhatsApp (n8n)";

        if (await db.Users.AnyAsync(u => u.Email == email))
            return;

        // Si ya hay una cuenta de servicio, no se crea otra. Pasa cuando el bot
        // se había creado a mano y después se lo convirtió desde el panel: sin
        // esta guarda, cada despliegue agregaría una cuenta paralela sin uso, y
        // habría dos candidatas a la hora de emitir el token.
        if (await db.Users.AnyAsync(u => u.IsServiceAccount))
        {
            logger.LogInformation("Ya existe una cuenta de servicio: no se crea la del seeder.");
            return;
        }

        await CreateUserAsync(db, name, email, RandomPassword(), Roles.CrmAdmin, isServiceAccount: true);
        logger.LogInformation("Cuenta de servicio del agente creada: {Email} (sin contraseña utilizable)", email);
    }

    /// <summary>Secreto de un solo uso: se hashea y se pierde. Nadie lo ve nunca.</summary>
    private static string RandomPassword()
        => Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));

    private static async Task CreateUserAsync(
        AppDbContext db, string name, string email, string password, string roleName, bool isServiceAccount = false)
    {
        var user = new User
        {
            Name = name,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            IsServiceAccount = isServiceAccount,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var role = await db.Roles.FirstAsync(r => r.Name == roleName);
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        await db.SaveChangesAsync();
    }
}

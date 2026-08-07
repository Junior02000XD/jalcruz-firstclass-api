using JalcruzFirstClass.Api.Data;
using JalcruzFirstClass.Api.Domain;
using JalcruzFirstClass.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JalcruzFirstClass.Api.Controllers;

// El [Authorize] va en cada acción y NO en la clase a propósito. En ASP.NET Core
// los filtros de autorización de la clase y de la acción se SUMAN: el de la
// acción no reemplaza al de la clase, hay que pasar los dos. Con
// `[Authorize(Roles = SuperAdmin)]` acá arriba, el `[Authorize(Roles =
// "CRM Admin,Super Admin")]` de Assignable quedaba en "SuperAdmin Y (CrmAdmin O
// SuperAdmin)", o sea sólo Super Admin, y un CRM Admin recibía 403 al abrir el
// desplegable de derivación de ContentPage.
[ApiController]
[Route("api/users")]
public class UsersController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = Roles.SuperAdmin)]
    public async Task<IActionResult> Index()
    {
        var users = await db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .OrderBy(u => u.Name)
            .Select(u => new UserDto(u.Id, u.Name, u.Email,
                u.UserRoles.Select(ur => ur.Role.Name).ToArray(), u.IsServiceAccount))
            .ToListAsync();
        return Ok(users);
    }

    /// <summary>
    /// Personas del CRM a las que se le puede derivar una conversación, con lo
    /// justo para llenar un desplegable (id y nombre, sin correos ni roles).
    ///
    /// Existe porque el resto del controller es sólo para Super Admin y quien
    /// carga los flujos del embudo es un CRM Admin: aflojar el Index entero para
    /// llenar un select habría expuesto de más.
    /// </summary>
    [HttpGet("assignable")]
    [Authorize(Roles = $"{Roles.CrmAdmin},{Roles.SuperAdmin}")]
    public async Task<IActionResult> Assignable()
        => Ok(await db.Users
            // Las cuentas de servicio quedan fuera: derivar una conversación AL
            // BOT no significa nada —lo que apaga al bot es justamente que haya
            // alguien asignado— y aparecía en el desplegable invitando al error.
            // Julio había llegado a llamar a la cuenta "Bot — no derivar acá"
            // para defenderse de esto desde el nombre.
            .Where(u => !u.IsServiceAccount)
            .Where(u => u.UserRoles.Any(ur => ur.Role.Name == Roles.CrmAdmin || ur.Role.Name == Roles.SuperAdmin))
            .OrderBy(u => u.Name)
            .Select(u => new { u.Id, u.Name })
            .ToListAsync());

    [HttpPost("{userId:int}/roles")]
    [Authorize(Roles = Roles.SuperAdmin)]
    public async Task<IActionResult> AssignRoles(int userId, AssignRolesRequest req)
    {
        var user = await db.Users
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null) return NotFound(new { message = "Usuario no encontrado." });

        var roles = await db.Roles.Where(r => req.Roles.Contains(r.Name)).ToListAsync();

        var unknown = req.Roles.Except(roles.Select(r => r.Name)).ToList();
        if (unknown.Count > 0)
            return BadRequest(new { message = $"Roles desconocidos: {string.Join(", ", unknown)}" });

        // Reemplaza el set de roles (equivalente a syncRoles de Spatie).
        db.UserRoles.RemoveRange(user.UserRoles);
        foreach (var role in roles)
            db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        await db.SaveChangesAsync();

        return Ok(new { message = "Roles actualizados.", roles = roles.Select(r => r.Name) });
    }

    /// <summary>
    /// Convierte un usuario existente en cuenta de servicio (un bot, no una
    /// persona). Existe porque la cuenta del agente puede haber sido creada a
    /// mano antes de que existiera este mecanismo: sin esto habría que borrarla y
    /// rehacerla, perdiendo los prospectos que tenga asignados.
    ///
    /// Dos efectos, los dos deliberados:
    ///  1. Deja de poder iniciar sesión (lo bloquea /api/login por la bandera).
    ///  2. **Se destruye su contraseña**, reemplazándola por un aleatorio que no
    ///     se guarda en ningún lado. Así la contraseña que alguien haya anotado
    ///     cuando creó la cuenta deja de servir para algo — que es justamente el
    ///     motivo de pasar a tokens.
    ///
    /// Es reversible con DELETE, que exige fijar una contraseña nueva.
    /// </summary>
    [HttpPost("{userId:int}/service-account")]
    [Authorize(Roles = Roles.SuperAdmin)]
    public async Task<IActionResult> ConvertirEnCuentaDeServicio(int userId)
    {
        var user = await db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null) return NotFound(new { message = "Usuario no encontrado." });

        // Convertirse a uno mismo sería encerrarse afuera: el que ejecuta esto es
        // Super Admin y perdería el acceso al panel, incluso para revertirlo.
        var sub = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(sub, out var propio) && propio == userId)
            return BadRequest(new { message = "No podés convertir tu propia cuenta: quedarías sin acceso al panel." });

        if (user.IsServiceAccount)
            return BadRequest(new { message = "Esa cuenta ya es de servicio." });

        user.IsServiceAccount = true;
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(
            Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)));
        await db.SaveChangesAsync();

        return Ok(new
        {
            message = $"{user.Email} pasó a ser cuenta de servicio: ya no puede iniciar sesión y su contraseña anterior dejó de existir.",
            user = new UserDto(user.Id, user.Name, user.Email,
                user.UserRoles.Select(ur => ur.Role.Name).ToArray(), user.IsServiceAccount),
        });
    }

    /// <summary>
    /// Devuelve una cuenta de servicio al estado de persona. Pide contraseña
    /// nueva porque la anterior se destruyó al convertirla.
    ///
    /// ⚠️ Los tokens ya emitidos para esa cuenta **siguen siendo válidos**: son
    /// JWT sin estado. Para cortarlos hay que rotar `Jwt:Key`, o quitarle los
    /// roles, que es lo que de verdad limita lo que el token puede hacer.
    /// </summary>
    [HttpDelete("{userId:int}/service-account")]
    [Authorize(Roles = Roles.SuperAdmin)]
    public async Task<IActionResult> RevertirCuentaDeServicio(int userId, RevertirCuentaDeServicioRequest req)
    {
        var user = await db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null) return NotFound(new { message = "Usuario no encontrado." });

        if (!user.IsServiceAccount)
            return BadRequest(new { message = "Esa cuenta no es de servicio." });

        user.IsServiceAccount = false;
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        await db.SaveChangesAsync();

        return Ok(new
        {
            message = $"{user.Email} vuelve a ser una cuenta de persona y ya puede iniciar sesión. " +
                      "Ojo: los tokens de servicio que se le hayan emitido siguen valiendo.",
            user = new UserDto(user.Id, user.Name, user.Email,
                user.UserRoles.Select(ur => ur.Role.Name).ToArray(), user.IsServiceAccount),
        });
    }

    [HttpDelete("{userId:int}")]
    [Authorize(Roles = Roles.SuperAdmin)]
    public async Task<IActionResult> Destroy(int userId)
    {
        var user = await db.Users.FindAsync(userId);
        if (user is null) return NotFound();
        db.Users.Remove(user);
        await db.SaveChangesAsync();
        return NoContent();
    }
}

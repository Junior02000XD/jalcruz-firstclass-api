using JalcruzFirstClass.Api.Data;
using JalcruzFirstClass.Api.Domain;
using JalcruzFirstClass.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JalcruzFirstClass.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = Roles.SuperAdmin)]
public class UsersController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var users = await db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .OrderBy(u => u.Name)
            .Select(u => new UserDto(u.Id, u.Name, u.Email,
                u.UserRoles.Select(ur => ur.Role.Name).ToArray()))
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
            .Where(u => u.UserRoles.Any(ur => ur.Role.Name == Roles.CrmAdmin || ur.Role.Name == Roles.SuperAdmin))
            .OrderBy(u => u.Name)
            .Select(u => new { u.Id, u.Name })
            .ToListAsync());

    [HttpPost("{userId:int}/roles")]
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

    [HttpDelete("{userId:int}")]
    public async Task<IActionResult> Destroy(int userId)
    {
        var user = await db.Users.FindAsync(userId);
        if (user is null) return NotFound();
        db.Users.Remove(user);
        await db.SaveChangesAsync();
        return NoContent();
    }
}

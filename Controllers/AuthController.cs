using JalcruzFirstClass.Api.Data;
using JalcruzFirstClass.Api.Domain;
using JalcruzFirstClass.Api.Dtos;
using JalcruzFirstClass.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JalcruzFirstClass.Api.Controllers;

[ApiController]
[Route("api")]
public class AuthController(AppDbContext db, JwtTokenService jwt) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequest req)
    {
        var user = await db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Email == req.Email);

        if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return Unauthorized(new { message = "Credenciales incorrectas" });

        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToArray();
        var token = jwt.CreateToken(user, roles);

        // Forma de respuesta idéntica al backend Laravel para no romper el frontend React.
        return Ok(new LoginResponse(
            "Inicio de sesión exitoso",
            token,
            "Bearer",
            new UserDto(user.Id, user.Name, user.Email, roles)));
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register(RegisterRequest req)
    {
        if (await db.Users.AnyAsync(u => u.Email == req.Email))
            return BadRequest(new { message = "El correo ya está registrado." });

        var user = new User
        {
            Name = req.Name,
            Email = req.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // Sin roles asignados: queda en el sistema pero sin acceso hasta que un admin le asigne permisos.
        return StatusCode(201, new
        {
            message = "Usuario registrado con éxito. Espera a que un administrador te asigne permisos.",
            user = new { user.Id, user.Name, user.Email }
        });
    }

    [HttpPost("logout")]
    [Authorize]
    public IActionResult Logout()
        // Con JWT sin estado el logout es del lado del cliente (descartar el token).
        => Ok(new { message = "Sesión cerrada con éxito" });

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var id = int.Parse(User.FindFirst("sub")!.Value);
        var user = await db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user is null) return NotFound();

        return Ok(new UserDto(user.Id, user.Name, user.Email,
            user.UserRoles.Select(ur => ur.Role.Name).ToArray()));
    }
}

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
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
public class AuthController(AppDbContext db, JwtTokenService jwt, JwtOptions jwtOptions) : ControllerBase
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

        // Las cuentas de servicio no entran por acá ni con la contraseña correcta.
        // En la práctica no hay contraseña correcta (el hash es de un secreto
        // aleatorio descartado), pero el chequeo explícito hace que la garantía no
        // dependa de cómo se creó la cuenta: si alguien le asigna una contraseña a
        // mano en la base, sigue sin poder iniciar sesión.
        if (user.IsServiceAccount)
            return Unauthorized(new { message = "Esta es una cuenta de servicio: no inicia sesión, usa un token emitido por un administrador." });

        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToArray();
        var token = jwt.CreateToken(user, roles);

        // Forma de respuesta idéntica al backend Laravel para no romper el frontend React.
        return Ok(new LoginResponse(
            "Inicio de sesión exitoso",
            token,
            "Bearer",
            new UserDto(user.Id, user.Name, user.Email, roles)));
    }

    /// <summary>
    /// Alta de usuario. Reservada al Super Admin: a esta escala las cuentas las
    /// crea el administrador, no se auto-registran. Antes era pública y, aunque
    /// la cuenta nacía sin roles y sin acceso, dejaba que cualquiera llenara la
    /// tabla de usuarios desde internet.
    /// </summary>
    [HttpPost("register")]
    [Authorize(Roles = Roles.SuperAdmin)]
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

    /// <summary>
    /// Emite un JWT de larga duración para una cuenta de servicio (hoy, el agente
    /// de WhatsApp que corre en n8n). Reservado al Super Admin.
    ///
    /// Por qué existe: n8n necesita autenticarse contra la API sin nadie delante.
    /// Guardar ahí el email y la contraseña de un usuario sería darle la cuenta
    /// entera —incluida la posibilidad de cambiarse la contraseña y quedarse
    /// adentro—, y sería una credencial más viajando en respaldos. Un token abre
    /// sólo lo que el rol de la cuenta permite.
    ///
    /// No es un mecanismo de autenticación nuevo: el token lo firma y lo valida el
    /// mismo middleware JWT de siempre, con los mismos claims y los mismos roles.
    /// Lo único distinto es la expiración (Jwt:ServiceTokenDays, 10 años por
    /// defecto) — el login normal de todos los demás sigue en 12 h, sin cambios.
    ///
    /// ⚠️ Revocación: al ser JWT sin estado, el único modo de invalidar un token
    /// ya emitido es rotar Jwt:Key, y eso cierra la sesión de TODOS. Si el token
    /// se filtra, hay que aceptar ese costo. Se prefirió eso a inventar una lista
    /// de revocación que habría que mantener y consultar en cada petición.
    /// </summary>
    [HttpPost("service-token")]
    [Authorize(Roles = Roles.SuperAdmin)]
    public async Task<IActionResult> ServiceToken([FromBody] ServiceTokenRequest? req)
    {
        var candidatos = db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Where(u => u.IsServiceAccount);

        if (!string.IsNullOrWhiteSpace(req?.Email))
            candidatos = candidatos.Where(u => u.Email == req.Email);

        var cuentas = await candidatos.OrderBy(u => u.Id).ToListAsync();

        if (cuentas.Count == 0)
            return NotFound(new { message = "No hay ninguna cuenta de servicio. Se crea al arrancar la API con Seed:AgentUserEmail definida." });

        // Con más de una cuenta de servicio hay que decir cuál: elegir la primera
        // en silencio emitiría un token con permisos que quizá no son los pedidos.
        if (cuentas.Count > 1)
            return BadRequest(new
            {
                message = "Hay más de una cuenta de servicio: indicá cuál en el campo email.",
                cuentas = cuentas.Select(u => u.Email),
            });

        var user = cuentas[0];
        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToArray();

        if (roles.Length == 0)
            return BadRequest(new { message = $"La cuenta de servicio {user.Email} no tiene roles: el token no serviría para nada." });

        var token = jwt.CreateServiceToken(user, roles);

        return Ok(new
        {
            message = "Token de servicio emitido. Guardalo ahora: no se puede volver a consultar.",
            access_token = token,
            token_type = "Bearer",
            expires_at = DateTime.UtcNow.AddDays(jwtOptions.ServiceTokenDays),
            user = new UserDto(user.Id, user.Name, user.Email, roles),
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
        // Buscar "sub" a secas devolvía null y tiraba un 500 en TODAS las llamadas:
        // el handler de JwtBearer traduce los claims estándar a los URI largos de
        // .NET, así que el "sub" que se firmó llega como ClaimTypes.NameIdentifier.
        // Se prueban los dos por si alguna vez se desactiva ese mapeo.
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                  ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

        if (!int.TryParse(sub, out var id))
            return Unauthorized(new { message = "El token no identifica a ningún usuario." });

        var user = await db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user is null) return NotFound();

        return Ok(new UserDto(user.Id, user.Name, user.Email,
            user.UserRoles.Select(ur => ur.Role.Name).ToArray()));
    }
}

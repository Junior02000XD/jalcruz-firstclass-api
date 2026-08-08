using System.Text.Json;
using JalcruzFirstClass.Api.Data;
using JalcruzFirstClass.Api.Domain;
using JalcruzFirstClass.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace JalcruzFirstClass.Api.Controllers;

[ApiController]
[Route("api/people")]
[Authorize(Roles = $"{Roles.HrAdmin},{Roles.CrmAdmin},{Roles.SuperAdmin}")]
public class PeopleController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] string? search)
    {
        var query = db.People.AsNoTracking().Include(p => p.City).Include(p => p.Phones).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(p =>
                p.FirstName.ToLower().Contains(s) ||
                p.LastName.ToLower().Contains(s) ||
                (p.Ci != null && p.Ci.ToLower().Contains(s)));
        }

        return Ok(await query.OrderBy(p => p.FirstName).ThenBy(p => p.LastName).ToListAsync());
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Show(int id)
    {
        var person = await db.People.AsNoTracking()
            .Include(p => p.City).Include(p => p.Phones).Include(p => p.WorkerDetail)
            .FirstOrDefaultAsync(p => p.Id == id);
        return person is null ? NotFound() : Ok(person);
    }

    [HttpPost]
    public async Task<IActionResult> Store(PersonInput input)
    {
        var person = new Person
        {
            CityId = input.CityId,
            FirstName = input.FirstName,
            LastName = input.LastName,
            Ci = input.Ci,
            CiComplement = input.CiComplement,
            Email = input.Email,
            BirthDate = input.BirthDate,
        };
        db.People.Add(person);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Show), new { id = person.Id }, person);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, PersonInput input)
    {
        var person = await db.People.FindAsync(id);
        if (person is null) return NotFound();

        person.CityId = input.CityId;
        person.FirstName = input.FirstName;
        person.LastName = input.LastName;
        person.Ci = input.Ci;
        person.CiComplement = input.CiComplement;
        person.Email = input.Email;
        person.BirthDate = input.BirthDate;
        await db.SaveChangesAsync();
        return Ok(person);
    }

    /// <summary>
    /// Actualización PARCIAL. La usa el agente de WhatsApp para completar lo que
    /// va averiguando en la conversación (el nombre real, el correo) sin tocar
    /// nada más.
    ///
    /// Existe porque el `PUT` de arriba pisa con null todo campo que el payload
    /// no traiga: el agente, que sólo sabe el correo, borraría de paso la fecha
    /// de nacimiento y la ciudad que alguien cargó a mano en el panel. Es el
    /// mismo problema que ya se resolvió para el estado del prospecto.
    ///
    /// Recibe `JsonElement` crudo y no un DTO de campos nullable **a propósito**:
    /// con un DTO, "no mandé el campo" y "mandé el campo en null" llegan los dos
    /// como null y son indistinguibles — que es justamente la ambigüedad que este
    /// endpoint viene a evitar. Acá, la ausencia de la clave es la señal.
    ///
    /// Las claves van en snake_case, igual que todo el contrato.
    /// </summary>
    [HttpPatch("{id:int}")]
    public async Task<IActionResult> Patch(int id, [FromBody] JsonElement cambios)
    {
        var person = await db.People.FindAsync(id);
        if (person is null) return NotFound(new { message = "Persona no encontrada." });

        if (cambios.ValueKind != JsonValueKind.Object)
            return BadRequest(new { message = "El cuerpo tiene que ser un objeto JSON." });

        // Devuelve el texto sólo si la clave vino; "" y "   " cuentan como null,
        // porque un campo vaciado desde un formulario significa "no sé el dato",
        // no "guardá una cadena vacía".
        bool Texto(string clave, out string? valor)
        {
            valor = null;
            if (!cambios.TryGetProperty(clave, out var v)) return false;
            if (v.ValueKind == JsonValueKind.Null) return true;
            if (v.ValueKind != JsonValueKind.String) return false;
            var s = v.GetString();
            valor = string.IsNullOrWhiteSpace(s) ? null : s!.Trim();
            return true;
        }

        // Los obligatorios se pueden cambiar pero no vaciar: una persona sin
        // nombre rompe el panel y los listados.
        if (Texto("first_name", out var nombre))
        {
            if (nombre is null) return BadRequest(new { message = "first_name no puede quedar vacío." });
            person.FirstName = nombre;
        }
        if (Texto("last_name", out var apellido))
        {
            if (apellido is null) return BadRequest(new { message = "last_name no puede quedar vacío." });
            person.LastName = apellido;
        }

        if (Texto("email", out var email)) person.Email = email;
        if (Texto("ci", out var ci)) person.Ci = ci;
        if (Texto("ci_complement", out var comp)) person.CiComplement = comp;

        if (cambios.TryGetProperty("city_id", out var ciudad))
        {
            if (ciudad.ValueKind == JsonValueKind.Null) person.CityId = null;
            // El ValueKind se comprueba ANTES: TryGetInt32 sobre un JSON de texto no
            // devuelve false, lanza — y un tipo mal mandado saldría como 500.
            else if (ciudad.ValueKind == JsonValueKind.Number && ciudad.TryGetInt32(out var cityId))
            {
                if (!await db.Cities.AnyAsync(c => c.Id == cityId))
                    return BadRequest(new { message = $"No existe la ciudad {cityId}." });
                person.CityId = cityId;
            }
            else return BadRequest(new { message = "city_id tiene que ser un número o null." });
        }

        if (cambios.TryGetProperty("birth_date", out var nacimiento))
        {
            if (nacimiento.ValueKind == JsonValueKind.Null) person.BirthDate = null;
            else if (nacimiento.ValueKind == JsonValueKind.String
                     && DateOnly.TryParse(nacimiento.GetString(), out var fecha)) person.BirthDate = fecha;
            else return BadRequest(new { message = "birth_date tiene que ser una fecha AAAA-MM-DD o null." });
        }

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // `people.email` y `people.ci` son ÚNICOS. Pasa de verdad: dos
            // hermanos inscritos con el correo de la madre, o un prospecto que
            // da un correo que ya está cargado a nombre de otro. Sin esto, el
            // agente cortaría la conversación con un 500 por un dato repetido.
            return Conflict(new
            {
                message = "Ese correo o CI ya está cargado en otra persona. No se guardó el cambio; el resto de la conversación sigue igual.",
            });
        }

        return Ok(await db.People.AsNoTracking()
            .Include(p => p.City).Include(p => p.Phones)
            .FirstAsync(p => p.Id == id));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Destroy(int id)
    {
        var person = await db.People.FindAsync(id);
        if (person is null) return NotFound();
        db.People.Remove(person);
        await db.SaveChangesAsync();
        return NoContent();
    }
}

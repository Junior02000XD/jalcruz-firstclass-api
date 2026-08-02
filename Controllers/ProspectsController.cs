using JalcruzFirstClass.Api.Data;
using JalcruzFirstClass.Api.Domain;
using JalcruzFirstClass.Api.Dtos;
using JalcruzFirstClass.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace JalcruzFirstClass.Api.Controllers;

[ApiController]
[Route("api/prospects")]
[Authorize(Roles = $"{Roles.CrmAdmin},{Roles.SuperAdmin}")]
public class ProspectsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] string? status)
    {
        var query = db.Prospects.AsNoTracking()
            .Include(p => p.Person)
            .Include(p => p.Campaign)
            .Include(p => p.Zone)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var parsed = ParseStatus(status);
            query = query.Where(p => p.Status == parsed);
        }

        return Ok(await query.OrderByDescending(p => p.Id).ToListAsync());
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Show(int id)
    {
        var prospect = await db.Prospects.AsNoTracking()
            .Include(p => p.Person).ThenInclude(pe => pe.Phones)
            .Include(p => p.Campaign).Include(p => p.Zone).Include(p => p.Entity)
            .Include(p => p.AssignedTo)
            .Include(p => p.TrialClasses)
            .Include(p => p.Reminders)
            .Include(p => p.Enrollments).ThenInclude(e => e.Product)
            .FirstOrDefaultAsync(p => p.Id == id);
        return prospect is null ? NotFound() : Ok(prospect);
    }

    /// <summary>
    /// Busca el prospecto dueño de un número de WhatsApp. Es la primera llamada de
    /// n8n ante cada mensaje entrante: Meta manda el número con código de país y
    /// acá se compara contra la forma canónica guardada (ver PhoneNormalizer).
    /// Si una persona tiene más de un prospecto, gana el más reciente.
    /// </summary>
    [HttpGet("by-phone/{number}")]
    public async Task<IActionResult> ByPhone(string number)
    {
        var normalized = PhoneNormalizer.Normalize(number);
        if (normalized is null)
            return BadRequest(new { message = "El número no tiene ningún dígito." });

        var prospect = await FindByNormalizedPhoneAsync(normalized);
        return prospect is null ? NotFound() : Ok(prospect);
    }

    [HttpPost]
    public async Task<IActionResult> Store(ProspectInput input)
    {
        var prospect = new Prospect
        {
            PersonId = input.PersonId,
            CampaignId = input.CampaignId,
            EntityId = input.EntityId,
            ZoneId = input.ZoneId,
            Origin = input.Origin,
            Address = input.Address,
            Notes = input.Notes,
            Status = ParseStatus(input.Status),
        };
        db.Prospects.Add(prospect);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Show), new { id = prospect.Id }, prospect);
    }

    /// <summary>
    /// Alta rápida desde móvil o desde n8n: crea Persona (+ teléfono opcional) y
    /// Prospecto en una transacción. Es idempotente por teléfono — si el número ya
    /// pertenece a un prospecto devuelve ese (200) en vez de duplicarlo.
    ///
    /// La deduplicación NO puede ser sólo "buscar y después insertar": una ráfaga de
    /// mensajes de WhatsApp dispara varias ejecuciones de n8n a la vez para el mismo
    /// número, y las dos buscarían antes de que cualquiera inserte. Por eso la
    /// transacción toma primero un advisory lock de Postgres sobre el número
    /// normalizado: la segunda ejecución espera y encuentra al prospecto ya creado.
    /// El lock es del motor, no del proceso, así que aguanta varias instancias de la API.
    /// </summary>
    [HttpPost("quick")]
    public async Task<IActionResult> QuickCreate(ProspectQuickInput input)
    {
        var normalized = PhoneNormalizer.Normalize(input.Phone);

        // Camino rápido: sin lock ni transacción para el caso habitual de "ya existe".
        if (normalized is not null)
        {
            var known = await FindByNormalizedPhoneAsync(normalized);
            if (known is not null) return Ok(known);
        }

        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            if (normalized is not null)
            {
                // Se libera solo al terminar la transacción (xact). Serializa únicamente
                // a quienes traen el MISMO número: dos prospectos distintos no se estorban.
                await db.Database.ExecuteSqlAsync(
                    $"SELECT pg_advisory_xact_lock(hashtextextended({normalized}, 0))");

                var raced = await FindByNormalizedPhoneAsync(normalized);
                if (raced is not null)
                {
                    await tx.CommitAsync();
                    return Ok(raced);
                }
            }

            var person = new Person
            {
                FirstName = input.FirstName,
                LastName = string.IsNullOrWhiteSpace(input.LastName) ? "" : input.LastName,
            };
            db.People.Add(person);
            await db.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(input.Phone))
            {
                // NormalizedNumber lo completa AppDbContext.SaveChanges.
                db.Phones.Add(new Phone { PersonId = person.Id, Number = input.Phone.Trim(), Label = "WhatsApp" });
                await db.SaveChangesAsync();
            }

            var prospect = new Prospect
            {
                PersonId = person.Id,
                CampaignId = input.CampaignId,
                ZoneId = input.ZoneId,
                Origin = input.Origin,
                Address = input.Address,
                Notes = input.Notes,
                Status = ParseStatus(input.Status),
            };
            db.Prospects.Add(prospect);
            await db.SaveChangesAsync();

            await tx.CommitAsync();

            await db.Entry(prospect).Reference(p => p.Person).LoadAsync();
            return CreatedAtAction(nameof(Show), new { id = prospect.Id }, prospect);
        }
        catch (DbUpdateException ex) when (normalized is not null && IsUniqueViolation(ex))
        {
            // Red de seguridad por si el índice de phones se promueve a único algún día
            // (ver AppDbContext): la carrera termina en 200 con el existente, no en 500.
            await tx.RollbackAsync();
            db.ChangeTracker.Clear();

            var existing = await FindByNormalizedPhoneAsync(normalized);
            if (existing is null) throw;
            return Ok(existing);
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, ProspectInput input)
    {
        var prospect = await db.Prospects.FindAsync(id);
        if (prospect is null) return NotFound();
        prospect.PersonId = input.PersonId;
        prospect.CampaignId = input.CampaignId;
        prospect.EntityId = input.EntityId;
        prospect.ZoneId = input.ZoneId;
        prospect.Origin = input.Origin;
        prospect.Address = input.Address;
        prospect.Notes = input.Notes;
        if (!string.IsNullOrWhiteSpace(input.Status))
            prospect.Status = ParseStatus(input.Status);
        await db.SaveChangesAsync();
        return Ok(prospect);
    }

    /// <summary>
    /// Mueve el prospecto en el embudo tocando SÓLO el estado. Existe para que el
    /// agente de n8n no tenga que mandar el PUT completo, que pisaría con null
    /// todo campo ausente del payload.
    /// </summary>
    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, ProspectStatusPatchInput input)
    {
        var prospect = await db.Prospects.FindAsync(id);
        if (prospect is null) return NotFound();

        // Estricto, al revés que ParseStatus: en un PATCH, un valor mal escrito por
        // la IA no puede terminar silenciosamente en "nuevo" y perder el avance.
        if (!TryParseStatus(input.Status, out var status))
            return BadRequest(new
            {
                message = $"Estado inválido '{input.Status}'.",
                valid_values = EnumMaps.ProspectStatus.Values,
            });

        prospect.Status = status;
        await db.SaveChangesAsync();

        await db.Entry(prospect).Reference(p => p.AssignedTo).LoadAsync();
        return Ok(prospect);
    }

    /// <summary>
    /// Hand-off: pasa la conversación a un humano (assigned_to_user_id) o se la
    /// devuelve al agente de IA mandando null. Va aparte del PATCH de estado
    /// porque son decisiones independientes — derivar a mamá no cambia el embudo —
    /// y porque al ser el único campo del cuerpo, null significa "limpiar" sin
    /// confundirse con "no lo mandé".
    /// </summary>
    [HttpPatch("{id:int}/assignment")]
    public async Task<IActionResult> UpdateAssignment(int id, ProspectAssignmentPatchInput input)
    {
        var prospect = await db.Prospects.FindAsync(id);
        if (prospect is null) return NotFound();

        if (input.AssignedToUserId is int userId && !await db.Users.AnyAsync(u => u.Id == userId))
            return BadRequest(new { message = $"No existe el usuario {userId}." });

        prospect.AssignedToUserId = input.AssignedToUserId;
        await db.SaveChangesAsync();

        await db.Entry(prospect).Reference(p => p.AssignedTo).LoadAsync();
        return Ok(prospect);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Destroy(int id)
    {
        var prospect = await db.Prospects.FindAsync(id);
        if (prospect is null) return NotFound();
        db.Prospects.Remove(prospect);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Prospecto dueño de un número ya normalizado, con lo que n8n necesita para decidir.</summary>
    private Task<Prospect?> FindByNormalizedPhoneAsync(string normalized) =>
        db.Prospects.AsNoTracking()
            .Include(p => p.Person).ThenInclude(pe => pe.Phones)
            .Include(p => p.Campaign).Include(p => p.Zone)
            .Include(p => p.AssignedTo)
            .Where(p => p.Person.Phones.Any(ph => ph.NormalizedNumber == normalized))
            .OrderByDescending(p => p.Id)
            .FirstOrDefaultAsync();

    /// <summary>23505 = unique_violation de Postgres.</summary>
    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private static ProspectStatus ParseStatus(string? value)
        => TryParseStatus(value, out var status) ? status : ProspectStatus.New;

    private static bool TryParseStatus(string? value, out ProspectStatus status)
    {
        status = ProspectStatus.New;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var match = EnumMaps.ProspectStatus.FirstOrDefault(kv =>
            string.Equals(kv.Value, value, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kv.Key.ToString(), value, StringComparison.OrdinalIgnoreCase));
        if (match.Value is null) return false;

        status = match.Key;
        return true;
    }
}

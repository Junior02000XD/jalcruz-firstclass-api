using JalcruzFirstClass.Api.Data;
using JalcruzFirstClass.Api.Domain;
using JalcruzFirstClass.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JalcruzFirstClass.Api.Controllers;

/// <summary>
/// Contenido de negocio que el agente lee para armar el prompt: preguntas
/// frecuentes, reglas, promociones y flujos del embudo. Lo cargan los dueños
/// desde el panel; el agente lo consume por /api/agent-context.
/// </summary>
[ApiController]
[Route("api/context-entries")]
[Authorize(Roles = $"{Roles.CrmAdmin},{Roles.SuperAdmin}")]
public class ContextEntriesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] string? type, [FromQuery] bool? active)
    {
        var query = BaseQuery().AsQueryable();

        if (!string.IsNullOrWhiteSpace(type))
        {
            if (!EnumMaps.TryParse(EnumMaps.ContextEntryType, type, out var parsed))
                return BadRequest(new { message = $"Tipo inválido '{type}'.", valid_values = EnumMaps.ContextEntryType.Values });
            query = query.Where(c => c.Type == parsed);
        }
        if (active.HasValue) query = query.Where(c => c.Active == active.Value);

        return Ok(await query.OrderBy(c => c.Type).ThenByDescending(c => c.Id).ToListAsync());
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Show(int id)
    {
        var entry = await BaseQuery().FirstOrDefaultAsync(c => c.Id == id);
        return entry is null ? NotFound() : Ok(entry);
    }

    [HttpPost]
    public async Task<IActionResult> Store(ContextEntryInput input)
    {
        var entry = new ContextEntry();
        var error = await ApplyAsync(entry, input);
        if (error is not null) return error;

        db.ContextEntries.Add(entry);
        await db.SaveChangesAsync();
        await SetMediaAsync(entry.Id, input.MediaAssetIds);
        // Si la ficha no es una promoción, la lista se vacía: una regla con zonas
        // colgadas sería una restricción invisible que nadie puede ver ni editar.
        await SetZonesAsync(entry.Id, entry.Type == ContextEntryType.Promotion ? input.RestrictedZoneIds : new List<int>());

        return CreatedAtAction(nameof(Show), new { id = entry.Id }, await BaseQuery().FirstAsync(c => c.Id == entry.Id));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, ContextEntryInput input)
    {
        var entry = await db.ContextEntries.FindAsync(id);
        if (entry is null) return NotFound();

        var error = await ApplyAsync(entry, input);
        if (error is not null) return error;

        await db.SaveChangesAsync();
        await SetMediaAsync(entry.Id, input.MediaAssetIds);
        // Si la ficha no es una promoción, la lista se vacía: una regla con zonas
        // colgadas sería una restricción invisible que nadie puede ver ni editar.
        await SetZonesAsync(entry.Id, entry.Type == ContextEntryType.Promotion ? input.RestrictedZoneIds : new List<int>());

        db.ChangeTracker.Clear();
        return Ok(await BaseQuery().FirstAsync(c => c.Id == id));
    }

    /// <summary>
    /// Interruptor de la tarjeta del panel. Aparte del PUT para que encender o
    /// apagar una entrada no dependa de reenviar bien todo el resto del formulario.
    /// </summary>
    [HttpPatch("{id:int}/active")]
    public async Task<IActionResult> SetActive(int id, [FromBody] bool active)
    {
        var entry = await db.ContextEntries.FindAsync(id);
        if (entry is null) return NotFound();

        entry.Active = active;
        await db.SaveChangesAsync();
        return Ok(entry);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Destroy(int id)
    {
        var entry = await db.ContextEntries.FindAsync(id);
        if (entry is null) return NotFound();

        db.ContextEntries.Remove(entry);   // las filas de context_entry_media caen en cascada
        await db.SaveChangesAsync();
        return NoContent();
    }

    private IQueryable<ContextEntry> BaseQuery() =>
        db.ContextEntries.AsNoTracking()
            .Include(c => c.Zones).ThenInclude(z => z.Zone)
            .Include(c => c.HandoffToUser)
            .Include(c => c.Media).ThenInclude(m => m.MediaAsset);

    /// <summary>Vuelca el input en la entidad. Devuelve un 400 armado, o null si está todo bien.</summary>
    private async Task<IActionResult?> ApplyAsync(ContextEntry entry, ContextEntryInput input)
    {
        if (!EnumMaps.TryParse(EnumMaps.ContextEntryType, input.Type, out var type))
            return BadRequest(new { message = $"Tipo inválido '{input.Type}'.", valid_values = EnumMaps.ContextEntryType.Values });

        NextAction? nextAction = null;
        if (!string.IsNullOrWhiteSpace(input.NextAction))
        {
            if (!EnumMaps.TryParse(EnumMaps.NextAction, input.NextAction, out var parsed))
                return BadRequest(new { message = $"Acción inválida '{input.NextAction}'.", valid_values = EnumMaps.NextAction.Values });
            nextAction = parsed;
        }

        if (input.RestrictedZoneIds is { Count: > 0 } pedidas)
        {
            var existen = await db.Zones.Where(z => pedidas.Contains(z.Id)).Select(z => z.Id).ToListAsync();
            var faltan = pedidas.Distinct().Except(existen).ToList();
            if (faltan.Count > 0)
                return BadRequest(new { message = $"No existen las zonas: {string.Join(", ", faltan)}." });
        }

        if (input.HandoffToUserId is int userId && !await db.Users.AnyAsync(u => u.Id == userId))
            return BadRequest(new { message = $"No existe el usuario {userId}." });

        entry.Type = type;
        entry.Title = input.Title;
        entry.Content = input.Content;
        entry.Active = input.Active ?? true;

        // Los campos de un tipo se limpian si la entrada es de otro: si una
        // promoción se convierte en regla, su vencimiento no puede quedar colgado
        // y reapareciendo en el prompt.
        entry.ValidUntil = type == ContextEntryType.Promotion ? input.ValidUntil : null;
        entry.ConditionsText = type == ContextEntryType.Promotion ? input.ConditionsText : null;
        entry.NextAction = type == ContextEntryType.Flow ? nextAction : null;
        entry.HandoffToUserId = type == ContextEntryType.Flow ? input.HandoffToUserId : null;

        return null;
    }

    /// <summary>
    /// Reemplaza las zonas a las que se limita la ficha. Mismo criterio que los
    /// archivos: null = "no la toques"; lista vacía = **vale para todas**, que es
    /// también lo que se fuerza cuando la ficha deja de ser una promoción.
    /// </summary>
    private async Task SetZonesAsync(int entryId, List<int>? zoneIds)
    {
        if (zoneIds is null) return;

        var current = await db.ContextEntryZones.Where(z => z.ContextEntryId == entryId).ToListAsync();
        db.ContextEntryZones.RemoveRange(current);

        // Distinct porque la clave es compuesta: un id repetido en el payload
        // reventaría con violación de clave primaria.
        var wanted = zoneIds.Distinct().ToList();
        var existing = await db.Zones.Where(z => wanted.Contains(z.Id)).Select(z => z.Id).ToListAsync();

        foreach (var zoneId in existing)
            db.ContextEntryZones.Add(new ContextEntryZone { ContextEntryId = entryId, ZoneId = zoneId });

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Reemplaza la lista de archivos asociados. Null significa "no la toques"
    /// (para un PUT que sólo edita el texto); una lista vacía la borra.
    /// </summary>
    private async Task SetMediaAsync(int entryId, List<int>? mediaAssetIds)
    {
        if (mediaAssetIds is null) return;

        var current = await db.ContextEntryMedia.Where(m => m.ContextEntryId == entryId).ToListAsync();
        db.ContextEntryMedia.RemoveRange(current);

        // Distinct porque la clave es compuesta: un id repetido en el payload
        // reventaría con violación de clave primaria.
        var wanted = mediaAssetIds.Distinct().ToList();
        var existing = await db.MediaAssets.Where(a => wanted.Contains(a.Id)).Select(a => a.Id).ToListAsync();

        foreach (var assetId in existing)
            db.ContextEntryMedia.Add(new ContextEntryMedia { ContextEntryId = entryId, MediaAssetId = assetId });

        await db.SaveChangesAsync();
    }
}

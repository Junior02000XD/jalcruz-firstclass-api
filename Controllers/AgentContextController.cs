using JalcruzFirstClass.Api.Data;
using JalcruzFirstClass.Api.Domain;
using JalcruzFirstClass.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JalcruzFirstClass.Api.Controllers;

/// <summary>
/// Todo lo que n8n necesita para armar el prompt de un mensaje entrante, en una
/// sola llamada: la voz del número y el contenido de negocio encendido.
///
/// La respuesta es DETERMINÍSTICA: con el mismo contenido, dos llamadas devuelven
/// exactamente los mismos bytes. Eso es lo que permitirá activar el prompt caching
/// de Claude más adelante sin rehacer nada — un orden que cambie entre llamadas
/// invalidaría el caché en cada mensaje. De ahí que se ordene explícitamente todo
/// (entradas y archivos) y que se usen DTOs sin timestamps.
/// </summary>
[ApiController]
[Route("api/agent-context")]
[Authorize(Roles = $"{Roles.CrmAdmin},{Roles.SuperAdmin}")]
public class AgentContextController(AppDbContext db) : ControllerBase
{
    /// <summary>
    /// Zona horaria de Bolivia. Se compara el vencimiento contra la fecha LOCAL:
    /// con UTC, una promoción que vence hoy se apagaría a las 20:00 de Santa Cruz,
    /// en pleno horario de atención.
    /// </summary>
    private static readonly TimeSpan BoliviaOffset = TimeSpan.FromHours(-4);

    [HttpGet("{phoneNumberId}")]
    public async Task<IActionResult> Show(string phoneNumberId)
    {
        var persona = await db.Personas.AsNoTracking()
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.PhoneNumberId == phoneNumberId && p.Active);

        // 404 explícito: para n8n significa "este número está pausado", no un error.
        if (persona is null)
            return NotFound(new
            {
                message = $"No hay una persona activa para el número {phoneNumberId}.",
                phone_number_id = phoneNumberId,
                paused = true,
            });

        var today = DateOnly.FromDateTime(DateTime.UtcNow + BoliviaOffset);

        var entries = await db.ContextEntries.AsNoTracking()
            .Include(c => c.RestrictedZone)
            .Include(c => c.HandoffToUser)
            .Include(c => c.Media).ThenInclude(m => m.MediaAsset)
            .Where(c => c.Active)
            // Una promoción vencida no se manda: la IA no puede ofrecer algo que ya no existe.
            // Sin fecha = sin vencimiento.
            .Where(c => c.ValidUntil == null || c.ValidUntil >= today)
            .ToListAsync();

        // El orden se fija acá y no en SQL: por el valor del enum (preguntas,
        // reglas, promociones, flujos) y después por id. Ordenar por la columna
        // dejaría el orden atado al texto español de cada enum.
        var ordered = entries
            .OrderBy(c => c.Type)
            .ThenBy(c => c.Id)
            .Select(c => new AgentContextEntryDto(
                c.Id,
                EnumMaps.ContextEntryType[c.Type],
                c.Title,
                c.Content,
                c.ValidUntil,
                c.RestrictedZoneId,
                c.RestrictedZone?.Name,
                c.ConditionsText,
                c.NextAction is null ? null : EnumMaps.NextAction[c.NextAction.Value],
                c.HandoffToUserId,
                c.HandoffToUser?.Name,
                c.Media
                    .OrderBy(m => m.MediaAssetId)
                    .Select(m => new AgentMediaDto(
                        m.MediaAsset.Id,
                        EnumMaps.MediaType[m.MediaAsset.Type],
                        m.MediaAsset.UrlR2,
                        m.MediaAsset.Label,
                        m.MediaAsset.Transcript))
                    .ToList()))
            .ToList();

        return Ok(new AgentContextResponse(
            new AgentPersonaDto(persona.Id, persona.PhoneNumberId, persona.StyleGuide, persona.UserId, persona.User.Name),
            ordered));
    }
}

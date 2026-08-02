using JalcruzFirstClass.Api.Data;
using JalcruzFirstClass.Api.Domain;
using JalcruzFirstClass.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace JalcruzFirstClass.Api.Controllers;

[ApiController]
[Route("api/messages")]
[Authorize(Roles = $"{Roles.CrmAdmin},{Roles.SuperAdmin}")]
public class MessagesController(AppDbContext db) : ControllerBase
{
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Show(int id)
    {
        var message = await db.Messages.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
        return message is null ? NotFound() : Ok(message);
    }

    /// <summary>
    /// Registra un mensaje del historial. Es idempotente por whatsapp_message_id:
    /// Meta reintenta el webhook cuando no recibe el 200 a tiempo, y el mismo wamid
    /// puede llegar dos veces. La garantía la da el índice único filtrado de la
    /// tabla, no un chequeo previo — dos reintentos concurrentes lo pasarían los dos.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Store(MessageInput input)
    {
        if (!await db.Prospects.AnyAsync(p => p.Id == input.ProspectId))
            return BadRequest(new { message = $"No existe el prospecto {input.ProspectId}." });

        if (!TryParseDirection(input.Direction, out var direction))
            return BadRequest(new
            {
                message = $"Dirección inválida '{input.Direction}'.",
                valid_values = EnumMaps.MessageDirection.Values,
            });

        if (!TryParseOrigin(input.Origin, out var origin))
            return BadRequest(new
            {
                message = $"Origen inválido '{input.Origin}'.",
                valid_values = EnumMaps.MessageOrigin.Values,
            });

        var wamid = string.IsNullOrWhiteSpace(input.WhatsappMessageId) ? null : input.WhatsappMessageId.Trim();

        // Camino rápido del reintento de Meta, que es el caso habitual.
        if (wamid is not null)
        {
            var known = await FindByWamidAsync(wamid);
            if (known is not null) return Ok(known);
        }

        var message = new Message
        {
            ProspectId = input.ProspectId,
            Direction = direction,
            Origin = origin,
            Content = input.Content ?? "",   // un adjunto sin epígrafe no trae texto
            MediaAssetId = input.MediaAssetId,
            WhatsappMediaUrl = input.WhatsappMediaUrl,
            WhatsappMessageId = wamid,
        };

        try
        {
            db.Messages.Add(message);
            await db.SaveChangesAsync();
            return CreatedAtAction(nameof(Show), new { id = message.Id }, message);
        }
        catch (DbUpdateException ex) when (wamid is not null && IsUniqueViolation(ex))
        {
            // Dos entregas del mismo webhook en paralelo: la que perdió devuelve
            // el mensaje que guardó la otra, no un 500.
            db.ChangeTracker.Clear();
            var existing = await FindByWamidAsync(wamid);
            if (existing is null) throw;
            return Ok(existing);
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Destroy(int id)
    {
        var message = await db.Messages.FindAsync(id);
        if (message is null) return NotFound();
        db.Messages.Remove(message);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Historial de un prospecto en orden cronológico (más viejo primero), que es
    /// como lo espera el modelo. Con ?limit=N devuelve los N ÚLTIMOS manteniendo
    /// ese orden: recortar por el principio daría el arranque de la conversación
    /// en vez de lo que se acaba de hablar.
    /// </summary>
    [HttpGet("/api/prospects/{prospectId:int}/messages")]
    public async Task<IActionResult> ByProspect(int prospectId, [FromQuery] int? limit)
    {
        if (!await db.Prospects.AnyAsync(p => p.Id == prospectId)) return NotFound();

        var query = db.Messages.AsNoTracking().Where(m => m.ProspectId == prospectId);

        if (limit is int take)
        {
            if (take <= 0) return BadRequest(new { message = "limit debe ser mayor que 0." });

            // Id como desempate: varios mensajes de una ráfaga comparten created_at.
            var last = await query
                .OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id)
                .Take(take)
                .ToListAsync();
            last.Reverse();
            return Ok(last);
        }

        return Ok(await query.OrderBy(m => m.CreatedAt).ThenBy(m => m.Id).ToListAsync());
    }

    private Task<Message?> FindByWamidAsync(string wamid) =>
        db.Messages.AsNoTracking().FirstOrDefaultAsync(m => m.WhatsappMessageId == wamid);

    /// <summary>23505 = unique_violation de Postgres.</summary>
    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private static bool TryParseDirection(string? value, out MessageDirection direction)
    {
        direction = MessageDirection.Inbound;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var match = EnumMaps.MessageDirection.FirstOrDefault(kv =>
            string.Equals(kv.Value, value, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kv.Key.ToString(), value, StringComparison.OrdinalIgnoreCase));
        if (match.Value is null) return false;

        direction = match.Key;
        return true;
    }

    /// <summary>Origen omitido = "humano", el supuesto conservador: no marca como IA lo que no lo es.</summary>
    private static bool TryParseOrigin(string? value, out MessageOrigin origin)
    {
        origin = MessageOrigin.Human;
        if (string.IsNullOrWhiteSpace(value)) return true;

        var match = EnumMaps.MessageOrigin.FirstOrDefault(kv =>
            string.Equals(kv.Value, value, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kv.Key.ToString(), value, StringComparison.OrdinalIgnoreCase));
        if (match.Value is null) return false;

        origin = match.Key;
        return true;
    }
}

using JalcruzFirstClass.Api.Data;
using JalcruzFirstClass.Api.Domain;
using JalcruzFirstClass.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JalcruzFirstClass.Api.Controllers;

[ApiController]
[Route("api/reminders")]
[Authorize(Roles = $"{Roles.CrmAdmin},{Roles.SuperAdmin}")]
public class RemindersController(AppDbContext db) : ControllerBase
{
    /// <summary>
    /// Recordatorios, del más próximo al más lejano. Los filtros están pensados
    /// para la consulta que hace el agente de n8n cada vez que revisa qué
    /// seguimientos vencieron: ?is_done=false&amp;due_before=2026-08-02T12:00:00Z.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(
        [FromQuery(Name = "is_done")] bool? isDone,
        [FromQuery(Name = "due_before")] DateTime? dueBefore,
        [FromQuery(Name = "prospect_id")] int? prospectId)
    {
        var query = db.Reminders.AsNoTracking()
            .Include(r => r.Prospect).ThenInclude(p => p.Person)
            .AsQueryable();

        if (isDone.HasValue) query = query.Where(r => r.IsDone == isDone.Value);
        if (prospectId.HasValue) query = query.Where(r => r.ProspectId == prospectId.Value);
        if (dueBefore.HasValue)
        {
            var limit = ToUtc(dueBefore.Value);
            query = query.Where(r => r.RemindAt <= limit);
        }

        return Ok(await query.OrderBy(r => r.RemindAt).ToListAsync());
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Show(int id)
    {
        var reminder = await db.Reminders.AsNoTracking()
            .Include(r => r.Prospect).ThenInclude(p => p.Person)
            .FirstOrDefaultAsync(r => r.Id == id);
        return reminder is null ? NotFound() : Ok(reminder);
    }

    [HttpPost]
    public async Task<IActionResult> Store(ReminderInput input)
    {
        if (!await db.Prospects.AnyAsync(p => p.Id == input.ProspectId))
            return BadRequest(new { message = $"No existe el prospecto {input.ProspectId}." });

        var reminder = new Reminder
        {
            ProspectId = input.ProspectId,
            Note = input.Note,
            RemindAt = ToUtc(input.RemindAt),
            IsDone = input.IsDone ?? false,
        };
        db.Reminders.Add(reminder);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Show), new { id = reminder.Id }, reminder);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, ReminderInput input)
    {
        var reminder = await db.Reminders.FindAsync(id);
        if (reminder is null) return NotFound();

        reminder.ProspectId = input.ProspectId;
        reminder.Note = input.Note;
        reminder.RemindAt = ToUtc(input.RemindAt);
        reminder.IsDone = input.IsDone ?? reminder.IsDone;
        await db.SaveChangesAsync();
        return Ok(reminder);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Destroy(int id)
    {
        var reminder = await db.Reminders.FindAsync(id);
        if (reminder is null) return NotFound();
        db.Reminders.Remove(reminder);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// remind_at es timestamptz: Npgsql rechaza un DateTime sin zona. Un valor
    /// sin sufijo ("2026-08-02T15:00:00") se toma como UTC en vez de reventar,
    /// que es lo que manda n8n cuando arma la fecha sin zona horaria.
    /// </summary>
    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };
}

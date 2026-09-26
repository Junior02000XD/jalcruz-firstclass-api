using JalcruzFirstClass.Api.Data;
using JalcruzFirstClass.Api.Domain;
using JalcruzFirstClass.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JalcruzFirstClass.Api.Controllers;

[ApiController]
[Route("api/trial-classes")]
[Authorize(Roles = $"{Roles.CrmAdmin},{Roles.SuperAdmin}")]
public class TrialClassesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index()
        => Ok(await db.TrialClasses.AsNoTracking()
            .Include(t => t.Prospect).ThenInclude(p => p.Person)
            .Include(t => t.Teacher)
            .OrderByDescending(t => t.Schedule).ToListAsync());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Show(int id)
    {
        var tc = await db.TrialClasses.AsNoTracking()
            .Include(t => t.Prospect).ThenInclude(p => p.Person)
            .Include(t => t.Teacher)
            .FirstOrDefaultAsync(t => t.Id == id);
        return tc is null ? NotFound() : Ok(tc);
    }

    [HttpPost]
    public async Task<IActionResult> Store(TrialClassInput input)
    {
        var tc = new TrialClass
        {
            ProspectId = input.ProspectId,
            TeacherId = input.TeacherId,
            Schedule = ToUtc(input.Schedule),
            AttendanceBool = input.AttendanceBool ?? false,
            Status = ParseStatus(input.Status),
            ReprogrammedFromId = input.ReprogrammedFromId,
        };
        db.TrialClasses.Add(tc);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Show), new { id = tc.Id }, tc);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, TrialClassInput input)
    {
        var tc = await db.TrialClasses.FindAsync(id);
        if (tc is null) return NotFound();
        tc.ProspectId = input.ProspectId;
        tc.TeacherId = input.TeacherId;
        tc.Schedule = ToUtc(input.Schedule);
        tc.AttendanceBool = input.AttendanceBool ?? tc.AttendanceBool;
        if (!string.IsNullOrWhiteSpace(input.Status))
            tc.Status = ParseStatus(input.Status);
        // Se conserva si no viene: el panel no lo edita y un PUT sin él
        // cortaba el vínculo con la clase original.
        tc.ReprogrammedFromId = input.ReprogrammedFromId ?? tc.ReprogrammedFromId;
        await db.SaveChangesAsync();
        return Ok(tc);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Destroy(int id)
    {
        var tc = await db.TrialClasses.FindAsync(id);
        if (tc is null) return NotFound();
        db.TrialClasses.Remove(tc);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// `schedule` es timestamptz y Npgsql rechaza un DateTime con Kind=Unspecified:
    /// con el SpecifyKind(Unspecified) de antes, crear o editar una clase daba 500
    /// siempre. Mismo criterio que RemindersController: sin sufijo se toma como UTC.
    /// El panel manda ISO con Z (convierte la hora local de Bolivia antes).
    /// </summary>
    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };

    private static TrialClassStatus ParseStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return TrialClassStatus.Scheduled;
        var match = EnumMaps.TrialClassStatus.FirstOrDefault(kv =>
            string.Equals(kv.Value, value, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kv.Key.ToString(), value, StringComparison.OrdinalIgnoreCase));
        return match.Value is null ? TrialClassStatus.Scheduled : match.Key;
    }
}

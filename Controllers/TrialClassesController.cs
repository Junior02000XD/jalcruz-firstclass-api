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
            Schedule = DateTime.SpecifyKind(input.Schedule, DateTimeKind.Unspecified),
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
        tc.Schedule = DateTime.SpecifyKind(input.Schedule, DateTimeKind.Unspecified);
        tc.AttendanceBool = input.AttendanceBool ?? tc.AttendanceBool;
        if (!string.IsNullOrWhiteSpace(input.Status))
            tc.Status = ParseStatus(input.Status);
        tc.ReprogrammedFromId = input.ReprogrammedFromId;
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

    private static TrialClassStatus ParseStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return TrialClassStatus.Scheduled;
        var match = EnumMaps.TrialClassStatus.FirstOrDefault(kv =>
            string.Equals(kv.Value, value, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kv.Key.ToString(), value, StringComparison.OrdinalIgnoreCase));
        return match.Value is null ? TrialClassStatus.Scheduled : match.Key;
    }
}

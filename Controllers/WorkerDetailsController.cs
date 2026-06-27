using JalcruzFirstClass.Api.Data;
using JalcruzFirstClass.Api.Domain;
using JalcruzFirstClass.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JalcruzFirstClass.Api.Controllers;

[ApiController]
[Route("api/worker-details")]
[Authorize(Roles = $"{Roles.HrAdmin},{Roles.SuperAdmin}")]
public class WorkerDetailsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index()
        => Ok(await db.WorkerDetails.AsNoTracking()
            .Include(w => w.Person).ThenInclude(p => p!.City)
            .OrderBy(w => w.Person.FirstName).ToListAsync());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Show(int id)
    {
        var worker = await db.WorkerDetails.AsNoTracking()
            .Include(w => w.Person).ThenInclude(p => p!.City)
            .FirstOrDefaultAsync(w => w.Id == id);
        return worker is null ? NotFound() : Ok(worker);
    }

    [HttpPost]
    public async Task<IActionResult> Store(WorkerDetailInput input)
    {
        if (await db.WorkerDetails.AnyAsync(w => w.PersonId == input.PersonId))
            return BadRequest(new { message = "Esta persona ya tiene una ficha de trabajador." });

        var worker = new WorkerDetail
        {
            PersonId = input.PersonId,
            Reliability = ParseReliability(input.Reliability),
        };
        db.WorkerDetails.Add(worker);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Show), new { id = worker.Id }, worker);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, WorkerDetailInput input)
    {
        var worker = await db.WorkerDetails.FindAsync(id);
        if (worker is null) return NotFound();
        worker.Reliability = ParseReliability(input.Reliability);
        await db.SaveChangesAsync();
        return Ok(worker);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Destroy(int id)
    {
        var worker = await db.WorkerDetails.FindAsync(id);
        if (worker is null) return NotFound();
        db.WorkerDetails.Remove(worker);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static Reliability ParseReliability(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Reliability.Good;
        // Acepta tanto el valor persistido ("bueno") como el nombre del enum ("Good").
        var match = EnumMaps.Reliability.FirstOrDefault(kv =>
            string.Equals(kv.Value, value, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kv.Key.ToString(), value, StringComparison.OrdinalIgnoreCase));
        return match.Value is null ? Reliability.Good : match.Key;
    }
}

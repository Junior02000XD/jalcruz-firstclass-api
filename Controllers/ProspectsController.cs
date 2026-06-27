using JalcruzFirstClass.Api.Data;
using JalcruzFirstClass.Api.Domain;
using JalcruzFirstClass.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
            .Include(p => p.TrialClasses)
            .Include(p => p.Reminders)
            .FirstOrDefaultAsync(p => p.Id == id);
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

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Destroy(int id)
    {
        var prospect = await db.Prospects.FindAsync(id);
        if (prospect is null) return NotFound();
        db.Prospects.Remove(prospect);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static ProspectStatus ParseStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return ProspectStatus.New;
        var match = EnumMaps.ProspectStatus.FirstOrDefault(kv =>
            string.Equals(kv.Value, value, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kv.Key.ToString(), value, StringComparison.OrdinalIgnoreCase));
        return match.Value is null ? ProspectStatus.New : match.Key;
    }
}

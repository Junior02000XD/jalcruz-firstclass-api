using JalcruzFirstClass.Api.Data;
using JalcruzFirstClass.Api.Domain;
using JalcruzFirstClass.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JalcruzFirstClass.Api.Controllers;

[ApiController]
[Route("api/zones")]
[Authorize(Roles = $"{Roles.HrAdmin},{Roles.CrmAdmin},{Roles.SuperAdmin}")]
public class ZonesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index()
        => Ok(await db.Zones.AsNoTracking().OrderBy(z => z.Name).ToListAsync());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Show(int id)
    {
        var zone = await db.Zones.FindAsync(id);
        return zone is null ? NotFound() : Ok(zone);
    }

    [HttpPost]
    public async Task<IActionResult> Store(ZoneInput input)
    {
        var zone = new Zone { Name = input.Name };
        db.Zones.Add(zone);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Show), new { id = zone.Id }, zone);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, ZoneInput input)
    {
        var zone = await db.Zones.FindAsync(id);
        if (zone is null) return NotFound();
        zone.Name = input.Name;
        await db.SaveChangesAsync();
        return Ok(zone);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Destroy(int id)
    {
        var zone = await db.Zones.FindAsync(id);
        if (zone is null) return NotFound();
        db.Zones.Remove(zone);
        await db.SaveChangesAsync();
        return NoContent();
    }
}

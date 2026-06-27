using JalcruzFirstClass.Api.Data;
using JalcruzFirstClass.Api.Domain;
using JalcruzFirstClass.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JalcruzFirstClass.Api.Controllers;

[ApiController]
[Route("api/work-areas")]
[Authorize(Roles = $"{Roles.HrAdmin},{Roles.SuperAdmin}")]
public class WorkAreasController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index()
        => Ok(await db.WorkAreas.AsNoTracking()
            .Include(w => w.Company).Include(w => w.City).Include(w => w.InCharge)
            .OrderBy(w => w.Name).ToListAsync());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Show(int id)
    {
        var area = await db.WorkAreas.AsNoTracking()
            .Include(w => w.Company).Include(w => w.City).Include(w => w.InCharge)
            .FirstOrDefaultAsync(w => w.Id == id);
        return area is null ? NotFound() : Ok(area);
    }

    [HttpPost]
    public async Task<IActionResult> Store(WorkAreaInput input)
    {
        var area = new WorkArea
        {
            CompanyId = input.CompanyId,
            Name = input.Name,
            Location = input.Location,
            CityId = input.CityId,
            InChargeId = input.InChargeId,
        };
        db.WorkAreas.Add(area);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Show), new { id = area.Id }, area);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, WorkAreaInput input)
    {
        var area = await db.WorkAreas.FindAsync(id);
        if (area is null) return NotFound();
        area.CompanyId = input.CompanyId;
        area.Name = input.Name;
        area.Location = input.Location;
        area.CityId = input.CityId;
        area.InChargeId = input.InChargeId;
        await db.SaveChangesAsync();
        return Ok(area);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Destroy(int id)
    {
        var area = await db.WorkAreas.FindAsync(id);
        if (area is null) return NotFound();
        db.WorkAreas.Remove(area);
        await db.SaveChangesAsync();
        return NoContent();
    }
}

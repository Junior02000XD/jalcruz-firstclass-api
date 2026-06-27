using JalcruzFirstClass.Api.Data;
using JalcruzFirstClass.Api.Domain;
using JalcruzFirstClass.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JalcruzFirstClass.Api.Controllers;

[ApiController]
[Route("api/cities")]
[Authorize(Roles = $"{Roles.HrAdmin},{Roles.CrmAdmin},{Roles.SuperAdmin}")]
public class CitiesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index()
        => Ok(await db.Cities.AsNoTracking().OrderBy(c => c.Name).ToListAsync());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Show(int id)
    {
        var city = await db.Cities.FindAsync(id);
        return city is null ? NotFound() : Ok(city);
    }

    [HttpPost]
    public async Task<IActionResult> Store(CityInput input)
    {
        var city = new City { Name = input.Name };
        db.Cities.Add(city);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Show), new { id = city.Id }, city);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, CityInput input)
    {
        var city = await db.Cities.FindAsync(id);
        if (city is null) return NotFound();
        city.Name = input.Name;
        await db.SaveChangesAsync();
        return Ok(city);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Destroy(int id)
    {
        var city = await db.Cities.FindAsync(id);
        if (city is null) return NotFound();
        db.Cities.Remove(city);
        await db.SaveChangesAsync();
        return NoContent();
    }
}

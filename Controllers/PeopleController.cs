using JalcruzFirstClass.Api.Data;
using JalcruzFirstClass.Api.Domain;
using JalcruzFirstClass.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JalcruzFirstClass.Api.Controllers;

[ApiController]
[Route("api/people")]
[Authorize(Roles = $"{Roles.HrAdmin},{Roles.CrmAdmin},{Roles.SuperAdmin}")]
public class PeopleController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] string? search)
    {
        var query = db.People.AsNoTracking().Include(p => p.City).Include(p => p.Phones).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(p =>
                p.FirstName.ToLower().Contains(s) ||
                p.LastName.ToLower().Contains(s) ||
                (p.Ci != null && p.Ci.ToLower().Contains(s)));
        }

        return Ok(await query.OrderBy(p => p.FirstName).ThenBy(p => p.LastName).ToListAsync());
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Show(int id)
    {
        var person = await db.People.AsNoTracking()
            .Include(p => p.City).Include(p => p.Phones).Include(p => p.WorkerDetail)
            .FirstOrDefaultAsync(p => p.Id == id);
        return person is null ? NotFound() : Ok(person);
    }

    [HttpPost]
    public async Task<IActionResult> Store(PersonInput input)
    {
        var person = new Person
        {
            CityId = input.CityId,
            FirstName = input.FirstName,
            LastName = input.LastName,
            Ci = input.Ci,
            CiComplement = input.CiComplement,
            Email = input.Email,
            BirthDate = input.BirthDate,
        };
        db.People.Add(person);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Show), new { id = person.Id }, person);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, PersonInput input)
    {
        var person = await db.People.FindAsync(id);
        if (person is null) return NotFound();

        person.CityId = input.CityId;
        person.FirstName = input.FirstName;
        person.LastName = input.LastName;
        person.Ci = input.Ci;
        person.CiComplement = input.CiComplement;
        person.Email = input.Email;
        person.BirthDate = input.BirthDate;
        await db.SaveChangesAsync();
        return Ok(person);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Destroy(int id)
    {
        var person = await db.People.FindAsync(id);
        if (person is null) return NotFound();
        db.People.Remove(person);
        await db.SaveChangesAsync();
        return NoContent();
    }
}

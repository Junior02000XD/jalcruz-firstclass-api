using JalcruzFirstClass.Api.Data;
using JalcruzFirstClass.Api.Domain;
using JalcruzFirstClass.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JalcruzFirstClass.Api.Controllers;

[ApiController]
[Route("api/companies")]
[Authorize(Roles = $"{Roles.HrAdmin},{Roles.SuperAdmin}")]
public class CompaniesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index()
        => Ok(await db.Companies.AsNoTracking().OrderBy(c => c.Name).ToListAsync());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Show(int id)
    {
        var company = await db.Companies.AsNoTracking()
            .Include(c => c.WorkAreas)
            .FirstOrDefaultAsync(c => c.Id == id);
        return company is null ? NotFound() : Ok(company);
    }

    [HttpPost]
    public async Task<IActionResult> Store(CompanyInput input)
    {
        var company = new Company { Name = input.Name, BusinessName = input.BusinessName, Nit = input.Nit };
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Show), new { id = company.Id }, company);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, CompanyInput input)
    {
        var company = await db.Companies.FindAsync(id);
        if (company is null) return NotFound();
        company.Name = input.Name;
        company.BusinessName = input.BusinessName;
        company.Nit = input.Nit;
        await db.SaveChangesAsync();
        return Ok(company);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Destroy(int id)
    {
        var company = await db.Companies.FindAsync(id);
        if (company is null) return NotFound();
        db.Companies.Remove(company);
        await db.SaveChangesAsync();
        return NoContent();
    }
}

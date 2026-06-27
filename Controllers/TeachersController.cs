using JalcruzFirstClass.Api.Data;
using JalcruzFirstClass.Api.Domain;
using JalcruzFirstClass.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JalcruzFirstClass.Api.Controllers;

[ApiController]
[Route("api/teachers")]
[Authorize(Roles = $"{Roles.CrmAdmin},{Roles.SuperAdmin}")]
public class TeachersController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index()
        => Ok(await db.Teachers.AsNoTracking().OrderBy(t => t.Name).ToListAsync());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Show(int id)
    {
        var teacher = await db.Teachers.FindAsync(id);
        return teacher is null ? NotFound() : Ok(teacher);
    }

    [HttpPost]
    public async Task<IActionResult> Store(TeacherInput input)
    {
        var teacher = new Teacher { Name = input.Name, Specialty = input.Specialty };
        db.Teachers.Add(teacher);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Show), new { id = teacher.Id }, teacher);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, TeacherInput input)
    {
        var teacher = await db.Teachers.FindAsync(id);
        if (teacher is null) return NotFound();
        teacher.Name = input.Name;
        teacher.Specialty = input.Specialty;
        await db.SaveChangesAsync();
        return Ok(teacher);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Destroy(int id)
    {
        var teacher = await db.Teachers.FindAsync(id);
        if (teacher is null) return NotFound();
        db.Teachers.Remove(teacher);
        await db.SaveChangesAsync();
        return NoContent();
    }
}

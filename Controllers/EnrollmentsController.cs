using JalcruzFirstClass.Api.Data;
using JalcruzFirstClass.Api.Domain;
using JalcruzFirstClass.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JalcruzFirstClass.Api.Controllers;

[ApiController]
[Route("api/enrollments")]
[Authorize(Roles = $"{Roles.CrmAdmin},{Roles.SuperAdmin}")]
public class EnrollmentsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index()
        => Ok(await db.Enrollments.AsNoTracking()
            .Include(e => e.Product)
            .Include(e => e.Prospect).ThenInclude(p => p.Person)
            .OrderByDescending(e => e.EnrollmentDate).ThenByDescending(e => e.Id)
            .ToListAsync());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Show(int id)
    {
        var enrollment = await db.Enrollments.AsNoTracking()
            .Include(e => e.Product)
            .Include(e => e.Prospect).ThenInclude(p => p.Person)
            .FirstOrDefaultAsync(e => e.Id == id);
        return enrollment is null ? NotFound() : Ok(enrollment);
    }

    [HttpPost]
    public async Task<IActionResult> Store(EnrollmentInput input)
    {
        var enrollment = new Enrollment
        {
            ProspectId = input.ProspectId,
            ProductId = input.ProductId,
            EnrollmentDate = input.EnrollmentDate,
            ReceiptNumber = input.ReceiptNumber,
            Commission = input.Commission ?? 0,
        };
        db.Enrollments.Add(enrollment);

        // Registrar una venta marca al prospecto como inscrito (avanza el embudo).
        var prospect = await db.Prospects.FindAsync(input.ProspectId);
        if (prospect is not null) prospect.Status = ProspectStatus.Enrolled;

        await db.SaveChangesAsync();
        await db.Entry(enrollment).Reference(e => e.Product).LoadAsync();
        return StatusCode(201, new { message = "Venta registrada con éxito", data = enrollment });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, EnrollmentInput input)
    {
        var enrollment = await db.Enrollments.FindAsync(id);
        if (enrollment is null) return NotFound();
        enrollment.ProspectId = input.ProspectId;
        enrollment.ProductId = input.ProductId;
        enrollment.EnrollmentDate = input.EnrollmentDate;
        enrollment.ReceiptNumber = input.ReceiptNumber;
        enrollment.Commission = input.Commission ?? enrollment.Commission;
        await db.SaveChangesAsync();
        return Ok(enrollment);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Destroy(int id)
    {
        var enrollment = await db.Enrollments.FindAsync(id);
        if (enrollment is null) return NotFound();
        db.Enrollments.Remove(enrollment);
        await db.SaveChangesAsync();
        return NoContent();
    }
}

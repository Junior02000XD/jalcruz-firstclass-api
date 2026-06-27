using JalcruzFirstClass.Api.Data;
using JalcruzFirstClass.Api.Domain;
using JalcruzFirstClass.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JalcruzFirstClass.Api.Controllers;

[ApiController]
[Route("api/attendances")]
[Authorize(Roles = $"{Roles.HrAdmin},{Roles.SuperAdmin}")]
public class AttendancesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] int? payrollId)
    {
        var query = db.Attendances.AsNoTracking()
            .Include(a => a.Person).Include(a => a.Payroll).AsQueryable();
        if (payrollId is not null)
            query = query.Where(a => a.PayrollId == payrollId);
        return Ok(await query.OrderByDescending(a => a.Id).ToListAsync());
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Show(int id)
    {
        var att = await db.Attendances.AsNoTracking()
            .Include(a => a.Person)
            .Include(a => a.Payroll).ThenInclude(p => p.WorkArea)
            .FirstOrDefaultAsync(a => a.Id == id);
        return att is null ? NotFound() : Ok(att);
    }

    [HttpPost]
    public async Task<IActionResult> Store(AttendanceInput input)
    {
        var att = new Attendance
        {
            PayrollId = input.PayrollId,
            PersonId = input.PersonId,
            Date = input.Date,
            Status = ParseStatus(input.Status),
            Amount = input.Amount,
            ExtraAmount = input.ExtraAmount ?? 0,
            DidEat = input.DidEat ?? false,
            IsPaid = input.IsPaid ?? false,
        };
        db.Attendances.Add(att);
        await db.SaveChangesAsync();
        await db.Entry(att).Reference(a => a.Person).LoadAsync();
        return StatusCode(201, new { message = "Asistencia registrada con éxito", data = att });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, AttendanceInput input)
    {
        var att = await db.Attendances.FindAsync(id);
        if (att is null) return NotFound();

        att.Date = input.Date;
        att.Amount = input.Amount;
        att.ExtraAmount = input.ExtraAmount ?? att.ExtraAmount;
        att.DidEat = input.DidEat ?? att.DidEat;
        att.IsPaid = input.IsPaid ?? att.IsPaid;
        if (!string.IsNullOrWhiteSpace(input.Status))
            att.Status = ParseStatus(input.Status);
        await db.SaveChangesAsync();
        await db.Entry(att).Reference(a => a.Person).LoadAsync();
        return Ok(new { message = "Asistencia actualizada", data = att });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Destroy(int id)
    {
        var att = await db.Attendances.FindAsync(id);
        if (att is null) return NotFound();
        db.Attendances.Remove(att);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static AttendanceStatus ParseStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return AttendanceStatus.Present;
        var match = EnumMaps.AttendanceStatus.FirstOrDefault(kv =>
            string.Equals(kv.Value, value, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kv.Key.ToString(), value, StringComparison.OrdinalIgnoreCase));
        return match.Value is null ? AttendanceStatus.Present : match.Key;
    }
}

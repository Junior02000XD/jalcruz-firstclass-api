using JalcruzFirstClass.Api.Data;
using JalcruzFirstClass.Api.Domain;
using JalcruzFirstClass.Api.Dtos;
using JalcruzFirstClass.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JalcruzFirstClass.Api.Controllers;

[ApiController]
[Route("api/payrolls")]
[Authorize(Roles = $"{Roles.HrAdmin},{Roles.SuperAdmin}")]
public class PayrollsController(AppDbContext db, PayrollExportService exporter) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index()
        => Ok(await db.Payrolls.AsNoTracking()
            .Include(p => p.WorkArea)
            .OrderByDescending(p => p.Id).ToListAsync());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Show(int id)
    {
        var payroll = await db.Payrolls.AsNoTracking()
            .Include(p => p.WorkArea)
            .Include(p => p.Attendances)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (payroll is null) return NotFound();

        // Trabajadores activos en esta planilla (espeja la forma que consume el frontend).
        var activePersonIds = payroll.Attendances.Select(a => a.PersonId).Distinct().ToList();
        var activeWorkers = await db.WorkerDetails.AsNoTracking()
            .Include(w => w.Person)
            .Where(w => activePersonIds.Contains(w.PersonId))
            .ToListAsync();

        return Ok(new
        {
            payroll,
            attendances = payroll.Attendances,
            active_workers = activeWorkers,
        });
    }

    [HttpPost]
    public async Task<IActionResult> Store(PayrollInput input)
    {
        if (await db.Payrolls.AnyAsync(p => p.Code == input.Code))
            return BadRequest(new { message = "El código de planilla ya existe." });

        var payroll = new Payroll
        {
            Code = input.Code,
            WorkAreaId = input.WorkAreaId,
            StartDate = input.StartDate,
            EndDate = input.EndDate,
        };
        db.Payrolls.Add(payroll);
        await db.SaveChangesAsync();
        return StatusCode(201, new { message = "Planilla generada correctamente.", payroll });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, PayrollInput input)
    {
        var payroll = await db.Payrolls.FindAsync(id);
        if (payroll is null) return NotFound();

        if (input.Code != payroll.Code && await db.Payrolls.AnyAsync(p => p.Code == input.Code))
            return BadRequest(new { message = "El código de planilla ya existe." });

        payroll.Code = input.Code;
        payroll.WorkAreaId = input.WorkAreaId;
        payroll.StartDate = input.StartDate;
        payroll.EndDate = input.EndDate;
        await db.SaveChangesAsync();
        return Ok(new { message = "Planilla actualizada correctamente", data = payroll });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Destroy(int id)
    {
        var payroll = await db.Payrolls.FindAsync(id);
        if (payroll is null) return NotFound();
        db.Payrolls.Remove(payroll);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("{id:int}/export")]
    public async Task<IActionResult> ExportExcel(int id)
    {
        try
        {
            var (content, fileName) = await exporter.ExportAsync(id);
            return File(content,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }
        catch (KeyNotFoundException e)
        {
            return NotFound(new { message = e.Message });
        }
    }
}

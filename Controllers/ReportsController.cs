using JalcruzFirstClass.Api.Data;
using JalcruzFirstClass.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JalcruzFirstClass.Api.Controllers;

[ApiController]
[Route("api/reports")]
public class ReportsController(AppDbContext db) : ControllerBase
{
    /// <summary>
    /// Marketing ROI: costo por inscrito y tasa de conversión por campaña.
    /// (Corrige el bug del Laravel original que comparaba contra 'Inscrito' con mayúscula
    /// en vez del valor real 'inscrito', dando siempre 0 inscritos.)
    /// </summary>
    [HttpGet("marketing-roi")]
    [Authorize(Roles = $"{Roles.CrmAdmin},{Roles.SuperAdmin}")]
    public async Task<IActionResult> MarketingRoi()
    {
        var enrolledValue = EnumMaps.ProspectStatus[ProspectStatus.Enrolled]; // "inscrito"

        var data = await db.Campaigns
            .Select(c => new
            {
                c.Name,
                Budget = c.Budget ?? 0,
                Leads = c.Prospects.Count(),
                Enrolled = c.Prospects.Count(p => p.Status == ProspectStatus.Enrolled),
            })
            .ToListAsync();

        var report = data.Select(c => new
        {
            campaign_name = c.Name,
            budget = c.Budget,
            leads = c.Leads,
            enrolled = c.Enrolled,
            cost_per_enrolled = c.Enrolled > 0 ? Math.Round(c.Budget / c.Enrolled, 2) : 0,
            conversion_rate = c.Leads > 0
                ? $"{Math.Round((decimal)c.Enrolled / c.Leads * 100, 2)}%"
                : "0%",
        });

        return Ok(report);
    }

    /// <summary>Embudo de conversión: cantidad de prospectos por estado.</summary>
    [HttpGet("funnel")]
    [Authorize(Roles = $"{Roles.CrmAdmin},{Roles.SuperAdmin}")]
    public async Task<IActionResult> ConversionFunnel()
    {
        var grouped = await db.Prospects
            .GroupBy(p => p.Status)
            .Select(g => new { Status = g.Key, Total = g.Count() })
            .ToListAsync();

        var stats = grouped.Select(g => new
        {
            status = EnumMaps.ProspectStatus[g.Status],
            total = g.Total,
        });

        return Ok(stats);
    }

    /// <summary>
    /// Resumen de planilla: total a pagar por trabajador y total general.
    /// (Expande el reporte original incluyendo extra_amount, que antes se omitía.)
    /// </summary>
    [HttpGet("payroll/{payrollId:int}")]
    [Authorize(Roles = $"{Roles.HrAdmin},{Roles.SuperAdmin}")]
    public async Task<IActionResult> PayrollSummary(int payrollId)
    {
        var payroll = await db.Payrolls.AsNoTracking().FirstOrDefaultAsync(p => p.Id == payrollId);
        if (payroll is null) return NotFound();

        var details = await db.Attendances
            .Where(a => a.PayrollId == payrollId)
            .GroupBy(a => new { a.PersonId, a.Person.FirstName, a.Person.LastName })
            .Select(g => new
            {
                person_id = g.Key.PersonId,
                first_name = g.Key.FirstName,
                last_name = g.Key.LastName,
                days_worked = g.Count(),
                base_amount = g.Sum(x => x.Amount),
                extra_amount = g.Sum(x => x.ExtraAmount),
                total_to_pay = g.Sum(x => x.Amount + x.ExtraAmount),
            })
            .ToListAsync();

        return Ok(new
        {
            payroll_code = payroll.Code,
            total_amount = details.Sum(d => d.total_to_pay),
            workers_count = details.Count,
            details,
        });
    }
}

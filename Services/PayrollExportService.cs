using ClosedXML.Excel;
using JalcruzFirstClass.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace JalcruzFirstClass.Api.Services;

/// <summary>
/// Genera el Excel de una planilla (reemplaza PayrollExport de Maatwebsite/Excel).
/// Una fila por trabajador con sus totales del periodo.
/// </summary>
public class PayrollExportService(AppDbContext db)
{
    public async Task<(byte[] Content, string FileName)> ExportAsync(int payrollId)
    {
        var payroll = await db.Payrolls
            .Include(p => p.WorkArea).ThenInclude(w => w.Company)
            .FirstOrDefaultAsync(p => p.Id == payrollId)
            ?? throw new KeyNotFoundException($"Planilla {payrollId} no encontrada.");

        // Agregado por persona: días trabajados y montos del periodo.
        var rows = await db.Attendances
            .Where(a => a.PayrollId == payrollId)
            .GroupBy(a => new { a.PersonId, a.Person.FirstName, a.Person.LastName })
            .Select(g => new
            {
                g.Key.FirstName,
                g.Key.LastName,
                DaysWorked = g.Count(),
                Base = g.Sum(x => x.Amount),
                Extra = g.Sum(x => x.ExtraAmount),
            })
            .OrderBy(r => r.FirstName).ThenBy(r => r.LastName)
            .ToListAsync();

        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Planilla");

        // Encabezado informativo.
        ws.Cell(1, 1).Value = $"Planilla: {payroll.Code}";
        ws.Cell(2, 1).Value = $"Área: {payroll.WorkArea.Name} — {payroll.WorkArea.Company.Name}";
        ws.Cell(3, 1).Value = $"Periodo: {payroll.StartDate:dd/MM/yyyy} al {payroll.EndDate:dd/MM/yyyy}";
        ws.Range(1, 1, 3, 1).Style.Font.Bold = true;

        // Cabecera de tabla.
        const int header = 5;
        string[] cols = ["#", "Nombre", "Apellido", "Días", "Monto Base", "Extra", "Total"];
        for (var c = 0; c < cols.Length; c++)
        {
            var cell = ws.Cell(header, c + 1);
            cell.Value = cols[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.LightGray;
            cell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        }

        var r = header + 1;
        decimal grandTotal = 0;
        var i = 1;
        foreach (var row in rows)
        {
            var total = row.Base + row.Extra;
            grandTotal += total;
            ws.Cell(r, 1).Value = i++;
            ws.Cell(r, 2).Value = row.FirstName;
            ws.Cell(r, 3).Value = row.LastName;
            ws.Cell(r, 4).Value = row.DaysWorked;
            ws.Cell(r, 5).Value = row.Base;
            ws.Cell(r, 6).Value = row.Extra;
            ws.Cell(r, 7).Value = total;
            r++;
        }

        // Fila de total general.
        ws.Cell(r, 6).Value = "TOTAL";
        ws.Cell(r, 6).Style.Font.Bold = true;
        ws.Cell(r, 7).Value = grandTotal;
        ws.Cell(r, 7).Style.Font.Bold = true;

        ws.Range(header, 5, r, 7).Style.NumberFormat.Format = "#,##0.00";
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);

        var safeCode = string.Concat((payroll.Code).Select(ch =>
            char.IsLetterOrDigit(ch) || ch == '-' ? ch : '_'));
        var fileName = $"Planilla_{safeCode}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

        return (ms.ToArray(), fileName);
    }
}

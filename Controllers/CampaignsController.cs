using JalcruzFirstClass.Api.Data;
using JalcruzFirstClass.Api.Domain;
using JalcruzFirstClass.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace JalcruzFirstClass.Api.Controllers;

[ApiController]
[Route("api/campaigns")]
[Authorize(Roles = $"{Roles.CrmAdmin},{Roles.SuperAdmin}")]
public class CampaignsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index()
        => Ok(await db.Campaigns.AsNoTracking().OrderByDescending(c => c.Id).ToListAsync());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Show(int id)
    {
        var campaign = await db.Campaigns.FindAsync(id);
        return campaign is null ? NotFound() : Ok(campaign);
    }

    /// <summary>
    /// Busca la campaña dueña de un anuncio de Meta. La usa el agente de WhatsApp
    /// con el `referral.source_id` que trae el mensaje cuando alguien llega por un
    /// anuncio click-to-WhatsApp.
    ///
    /// **404 no es un error**: significa que ese anuncio todavía no está mapeado a
    /// ninguna campaña. El agente sigue igual y crea el prospecto sin campaña —
    /// perder la atribución es aceptable, perder el lead no.
    /// </summary>
    [HttpGet("by-ad/{sourceId}")]
    public async Task<IActionResult> ByAd(string sourceId)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
            return BadRequest(new { message = "Falta el id del anuncio." });

        var campaign = await db.Campaigns.AsNoTracking()
            .FirstOrDefaultAsync(c => c.AdId == sourceId.Trim());

        return campaign is null
            ? NotFound(new { message = $"Ningún anuncio {sourceId} está mapeado a una campaña.", ad_id = sourceId })
            : Ok(campaign);
    }

    [HttpPost]
    public async Task<IActionResult> Store(CampaignInput input)
    {
        var campaign = new Campaign
        {
            Name = input.Name,
            ExecutionDate = input.ExecutionDate,
            Description = input.Description,
            Url = input.Url,
            Budget = input.Budget,
            Type = input.Type,
            AdId = Vacio(input.AdId),
        };
        db.Campaigns.Add(campaign);

        if (await GuardarChocandoPorAnuncio(campaign.AdId) is IActionResult choque) return choque;

        return CreatedAtAction(nameof(Show), new { id = campaign.Id }, campaign);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, CampaignInput input)
    {
        var campaign = await db.Campaigns.FindAsync(id);
        if (campaign is null) return NotFound();
        campaign.Name = input.Name;
        campaign.ExecutionDate = input.ExecutionDate;
        campaign.Description = input.Description;
        campaign.Url = input.Url;
        campaign.Budget = input.Budget;
        campaign.Type = input.Type;
        campaign.AdId = Vacio(input.AdId);

        if (await GuardarChocandoPorAnuncio(campaign.AdId) is IActionResult choque) return choque;

        return Ok(campaign);
    }

    /// <summary>
    /// Un campo de texto vacío del formulario llega como "", y varias campañas
    /// con "" chocarían contra el índice único. Como NULL, conviven.
    /// </summary>
    private static string? Vacio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>
    /// Guarda, y traduce el choque del índice único de `ad_id` a un 409 con el
    /// nombre de la campaña que ya lo tiene. Sin esto sale un 500 genérico y el
    /// panel no puede decir cuál es el conflicto ni cómo resolverlo.
    /// Devuelve null si guardó bien.
    /// </summary>
    private async Task<IActionResult?> GuardarChocandoPorAnuncio(string? adId)
    {
        try
        {
            await db.SaveChangesAsync();
            return null;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            var dueña = await db.Campaigns.AsNoTracking()
                .Where(c => c.AdId == adId)
                .Select(c => new { c.Id, c.Name })
                .FirstOrDefaultAsync();

            return Conflict(new
            {
                message = dueña is null
                    ? "Ese id de anuncio ya está usado por otra campaña."
                    : $"El anuncio {adId} ya pertenece a la campaña \"{dueña.Name}\" (id {dueña.Id}).",
            });
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Destroy(int id)
    {
        var campaign = await db.Campaigns.FindAsync(id);
        if (campaign is null) return NotFound();
        db.Campaigns.Remove(campaign);
        await db.SaveChangesAsync();
        return NoContent();
    }
}

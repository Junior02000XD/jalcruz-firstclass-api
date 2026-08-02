using JalcruzFirstClass.Api.Data;
using JalcruzFirstClass.Api.Domain;
using JalcruzFirstClass.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace JalcruzFirstClass.Api.Controllers;

/// <summary>
/// Voz de la IA en cada número de WhatsApp. Una activa por número: es lo que
/// permite que dos números respondan distinto y que se pause uno solo.
/// </summary>
[ApiController]
[Route("api/personas")]
[Authorize(Roles = $"{Roles.CrmAdmin},{Roles.SuperAdmin}")]
public class PersonasController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index()
        => Ok(await db.Personas.AsNoTracking().Include(p => p.User)
            .OrderByDescending(p => p.Active).ThenBy(p => p.Id).ToListAsync());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Show(int id)
    {
        var persona = await db.Personas.AsNoTracking().Include(p => p.User).FirstOrDefaultAsync(p => p.Id == id);
        return persona is null ? NotFound() : Ok(persona);
    }

    [HttpPost]
    public async Task<IActionResult> Store(PersonaInput input)
    {
        if (!await db.Users.AnyAsync(u => u.Id == input.UserId))
            return BadRequest(new { message = $"No existe el usuario {input.UserId}." });

        var persona = new Persona
        {
            UserId = input.UserId,
            PhoneNumberId = input.PhoneNumberId.Trim(),
            StyleGuide = input.StyleGuide,
            Active = input.Active ?? true,
        };

        db.Personas.Add(persona);
        return await SaveGuardingUniqueAsync(persona,
            () => CreatedAtAction(nameof(Show), new { id = persona.Id }, persona));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, PersonaInput input)
    {
        var persona = await db.Personas.FindAsync(id);
        if (persona is null) return NotFound();

        if (!await db.Users.AnyAsync(u => u.Id == input.UserId))
            return BadRequest(new { message = $"No existe el usuario {input.UserId}." });

        persona.UserId = input.UserId;
        persona.PhoneNumberId = input.PhoneNumberId.Trim();
        persona.StyleGuide = input.StyleGuide;
        persona.Active = input.Active ?? persona.Active;

        return await SaveGuardingUniqueAsync(persona, () => Ok(persona));
    }

    /// <summary>
    /// Pausa o reanuda el agente en un número. Con Active=false, el endpoint del
    /// agente devuelve 404 para ese número y n8n deja de responder.
    /// </summary>
    [HttpPatch("{id:int}/active")]
    public async Task<IActionResult> SetActive(int id, [FromBody] bool active)
    {
        var persona = await db.Personas.FindAsync(id);
        if (persona is null) return NotFound();

        persona.Active = active;
        return await SaveGuardingUniqueAsync(persona, () => Ok(persona));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Destroy(int id)
    {
        var persona = await db.Personas.FindAsync(id);
        if (persona is null) return NotFound();

        db.Personas.Remove(persona);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Traduce el choque contra el índice único filtrado en un 409 entendible.
    /// El índice es la garantía real —dos altas simultáneas no pueden dejar dos
    /// agentes hablando por el mismo número—, pero el 500 pelado no le dice
    /// nada a quien está en el panel.
    /// </summary>
    private async Task<IActionResult> SaveGuardingUniqueAsync(Persona persona, Func<IActionResult> onOk)
    {
        try
        {
            await db.SaveChangesAsync();
            await db.Entry(persona).Reference(p => p.User).LoadAsync();
            return onOk();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.ChangeTracker.Clear();
            return Conflict(new
            {
                message = $"Ya hay una persona activa para el número {persona.PhoneNumberId}. " +
                          "Desactivá la actual antes de activar otra.",
            });
        }
    }
}

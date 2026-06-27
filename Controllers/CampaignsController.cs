using JalcruzFirstClass.Api.Data;
using JalcruzFirstClass.Api.Domain;
using JalcruzFirstClass.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
        };
        db.Campaigns.Add(campaign);
        await db.SaveChangesAsync();
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
        await db.SaveChangesAsync();
        return Ok(campaign);
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

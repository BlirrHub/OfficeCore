using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Data;
using OfficeCore.Api.Models;
using OfficeCore.Api.Services;

namespace OfficeCore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TimeOffPoliciesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly TenantService _tenantService;

    public TimeOffPoliciesController(AppDbContext context, TenantService tenantService)
    {
        _context = context;
        _tenantService = tenantService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TimeOffPolicy>>> GetPolicies()
    {
        var orgId = _tenantService.CurrentOrganizationId;
        var policies = await _context.TimeOffPolicies
            .Where(p => p.OrganizationId == orgId && p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync();

        return Ok(policies);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<TimeOffPolicy>> GetPolicy(Guid id)
    {
        var orgId = _tenantService.CurrentOrganizationId;
        var policy = await _context.TimeOffPolicies
            .FirstOrDefaultAsync(p => p.Id == id && p.OrganizationId == orgId);

        if (policy == null) return NotFound();
        return Ok(policy);
    }

    [HttpPost]
    public async Task<ActionResult<TimeOffPolicy>> CreatePolicy([FromBody] TimeOffPolicy policy)
    {
        var orgId = _tenantService.CurrentOrganizationId;
        policy.Id = Guid.NewGuid();
        policy.OrganizationId = orgId;
        policy.CreatedAt = DateTime.UtcNow;

        _context.TimeOffPolicies.Add(policy);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetPolicy), new { id = policy.Id }, policy);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdatePolicy(Guid id, [FromBody] TimeOffPolicy updated)
    {
        var orgId = _tenantService.CurrentOrganizationId;
        var existing = await _context.TimeOffPolicies
            .FirstOrDefaultAsync(p => p.Id == id && p.OrganizationId == orgId);

        if (existing == null) return NotFound();

        existing.Name = updated.Name;
        existing.LeaveType = updated.LeaveType;
        existing.DefaultDaysPerYear = updated.DefaultDaysPerYear;
        existing.IsPaid = updated.IsPaid;
        existing.RequiresAttachment = updated.RequiresAttachment;
        existing.IsActive = updated.IsActive;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePolicy(Guid id)
    {
        var orgId = _tenantService.CurrentOrganizationId;
        var policy = await _context.TimeOffPolicies
            .FirstOrDefaultAsync(p => p.Id == id && p.OrganizationId == orgId);

        if (policy == null) return NotFound();

        // Soft delete
        policy.IsActive = false;
        await _context.SaveChangesAsync();

        return NoContent();
    }
}

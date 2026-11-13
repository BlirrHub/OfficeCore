using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Data;
using OfficeCore.Api.Models;
using OfficeCore.Api.Services;

namespace OfficeCore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "RequireTimeOff")]
public class TimeOffPoliciesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly TenantService _tenantService;

    public TimeOffPoliciesController(AppDbContext context, TenantService tenantService)
    {
        _context = context;
        _tenantService = tenantService;
    }

    public sealed class TimeOffPolicyRequest
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
        public LeaveType LeaveType { get; set; }
        [Range(0, 365)]
        public decimal DefaultDaysPerYear { get; set; } = 15;
        public bool IsPaid { get; set; } = true;
        public bool RequiresAttachment { get; set; }
        public bool IsActive { get; set; } = true;
    }

    [HttpGet]
    [Authorize(Policy = "Permission:time-off.view")]
    public async Task<ActionResult<IEnumerable<TimeOffPolicy>>> GetPolicies()
    {
        var orgId = await _tenantService.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Forbid();
        var policies = await _context.TimeOffPolicies
            .Where(p => p.OrganizationId == orgId && p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync();

        return Ok(policies);
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "Permission:time-off.view")]
    public async Task<ActionResult<TimeOffPolicy>> GetPolicy(Guid id)
    {
        var orgId = await _tenantService.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Forbid();
        var policy = await _context.TimeOffPolicies
            .FirstOrDefaultAsync(p => p.Id == id && p.OrganizationId == orgId);

        if (policy == null) return NotFound();
        return Ok(policy);
    }

    [HttpPost]
    [Authorize(Policy = "RequireTimeOff")]
    [Authorize(Policy = "Permission:time-off.manage")]
    public async Task<ActionResult<TimeOffPolicy>> CreatePolicy([FromBody] TimeOffPolicyRequest request)
    {
        var orgId = await _tenantService.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Forbid();
        if (!Enum.IsDefined(typeof(LeaveType), request.LeaveType)) return BadRequest("Invalid leave type.");
        var policy = new TimeOffPolicy
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            LeaveType = request.LeaveType,
            DefaultDaysPerYear = request.DefaultDaysPerYear,
            IsPaid = request.IsPaid,
            RequiresAttachment = request.RequiresAttachment,
            IsActive = true,
            OrganizationId = orgId.Value,
            CreatedAt = DateTime.UtcNow
        };

        _context.TimeOffPolicies.Add(policy);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetPolicy), new { id = policy.Id }, policy);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "RequireTimeOff")]
    [Authorize(Policy = "Permission:time-off.manage")]
    public async Task<IActionResult> UpdatePolicy(Guid id, [FromBody] TimeOffPolicyRequest updated)
    {
        var orgId = await _tenantService.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Forbid();
        if (!Enum.IsDefined(typeof(LeaveType), updated.LeaveType)) return BadRequest("Invalid leave type.");
        var existing = await _context.TimeOffPolicies
            .FirstOrDefaultAsync(p => p.Id == id && p.OrganizationId == orgId);

        if (existing == null) return NotFound();

        existing.Name = updated.Name.Trim();
        existing.LeaveType = updated.LeaveType;
        existing.DefaultDaysPerYear = updated.DefaultDaysPerYear;
        existing.IsPaid = updated.IsPaid;
        existing.RequiresAttachment = updated.RequiresAttachment;
        existing.IsActive = updated.IsActive;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "RequireTimeOff")]
    [Authorize(Policy = "Permission:time-off.manage")]
    public async Task<IActionResult> DeletePolicy(Guid id)
    {
        var orgId = await _tenantService.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Forbid();
        var policy = await _context.TimeOffPolicies
            .FirstOrDefaultAsync(p => p.Id == id && p.OrganizationId == orgId);

        if (policy == null) return NotFound();

        // Soft delete
        policy.IsActive = false;
        await _context.SaveChangesAsync();

        return NoContent();
    }
}

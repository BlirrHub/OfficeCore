using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Models;
using OfficeCore.Api.Services;

namespace OfficeCore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "RequireRoles")]
public class RoleController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly TenantService _tenant;

    public RoleController(AppDbContext context, TenantService tenant)
    {
        _context = context;
        _tenant = tenant;
    }

    [HttpGet]
    public async Task<IActionResult> GetRoles()
    {
        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Forbid();

        var roles = await _context.OrganizationRoles
            .Where(r => r.OrganizationId == orgId)
            .Select(r => new
            {
                r.Id,
                r.Name,
                r.Description,
                r.ShowAsTitle
            })
            .ToListAsync();

        return Ok(roles);
    }

    [HttpPost]
    public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequest request)
    {
        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Forbid();

        var role = new OrganizationRole
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId.Value,
            Name = request.Name,
            Description = request.Description,
            ShowAsTitle = request.ShowAsTitle
        };

        if (request.Modules != null)
        {
            foreach (var module in request.Modules)
            {
                role.Claims.Add(new OrganizationRoleClaim
                {
                    Id = Guid.NewGuid(),
                    OrganizationRoleId = role.Id,
                    ClaimType = "ModuleAccess",
                    ClaimValue = module
                });
            }
        }

        _context.OrganizationRoles.Add(role);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Role created successfully", roleId = role.Id });
    }

    [HttpPut("{id}/details")]
    public async Task<IActionResult> UpdateRoleDetails(Guid id, [FromBody] UpdateRoleRequest request)
    {
        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Forbid();

        var role = await _context.OrganizationRoles
            .FirstOrDefaultAsync(r => r.Id == id && r.OrganizationId == orgId);

        if (role == null) return NotFound();

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            role.Name = request.Name;
        }
        role.Description = request.Description;
        role.ShowAsTitle = request.ShowAsTitle;

        await _context.SaveChangesAsync();

        return Ok(new { message = "Role updated successfully" });
    }

    [HttpGet("{id}/modules")]
    public async Task<IActionResult> GetRoleModules(Guid id)
    {
        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Forbid();

        var role = await _context.OrganizationRoles
            .Include(r => r.Claims)
            .FirstOrDefaultAsync(r => r.Id == id && r.OrganizationId == orgId);

        if (role == null) return NotFound();

        var modules = role.Claims
            .Where(c => c.ClaimType == "ModuleAccess")
            .Select(c => c.ClaimValue)
            .ToList();
        
        return Ok(modules);
    }

    [HttpPut("{id}/modules")]
    public async Task<IActionResult> UpdateRoleModules(Guid id, [FromBody] List<string> modules)
    {
        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Forbid();

        var role = await _context.OrganizationRoles
            .Include(r => r.Claims)
            .FirstOrDefaultAsync(r => r.Id == id && r.OrganizationId == orgId);

        if (role == null) return NotFound();

        // Remove existing module claims
        var existingModuleClaims = role.Claims.Where(c => c.ClaimType == "ModuleAccess").ToList();
        _context.OrganizationRoleClaims.RemoveRange(existingModuleClaims);

        // Add new module claims
        var newClaims = modules.Select(module => new OrganizationRoleClaim
        {
            Id = Guid.NewGuid(),
            OrganizationRoleId = role.Id,
            ClaimType = "ModuleAccess",
            ClaimValue = module
        }).ToList();
        
        _context.OrganizationRoleClaims.AddRange(newClaims);

        await _context.SaveChangesAsync();

        return Ok(new { message = "Role modules updated successfully" });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteRole(Guid id)
    {
        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Forbid();

        var role = await _context.OrganizationRoles
            .FirstOrDefaultAsync(r => r.Id == id && r.OrganizationId == orgId);

        if (role == null) return NotFound();

        _context.OrganizationRoles.Remove(role);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Role deleted successfully" });
    }
}

public class CreateRoleRequest
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public bool ShowAsTitle { get; set; }
    public List<string>? Modules { get; set; }
}

public class UpdateRoleRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public bool ShowAsTitle { get; set; }
}

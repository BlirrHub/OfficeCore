using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Models;
using OfficeCore.Api.Services;
using OfficeCore.Shared.Authorization;

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
                r.ShowAsTitle,
                Modules = r.Claims
                    .Where(c => c.ClaimType == "ModuleAccess")
                    .Select(c => c.ClaimValue)
                    .ToList()
            })
            .ToListAsync();

        return Ok(roles);
    }

    [HttpPost]
    public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequest request)
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null || !await _tenant.IsCurrentUserWorkspaceOwnerAsync(orgId)) return Forbid();

        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name)) return BadRequest(new { message = "Role name is required." });
        if (await _context.OrganizationRoles.AnyAsync(r => r.OrganizationId == orgId && r.Name.ToLower() == name.ToLower()))
            return Conflict(new { message = "A role with this name already exists." });

        if (!ModuleCatalog.TryNormalizeMany(request.Modules, out var modules, out var invalid))
            return BadRequest(new { message = "Unknown modules were supplied.", invalidModules = invalid });

        var role = new OrganizationRole
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId.Value,
            Name = name,
            Description = request.Description,
            ShowAsTitle = request.ShowAsTitle
        };

        foreach (var module in modules)
        {
            role.Claims.Add(new OrganizationRoleClaim
            {
                Id = Guid.NewGuid(),
                OrganizationRoleId = role.Id,
                ClaimType = "ModuleAccess",
                ClaimValue = module
            });
        }

        _context.OrganizationRoles.Add(role);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Role created successfully", roleId = role.Id });
    }

    [HttpPut("{id}/details")]
    public async Task<IActionResult> UpdateRoleDetails(Guid id, [FromBody] UpdateRoleRequest request)
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null || !await _tenant.IsCurrentUserWorkspaceOwnerAsync(orgId)) return Forbid();

        var role = await _context.OrganizationRoles
            .FirstOrDefaultAsync(r => r.Id == id && r.OrganizationId == orgId);

        if (role == null) return NotFound();

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            var name = request.Name.Trim();
            if (await _context.OrganizationRoles.AnyAsync(r => r.OrganizationId == orgId && r.Id != id && r.Name.ToLower() == name.ToLower()))
                return Conflict(new { message = "A role with this name already exists." });
            role.Name = name;
        }
        role.Description = request.Description;
        role.ShowAsTitle = request.ShowAsTitle;

        await _context.SaveChangesAsync();

        return Ok(new { message = "Role updated successfully" });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateRole(Guid id, [FromBody] SaveRoleRequest request)
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null || !await _tenant.IsCurrentUserWorkspaceOwnerAsync(orgId)) return Forbid();

        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name)) return BadRequest(new { message = "Role name is required." });
        if (!ModuleCatalog.TryNormalizeMany(request.Modules, out var modules, out var invalid))
            return BadRequest(new { message = "Unknown modules were supplied.", invalidModules = invalid });

        var role = await _context.OrganizationRoles
            .Include(r => r.Claims)
            .FirstOrDefaultAsync(r => r.Id == id && r.OrganizationId == orgId);
        if (role == null) return NotFound();

        if (await _context.OrganizationRoles.AnyAsync(r => r.OrganizationId == orgId && r.Id != id && r.Name.ToLower() == name.ToLower()))
            return Conflict(new { message = "A role with this name already exists." });

        role.Name = name;
        role.Description = request.Description;
        role.ShowAsTitle = request.ShowAsTitle;

        _context.OrganizationRoleClaims.RemoveRange(role.Claims.Where(c => c.ClaimType == "ModuleAccess"));
        foreach (var module in modules)
        {
            role.Claims.Add(new OrganizationRoleClaim
            {
                Id = Guid.NewGuid(),
                OrganizationRoleId = role.Id,
                ClaimType = "ModuleAccess",
                ClaimValue = module
            });
        }

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
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null || !await _tenant.IsCurrentUserWorkspaceOwnerAsync(orgId)) return Forbid();

        if (!ModuleCatalog.TryNormalizeMany(modules, out var normalizedModules, out var invalid))
            return BadRequest(new { message = "Unknown modules were supplied.", invalidModules = invalid });

        var role = await _context.OrganizationRoles
            .Include(r => r.Claims)
            .FirstOrDefaultAsync(r => r.Id == id && r.OrganizationId == orgId);

        if (role == null) return NotFound();

        // Remove existing module claims
        var existingModuleClaims = role.Claims.Where(c => c.ClaimType == "ModuleAccess").ToList();
        _context.OrganizationRoleClaims.RemoveRange(existingModuleClaims);

        // Add new module claims
        var newClaims = normalizedModules.Select(module => new OrganizationRoleClaim
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
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null || !await _tenant.IsCurrentUserWorkspaceOwnerAsync(orgId)) return Forbid();

        var role = await _context.OrganizationRoles
            .FirstOrDefaultAsync(r => r.Id == id && r.OrganizationId == orgId);

        if (role == null) return NotFound();

        if (await _context.EmployeeRoles.AnyAsync(er => er.OrganizationRoleId == id))
            return Conflict(new { message = "Reassign employees before deleting this role." });

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

public class SaveRoleRequest
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public bool ShowAsTitle { get; set; }
    public List<string> Modules { get; set; } = [];
}

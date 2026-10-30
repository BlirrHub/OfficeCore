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
    private readonly IAuditService _audit;

    public RoleController(AppDbContext context, TenantService tenant, IAuditService audit)
    {
        _context = context;
        _tenant = tenant;
        _audit = audit;
    }

    private HashSet<string> GetCallerEffectivePermissions()
    {
        var explicitPermissions = User.FindAll(PermissionCatalog.ClaimType).Select(c => c.Value).ToList();
        if (explicitPermissions.Count > 0)
            return new HashSet<string>(explicitPermissions, StringComparer.OrdinalIgnoreCase);
        var moduleExpanded = PermissionCatalog.ExpandModules(User.FindAll("ModuleAccess").Select(c => c.Value));
        return new HashSet<string>(moduleExpanded, StringComparer.OrdinalIgnoreCase);
    }

    private bool HasCallerPermission(string permission, bool isOwner)
    {
        if (isOwner) return true;
        return GetCallerEffectivePermissions().Contains(permission);
    }

    private bool CallerCanGrant(IEnumerable<string> modules, IEnumerable<string> permissions, bool isOwner)
    {
        if (isOwner) return true;
        var callerModules = new HashSet<string>(User.FindAll("ModuleAccess").Select(c => c.Value), StringComparer.OrdinalIgnoreCase);
        var callerPermissions = GetCallerEffectivePermissions();

        foreach (var module in modules)
        {
            if (!ModuleCatalog.DefaultKeys.Contains(module, StringComparer.OrdinalIgnoreCase) && !callerModules.Contains(module))
                return false;
        }

        foreach (var perm in permissions)
        {
            if (!callerPermissions.Contains(perm))
                return false;
        }

        return true;
    }

    [HttpGet]
    [Authorize(Policy = "Permission:roles.view")]
    public async Task<IActionResult> GetRoles()
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
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
                    .ToList(),
                Permissions = r.Claims
                    .Where(c => c.ClaimType == PermissionCatalog.ClaimType)
                    .Select(c => c.ClaimValue)
                    .ToList()
            })
            .ToListAsync();

        return Ok(roles);
    }

    [HttpPost]
    [Authorize(Policy = "Permission:roles.create")]
    public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequest request)
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Forbid();
        var isOwner = await _tenant.IsCurrentUserWorkspaceOwnerAsync(orgId);
        if (!HasCallerPermission(AccessPermissions.RolesCreate, isOwner)) return Forbid();

        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name)) return BadRequest(new { message = "Role name is required." });
        if (!isOwner && string.Equals(name, "Admin", StringComparison.OrdinalIgnoreCase))
            return Forbid();
        if (await _context.OrganizationRoles.AnyAsync(r => r.OrganizationId == orgId && r.Name.ToLower() == name.ToLower()))
            return Conflict(new { message = "A role with this name already exists." });

        if (!ModuleCatalog.TryNormalizeMany(request.Modules, out var modules, out var invalid))
            return BadRequest(new { message = "Unknown modules were supplied.", invalidModules = invalid });
        var requestedPermissions = request.Permissions ?? PermissionCatalog.ExpandModules(modules).ToList();
        if (!PermissionCatalog.TryNormalizeMany(requestedPermissions, out var permissions, out var invalidPermissions))
            return BadRequest(new { message = "Unknown permissions were supplied.", invalidPermissions });
        if (!CallerCanGrant(modules, permissions, isOwner))
            return Forbid();

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
        foreach (var permission in permissions)
        {
            role.Claims.Add(new OrganizationRoleClaim
            {
                Id = Guid.NewGuid(), OrganizationRoleId = role.Id,
                ClaimType = PermissionCatalog.ClaimType, ClaimValue = permission
            });
        }

        _context.OrganizationRoles.Add(role);
        await _context.SaveChangesAsync();
        await _audit.LogSecurityAsync(AuditActions.Create, $"Created role '{role.Name}'.", role.Id.ToString(), new { modules, permissions });

        return Ok(new { message = "Role created successfully", roleId = role.Id });
    }

    [HttpPut("{id}/details")]
    [Authorize(Policy = "Permission:roles.edit")]
    public async Task<IActionResult> UpdateRoleDetails(Guid id, [FromBody] UpdateRoleRequest request)
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Forbid();
        var isOwner = await _tenant.IsCurrentUserWorkspaceOwnerAsync(orgId);
        if (!HasCallerPermission(AccessPermissions.RolesEdit, isOwner)) return Forbid();

        var role = await _context.OrganizationRoles
            .FirstOrDefaultAsync(r => r.Id == id && r.OrganizationId == orgId);

        if (role == null) return NotFound();
        if (!isOwner && string.Equals(role.Name, "Admin", StringComparison.OrdinalIgnoreCase))
            return Forbid();

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            var name = request.Name.Trim();
            if (!isOwner && string.Equals(name, "Admin", StringComparison.OrdinalIgnoreCase))
                return Forbid();
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
    [Authorize(Policy = "Permission:roles.edit")]
    public async Task<IActionResult> UpdateRole(Guid id, [FromBody] SaveRoleRequest request)
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Forbid();
        var isOwner = await _tenant.IsCurrentUserWorkspaceOwnerAsync(orgId);
        if (!HasCallerPermission(AccessPermissions.RolesEdit, isOwner)) return Forbid();

        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name)) return BadRequest(new { message = "Role name is required." });
        if (!ModuleCatalog.TryNormalizeMany(request.Modules, out var modules, out var invalid))
            return BadRequest(new { message = "Unknown modules were supplied.", invalidModules = invalid });
        var requestedPermissions = request.Permissions ?? PermissionCatalog.ExpandModules(modules).ToList();
        if (!PermissionCatalog.TryNormalizeMany(requestedPermissions, out var permissions, out var invalidPermissions))
            return BadRequest(new { message = "Unknown permissions were supplied.", invalidPermissions });
        if (!CallerCanGrant(modules, permissions, isOwner))
            return Forbid();

        var role = await _context.OrganizationRoles
            .FirstOrDefaultAsync(r => r.Id == id && r.OrganizationId == orgId);
        if (role == null) return NotFound();
        if (!isOwner && (string.Equals(role.Name, "Admin", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "Admin", StringComparison.OrdinalIgnoreCase)))
            return Forbid();

        if (await _context.OrganizationRoles.AnyAsync(r => r.OrganizationId == orgId && r.Id != id && r.Name.ToLower() == name.ToLower()))
            return Conflict(new { message = "A role with this name already exists." });

        role.Name = name;
        role.Description = request.Description;
        role.ShowAsTitle = request.ShowAsTitle;

        await using var transaction = await _context.Database.BeginTransactionAsync();

        // Delete directly in the database. Replacing a tracked navigation collection can
        // emit duplicate/stale DELETE commands and trigger DbUpdateConcurrencyException.
        await _context.OrganizationRoleClaims
            .Where(c => c.OrganizationRoleId == role.Id &&
                        (c.ClaimType == "ModuleAccess" || c.ClaimType == PermissionCatalog.ClaimType))
            .ExecuteDeleteAsync();

        var newClaims = modules.Select(module => new OrganizationRoleClaim
        {
            Id = Guid.NewGuid(),
            OrganizationRoleId = role.Id,
            ClaimType = "ModuleAccess",
            ClaimValue = module
        });
        _context.OrganizationRoleClaims.AddRange(newClaims);
        _context.OrganizationRoleClaims.AddRange(permissions.Select(permission => new OrganizationRoleClaim
        {
            Id = Guid.NewGuid(), OrganizationRoleId = role.Id,
            ClaimType = PermissionCatalog.ClaimType, ClaimValue = permission
        }));

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        await _audit.LogSecurityAsync(AuditActions.Update, $"Updated role '{role.Name}'.", role.Id.ToString(), new { modules, permissions });
        return Ok(new { message = "Role updated successfully" });
    }

    [HttpGet("{id}/modules")]
    [Authorize(Policy = "Permission:roles.view")]
    public async Task<IActionResult> GetRoleModules(Guid id)
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Forbid();

        var modules = await _context.OrganizationRoles
            .Where(r => r.Id == id && r.OrganizationId == orgId)
            .Select(r => r.Claims
                .Where(c => c.ClaimType == "ModuleAccess")
                .Select(c => c.ClaimValue)
                .ToList())
            .FirstOrDefaultAsync();

        if (modules == null) return NotFound();
        
        return Ok(modules);
    }

    [HttpPut("{id}/modules")]
    [Authorize(Policy = "Permission:roles.edit")]
    public async Task<IActionResult> UpdateRoleModules(Guid id, [FromBody] List<string> modules)
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Forbid();
        var isOwner = await _tenant.IsCurrentUserWorkspaceOwnerAsync(orgId);
        if (!HasCallerPermission(AccessPermissions.RolesEdit, isOwner)) return Forbid();

        if (!ModuleCatalog.TryNormalizeMany(modules, out var normalizedModules, out var invalid))
            return BadRequest(new { message = "Unknown modules were supplied.", invalidModules = invalid });
        var expandedPermissions = PermissionCatalog.ExpandModules(normalizedModules);
        if (!CallerCanGrant(normalizedModules, expandedPermissions, isOwner))
            return Forbid();

        var role = await _context.OrganizationRoles
            .Include(r => r.Claims)
            .FirstOrDefaultAsync(r => r.Id == id && r.OrganizationId == orgId);

        if (role == null) return NotFound();
        if (!isOwner && string.Equals(role.Name, "Admin", StringComparison.OrdinalIgnoreCase))
            return Forbid();

        await using var transaction = await _context.Database.BeginTransactionAsync();

        await _context.OrganizationRoleClaims
            .Where(c => c.OrganizationRoleId == role.Id &&
                        (c.ClaimType == "ModuleAccess" || c.ClaimType == PermissionCatalog.ClaimType))
            .ExecuteDeleteAsync();

        // Add new module claims
        var newClaims = normalizedModules.Select(module => new OrganizationRoleClaim
        {
            Id = Guid.NewGuid(),
            OrganizationRoleId = role.Id,
            ClaimType = "ModuleAccess",
            ClaimValue = module
        }).ToList();
        
        _context.OrganizationRoleClaims.AddRange(newClaims);
        _context.OrganizationRoleClaims.AddRange(expandedPermissions
            .Select(permission => new OrganizationRoleClaim
            {
                Id = Guid.NewGuid(), OrganizationRoleId = role.Id,
                ClaimType = PermissionCatalog.ClaimType, ClaimValue = permission
            }));

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        await _audit.LogSecurityAsync(AuditActions.Update, $"Updated module access for role '{role.Name}'.", role.Id.ToString(), new { modules = normalizedModules });

        return Ok(new { message = "Role modules updated successfully" });
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "Permission:roles.delete")]
    public async Task<IActionResult> DeleteRole(Guid id)
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Forbid();
        var isOwner = await _tenant.IsCurrentUserWorkspaceOwnerAsync(orgId);
        if (!HasCallerPermission(AccessPermissions.RolesDelete, isOwner)) return Forbid();

        var role = await _context.OrganizationRoles
            .FirstOrDefaultAsync(r => r.Id == id && r.OrganizationId == orgId);

        if (role == null) return NotFound();
        if (!isOwner && string.Equals(role.Name, "Admin", StringComparison.OrdinalIgnoreCase))
            return Forbid();

        await using var transaction = await _context.Database.BeginTransactionAsync();

        // A role assignment is a permission link, not employee data. Remove those
        // links atomically so an assigned custom role can still be deleted safely.
        var removedAssignments = await _context.EmployeeRoles
            .Where(er => er.OrganizationRoleId == id)
            .ExecuteDeleteAsync();

        _context.OrganizationRoles.Remove(role);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        await _audit.LogSecurityAsync(AuditActions.Delete, $"Deleted role '{role.Name}' and removed {removedAssignments} assignment(s).", role.Id.ToString());

        return Ok(new
        {
            message = "Role deleted successfully",
            removedAssignments
        });
    }
}

public class CreateRoleRequest
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public bool ShowAsTitle { get; set; }
    public List<string>? Modules { get; set; }
    public List<string>? Permissions { get; set; }
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
    public List<string>? Permissions { get; set; }
}

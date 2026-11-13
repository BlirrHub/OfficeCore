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
    private readonly IWorkspaceAuthorizationService _workspaceAuth;

    public RoleController(
        AppDbContext context,
        TenantService tenant,
        IAuditService audit,
        IWorkspaceAuthorizationService? workspaceAuth = null)
    {
        _context = context;
        _tenant = tenant;
        _audit = audit;
        _workspaceAuth = workspaceAuth ?? new WorkspaceAuthorizationService(context);
    }

    private bool HasCallerPermission(string permission, bool isOwner) =>
        isOwner || User.HasPermission(permission);

    private bool CallerCanGrant(IEnumerable<string> modules, IEnumerable<string> permissions, bool isOwner)
    {
        if (isOwner) return true;
        var callerModules = User.GetEffectiveModules();
        var callerPermissions = User.GetEffectivePermissions();

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

        var rawRoles = await _context.OrganizationRoles
            .AsNoTracking()
            .Where(r => r.OrganizationId == orgId)
            .Select(r => new
            {
                r.Id,
                r.Name,
                r.Description,
                r.ShowAsTitle,
                Claims = r.Claims.Select(c => new { c.ClaimType, c.ClaimValue }).ToList()
            })
            .ToListAsync();

        var roles = rawRoles.Select(r =>
        {
            var legacyModules = r.Claims
                .Where(c => c.ClaimType == "ModuleAccess")
                .Select(c => c.ClaimValue)
                .ToList();
            var explicitPermissions = r.Claims
                .Where(c => c.ClaimType == PermissionCatalog.ClaimType)
                .Select(c => c.ClaimValue)
                .ToList();
            var effectivePermissions = explicitPermissions.Count > 0
                ? explicitPermissions
                : PermissionCatalog.ExpandModules(legacyModules).ToList();
            var derivedModules = PermissionCatalog.DeriveModules(effectivePermissions)
                .Union(legacyModules, StringComparer.OrdinalIgnoreCase)
                .OrderBy(m => m, StringComparer.Ordinal)
                .ToList();

            return new
            {
                r.Id,
                r.Name,
                r.Description,
                r.ShowAsTitle,
                Modules = derivedModules,
                Permissions = effectivePermissions
            };
        }).ToList();

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

        foreach (var permission in permissions)
        {
            role.Claims.Add(new OrganizationRoleClaim
            {
                Id = Guid.NewGuid(),
                OrganizationRoleId = role.Id,
                ClaimType = PermissionCatalog.ClaimType,
                ClaimValue = permission
            });
        }

        _context.OrganizationRoles.Add(role);
        await _context.SaveChangesAsync();
        _workspaceAuth.InvalidateWorkspace(orgId.Value);
        await _audit.LogSecurityAsync(AuditActions.Create, $"Created role '{role.Name}'.", role.Id.ToString(), new { modules = PermissionCatalog.DeriveModules(permissions), permissions });

        return Ok(new { message = "Role created successfully", roleId = role.Id });
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

        _context.OrganizationRoleClaims.AddRange(permissions.Select(permission => new OrganizationRoleClaim
        {
            Id = Guid.NewGuid(),
            OrganizationRoleId = role.Id,
            ClaimType = PermissionCatalog.ClaimType,
            ClaimValue = permission
        }));

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        _workspaceAuth.InvalidateWorkspace(orgId.Value);
        await _audit.LogSecurityAsync(AuditActions.Update, $"Updated role '{role.Name}'.", role.Id.ToString(), new { modules = PermissionCatalog.DeriveModules(permissions), permissions });
        return Ok(new { message = "Role updated successfully" });
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
        _workspaceAuth.InvalidateWorkspace(orgId.Value);
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



public class SaveRoleRequest
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public bool ShowAsTitle { get; set; }
    public List<string> Modules { get; set; } = [];
    public List<string>? Permissions { get; set; }
}

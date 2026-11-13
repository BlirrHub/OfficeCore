using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using OfficeCore.Api.Models;
using OfficeCore.Shared.Authorization;

namespace OfficeCore.Api.Services;

public class OrganizationClaimsTransformation : IClaimsTransformation
{
    private readonly AppDbContext _context;
    private readonly TenantService _tenant;
    private readonly IWorkspaceAuthorizationService _workspaceAuth;

    public OrganizationClaimsTransformation(
        AppDbContext context,
        TenantService tenant,
        IWorkspaceAuthorizationService? workspaceAuth = null)
    {
        _context = context;
        _tenant = tenant;
        _workspaceAuth = workspaceAuth ?? new WorkspaceAuthorizationService(context);
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
            return principal;

        if (principal.HasClaim(c => c.Type == "TenantTransformed"))
            return principal;

        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null)
            return principal;

        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return principal;

        var access = await _workspaceAuth.GetAccessAsync(userId, orgId.Value);
        if (!access.IsActiveMember)
            return principal;

        var claimsIdentity = new ClaimsIdentity();
        claimsIdentity.AddClaim(new Claim("TenantTransformed", "true"));

        if (access.IsOwner)
            claimsIdentity.AddClaim(new Claim("IsWorkspaceOwner", "true"));

        foreach (var roleName in access.Roles)
            claimsIdentity.AddClaim(new Claim(claimsIdentity.RoleClaimType, roleName));

        foreach (var module in access.Modules)
            claimsIdentity.AddClaim(new Claim("ModuleAccess", module));

        foreach (var permission in access.Permissions)
            claimsIdentity.AddClaim(new Claim(PermissionCatalog.ClaimType, permission));

        principal.AddIdentity(claimsIdentity);
        return principal;
    }
}

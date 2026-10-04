using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Models;

namespace OfficeCore.Api.Services;

public class OrganizationClaimsTransformation : IClaimsTransformation
{
    private readonly AppDbContext _context;
    private readonly TenantService _tenant;

    public OrganizationClaimsTransformation(AppDbContext context, TenantService tenant)
    {
        _context = context;
        _tenant = tenant;
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        // Don't transform if not authenticated
        if (principal.Identity?.IsAuthenticated != true)
        {
            return principal;
        }

        // Avoid infinite loop or duplicate claims if we've already transformed
        if (principal.HasClaim(c => c.Type == "TenantTransformed"))
        {
            return principal;
        }

        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null)
        {
            return principal;
        }

        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return principal;
        }

        var employee = await _context.Employees
            .FirstOrDefaultAsync(e => e.UserId == userId && e.OrganizationId == orgId.Value);

        if (employee == null)
        {
            return principal;
        }

        var employeeRoles = await _context.EmployeeRoles
            .Include(er => er.Role)
                .ThenInclude(r => r!.Claims)
            .Where(er => er.EmployeeId == employee.Id)
            .ToListAsync();

        var claimsIdentity = new ClaimsIdentity();
        claimsIdentity.AddClaim(new Claim("TenantTransformed", "true"));

        var org = await _context.Organizations.FindAsync(orgId.Value);
        bool isOwner = org != null && org.CreatedByUserId == userId;
        if (isOwner)
        {
            claimsIdentity.AddClaim(new Claim("IsWorkspaceOwner", "true"));
            // Grant all modules to workspace owner automatically
            var allModules = new[]
            {
                "Dashboard", "MyAttendance", "MyLeaves", "Advances", "Boards",
                "Clients", "Attendance", "Ledger", "Payroll", "Invoices",
                "Users", "Roles", "LeaveApprovals", "WorkSchedules", "TimeOff",
                "Locations", "Organization", "AuditLogs"
            };
            foreach (var m in allModules)
                claimsIdentity.AddClaim(new Claim("ModuleAccess", m));
        }
        else
        {
            // Default modules for all authenticated accounts in the workspace
            claimsIdentity.AddClaim(new Claim("ModuleAccess", "Dashboard"));
            claimsIdentity.AddClaim(new Claim("ModuleAccess", "MyAttendance"));
            claimsIdentity.AddClaim(new Claim("ModuleAccess", "MyLeaves"));
            claimsIdentity.AddClaim(new Claim("ModuleAccess", "Advances"));
            claimsIdentity.AddClaim(new Claim("ModuleAccess", "Boards"));
        }

        foreach (var er in employeeRoles)
        {
            if (er.Role == null) continue;

            // Add standard Role claim (e.g. for [Authorize(Roles = "Admin")])
            if (!string.IsNullOrEmpty(er.Role.Name))
            {
                claimsIdentity.AddClaim(new Claim(claimsIdentity.RoleClaimType, er.Role.Name));
            }

            // Add ModuleAccess claims (e.g. for [Authorize(Policy = "RequireClients")])
            foreach (var claim in er.Role.Claims.Where(c => c.ClaimType == "ModuleAccess"))
            {
                claimsIdentity.AddClaim(new Claim("ModuleAccess", claim.ClaimValue));
            }
        }

        principal.AddIdentity(claimsIdentity);
        return principal;
    }
}

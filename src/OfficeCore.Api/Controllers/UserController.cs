using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using OfficeCore.Api.Hubs;
using OfficeCore.Api.Models;
using OfficeCore.Api.Services;
using OfficeCore.Shared.Authorization;

namespace OfficeCore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "RequireUsers")]
public class UserController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _context;
    private readonly TenantService _tenant;
    private readonly IAuditService _audit;
    private readonly IHubContext<ChatHub> _chatHub;

    public UserController(
        UserManager<ApplicationUser> userManager,
        AppDbContext context,
        TenantService tenant,
        IAuditService audit,
        IHubContext<ChatHub> chatHub)
    {
        _userManager = userManager;
        _context = context;
        _tenant = tenant;
        _audit = audit;
        _chatHub = chatHub;
    }

    private HashSet<string> GetCallerEffectivePermissions()
    {
        var explicitPermissions = User.FindAll(PermissionCatalog.ClaimType).Select(c => c.Value).ToList();
        var moduleClaims = User.FindAll("ModuleAccess").Select(c => c.Value).ToList();
        if (explicitPermissions.Count > 0)
            return new HashSet<string>(explicitPermissions, StringComparer.OrdinalIgnoreCase);

        return new HashSet<string>(PermissionCatalog.ExpandModules(moduleClaims), StringComparer.OrdinalIgnoreCase);
    }

    private bool CallerCanAssignRoles(List<OrganizationRole> rolesToAssign, bool isCurrentUserOwner)
    {
        if (isCurrentUserOwner) return true;
        var callerPermissions = GetCallerEffectivePermissions();
        if (!callerPermissions.Contains(AccessPermissions.EmployeeRolesAssign))
            return false;

        var callerModules = new HashSet<string>(User.FindAll("ModuleAccess").Select(c => c.Value), StringComparer.OrdinalIgnoreCase);

        foreach (var role in rolesToAssign)
        {
            if (string.Equals(role.Name, "Admin", StringComparison.OrdinalIgnoreCase))
                return false;

            var roleModules = role.Claims.Where(c => c.ClaimType == "ModuleAccess").Select(c => c.ClaimValue);
            foreach (var mod in roleModules)
            {
                if (!ModuleCatalog.DefaultKeys.Contains(mod, StringComparer.OrdinalIgnoreCase) && !callerModules.Contains(mod))
                    return false;
            }

            var rolePerms = role.Claims.Where(c => c.ClaimType == PermissionCatalog.ClaimType).Select(c => c.ClaimValue);
            foreach (var perm in rolePerms)
            {
                if (!callerPermissions.Contains(perm))
                    return false;
            }
        }

        return true;
    }

    [HttpGet]
    [Authorize(Policy = "Permission:users.view")]
    public async Task<IActionResult> GetUsers()
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Forbid();

        var org = await _context.Organizations.FindAsync(orgId);
        var createdByUserId = org?.CreatedByUserId;

        // SQLite cannot translate a correlated sub-query for Roles inside a .Select().
        // We use Include/ThenInclude to eagerly load the roles, then project in-memory.
        var employees = await _context.Employees
            .Include(e => e.User)
            .Include(e => e.GovernmentRecord)
            .Include(e => e.EmployeeRoles)
                .ThenInclude(er => er.Role)
            .Where(e => e.OrganizationId == orgId)
            .ToListAsync();

        var result = employees.Select(e => new
        {
            e.User!.Id,
            e.User.Email,
            e.User.FirstName,
            e.User.LastName,
            e.EmployeeId,
            EmployeePk = e.Id,
            e.IsActive,
            e.IsEmployee,
            e.DailyRate,
            e.UseCustomDailyRate,
            e.DateHired,
            e.ResignedDate,
            e.IsEligibleForLeave,
            e.WorkScheduleId,
            IsOwner = e.UserId == createdByUserId,
            GovernmentRecord = e.GovernmentRecord != null ? new
            {
                e.GovernmentRecord.IsEligibleForDeductions,
                e.GovernmentRecord.SssNumber,
                e.GovernmentRecord.PagIbigNumber,
                e.GovernmentRecord.PhilHealthNumber,
                e.GovernmentRecord.TinNumber
            } : null,
            Roles = e.EmployeeRoles.Select(er => er.Role?.Name).Where(n => n != null).ToList()
        });

        return Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = "Permission:users.manage")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Forbid();

        var isCurrentUserOwner = await _tenant.IsCurrentUserWorkspaceOwnerAsync(orgId);

        var requestedRoleNames = (request.Roles ?? [])
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var rolesToAssign = requestedRoleNames.Count == 0
            ? []
            : await _context.OrganizationRoles
                .Include(r => r.Claims)
                .Where(r => r.OrganizationId == orgId.Value && requestedRoleNames.Contains(r.Name))
                .ToListAsync();
        if (rolesToAssign.Count != requestedRoleNames.Count)
            return BadRequest(new { message = "One or more selected roles do not exist in this workspace." });

        if (request.Roles is { Count: > 0 } && !CallerCanAssignRoles(rolesToAssign, isCurrentUserOwner))
            return Forbid("You do not have permission to assign the requested roles.");

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = TextHelper.ToProperCase(request.FirstName),
            LastName = TextHelper.ToProperCase(request.LastName),
            EmailConfirmed = true,
            IsActive = true,
            MustChangePassword = true
        };

        var result = await _userManager.CreateAsync(user, "password");
        if (!result.Succeeded)
            return BadRequest(result.Errors);

        var employee = new Employee
        {
            UserId = user.Id,
            OrganizationId = orgId.Value,
            EmployeeId = request.EmployeeId,
            IsActive = true,
            IsEmployee = request.IsEmployee,
            DailyRate = request.DailyRate,
            UseCustomDailyRate = request.UseCustomDailyRate,
            DateHired = request.DateHired.HasValue ? DateTime.SpecifyKind(request.DateHired.Value, DateTimeKind.Utc) : null,
            IsEligibleForLeave = request.IsEligibleForLeave,
            WorkScheduleId = request.WorkScheduleId.HasValue && await _context.WorkSchedules.AnyAsync(w => w.Id == request.WorkScheduleId.Value && w.OrganizationId == orgId.Value) ? request.WorkScheduleId : null,
            GovernmentRecord = request.GovernmentRecord != null ? new EmployeeGovernmentRecord
            {
                EmployeeId = "", // Updated after save or handled by EF
                OrganizationId = orgId.Value,
                IsEligibleForDeductions = request.GovernmentRecord.IsEligibleForDeductions,
                SssNumber = request.GovernmentRecord.SssNumber,
                PagIbigNumber = request.GovernmentRecord.PagIbigNumber,
                PhilHealthNumber = request.GovernmentRecord.PhilHealthNumber,
                TinNumber = request.GovernmentRecord.TinNumber
            } : null
        };
        _context.Employees.Add(employee);
        await _context.SaveChangesAsync();

        if (employee.GovernmentRecord != null)
        {
            employee.GovernmentRecord.EmployeeId = employee.Id;
            await _context.SaveChangesAsync();
        }

        if (rolesToAssign.Count > 0)
        {
            foreach (var r in rolesToAssign)
            {
                _context.EmployeeRoles.Add(new EmployeeRole
                {
                    EmployeeId = employee.Id,
                    OrganizationRoleId = r.Id
                });
            }
            await _context.SaveChangesAsync();
        }

        await _audit.LogEmployeeAsync(
            AuditActions.Create,
            $"Created new employee profile for {request.FirstName} {request.LastName} ({request.Email}, ID: {request.EmployeeId}, Daily Rate: ₱{request.DailyRate:N2})",
            employee.Id);

        try
        {
            await _chatHub.Clients.Group($"org_{orgId.Value}_chat").SendAsync("MemberJoined");
        }
        catch { }

        return Ok(new { message = "User created successfully", userId = user.Id });
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "Permission:users.manage")]
    public async Task<IActionResult> UpdateUser(string id, [FromBody] UpdateUserRequest request)
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Forbid();
        
        var currentUserId = _userManager.GetUserId(User);
        var isCurrentUserOwner = await _tenant.IsCurrentUserWorkspaceOwnerAsync(orgId);

        var requestedRoleNames = (request.Roles ?? [])
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var rolesToAssign = request.Roles == null || requestedRoleNames.Count == 0
            ? []
            : await _context.OrganizationRoles
                .Include(r => r.Claims)
                .Where(r => r.OrganizationId == orgId.Value && requestedRoleNames.Contains(r.Name))
                .ToListAsync();
        if (request.Roles != null && rolesToAssign.Count != requestedRoleNames.Count)
            return BadRequest(new { message = "One or more selected roles do not exist in this workspace." });

        bool canAssignRoles = request.Roles != null && CallerCanAssignRoles(rolesToAssign, isCurrentUserOwner);
        if (request.Roles != null && !canAssignRoles)
            return Forbid("You do not have permission to assign the requested roles.");

        var org = await _context.Organizations.FindAsync(orgId);
        var createdByUserId = org?.CreatedByUserId;

        var employee = await _context.Employees
            .Include(e => e.User)
            .Include(e => e.GovernmentRecord)
            .Include(e => e.EmployeeRoles)
                .ThenInclude(er => er.Role)
                    .ThenInclude(r => r!.Claims)
            .FirstOrDefaultAsync(e => e.UserId == id && e.OrganizationId == orgId.Value);

        if (employee == null || employee.User == null)
            return NotFound();

        bool isTargetOwner = employee.UserId == createdByUserId;
        bool isTargetPrivileged = employee.EmployeeRoles.Any(er =>
            er.Role?.Name == "Admin" ||
            er.Role?.Claims.Any(c =>
                (c.ClaimType == PermissionCatalog.ClaimType &&
                 (c.ClaimValue == AccessPermissions.UsersManage || c.ClaimValue == AccessPermissions.RolesEdit || c.ClaimValue == AccessPermissions.EmployeeRolesAssign)) ||
                (c.ClaimType == "ModuleAccess" && (c.ClaimValue == AccessModules.Users || c.ClaimValue == AccessModules.Roles))) == true);

        // Rule: Only the workspace owner can edit the workspace owner
        if (isTargetOwner && !isCurrentUserOwner)
            return Forbid();

        // Rule: Only the workspace owner can edit other admins, and non-owners cannot self-assign roles
        if (isTargetPrivileged && !isCurrentUserOwner && currentUserId != employee.UserId)
            return Forbid();
        if (request.Roles != null && !isCurrentUserOwner && currentUserId == employee.UserId)
            return Forbid();

        var oldState = new
        {
            employee.EmployeeId,
            employee.IsActive,
            employee.DailyRate,
            employee.UseCustomDailyRate,
            employee.WorkScheduleId
        };

        employee.EmployeeId = request.EmployeeId;
        employee.IsActive = isTargetOwner ? true : request.IsActive; // Owner is always active

        employee.IsEmployee = request.IsEmployee;
        employee.DailyRate = request.DailyRate;
        employee.UseCustomDailyRate = request.UseCustomDailyRate;
        employee.DateHired = request.DateHired.HasValue ? DateTime.SpecifyKind(request.DateHired.Value, DateTimeKind.Utc) : null;
        employee.IsEligibleForLeave = request.IsEligibleForLeave;
        employee.WorkScheduleId = request.WorkScheduleId.HasValue && await _context.WorkSchedules.AnyAsync(w => w.Id == request.WorkScheduleId.Value && w.OrganizationId == orgId.Value) ? request.WorkScheduleId : null;

        if (request.GovernmentRecord != null)
        {
            if (employee.GovernmentRecord == null)
            {
                employee.GovernmentRecord = new EmployeeGovernmentRecord { EmployeeId = employee.Id, OrganizationId = orgId.Value };
            }
            employee.GovernmentRecord.IsEligibleForDeductions = request.GovernmentRecord.IsEligibleForDeductions;
            employee.GovernmentRecord.SssNumber = request.GovernmentRecord.SssNumber;
            employee.GovernmentRecord.PagIbigNumber = request.GovernmentRecord.PagIbigNumber;
            employee.GovernmentRecord.PhilHealthNumber = request.GovernmentRecord.PhilHealthNumber;
            employee.GovernmentRecord.TinNumber = request.GovernmentRecord.TinNumber;
        }

        employee.User.FirstName = TextHelper.ToProperCase(request.FirstName);
        employee.User.LastName = TextHelper.ToProperCase(request.LastName);
        await _userManager.UpdateAsync(employee.User);
        _context.Employees.Update(employee);
        await _context.SaveChangesAsync();

        if (request.Roles != null && !isTargetOwner && canAssignRoles)
        {
            var currentEmployeeRoles = await _context.EmployeeRoles
                .Where(er => er.EmployeeId == employee.Id)
                .ToListAsync();

            _context.EmployeeRoles.RemoveRange(currentEmployeeRoles);

            foreach (var r in rolesToAssign)
            {
                _context.EmployeeRoles.Add(new EmployeeRole
                {
                    EmployeeId = employee.Id,
                    OrganizationRoleId = r.Id
                });
            }
            await _context.SaveChangesAsync();
        }

        var newState = new
        {
            employee.EmployeeId,
            employee.IsActive,
            employee.DailyRate,
            employee.UseCustomDailyRate,
            employee.WorkScheduleId
        };

        var empFullName = $"{employee.User.FirstName} {employee.User.LastName}".Trim();
        var rateChangeMsg = oldState.DailyRate != newState.DailyRate
            ? $" (Daily Rate updated: ₱{oldState.DailyRate:N2} -> ₱{newState.DailyRate:N2})"
            : "";

        await _audit.LogAsync(
            AuditActions.Update,
            AuditModules.Employee,
            employee.Id.ToString(),
            $"Updated employee profile for {empFullName}{rateChangeMsg}",
            oldState,
            newState);

        return Ok(new { message = "User updated successfully" });
    }

    [HttpPost("{id}/resign")]
    [Authorize(Policy = "Permission:users.manage")]
    public async Task<IActionResult> ResignUser(string id, [FromBody] ResignRequest request)
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Forbid();

        var org = await _context.Organizations.FindAsync(orgId);
        var createdByUserId = org?.CreatedByUserId;

        var employee = await _context.Employees
            .Include(e => e.User)
            .FirstOrDefaultAsync(e => e.UserId == id && e.OrganizationId == orgId.Value);
        if (employee == null)
            return NotFound();

        if (employee.UserId == createdByUserId)
            return BadRequest(new { message = "The workspace owner cannot be resigned." });

        employee.IsActive = false;
        employee.ResignedDate = DateTime.SpecifyKind(request.ResignedDate, DateTimeKind.Utc);

        _context.Employees.Update(employee);
        await _context.SaveChangesAsync();

        var name = $"{employee.User?.FirstName} {employee.User?.LastName}".Trim();
        await _audit.LogEmployeeAsync(
            AuditActions.Security,
            $"Processed resignation/termination for employee {name} (Effective Date: {request.ResignedDate:yyyy-MM-dd})",
            employee.Id);

        return Ok(new { message = "Employee resigned and archived successfully" });
    }

    [HttpGet("{id}/roles")]
    [Authorize(Policy = "Permission:users.view")]
    public async Task<IActionResult> GetUserRoles(string id)
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Forbid();

        var employee = await _context.Employees
            .FirstOrDefaultAsync(e => e.UserId == id && e.OrganizationId == orgId.Value);

        if (employee == null) return NotFound();

        var roleNames = await _context.EmployeeRoles
            .Where(er => er.EmployeeId == employee.Id)
            .Include(er => er.Role)
            .Select(er => er.Role!.Name)
            .ToListAsync();

        return Ok(roleNames);
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "Permission:users.manage")]
    public async Task<IActionResult> RemoveUserFromOrganization(string id)
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Forbid();

        var org = await _context.Organizations.FindAsync(orgId);
        var createdByUserId = org?.CreatedByUserId;

        var employee = await _context.Employees
            .FirstOrDefaultAsync(e => e.UserId == id && e.OrganizationId == orgId.Value);

        if (employee == null)
            return NotFound();

        if (employee.UserId == createdByUserId)
            return BadRequest(new { message = "The workspace owner cannot be removed." });

        // Remove the employee from this organization
        _context.Employees.Remove(employee);
        
        // Save changes will cascade delete org-specific records like BoardMember depending on EF config,
        // or the user just loses access to this org.
        await _context.SaveChangesAsync();

        return Ok(new { message = "User removed from workspace successfully" });
    }
}

public class CreateUserRequest
{
    public required string Email { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public string? EmployeeId { get; set; }
    public bool IsEmployee { get; set; } = true;
    public decimal? DailyRate { get; set; }
    public bool UseCustomDailyRate { get; set; } = false;
    public DateTime? DateHired { get; set; }
    public bool IsEligibleForLeave { get; set; } = true;
    public Guid? WorkScheduleId { get; set; }
    public List<string>? Roles { get; set; }
    public GovernmentRecordDto? GovernmentRecord { get; set; }
}

public class UpdateUserRequest
{
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public string? EmployeeId { get; set; }
    public bool IsActive { get; set; }
    public bool IsEmployee { get; set; } = true;
    public decimal? DailyRate { get; set; }
    public bool UseCustomDailyRate { get; set; } = false;
    public DateTime? DateHired { get; set; }
    public bool IsEligibleForLeave { get; set; } = true;
    public Guid? WorkScheduleId { get; set; }
    public List<string>? Roles { get; set; }
    public GovernmentRecordDto? GovernmentRecord { get; set; }
}

public class ResignRequest
{
    public DateTime ResignedDate { get; set; }
}

public class GovernmentRecordDto
{
    public bool IsEligibleForDeductions { get; set; }
    public string? SssNumber { get; set; }
    public string? PagIbigNumber { get; set; }
    public string? PhilHealthNumber { get; set; }
    public string? TinNumber { get; set; }
}

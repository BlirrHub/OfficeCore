using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using OfficeCore.Api.Hubs;
using OfficeCore.Api.Models;
using OfficeCore.Api.Services;

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

    [HttpGet]
    public async Task<IActionResult> GetUsers()
    {
        var orgId = _tenant.GetActiveOrganizationId();
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
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            EmailConfirmed = true,
            IsActive = true,
            MustChangePassword = true
        };

        var result = await _userManager.CreateAsync(user, "password");
        if (!result.Succeeded)
            return BadRequest(result.Errors);

        if (request.Roles != null && request.Roles.Any())
        {
            // Employee roles will be handled below after creating Employee
        }

        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Forbid();

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
            WorkScheduleId = request.WorkScheduleId,
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

        if (request.Roles != null && request.Roles.Any())
        {
            var orgRoles = await _context.OrganizationRoles
                .Where(r => r.OrganizationId == orgId.Value && request.Roles.Contains(r.Name))
                .ToListAsync();

            foreach (var r in orgRoles)
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
    public async Task<IActionResult> UpdateUser(string id, [FromBody] UpdateUserRequest request)
    {
        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Forbid();
        
        var currentUserId = _userManager.GetUserId(User);

        var org = await _context.Organizations.FindAsync(orgId);
        var createdByUserId = org?.CreatedByUserId;

        var employee = await _context.Employees
            .Include(e => e.User)
            .Include(e => e.GovernmentRecord)
            .Include(e => e.EmployeeRoles)
                .ThenInclude(er => er.Role)
            .FirstOrDefaultAsync(e => e.UserId == id && e.OrganizationId == orgId.Value);

        if (employee == null || employee.User == null)
            return NotFound();

        bool isTargetOwner = employee.UserId == createdByUserId;
        bool isCurrentUserOwner = currentUserId == createdByUserId;
        bool isTargetAdmin = employee.EmployeeRoles.Any(er => er.Role?.Name == "Admin");

        // Rule: Only the workspace owner can edit the workspace owner
        if (isTargetOwner && !isCurrentUserOwner)
            return Forbid();

        // Rule: Only the workspace owner can edit other admins
        if (isTargetAdmin && !isCurrentUserOwner && currentUserId != employee.UserId)
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
        employee.WorkScheduleId = request.WorkScheduleId;

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

        await _userManager.UpdateAsync(employee.User);
        _context.Employees.Update(employee);
        await _context.SaveChangesAsync();

        if (request.Roles != null && !isTargetOwner)
        {

            var currentEmployeeRoles = await _context.EmployeeRoles
                .Where(er => er.EmployeeId == employee.Id)
                .ToListAsync();

            _context.EmployeeRoles.RemoveRange(currentEmployeeRoles);

            var newOrgRoles = await _context.OrganizationRoles
                .Where(r => r.OrganizationId == orgId.Value && request.Roles.Contains(r.Name))
                .ToListAsync();

            foreach (var r in newOrgRoles)
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
    public async Task<IActionResult> ResignUser(string id, [FromBody] ResignRequest request)
    {
        var orgId = _tenant.GetActiveOrganizationId();
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
    public async Task<IActionResult> GetUserRoles(string id)
    {
        var orgId = _tenant.GetActiveOrganizationId();
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
    public async Task<IActionResult> RemoveUserFromOrganization(string id)
    {
        var orgId = _tenant.GetActiveOrganizationId();
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

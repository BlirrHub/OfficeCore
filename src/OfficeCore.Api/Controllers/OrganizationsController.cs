using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Models;
using System.Security.Claims;

namespace OfficeCore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrganizationsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public OrganizationsController(AppDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMyOrganizations()
    {
        var userId = _userManager.GetUserId(User);
        if (userId == null) return Unauthorized();

        var myEmployees = await _context.Employees
            .Include(e => e.Organization)
            .Where(e => e.UserId == userId && e.Organization != null)
            .ToListAsync();

        var result = myEmployees.Select(e => new
        {
            Id = e.Organization!.Id,
            Name = e.Organization.Name,
            JoinedAt = e.DateHired ?? e.Organization.CreatedAt
        });

        return Ok(result);
    }

    [HttpGet("current")]
    public async Task<IActionResult> GetCurrentOrganization()
    {
        if (Request.Cookies.TryGetValue("ActiveOrganizationId", out var activeOrgIdString) && Guid.TryParse(activeOrgIdString, out var activeOrgId))
        {
            var org = await _context.Organizations.FindAsync(activeOrgId);
            if (org != null)
            {
                return Ok(new { Id = org.Id, Name = org.Name });
            }
        }
        return NotFound("No active organization selected.");
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrganization([FromBody] CreateOrgRequest request)
    {
        var userId = _userManager.GetUserId(User);
        if (userId == null) return Unauthorized();

        var org = new Organization
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            CreatedByUserId = userId
            // Note: We don't have ITenantEntity on Organization since it IS the tenant!
        };

        _context.Organizations.Add(org);

        // Also add the user as an Admin employee of this new organization
        var employee = new Employee
        {
            UserId = userId,
            OrganizationId = org.Id,
            DateHired = DateTime.UtcNow,
            IsActive = true
            // ID will be auto-generated string since we initialized it in Employee
        };
        _context.Employees.Add(employee);

        // Create default roles for the organization
        var adminRole = new OrganizationRole
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            Name = "Admin",
            Description = "Administrator with full access",
            ShowAsTitle = true
        };
        var memberRole = new OrganizationRole
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            Name = "Member",
            Description = "Standard organization member",
            ShowAsTitle = false
        };

        var modules = new[]
        {
            "Dashboard", "Clients", "Attendance", "Boards",
            "Payroll", "Invoices", "Ledger",
            "WorkSchedules", "TimeOff", "Locations",
            "Users", "Roles", "Organization", "Advances", "MyLeaves"
        };

        foreach (var module in modules)
        {
            adminRole.Claims.Add(new OrganizationRoleClaim
            {
                Id = Guid.NewGuid(),
                OrganizationRoleId = adminRole.Id,
                ClaimType = "ModuleAccess",
                ClaimValue = module
            });
            
            // Give members basic access
            if (module == "Dashboard" || module == "Attendance" || module == "Boards" || module == "MyLeaves")
            {
                memberRole.Claims.Add(new OrganizationRoleClaim
                {
                    Id = Guid.NewGuid(),
                    OrganizationRoleId = memberRole.Id,
                    ClaimType = "ModuleAccess",
                    ClaimValue = module
                });
            }
        }

        _context.OrganizationRoles.AddRange(adminRole, memberRole);

        // Assign Admin role to the creator
        var employeeRole = new EmployeeRole
        {
            EmployeeId = employee.Id,
            OrganizationRoleId = adminRole.Id
        };
        _context.EmployeeRoles.Add(employeeRole);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            Id = org.Id,
            Name = org.Name,
            JoinedAt = employee.DateHired
        });
    }

    [HttpPost("{id}/select")]
    public async Task<IActionResult> SelectOrganization(Guid id)
    {
        var userId = _userManager.GetUserId(User);
        if (userId == null) return Unauthorized();

        // Verify the user actually belongs to this org
        var employee = await _context.Employees
            .FirstOrDefaultAsync(e => e.UserId == userId && e.OrganizationId == id);

        if (employee == null)
            return Forbid("You are not a member of this organization.");

        // Instead of issuing a whole new JWT right away, let's store it in a secure cookie
        // so that subsequent requests from the browser will have the active organization ID.
        Response.Cookies.Append("ActiveOrganizationId", id.ToString(), new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        });

        return Ok(new { message = "Organization selected", organizationId = id });
    }

    [HttpPost("{id}/invite")]
    public async Task<IActionResult> Invite(Guid id, [FromBody] InviteRequest request)
    {
        var userId = _userManager.GetUserId(User);
        if (userId == null) return Unauthorized();

        var employee = await _context.Employees
            .FirstOrDefaultAsync(e => e.UserId == userId && e.OrganizationId == id);
        
        if (employee == null)
            return Forbid("You are not a member of this organization.");

        var token = Guid.NewGuid().ToString("N");

        var invitation = new OrganizationInvitation
        {
            Id = Guid.NewGuid(),
            OrganizationId = id,
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email,
            Token = token,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            InvitedByUserId = userId
        };

        _context.OrganizationInvitations.Add(invitation);
        await _context.SaveChangesAsync();

        var joinUrl = $"/join/{token}";
        
        return Ok(new { message = "Invitation created", joinUrl, token });
    }

    [HttpPost("join")]
    public async Task<IActionResult> Join([FromBody] JoinRequest request)
    {
        var userId = _userManager.GetUserId(User);
        if (userId == null) return Unauthorized();

        var invitation = await _context.OrganizationInvitations
            .Include(i => i.Organization)
            .FirstOrDefaultAsync(i => i.Token == request.Token);

        if (invitation == null)
            return NotFound("Invalid invitation token.");

        if (invitation.ExpiresAt < DateTime.UtcNow)
            return BadRequest("Invitation has expired.");

        if (invitation.IsAccepted)
            return BadRequest("Invitation has already been accepted.");

        // Create the employee record
        var employee = new Employee
        {
            UserId = userId,
            OrganizationId = invitation.OrganizationId,
            DateHired = DateTime.UtcNow,
            IsActive = true
        };

        _context.Employees.Add(employee);

        // Assign default 'Member' role
        var memberRole = await _context.OrganizationRoles
            .FirstOrDefaultAsync(r => r.OrganizationId == invitation.OrganizationId && r.Name == "Member");

        if (memberRole != null)
        {
            _context.EmployeeRoles.Add(new EmployeeRole
            {
                EmployeeId = employee.Id,
                OrganizationRoleId = memberRole.Id
            });
        }
        
        // Mark invitation as accepted
        invitation.IsAccepted = true;
        invitation.AcceptedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Successfully joined organization",
            organizationId = invitation.OrganizationId,
            organizationName = invitation.Organization!.Name
        });
    }

    [HttpGet("invitations/pending")]
    public async Task<IActionResult> GetPendingInvitations()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var pendingInvites = await _context.OrganizationInvitations
            .Include(i => i.Organization)
            .Include(i => i.InvitedBy)
            .Where(i => i.Email == user.Email && !i.IsAccepted && i.ExpiresAt > DateTime.UtcNow)
            .Select(i => new
            {
                Token = i.Token,
                OrganizationName = i.Organization!.Name,
                InvitedBy = i.InvitedBy != null ? $"{i.InvitedBy.FirstName} {i.InvitedBy.LastName}" : "Someone",
                CreatedAt = i.CreatedAt,
                ExpiresAt = i.ExpiresAt
            })
            .ToListAsync();

        return Ok(pendingInvites);
    }

    [HttpGet("invitations/{token}")]
    public async Task<IActionResult> GetInvitation(string token)
    {
        var invitation = await _context.OrganizationInvitations
            .Include(i => i.Organization)
            .Include(i => i.InvitedBy)
            .FirstOrDefaultAsync(i => i.Token == token && !i.IsAccepted && i.ExpiresAt > DateTime.UtcNow);

        if (invitation == null) return NotFound("Invitation not found or has expired.");

        return Ok(new
        {
            Token = invitation.Token,
            OrganizationName = invitation.Organization!.Name,
            InvitedBy = invitation.InvitedBy != null ? $"{invitation.InvitedBy.FirstName} {invitation.InvitedBy.LastName}" : "Someone",
            HasEmail = !string.IsNullOrEmpty(invitation.Email),
            TargetEmail = invitation.Email
        });
    }

    [HttpPost("invitations/{token}/decline")]
    public async Task<IActionResult> DeclineInvitation(string token)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var invitation = await _context.OrganizationInvitations
            .FirstOrDefaultAsync(i => i.Token == token && i.Email == user.Email && !i.IsAccepted);

        if (invitation == null) return NotFound("Invitation not found.");

        _context.OrganizationInvitations.Remove(invitation);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Invitation declined" });
    }
}

public class CreateOrgRequest
{
    public required string Name { get; set; }
}

public class InviteRequest
{
    public string? Email { get; set; }
}

public class JoinRequest
{
    public required string Token { get; set; }
}

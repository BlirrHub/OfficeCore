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
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        });

        return Ok(new { message = "Organization selected", organizationId = id });
    }

    [HttpPost("{id}/invite")]
    public async Task<IActionResult> Invite(Guid id, [FromBody] InviteRequest request)
    {
        var userId = _userManager.GetUserId(User);
        if (userId == null) return Unauthorized();

        // Basic check: is user in this organization? (In a real app, check for Admin role)
        var employee = await _context.Employees
            .FirstOrDefaultAsync(e => e.UserId == userId && e.OrganizationId == id);
        
        if (employee == null)
            return Forbid("You are not a member of this organization.");

        var token = Guid.NewGuid().ToString("N");

        var invitation = new OrganizationInvitation
        {
            Id = Guid.NewGuid(),
            OrganizationId = id,
            Email = request.Email,
            Token = token,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            InvitedByUserId = userId
        };

        _context.OrganizationInvitations.Add(invitation);
        await _context.SaveChangesAsync();

        // Note: In production we would email this link. For now, just return it.
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
}

public class CreateOrgRequest
{
    public required string Name { get; set; }
}

public class InviteRequest
{
    public required string Email { get; set; }
}

public class JoinRequest
{
    public required string Token { get; set; }
}

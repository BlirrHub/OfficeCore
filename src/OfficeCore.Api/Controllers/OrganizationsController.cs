using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using OfficeCore.Api.Hubs;
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
    private readonly IHubContext<ChatHub> _chatHub;

    public OrganizationsController(
        AppDbContext context, 
        UserManager<ApplicationUser> userManager,
        IHubContext<ChatHub> chatHub)
    {
        _context = context;
        _userManager = userManager;
        _chatHub = chatHub;
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
            JoinedAt = e.DateHired ?? e.Organization.CreatedAt,
            LogoUrl = e.Organization.LogoUrl
        });

        return Ok(result);
    }

    [HttpGet("current")]
    public async Task<IActionResult> GetCurrentOrganization()
    {
        if (Request.Cookies.TryGetValue("ActiveOrganizationId", out var activeOrgIdString) && Guid.TryParse(activeOrgIdString, out var activeOrgId))
        {
            var org = await _context.Organizations
                .Include(o => o.Phones)
                .FirstOrDefaultAsync(o => o.Id == activeOrgId);
            if (org != null)
            {
                return Ok(new { 
                    Id = org.Id, 
                    Name = org.Name,
                    LogoUrl = org.LogoUrl,
                    Address = org.Address,
                    Phones = org.Phones.Select(p => new { p.Id, p.Number, p.Type })
                });
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

        // Workspace owner gets full access automatically via OrganizationClaimsTransformation.
        // They can create and assign custom roles for employees via Role Management.

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

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateOrganization(Guid id, [FromBody] UpdateOrgRequest request)
    {
        var userId = _userManager.GetUserId(User);
        if (userId == null) return Unauthorized();

        var isAdmin = await _context.EmployeeRoles
            .AnyAsync(er => er.Employee!.UserId == userId && er.Employee.OrganizationId == id && er.Role!.Name == "Admin");
            
        if (!isAdmin)
            return Forbid("Only workspace admins can update the workspace profile.");

        var org = await _context.Organizations
            .Include(o => o.Phones)
            .FirstOrDefaultAsync(o => o.Id == id);
        if (org == null) return NotFound();

        org.Name = request.Name;
        org.Address = request.Address;

        _context.OrganizationPhones.RemoveRange(org.Phones);
        if (request.Phones != null)
        {
            foreach (var phone in request.Phones)
            {
                org.Phones.Add(new OrganizationPhone
                {
                    OrganizationId = org.Id,
                    Number = phone.Number,
                    Type = phone.Type
                });
            }
        }

        await _context.SaveChangesAsync();

        return Ok(new { message = "Workspace updated successfully" });
    }

    [HttpPost("{id}/logo")]
    public async Task<IActionResult> UploadLogo(Guid id, IFormFile file)
    {
        var userId = _userManager.GetUserId(User);
        if (userId == null) return Unauthorized();

        var isAdmin = await _context.EmployeeRoles
            .AnyAsync(er => er.Employee!.UserId == userId && er.Employee.OrganizationId == id && er.Role!.Name == "Admin");
            
        if (!isAdmin)
            return Forbid("Only workspace admins can update the workspace profile.");

        var org = await _context.Organizations.FindAsync(id);
        if (org == null) return NotFound();

        if (file == null || file.Length == 0) return BadRequest("File is empty");

        var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "organizations");
        Directory.CreateDirectory(uploadDir);

        var fileName = $"{org.Id}_{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var filePath = Path.Combine(uploadDir, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        org.LogoUrl = $"/uploads/organizations/{fileName}";
        await _context.SaveChangesAsync();

        return Ok(new { logoUrl = org.LogoUrl });
    }

    public class DeleteOrgRequest
    {
        public string Password { get; set; } = string.Empty;
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteOrganization(Guid id, [FromBody] DeleteOrgRequest request)
    {
        var userId = _userManager.GetUserId(User);
        if (userId == null) return Unauthorized();

        var isAdmin = await _context.EmployeeRoles
            .AnyAsync(er => er.Employee!.UserId == userId && er.Employee.OrganizationId == id && er.Role!.Name == "Admin");
            
        if (!isAdmin)
            return Forbid("Only workspace admins can delete the workspace.");

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return Unauthorized();

        var isPasswordValid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!isPasswordValid)
            return BadRequest("Invalid password.");

        var org = await _context.Organizations.FindAsync(id);
        if (org == null) return NotFound();

        _context.Organizations.Remove(org);
        await _context.SaveChangesAsync();

        Response.Cookies.Delete("ActiveOrganizationId");

        return Ok(new { message = "Workspace deleted successfully" });
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

        string? email = request.Email?.Trim();

        if (!string.IsNullOrWhiteSpace(email))
        {
            var invitedUser = await _userManager.FindByEmailAsync(email);
            if (invitedUser == null)
            {
                return BadRequest("No user found with this email address.");
            }

            var alreadyMember = await _context.Employees.AnyAsync(e => e.UserId == invitedUser.Id && e.OrganizationId == id);
            if (alreadyMember)
            {
                return BadRequest("This user is already a member of this workspace.");
            }

            var alreadyInvited = await _context.OrganizationInvitations.AnyAsync(i => i.Email == email && i.OrganizationId == id && !i.IsAccepted && i.ExpiresAt > DateTime.UtcNow);
            if (alreadyInvited)
            {
                return BadRequest("This user already has a pending invitation to this workspace.");
            }
        }

        var token = Guid.NewGuid().ToString("N");

        var invitation = new OrganizationInvitation
        {
            Id = Guid.NewGuid(),
            OrganizationId = id,
            Email = string.IsNullOrWhiteSpace(email) ? null : email,
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

        // Prevent joining if already a member
        var alreadyMember = await _context.Employees
            .AnyAsync(e => e.UserId == userId && e.OrganizationId == invitation.OrganizationId);
            
        if (alreadyMember)
            return BadRequest("You are already a member of this workspace.");

        if (invitation.ExpiresAt < DateTime.UtcNow)
            return BadRequest("Invitation has expired.");

        // If it's targeted to an email, it's single use. Generic links are unlimited use.
        if (!string.IsNullOrEmpty(invitation.Email))
        {
            if (invitation.IsAccepted)
                return BadRequest("This email invitation has already been accepted.");
                
            invitation.IsAccepted = true;
            invitation.AcceptedAt = DateTime.UtcNow;
        }

        // Create the employee record
        var employee = new Employee
        {
            UserId = userId,
            OrganizationId = invitation.OrganizationId,
            DateHired = DateTime.UtcNow,
            IsActive = true
        };

        _context.Employees.Add(employee);

        // Assign default 'Employee' role
        var memberRole = await _context.OrganizationRoles
            .FirstOrDefaultAsync(r => r.OrganizationId == invitation.OrganizationId && r.Name == "Employee");

        if (memberRole != null)
        {
            _context.EmployeeRoles.Add(new EmployeeRole
            {
                EmployeeId = employee.Id,
                OrganizationRoleId = memberRole.Id
            });
        }

        await _context.SaveChangesAsync();

        try
        {
            await _chatHub.Clients.Group($"org_{invitation.OrganizationId}_chat").SendAsync("MemberJoined");
        }
        catch { }

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

public class UpdateOrgRequest
{
    public required string Name { get; set; }
    public string? Address { get; set; }
    public List<PhoneDto>? Phones { get; set; }
}

public class PhoneDto
{
    public required string Number { get; set; }
    public required string Type { get; set; }
}

public class InviteRequest
{
    public string? Email { get; set; }
}

public class JoinRequest
{
    public required string Token { get; set; }
}

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
}

public class CreateOrgRequest
{
    public required string Name { get; set; }
}

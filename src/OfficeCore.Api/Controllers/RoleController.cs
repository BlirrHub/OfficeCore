using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Models;

namespace OfficeCore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class RoleController : ControllerBase
{
    private readonly RoleManager<AppRole> _roleManager;

    public RoleController(RoleManager<AppRole> roleManager)
    {
        _roleManager = roleManager;
    }

    [HttpGet]
    public async Task<IActionResult> GetRoles()
    {
        var roles = await _roleManager.Roles.Select(r => new
        {
            r.Id,
            r.Name,
            r.Description
        }).ToListAsync();

        return Ok(roles);
    }

    [HttpPost]
    public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequest request)
    {
        var role = new AppRole(request.Name) { Description = request.Description };
        var result = await _roleManager.CreateAsync(role);
        
        if (!result.Succeeded)
            return BadRequest(result.Errors);

        if (request.Modules != null)
        {
            foreach (var module in request.Modules)
            {
                await _roleManager.AddClaimAsync(role, new Claim("ModuleAccess", module));
            }
        }

        return Ok(new { message = "Role created successfully", roleId = role.Id });
    }

    [HttpGet("{id}/modules")]
    public async Task<IActionResult> GetRoleModules(string id)
    {
        var role = await _roleManager.FindByIdAsync(id);
        if (role == null)
            return NotFound();

        var claims = await _roleManager.GetClaimsAsync(role);
        var modules = claims.Where(c => c.Type == "ModuleAccess").Select(c => c.Value).ToList();
        
        return Ok(modules);
    }

    [HttpPut("{id}/modules")]
    public async Task<IActionResult> UpdateRoleModules(string id, [FromBody] List<string> modules)
    {
        var role = await _roleManager.FindByIdAsync(id);
        if (role == null)
            return NotFound();

        // Remove existing module claims
        var existingClaims = await _roleManager.GetClaimsAsync(role);
        var moduleClaims = existingClaims.Where(c => c.Type == "ModuleAccess").ToList();
        
        foreach (var claim in moduleClaims)
        {
            await _roleManager.RemoveClaimAsync(role, claim);
        }

        // Add new module claims
        foreach (var module in modules)
        {
            await _roleManager.AddClaimAsync(role, new Claim("ModuleAccess", module));
        }

        return Ok(new { message = "Role modules updated successfully" });
    }
}

public class CreateRoleRequest
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public List<string>? Modules { get; set; }
}

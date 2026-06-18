using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Models;

namespace OfficeCore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "RequireRoles")]
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
            r.Description,
            r.ShowAsTitle
        }).ToListAsync();

        return Ok(roles);
    }

    [HttpPost]
    public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequest request)
    {
        var role = new AppRole(request.Name) 
        { 
            Description = request.Description,
            ShowAsTitle = request.ShowAsTitle
        };
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

    [HttpPut("{id}/details")]
    public async Task<IActionResult> UpdateRoleDetails(string id, [FromBody] UpdateRoleRequest request)
    {
        var role = await _roleManager.FindByIdAsync(id);
        if (role == null) return NotFound();

        if (role.Name != "Admin" && !string.IsNullOrWhiteSpace(request.Name))
        {
            role.Name = request.Name;
        }
        role.Description = request.Description;
        role.ShowAsTitle = request.ShowAsTitle;

        var result = await _roleManager.UpdateAsync(role);
        if (!result.Succeeded) return BadRequest(result.Errors);

        return Ok(new { message = "Role updated successfully" });
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

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteRole(string id)
    {
        var role = await _roleManager.FindByIdAsync(id);
        if (role == null)
            return NotFound();

        if (role.Name == "Admin")
            return BadRequest(new { message = "Cannot delete the system Admin role." });

        var result = await _roleManager.DeleteAsync(role);
        if (!result.Succeeded)
            return BadRequest(result.Errors);

        return Ok(new { message = "Role deleted successfully" });
    }
}

public class CreateRoleRequest
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public bool ShowAsTitle { get; set; }
    public List<string>? Modules { get; set; }
}

public class UpdateRoleRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public bool ShowAsTitle { get; set; }
}

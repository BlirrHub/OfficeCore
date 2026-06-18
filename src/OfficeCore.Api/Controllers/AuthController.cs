using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OfficeCore.Api.Models;

namespace OfficeCore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly SignInManager<Employee> _signInManager;
    private readonly UserManager<Employee> _userManager;
    private readonly RoleManager<AppRole> _roleManager;

    public AuthController(
        SignInManager<Employee> signInManager,
        UserManager<Employee> userManager,
        RoleManager<AppRole> roleManager)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null || !user.IsActive)
            return Unauthorized(new { message = "Invalid login attempt or account disabled." });

        var result = await _signInManager.PasswordSignInAsync(user, request.Password, request.RememberMe, lockoutOnFailure: false);
        
        if (result.Succeeded)
            return Ok(new { message = "Login successful" });

        return Unauthorized(new { message = "Invalid login attempt." });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return Ok(new { message = "Logout successful" });
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        if (!User.Identity?.IsAuthenticated ?? true)
            return Unauthorized();

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Unauthorized();

        var roles = await _userManager.GetRolesAsync(user);
        var claims = await _userManager.GetClaimsAsync(user);

        // Gather role claims (module access) and find TitleRoles
        var moduleClaims = new List<string>();
        var titleRoles = new List<string>();
        foreach (var role in roles)
        {
            var appRole = await _roleManager.FindByNameAsync(role);
            if (appRole != null)
            {
                if (appRole.ShowAsTitle && !string.IsNullOrEmpty(appRole.Name))
                    titleRoles.Add(appRole.Name);

                var roleClaims = await _roleManager.GetClaimsAsync(appRole);
                moduleClaims.AddRange(roleClaims.Where(c => c.Type == "ModuleAccess").Select(c => c.Value));
            }
        }

        var titleRole = titleRoles.Any() ? string.Join(" / ", titleRoles) : null;

        return Ok(new
        {
            id = user.Id,
            email = user.Email,
            firstName = user.FirstName,
            lastName = user.LastName,
            mustChangePassword = user.MustChangePassword,
            roles = roles,
            titleRole = titleRole,
            modules = moduleClaims.Distinct()
        });
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Unauthorized();

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
            return BadRequest(result.Errors);

        user.MustChangePassword = false;
        await _userManager.UpdateAsync(user);

        // Re-sign in to refresh the security stamp/cookie
        await _signInManager.RefreshSignInAsync(user);

        return Ok(new { message = "Password changed successfully" });
    }
}

public class LoginRequest
{
    public required string Email { get; set; }
    public required string Password { get; set; }
    public bool RememberMe { get; set; }
}

public class ChangePasswordRequest
{
    public required string CurrentPassword { get; set; }
    public required string NewPassword { get; set; }
}

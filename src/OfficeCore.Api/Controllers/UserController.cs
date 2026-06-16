using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Models;

namespace OfficeCore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class UserController : ControllerBase
{
    private readonly UserManager<Employee> _userManager;

    public UserController(UserManager<Employee> userManager)
    {
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _userManager.Users.Select(u => new
        {
            u.Id,
            u.Email,
            u.FirstName,
            u.LastName,
            u.EmployeeId,
            u.IsActive,
            u.DailyRate,
            u.WorkScheduleId
        }).ToListAsync();

        return Ok(users);
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        var user = new Employee
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            EmployeeId = request.EmployeeId,
            IsActive = true,
            EmailConfirmed = true,
            DailyRate = request.DailyRate,
            WorkScheduleId = request.WorkScheduleId
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return BadRequest(result.Errors);

        if (request.Roles != null && request.Roles.Any())
        {
            await _userManager.AddToRolesAsync(user, request.Roles);
        }

        return Ok(new { message = "User created successfully", userId = user.Id });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(string id, [FromBody] UpdateUserRequest request)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
            return NotFound();

        user.FirstName = request.FirstName;
        user.LastName = request.LastName;
        user.EmployeeId = request.EmployeeId;
        user.IsActive = request.IsActive;
        user.DailyRate = request.DailyRate;
        user.WorkScheduleId = request.WorkScheduleId;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
            return BadRequest(result.Errors);

        // Update Roles
        if (request.Roles != null)
        {
            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            await _userManager.AddToRolesAsync(user, request.Roles);
        }

        return Ok(new { message = "User updated successfully" });
    }

    [HttpGet("{id}/roles")]
    public async Task<IActionResult> GetUserRoles(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
            return NotFound();

        var roles = await _userManager.GetRolesAsync(user);
        return Ok(roles);
    }
}

public class CreateUserRequest
{
    public required string Email { get; set; }
    public required string Password { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public string? EmployeeId { get; set; }
    public decimal? DailyRate { get; set; }
    public Guid? WorkScheduleId { get; set; }
    public List<string>? Roles { get; set; }
}

public class UpdateUserRequest
{
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public string? EmployeeId { get; set; }
    public bool IsActive { get; set; }
    public decimal? DailyRate { get; set; }
    public Guid? WorkScheduleId { get; set; }
    public List<string>? Roles { get; set; }
}

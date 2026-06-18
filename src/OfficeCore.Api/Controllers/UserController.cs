using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Models;

namespace OfficeCore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "RequireUsers")]
public class UserController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _context;

    public UserController(UserManager<ApplicationUser> userManager, AppDbContext context)
    {
        _userManager = userManager;
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers()
    {
        var employees = await _context.Employees
            .Include(e => e.User)
            .Include(e => e.GovernmentRecord)
            .Select(e => new
            {
                e.User.Id,
                e.User.Email,
                e.User.FirstName,
                e.User.LastName,
                e.EmployeeId,
                e.IsActive,
                e.IsEmployee,
                e.DailyRate,
                e.UseCustomDailyRate,
                e.DateHired,
                e.ResignedDate,
                e.IsEligibleForLeave,
                e.WorkScheduleId,
                GovernmentRecord = e.GovernmentRecord != null ? new
                {
                    e.GovernmentRecord.IsEligibleForDeductions,
                    e.GovernmentRecord.SssNumber,
                    e.GovernmentRecord.PagIbigNumber,
                    e.GovernmentRecord.PhilHealthNumber,
                    e.GovernmentRecord.TinNumber
                } : null
            }).ToListAsync();

        return Ok(employees);
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
            IsActive = true
        };

        var result = await _userManager.CreateAsync(user, "password");
        if (!result.Succeeded)
            return BadRequest(result.Errors);

        if (request.Roles != null && request.Roles.Any())
        {
            await _userManager.AddToRolesAsync(user, request.Roles);
        }

        var employee = new Employee
        {
            UserId = user.Id,
            OrganizationId = Guid.Empty, // Will be set to context organization ID in future phases
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

        return Ok(new { message = "User created successfully", userId = user.Id });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(string id, [FromBody] UpdateUserRequest request)
    {
        var employee = await _context.Employees
            .Include(e => e.User)
            .Include(e => e.GovernmentRecord)
            .FirstOrDefaultAsync(e => e.UserId == id);

        if (employee == null || employee.User == null)
            return NotFound();

        employee.User.FirstName = request.FirstName;
        employee.User.LastName = request.LastName;
        employee.EmployeeId = request.EmployeeId;
        employee.IsActive = request.IsActive;
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
                employee.GovernmentRecord = new EmployeeGovernmentRecord { EmployeeId = employee.Id };
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

        if (request.Roles != null)
        {
            var currentRoles = await _userManager.GetRolesAsync(employee.User);
            await _userManager.RemoveFromRolesAsync(employee.User, currentRoles);
            await _userManager.AddToRolesAsync(employee.User, request.Roles);
        }

        return Ok(new { message = "User updated successfully" });
    }

    [HttpPost("{id}/resign")]
    public async Task<IActionResult> ResignUser(string id, [FromBody] ResignRequest request)
    {
        var employee = await _context.Employees.FirstOrDefaultAsync(e => e.UserId == id);
        if (employee == null)
            return NotFound();

        employee.IsActive = false;
        employee.ResignedDate = DateTime.SpecifyKind(request.ResignedDate, DateTimeKind.Utc);

        _context.Employees.Update(employee);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Employee resigned and archived successfully" });
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

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
            return NotFound();

        var employee = await _context.Employees.FirstOrDefaultAsync(e => e.UserId == id);
        if (employee != null)
        {
            _context.Employees.Remove(employee);
        }

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
            return BadRequest(result.Errors);

        return Ok(new { message = "User deleted successfully" });
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

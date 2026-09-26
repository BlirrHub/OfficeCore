using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Data;
using OfficeCore.Api.Hubs;
using OfficeCore.Api.Models;
using OfficeCore.Api.Services;

namespace OfficeCore.Api.Controllers;

public class LocationPingDto
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? AccuracyMeters { get; set; }
    public double? SpeedKmh { get; set; }
    public string? StatusNote { get; set; }
}

public class ActiveFieldEmployeeDto
{
    public string EmployeeId { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? ProfilePictureUrl { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? SpeedKmh { get; set; }
    public string? StatusNote { get; set; }
    public DateTime LastPingTime { get; set; }
    public bool IsOnline { get; set; }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LocationsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly TenantService _tenantService;
    private readonly IHubContext<NotificationHub> _notificationHub;

    public LocationsController(
        AppDbContext context, 
        TenantService tenantService,
        IHubContext<NotificationHub> notificationHub)
    {
        _context = context;
        _tenantService = tenantService;
        _notificationHub = notificationHub;
    }

    /// <summary>
    /// Called by mobile devices / field workers (riders, delivery, liaison) to record their live GPS position.
    /// </summary>
    [HttpPost("ping")]
    public async Task<IActionResult> PingLocation([FromBody] LocationPingDto dto)
    {
        var orgId = _tenantService.CurrentOrganizationId;
        if (orgId == Guid.Empty)
        {
            return BadRequest("No active workspace selected.");
        }

        var employee = await _context.Employees
            .Include(e => e.User)
            .Include(e => e.EmployeeRoles)
                .ThenInclude(er => er.Role)
            .FirstOrDefaultAsync(e => e.OrganizationId == orgId && e.UserId == _tenantService.GetUserId());

        if (employee == null)
        {
            return Unauthorized("Active employee record not found in the current workspace.");
        }

        var ping = new EmployeeLocationPing
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            EmployeeId = employee.Id,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            AccuracyMeters = dto.AccuracyMeters,
            SpeedKmh = dto.SpeedKmh,
            StatusNote = dto.StatusNote,
            Timestamp = DateTime.UtcNow
        };

        _context.EmployeeLocationPings.Add(ping);
        await _context.SaveChangesAsync();

        var roleName = employee.EmployeeRoles.Select(r => r.Role?.Name).FirstOrDefault(r => !string.IsNullOrEmpty(r)) ?? "Field Staff";
        var empName = employee.User != null ? $"{employee.User.FirstName} {employee.User.LastName}".Trim() : "Field Worker";

        // Broadcast real-time location update to Admin/Manager map listeners (lowercase org_ group)
        await _notificationHub.Clients.Group($"org_{orgId}").SendAsync("FieldLocationUpdated", new ActiveFieldEmployeeDto
        {
            EmployeeId = employee.Id,
            EmployeeName = empName,
            Department = "Field Operations",
            Role = roleName,
            ProfilePictureUrl = employee.User?.ProfilePictureUrl,
            Latitude = ping.Latitude,
            Longitude = ping.Longitude,
            SpeedKmh = ping.SpeedKmh,
            StatusNote = ping.StatusNote ?? "On Field Assignment",
            LastPingTime = ping.Timestamp,
            IsOnline = true
        });

        return Ok(new { success = true, timestamp = ping.Timestamp });
    }

    /// <summary>
    /// Retrieves the most recent GPS location for all field employees who clocked in or pinged recently.
    /// </summary>
    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<ActiveFieldEmployeeDto>>> GetActiveFieldEmployees()
    {
        var orgId = _tenantService.CurrentOrganizationId;
        var cutoff = DateTime.UtcNow.AddHours(-12); // Pings in the last 12 hours

        // Get latest ping per employee
        var recentPings = await _context.EmployeeLocationPings
            .Where(p => p.OrganizationId == orgId && p.Timestamp >= cutoff)
            .GroupBy(p => p.EmployeeId)
            .Select(g => g.OrderByDescending(p => p.Timestamp).FirstOrDefault())
            .ToListAsync();

        var employeeIds = recentPings.Where(p => p != null).Select(p => p!.EmployeeId).Distinct().ToList();

        // Support both Employee.Id and legacy ApplicationUser.Id ping keys
        var employees = await _context.Employees
            .Include(e => e.User)
            .Include(e => e.EmployeeRoles)
                .ThenInclude(er => er.Role)
            .Where(e => e.OrganizationId == orgId && (employeeIds.Contains(e.Id) || employeeIds.Contains(e.UserId)))
            .ToListAsync();

        var empLookup = new Dictionary<string, Employee>();
        foreach (var emp in employees)
        {
            empLookup[emp.Id] = emp;
            if (!string.IsNullOrEmpty(emp.UserId) && !empLookup.ContainsKey(emp.UserId))
            {
                empLookup[emp.UserId] = emp;
            }
        }

        var result = recentPings
            .Where(p => p != null && empLookup.ContainsKey(p.EmployeeId))
            .Select(p =>
            {
                var emp = empLookup[p!.EmployeeId];
                var minutesSincePing = (DateTime.UtcNow - p.Timestamp).TotalMinutes;
                var roleName = emp.EmployeeRoles.Select(r => r.Role?.Name).FirstOrDefault(r => !string.IsNullOrEmpty(r)) ?? "Field Staff";
                var empName = emp.User != null ? $"{emp.User.FirstName} {emp.User.LastName}".Trim() : "Field Worker";

                return new ActiveFieldEmployeeDto
                {
                    EmployeeId = emp.Id,
                    EmployeeName = empName,
                    Department = "Field Operations",
                    Role = roleName,
                    ProfilePictureUrl = emp.User?.ProfilePictureUrl,
                    Latitude = p.Latitude,
                    Longitude = p.Longitude,
                    SpeedKmh = p.SpeedKmh,
                    StatusNote = p.StatusNote ?? "On Field Assignment",
                    LastPingTime = p.Timestamp,
                    IsOnline = minutesSincePing <= 15
                };
            })
            .OrderByDescending(e => e.LastPingTime)
            .ToList();

        return Ok(result);
    }
}

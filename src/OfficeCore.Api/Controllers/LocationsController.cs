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
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var ping = new EmployeeLocationPing
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            EmployeeId = userId,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            AccuracyMeters = dto.AccuracyMeters,
            SpeedKmh = dto.SpeedKmh,
            StatusNote = dto.StatusNote,
            Timestamp = DateTime.UtcNow
        };

        _context.EmployeeLocationPings.Add(ping);
        await _context.SaveChangesAsync();

        // Broadcast real-time location update to Admin/Manager map listeners
        await _notificationHub.Clients.Group($"Org_{orgId}").SendAsync("FieldLocationUpdated", new
        {
            EmployeeId = userId,
            ping.Latitude,
            ping.Longitude,
            ping.SpeedKmh,
            ping.StatusNote,
            Timestamp = ping.Timestamp
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

        var employeeIds = recentPings.Select(p => p!.EmployeeId).Distinct().ToList();

        var employees = await _context.Users
            .Where(u => employeeIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id);

        var result = recentPings
            .Where(p => p != null && employees.ContainsKey(p.EmployeeId))
            .Select(p =>
            {
                var emp = employees[p!.EmployeeId];
                var minutesSincePing = (DateTime.UtcNow - p.Timestamp).TotalMinutes;

                return new ActiveFieldEmployeeDto
                {
                    EmployeeId = emp.Id,
                    EmployeeName = $"{emp.FirstName} {emp.LastName}".Trim(),
                    Department = emp.Department ?? "Field Operations",
                    Role = "Field Staff",
                    ProfilePictureUrl = emp.ProfilePictureUrl,
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

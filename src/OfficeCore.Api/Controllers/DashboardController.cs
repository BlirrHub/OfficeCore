using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Models;
using OfficeCore.Api.Services;

namespace OfficeCore.Api.Controllers;

public class DashboardStatsDto
{
    public int TotalEmployees { get; set; }
    public int PresentTodayCount { get; set; }
    public double AttendanceRatePercent { get; set; }
    public int PendingLeavesCount { get; set; }
    public int PendingAdvancesCount { get; set; }
    public int TotalTasksCount { get; set; }
    public int CompletedTasksCount { get; set; }
    public int InProgressTasksCount { get; set; }
    public int ActiveOutsideStaffCount { get; set; }
    public string DatabaseEngine { get; set; } = "PostgreSQL 18";
    public List<DashboardActivityItemDto> RecentActivities { get; set; } = new();
}

public class DashboardActivityItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // Attendance, Leave, Task, Advance, Location
    public string Icon { get; set; } = "📌";
    public string BadgeColor { get; set; } = "emerald";
    public DateTime Timestamp { get; set; }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly TenantService _tenantService;

    public DashboardController(AppDbContext context, TenantService tenantService)
    {
        _context = context;
        _tenantService = tenantService;
    }

    [HttpGet("stats")]
    public async Task<ActionResult<DashboardStatsDto>> GetStats()
    {
        var orgId = _tenantService.CurrentOrganizationId;
        if (orgId == Guid.Empty)
        {
            return Unauthorized("No active workspace selected.");
        }

        // 1. Team stats
        var totalEmployees = await _context.Employees
            .CountAsync(e => e.OrganizationId == orgId && e.IsActive && e.IsEmployee);

        // 2. Attendance stats today (UTC midnight to now)
        var todayUtcStart = DateTime.UtcNow.Date;
        var todayClockIns = await _context.AttendanceRecords
            .Where(a => a.OrganizationId == orgId 
                        && a.Type == AttendanceType.ClockIn 
                        && a.Timestamp >= todayUtcStart)
            .Select(a => a.EmployeeId)
            .Distinct()
            .CountAsync();

        var attendanceRate = totalEmployees > 0 
            ? Math.Round(((double)todayClockIns / totalEmployees) * 100, 1) 
            : 0;

        // 3. Pending approvals
        var pendingLeaves = await _context.LeaveRequests
            .CountAsync(l => l.OrganizationId == orgId && l.Status == LeaveRequestStatus.Pending);

        var pendingAdvances = await _context.AdvanceRequests
            .CountAsync(a => a.OrganizationId == orgId && a.Status == AdvanceStatus.Pending);

        // 4. Board cards
        var cards = await _context.BoardCards
            .Include(c => c.List)
            .Where(c => c.OrganizationId == orgId)
            .ToListAsync();

        var totalTasks = cards.Count;
        var completedTasks = cards.Count(c => c.List != null && 
            (c.List.Name.Contains("Done", StringComparison.OrdinalIgnoreCase) || 
             c.List.Name.Contains("Completed", StringComparison.OrdinalIgnoreCase)));
        var inProgressTasks = cards.Count(c => c.List != null && 
            c.List.Name.Contains("Progress", StringComparison.OrdinalIgnoreCase));

        // 5. Active outside field staff (pings within last 12h)
        var cutoff = DateTime.UtcNow.AddHours(-12);
        var activeOutsideStaff = await _context.EmployeeLocationPings
            .Where(p => p.OrganizationId == orgId && p.Timestamp >= cutoff)
            .Select(p => p.EmployeeId)
            .Distinct()
            .CountAsync();

        // 6. Recent activity timeline (aggregate recent events)
        var activities = new List<DashboardActivityItemDto>();

        // Recent attendance
        var recentAttendance = await _context.AttendanceRecords
            .Include(a => a.Employee)
                .ThenInclude(e => e!.User)
            .Where(a => a.OrganizationId == orgId)
            .OrderByDescending(a => a.Timestamp)
            .Take(4)
            .ToListAsync();

        foreach (var att in recentAttendance)
        {
            var name = att.Employee?.User != null 
                ? $"{att.Employee.User.FirstName} {att.Employee.User.LastName}".Trim() 
                : "Employee";
            activities.Add(new DashboardActivityItemDto
            {
                Id = att.Id.ToString(),
                Title = $"{name} clocked { (att.Type == AttendanceType.ClockIn ? "IN" : "OUT") }",
                Description = $"Via {att.Source} at {att.Timestamp.ToLocalTime():h:mm tt}",
                Category = "Attendance",
                Icon = "⏱️",
                BadgeColor = att.Type == AttendanceType.ClockIn ? "emerald" : "zinc",
                Timestamp = att.Timestamp.UtcDateTime
            });
        }

        // Recent leaves
        var recentLeaves = await _context.LeaveRequests
            .Include(l => l.Employee)
                .ThenInclude(e => e!.User)
            .Where(l => l.OrganizationId == orgId)
            .OrderByDescending(l => l.CreatedAt)
            .Take(3)
            .ToListAsync();

        foreach (var l in recentLeaves)
        {
            var name = l.Employee?.User != null 
                ? $"{l.Employee.User.FirstName} {l.Employee.User.LastName}".Trim() 
                : "Employee";
            activities.Add(new DashboardActivityItemDto
            {
                Id = l.Id.ToString(),
                Title = $"{name} requested {l.Type} Leave",
                Description = $"{l.StartDate:MMM dd} - {l.EndDate:MMM dd} • Status: {l.Status}",
                Category = "Leave",
                Icon = "🌴",
                BadgeColor = l.Status == LeaveRequestStatus.Approved ? "emerald" : l.Status == LeaveRequestStatus.Rejected ? "red" : "amber",
                Timestamp = l.CreatedAt
            });
        }

        // Recent advances
        var recentAdvances = await _context.AdvanceRequests
            .Include(a => a.Employee)
                .ThenInclude(e => e!.User)
            .Where(a => a.OrganizationId == orgId)
            .OrderByDescending(a => a.RequestDate)
            .Take(3)
            .ToListAsync();

        foreach (var adv in recentAdvances)
        {
            var name = adv.Employee?.User != null 
                ? $"{adv.Employee.User.FirstName} {adv.Employee.User.LastName}".Trim() 
                : "Employee";
            activities.Add(new DashboardActivityItemDto
            {
                Id = adv.Id.ToString(),
                Title = $"{name} filed {adv.Type}",
                Description = $"Amount: ₱{adv.RequestedAmount:N2} • Status: {adv.Status}",
                Category = "Advance",
                Icon = "💵",
                BadgeColor = adv.Status == AdvanceStatus.Approved ? "emerald" : adv.Status == AdvanceStatus.Rejected ? "red" : "amber",
                Timestamp = adv.RequestDate
            });
        }

        var orderedActivities = activities
            .OrderByDescending(a => a.Timestamp)
            .Take(6)
            .ToList();

        var dto = new DashboardStatsDto
        {
            TotalEmployees = totalEmployees,
            PresentTodayCount = todayClockIns,
            AttendanceRatePercent = attendanceRate,
            PendingLeavesCount = pendingLeaves,
            PendingAdvancesCount = pendingAdvances,
            TotalTasksCount = totalTasks,
            CompletedTasksCount = completedTasks,
            InProgressTasksCount = inProgressTasks,
            ActiveOutsideStaffCount = activeOutsideStaff,
            DatabaseEngine = "PostgreSQL 18 (Npgsql)",
            RecentActivities = orderedActivities
        };

        return Ok(dto);
    }
}

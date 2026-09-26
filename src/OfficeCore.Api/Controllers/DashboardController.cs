using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Models;
using OfficeCore.Api.Services;

namespace OfficeCore.Api.Controllers;

public class DashboardPermissionsDto
{
    public bool CanManageLeaves { get; set; }
    public bool CanManageAttendance { get; set; }
    public bool CanManageUsers { get; set; }
    public bool CanManageAdvances { get; set; }
    public bool CanManagePayroll { get; set; }
    public bool CanViewBoards { get; set; }
    public bool CanViewLocations { get; set; }
    public bool CanViewClients { get; set; }
    public bool CanViewInvoices { get; set; }
}

public class PersonalTaskItemDto
{
    public Guid CardId { get; set; }
    public Guid BoardId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ListName { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
}

public class DashboardStatsDto
{
    public string RoleName { get; set; } = "Employee";
    public bool IsManager { get; set; }
    public DashboardPermissionsDto Permissions { get; set; } = new();

    // Personal ESS metrics (for the logged in user)
    public bool PersonalClockedInToday { get; set; }
    public DateTimeOffset? PersonalLastClockInTime { get; set; }
    public double PersonalHoursWorkedToday { get; set; }
    public int PersonalPendingLeavesCount { get; set; }
    public int PersonalApprovedLeavesCount { get; set; }
    public int PersonalAssignedTasksCount { get; set; }
    public List<PersonalTaskItemDto> PersonalTasks { get; set; } = new();
    public decimal? PersonalActiveAdvanceAmount { get; set; }
    public string? PersonalActiveAdvanceStatus { get; set; }

    // Management MSS metrics (only populated for managers/admins)
    public int? TotalEmployees { get; set; }
    public int? PresentTodayCount { get; set; }
    public double? AttendanceRatePercent { get; set; }
    public int? PendingLeavesCount { get; set; }
    public int? PendingAdvancesCount { get; set; }
    public int? TotalTasksCount { get; set; }
    public int? CompletedTasksCount { get; set; }
    public int? InProgressTasksCount { get; set; }
    public int? ActiveOutsideStaffCount { get; set; }
    public Guid? PrimaryBoardId { get; set; }
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

        var userId = _tenantService.GetUserId();
        var employee = await _context.Employees
            .Include(e => e.User)
            .Include(e => e.EmployeeRoles)
                .ThenInclude(er => er.Role)
            .FirstOrDefaultAsync(e => e.OrganizationId == orgId && e.UserId == userId);

        var roleName = employee?.EmployeeRoles.Select(r => r.Role?.Name).FirstOrDefault(r => !string.IsNullOrEmpty(r)) ?? "Employee";

        // Check module claims for this user
        var perms = new DashboardPermissionsDto
        {
            CanManageLeaves = User.HasClaim("ModuleAccess", "LeaveApprovals"),
            CanManageAttendance = User.HasClaim("ModuleAccess", "Attendance"),
            CanManageUsers = User.HasClaim("ModuleAccess", "Users"),
            CanManageAdvances = User.HasClaim("ModuleAccess", "Ledger"),
            CanManagePayroll = User.HasClaim("ModuleAccess", "Payroll"),
            CanViewBoards = User.HasClaim("ModuleAccess", "Boards"),
            CanViewLocations = User.HasClaim("ModuleAccess", "Locations"),
            CanViewClients = User.HasClaim("ModuleAccess", "Clients"),
            CanViewInvoices = User.HasClaim("ModuleAccess", "Invoices")
        };

        // Determine if user has any managerial/admin capabilities
        bool isManager = perms.CanManageLeaves || perms.CanManageAttendance || perms.CanManageUsers || 
                         perms.CanManageAdvances || perms.CanManagePayroll;

        var dto = new DashboardStatsDto
        {
            RoleName = roleName,
            IsManager = isManager,
            Permissions = perms,
            DatabaseEngine = "PostgreSQL 18 (Npgsql)"
        };

        // Determine primary board for workspace
        var primaryBoardId = await _context.BoardMembers
            .Where(bm => bm.OrganizationId == orgId && (employee == null || bm.UserId == employee.Id))
            .OrderByDescending(bm => bm.IsStarred)
            .Select(bm => bm.BoardId)
            .FirstOrDefaultAsync();

        if (primaryBoardId == Guid.Empty)
        {
            primaryBoardId = await _context.Boards
                .Where(b => b.OrganizationId == orgId)
                .OrderBy(b => b.CreatedAt)
                .Select(b => b.Id)
                .FirstOrDefaultAsync();
        }

        dto.PrimaryBoardId = primaryBoardId != Guid.Empty ? primaryBoardId : null;

        var todayUtcStart = DateTime.UtcNow.Date;

        // ── 1. Calculate Personal ESS Data for the caller ──
        if (employee != null)
        {
            // Personal attendance today
            var myTodayRecords = await _context.AttendanceRecords
                .Where(a => a.OrganizationId == orgId && a.EmployeeId == employee.Id && a.Timestamp.UtcDateTime >= todayUtcStart)
                .OrderBy(a => a.Timestamp)
                .ToListAsync();

            var latestPunch = myTodayRecords.LastOrDefault();
            dto.PersonalClockedInToday = latestPunch != null && latestPunch.Type == AttendanceType.ClockIn;
            dto.PersonalLastClockInTime = myTodayRecords.FirstOrDefault(r => r.Type == AttendanceType.ClockIn)?.Timestamp;

            double hoursWorked = 0;
            DateTimeOffset? openPunch = null;
            foreach (var r in myTodayRecords)
            {
                if (r.Type == AttendanceType.ClockIn) openPunch = r.Timestamp;
                else if (r.Type == AttendanceType.ClockOut && openPunch.HasValue)
                {
                    hoursWorked += (r.Timestamp - openPunch.Value).TotalHours;
                    openPunch = null;
                }
            }
            if (openPunch.HasValue)
            {
                hoursWorked += Math.Max(0, (DateTimeOffset.UtcNow - openPunch.Value).TotalHours);
            }
            dto.PersonalHoursWorkedToday = Math.Round(hoursWorked, 1);

            // Personal leaves
            dto.PersonalPendingLeavesCount = await _context.LeaveRequests
                .CountAsync(l => l.OrganizationId == orgId && l.EmployeeId == employee.Id && l.Status == LeaveRequestStatus.Pending);

            dto.PersonalApprovedLeavesCount = await _context.LeaveRequests
                .CountAsync(l => l.OrganizationId == orgId && l.EmployeeId == employee.Id && l.Status == LeaveRequestStatus.Approved && l.StartDate.Year == DateTime.UtcNow.Year);

            // Personal tasks assigned to this employee
            var myCards = await _context.BoardCards
                .Include(c => c.List)
                .Include(c => c.Assignees)
                .Where(c => c.OrganizationId == orgId && (c.AssignedToUserId == employee.Id || c.Assignees.Any(a => a.EmployeeId == employee.Id)))
                .OrderByDescending(c => c.DueDate.HasValue)
                .ThenBy(c => c.DueDate)
                .Take(5)
                .ToListAsync();

            dto.PersonalAssignedTasksCount = await _context.BoardCards
                .CountAsync(c => c.OrganizationId == orgId && (c.AssignedToUserId == employee.Id || c.Assignees.Any(a => a.EmployeeId == employee.Id)));

            dto.PersonalTasks = myCards.Select(c => new PersonalTaskItemDto
            {
                CardId = c.Id,
                BoardId = c.List?.BoardId ?? Guid.Empty,
                Title = c.Title,
                ListName = c.List?.Name ?? "To Do",
                DueDate = c.DueDate
            }).ToList();

            // Personal active advance
            var latestAdvance = await _context.AdvanceRequests
                .Where(a => a.OrganizationId == orgId && a.EmployeeId == employee.Id)
                .OrderByDescending(a => a.RequestDate)
                .FirstOrDefaultAsync();

            if (latestAdvance != null)
            {
                dto.PersonalActiveAdvanceAmount = latestAdvance.RequestedAmount;
                dto.PersonalActiveAdvanceStatus = latestAdvance.Status.ToString();
            }
        }

        // ── 2. Calculate Workspace Management Data (if user is manager/admin) ──
        if (isManager)
        {
            // Total team members
            dto.TotalEmployees = await _context.Employees
                .CountAsync(e => e.OrganizationId == orgId && e.IsActive && e.IsEmployee);

            // Team attendance today
            dto.PresentTodayCount = await _context.AttendanceRecords
                .Where(a => a.OrganizationId == orgId && a.Type == AttendanceType.ClockIn && a.Timestamp >= todayUtcStart)
                .Select(a => a.EmployeeId)
                .Distinct()
                .CountAsync();

            dto.AttendanceRatePercent = dto.TotalEmployees > 0 
                ? Math.Round(((double)dto.PresentTodayCount.Value / dto.TotalEmployees.Value) * 100, 1) 
                : 0;

            // Pending approvals
            dto.PendingLeavesCount = await _context.LeaveRequests
                .CountAsync(l => l.OrganizationId == orgId && l.Status == LeaveRequestStatus.Pending);

            dto.PendingAdvancesCount = await _context.AdvanceRequests
                .CountAsync(a => a.OrganizationId == orgId && a.Status == AdvanceStatus.Pending);

            // Board tasks
            var allCards = await _context.BoardCards
                .Include(c => c.List)
                .Where(c => c.OrganizationId == orgId)
                .ToListAsync();

            dto.TotalTasksCount = allCards.Count;
            dto.CompletedTasksCount = allCards.Count(c => c.List != null && 
                (c.List.Name.Contains("Done", StringComparison.OrdinalIgnoreCase) || 
                 c.List.Name.Contains("Completed", StringComparison.OrdinalIgnoreCase)));
            dto.InProgressTasksCount = allCards.Count(c => c.List != null && 
                c.List.Name.Contains("Progress", StringComparison.OrdinalIgnoreCase));

            // Outside field staff
            var cutoff = DateTime.UtcNow.AddHours(-12);
            dto.ActiveOutsideStaffCount = await _context.EmployeeLocationPings
                .Where(p => p.OrganizationId == orgId && p.Timestamp >= cutoff)
                .Select(p => p.EmployeeId)
                .Distinct()
                .CountAsync();

            // Recent activity feed
            var activities = new List<DashboardActivityItemDto>();

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
                    Title = $"{name} clocked {(att.Type == AttendanceType.ClockIn ? "IN" : "OUT")}",
                    Description = $"Via {att.Source} at {att.Timestamp.ToLocalTime():h:mm tt}",
                    Category = "Attendance",
                    Icon = "⏱️",
                    BadgeColor = att.Type == AttendanceType.ClockIn ? "emerald" : "zinc",
                    Timestamp = att.Timestamp.UtcDateTime
                });
            }

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

            dto.RecentActivities = activities
                .OrderByDescending(a => a.Timestamp)
                .Take(6)
                .ToList();
        }
        else
        {
            // For standard employees, show their personal activity
            var myActivities = new List<DashboardActivityItemDto>();

            if (employee != null)
            {
                var myAtt = await _context.AttendanceRecords
                    .Where(a => a.OrganizationId == orgId && a.EmployeeId == employee.Id)
                    .OrderByDescending(a => a.Timestamp)
                    .Take(3)
                    .ToListAsync();

                foreach (var a in myAtt)
                {
                    myActivities.Add(new DashboardActivityItemDto
                    {
                        Id = a.Id.ToString(),
                        Title = $"You clocked {(a.Type == AttendanceType.ClockIn ? "IN" : "OUT")}",
                        Description = $"At {a.Timestamp.ToLocalTime():h:mm tt} via {a.Source}",
                        Category = "Attendance",
                        Icon = "⏱️",
                        BadgeColor = a.Type == AttendanceType.ClockIn ? "emerald" : "zinc",
                        Timestamp = a.Timestamp.UtcDateTime
                    });
                }
            }

            dto.RecentActivities = myActivities;
        }

        return Ok(dto);
    }
}

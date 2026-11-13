using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Models;
using OfficeCore.Api.Services;
using OfficeCore.Shared.Authorization;

namespace OfficeCore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // Requires authentication
public class WorkScheduleController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly TenantService _tenant;

    public WorkScheduleController(AppDbContext context, TenantService tenant)
    {
        _context = context;
        _tenant = tenant;
    }

    private async Task<bool> CanViewWorkSchedulesAsync(Guid orgId)
    {
        if (await _tenant.IsCurrentUserWorkspaceOwnerAsync(orgId))
            return true;

        return User.HasClaim(PermissionCatalog.ClaimType, AccessPermissions.WorkSchedulesView)
            || User.HasClaim(PermissionCatalog.ClaimType, AccessPermissions.WorkSchedulesManage)
            || User.HasClaim(PermissionCatalog.ClaimType, AccessPermissions.UsersView)
            || User.HasClaim(PermissionCatalog.ClaimType, AccessPermissions.UsersManage)
            || User.HasClaim("ModuleAccess", AccessModules.WorkSchedules)
            || User.HasClaim("ModuleAccess", AccessModules.Users);
    }

    [HttpGet]
    public async Task<IActionResult> GetWorkSchedules()
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null || !await CanViewWorkSchedulesAsync(orgId.Value)) return Forbid();

        var schedules = await _context.WorkSchedules
            .Where(ws => ws.OrganizationId == orgId)
            .Include(ws => ws.Days)
            .Include(ws => ws.Breaks)
            .ToListAsync();
            
        return Ok(schedules);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetWorkSchedule(Guid id)
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null || !await CanViewWorkSchedulesAsync(orgId.Value)) return Forbid();

        var schedule = await _context.WorkSchedules
            .Where(ws => ws.OrganizationId == orgId)
            .Include(ws => ws.Days)
            .Include(ws => ws.Breaks)
            .FirstOrDefaultAsync(ws => ws.Id == id);

        if (schedule == null)
            return NotFound();

        return Ok(schedule);
    }

    [HttpPost]
    [Authorize(Policy = "RequireWorkSchedules")]
    [Authorize(Policy = "Permission:work-schedules.manage")]
    public async Task<IActionResult> CreateWorkSchedule(WorkScheduleDto dto)
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Forbid();

        var schedule = new WorkSchedule
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId.Value,
            Name = dto.Name,
            WorkArrangement = (WorkArrangement)dto.WorkArrangement,
            IncludeTimeTrackedBeforeScheduledStartTime = dto.IncludeTimeTrackedBeforeScheduledStartTime,
            AutoClockOut = dto.AutoClockOut,
            SplitTimesheetsAt = dto.SplitTimesheetsAt,
            TotalWeeklyHours = dto.TotalWeeklyHours,
            IsFixedWeeklyRate = dto.IsFixedWeeklyRate,
            LateGracePeriodMinutes = dto.LateGracePeriodMinutes,
            OvertimeThresholdMinutes = dto.OvertimeThresholdMinutes > 0 ? dto.OvertimeThresholdMinutes : 15,
            AutoNormalizeHalfDay = dto.AutoNormalizeHalfDay,
            AutoSubmitOvertimeApproval = dto.AutoSubmitOvertimeApproval,
            HasOvertimePolicy = dto.HasOvertimePolicy,
            OvertimeMultiplier = dto.OvertimeMultiplier
        };
        
        if (dto.Days != null)
        {
            foreach (var day in dto.Days)
            {
                schedule.Days.Add(new WorkScheduleDay
                {
                    WorkScheduleId = schedule.Id,
                    OrganizationId = orgId.Value,
                    DayOfWeek = day.DayOfWeek,
                    IsRestDay = day.IsRestDay,
                    StartTime = day.StartTime,
                    EndTime = day.EndTime,
                    WorkingHours = day.WorkingHours
                });
            }
        }
        
        if (dto.Breaks != null)
        {
            foreach (var b in dto.Breaks)
            {
                schedule.Breaks.Add(new WorkScheduleBreak
                {
                    WorkScheduleId = schedule.Id,
                    OrganizationId = orgId.Value,
                    Name = b.Name,
                    IsPaid = b.IsPaid,
                    StartTime = b.StartTime,
                    EndTime = b.EndTime
                });
            }
        }
        


        try
        {
            _context.WorkSchedules.Add(schedule);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            return BadRequest(new { Error = ex.InnerException?.Message ?? ex.Message });
        }

        return CreatedAtAction(nameof(GetWorkSchedule), new { id = schedule.Id }, schedule);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "RequireWorkSchedules")]
    [Authorize(Policy = "Permission:work-schedules.manage")]
    public async Task<IActionResult> UpdateWorkSchedule(Guid id, WorkScheduleDto dto)
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Forbid();

        if (id != dto.Id)
            return BadRequest();

        var existingSchedule = await _context.WorkSchedules
            .Where(ws => ws.OrganizationId == orgId)
            .Include(ws => ws.Days)
            .Include(ws => ws.Breaks)
            .FirstOrDefaultAsync(ws => ws.Id == id);

        if (existingSchedule == null)
            return NotFound();

        existingSchedule.Name = dto.Name;
        existingSchedule.WorkArrangement = (WorkArrangement)dto.WorkArrangement;
        existingSchedule.IncludeTimeTrackedBeforeScheduledStartTime = dto.IncludeTimeTrackedBeforeScheduledStartTime;
        existingSchedule.AutoClockOut = dto.AutoClockOut;
        existingSchedule.SplitTimesheetsAt = dto.SplitTimesheetsAt;
        existingSchedule.TotalWeeklyHours = dto.TotalWeeklyHours;
        existingSchedule.IsFixedWeeklyRate = dto.IsFixedWeeklyRate;
        existingSchedule.LateGracePeriodMinutes = dto.LateGracePeriodMinutes;
        existingSchedule.OvertimeThresholdMinutes = dto.OvertimeThresholdMinutes > 0 ? dto.OvertimeThresholdMinutes : 15;
        existingSchedule.AutoNormalizeHalfDay = dto.AutoNormalizeHalfDay;
        existingSchedule.AutoSubmitOvertimeApproval = dto.AutoSubmitOvertimeApproval;
        existingSchedule.HasOvertimePolicy = dto.HasOvertimePolicy;
        existingSchedule.OvertimeMultiplier = dto.OvertimeMultiplier;

        // Update days
        _context.WorkScheduleDays.RemoveRange(existingSchedule.Days);
        if (dto.Days != null)
        {
            foreach (var day in dto.Days)
            {
                existingSchedule.Days.Add(new WorkScheduleDay
                {
                    WorkScheduleId = id,
                    OrganizationId = orgId.Value,
                    DayOfWeek = day.DayOfWeek,
                    IsRestDay = day.IsRestDay,
                    StartTime = day.StartTime,
                    EndTime = day.EndTime,
                    WorkingHours = day.WorkingHours
                });
            }
        }

        // Update breaks
        _context.WorkScheduleBreaks.RemoveRange(existingSchedule.Breaks);
        if (dto.Breaks != null)
        {
            foreach (var b in dto.Breaks)
            {
                existingSchedule.Breaks.Add(new WorkScheduleBreak
                {
                    WorkScheduleId = id,
                    OrganizationId = orgId.Value,
                    Name = b.Name,
                    IsPaid = b.IsPaid,
                    StartTime = b.StartTime,
                    EndTime = b.EndTime
                });
            }
        }
        


        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "RequireWorkSchedules")]
    [Authorize(Policy = "Permission:work-schedules.manage")]
    public async Task<IActionResult> DeleteWorkSchedule(Guid id)
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        var schedule = await _context.WorkSchedules.FirstOrDefaultAsync(ws => ws.Id == id && ws.OrganizationId == orgId);
        if (schedule == null)
            return NotFound();

        _context.WorkSchedules.Remove(schedule);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}

public class WorkScheduleDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int WorkArrangement { get; set; }
    public bool IncludeTimeTrackedBeforeScheduledStartTime { get; set; }
    public bool AutoClockOut { get; set; }
    public TimeSpan SplitTimesheetsAt { get; set; }
    public double? TotalWeeklyHours { get; set; }
    public bool IsFixedWeeklyRate { get; set; }
    public int LateGracePeriodMinutes { get; set; } = 0;
    public int OvertimeThresholdMinutes { get; set; } = 15;
    public bool AutoNormalizeHalfDay { get; set; } = true;
    public bool AutoSubmitOvertimeApproval { get; set; } = true;
    public List<WorkScheduleDayDto> Days { get; set; } = new();
    public List<WorkScheduleBreakDto> Breaks { get; set; } = new();
    public bool HasOvertimePolicy { get; set; }
    public double OvertimeMultiplier { get; set; } = 1.25;
}

public class WorkScheduleDayDto
{
    public DayOfWeek DayOfWeek { get; set; }
    public bool IsRestDay { get; set; }
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
    public double? WorkingHours { get; set; }
}

public class WorkScheduleBreakDto
{
    public string Name { get; set; } = string.Empty;
    public bool IsPaid { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
}



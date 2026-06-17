using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Models;

namespace OfficeCore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // Requires authentication
public class WorkScheduleController : ControllerBase
{
    private readonly AppDbContext _context;

    public WorkScheduleController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetWorkSchedules()
    {
        var schedules = await _context.WorkSchedules
            .Include(ws => ws.Days)
            .Include(ws => ws.Breaks)
            .Include(ws => ws.AutoDeductions)
            .Include(ws => ws.OvertimeRules)
            .ToListAsync();
            
        return Ok(schedules);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetWorkSchedule(Guid id)
    {
        var schedule = await _context.WorkSchedules
            .Include(ws => ws.Days)
            .Include(ws => ws.Breaks)
            .Include(ws => ws.AutoDeductions)
            .Include(ws => ws.OvertimeRules)
            .FirstOrDefaultAsync(ws => ws.Id == id);

        if (schedule == null)
            return NotFound();

        return Ok(schedule);
    }

    [HttpPost]
    [Authorize(Policy = "RequireWorkSchedules")]
    public async Task<IActionResult> CreateWorkSchedule(WorkSchedule schedule)
    {
        // WorkScheduleDay will be passed along with WorkSchedule
        schedule.Id = Guid.NewGuid();
        
        if (schedule.Days != null)
        {
            foreach (var day in schedule.Days)
            {
                day.WorkScheduleId = schedule.Id;
                day.Id = 0; // Let DB generate ID
            }
        }
        
        if (schedule.Breaks != null)
        {
            foreach (var b in schedule.Breaks)
            {
                b.WorkScheduleId = schedule.Id;
                b.Id = 0; // Let DB generate ID
            }
        }
        
        if (schedule.AutoDeductions != null)
        {
            foreach (var ad in schedule.AutoDeductions)
            {
                ad.WorkScheduleId = schedule.Id;
                ad.Id = 0;
            }
        }
        
        if (schedule.OvertimeRules != null)
        {
            foreach (var or in schedule.OvertimeRules)
            {
                or.WorkScheduleId = schedule.Id;
                or.Id = 0;
            }
        }

        _context.WorkSchedules.Add(schedule);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetWorkSchedule), new { id = schedule.Id }, schedule);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "RequireWorkSchedules")]
    public async Task<IActionResult> UpdateWorkSchedule(Guid id, WorkSchedule updatedSchedule)
    {
        if (id != updatedSchedule.Id)
            return BadRequest();

        var existingSchedule = await _context.WorkSchedules
            .Include(ws => ws.Days)
            .Include(ws => ws.Breaks)
            .Include(ws => ws.AutoDeductions)
            .Include(ws => ws.OvertimeRules)
            .FirstOrDefaultAsync(ws => ws.Id == id);

        if (existingSchedule == null)
            return NotFound();

        existingSchedule.Name = updatedSchedule.Name;
        existingSchedule.WorkArrangement = updatedSchedule.WorkArrangement;
        existingSchedule.IncludeTimeTrackedBeforeScheduledStartTime = updatedSchedule.IncludeTimeTrackedBeforeScheduledStartTime;
        existingSchedule.AutoClockOut = updatedSchedule.AutoClockOut;
        existingSchedule.SplitTimesheetsAt = updatedSchedule.SplitTimesheetsAt;
        existingSchedule.TotalWeeklyHours = updatedSchedule.TotalWeeklyHours;

        // Update days: remove old ones and add new ones (simple approach)
        _context.WorkScheduleDays.RemoveRange(existingSchedule.Days);
        
        if (updatedSchedule.Days != null)
        {
            foreach (var day in updatedSchedule.Days)
            {
                day.Id = 0;
                day.WorkScheduleId = id;
                existingSchedule.Days.Add(day);
            }
        }

        // Update breaks: remove old ones and add new ones
        _context.WorkScheduleBreaks.RemoveRange(existingSchedule.Breaks);
        
        if (updatedSchedule.Breaks != null)
        {
            foreach (var b in updatedSchedule.Breaks)
            {
                b.Id = 0;
                b.WorkScheduleId = id;
                existingSchedule.Breaks.Add(b);
            }
        }
        
        // Update auto deductions
        _context.WorkScheduleAutoDeductions.RemoveRange(existingSchedule.AutoDeductions);
        if (updatedSchedule.AutoDeductions != null)
        {
            foreach (var ad in updatedSchedule.AutoDeductions)
            {
                ad.Id = 0;
                ad.WorkScheduleId = id;
                existingSchedule.AutoDeductions.Add(ad);
            }
        }
        
        // Update overtime rules
        _context.WorkScheduleOvertimeRules.RemoveRange(existingSchedule.OvertimeRules);
        if (updatedSchedule.OvertimeRules != null)
        {
            foreach (var or in updatedSchedule.OvertimeRules)
            {
                or.Id = 0;
                or.WorkScheduleId = id;
                existingSchedule.OvertimeRules.Add(or);
            }
        }

        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "RequireWorkSchedules")]
    public async Task<IActionResult> DeleteWorkSchedule(Guid id)
    {
        var schedule = await _context.WorkSchedules.FindAsync(id);
        if (schedule == null)
            return NotFound();

        _context.WorkSchedules.Remove(schedule);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}

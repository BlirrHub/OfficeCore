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
            .ToListAsync();
            
        return Ok(schedules);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetWorkSchedule(Guid id)
    {
        var schedule = await _context.WorkSchedules
            .Include(ws => ws.Days)
            .FirstOrDefaultAsync(ws => ws.Id == id);

        if (schedule == null)
            return NotFound();

        return Ok(schedule);
    }

    [HttpPost]
    [Authorize(Policy = "RequireAdmin")] // Adjust this if you have a specific policy for creating schedules
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

        _context.WorkSchedules.Add(schedule);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetWorkSchedule), new { id = schedule.Id }, schedule);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> UpdateWorkSchedule(Guid id, WorkSchedule updatedSchedule)
    {
        if (id != updatedSchedule.Id)
            return BadRequest();

        var existingSchedule = await _context.WorkSchedules
            .Include(ws => ws.Days)
            .FirstOrDefaultAsync(ws => ws.Id == id);

        if (existingSchedule == null)
            return NotFound();

        existingSchedule.Name = updatedSchedule.Name;
        existingSchedule.WorkArrangement = updatedSchedule.WorkArrangement;
        existingSchedule.IncludeTimeTrackedBeforeScheduledStartTime = updatedSchedule.IncludeTimeTrackedBeforeScheduledStartTime;
        existingSchedule.SplitTimesheetsAt = updatedSchedule.SplitTimesheetsAt;

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

        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "RequireAdmin")]
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

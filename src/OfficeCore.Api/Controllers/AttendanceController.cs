using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Models;
using OfficeCore.Api.Services;

namespace OfficeCore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "RequireAttendance")]
public class AttendanceController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly FingerprintDeviceService _deviceService;

    public AttendanceController(AppDbContext context, FingerprintDeviceService deviceService)
    {
        _context = context;
        _deviceService = deviceService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAttendanceRecords()
    {
        var records = await _context.AttendanceRecords
            .Include(a => a.Employee)
                .ThenInclude(e => e!.WorkSchedule)
                    .ThenInclude(ws => ws!.Days)
            .OrderByDescending(a => a.Timestamp)
            .Take(100)
            .ToListAsync();

        var dtos = records.Select(a => {
            // Find schedule for this day
            var localTime = a.Timestamp.ToLocalTime();
            var dayOfWeek = localTime.DayOfWeek;
            
            bool includeTimeTracked = false;
            TimeSpan? scheduledStartTime = null;

            if (a.Employee?.WorkSchedule != null)
            {
                includeTimeTracked = a.Employee.WorkSchedule.IncludeTimeTrackedBeforeScheduledStartTime;
                var scheduleDay = a.Employee.WorkSchedule.Days?.FirstOrDefault(d => d.DayOfWeek == dayOfWeek && !d.IsRestDay);
                if (scheduleDay != null)
                {
                    scheduledStartTime = scheduleDay.StartTime;
                }
            }

            return new
            {
                a.Id,
                EmployeeName = a.Employee != null ? $"{a.Employee.FirstName} {a.Employee.LastName}" : "Unknown",
                a.EmployeeId,
                a.Timestamp,
                Type = a.Type.ToString(),
                Source = a.Source.ToString(),
                IncludeTimeTrackedBeforeScheduledStartTime = includeTimeTracked,
                ScheduledStartTime = scheduledStartTime
            };
        }).ToList();

        return Ok(dtos);
    }

    [HttpPost("enroll/{employeeId}")]
    public IActionResult StartEnrollment(string employeeId)
    {
        var started = _deviceService.StartEnrollment(employeeId);
        if (started)
        {
            return Ok(new { message = "Device is in enrollment mode. Please scan finger on the sensor." });
        }
        
        return BadRequest(new { message = "Could not start enrollment. Make sure the device is connected and not currently enrolling another user." });
    }

    [HttpGet("fingerprints/{employeeId}")]
    public async Task<IActionResult> GetEmployeeFingerprints(string employeeId)
    {
        var prints = await _context.EmployeeFingerprints
            .Where(f => f.EmployeeId == employeeId)
            .Select(f => new { f.Id, f.FingerprintId, f.EnrolledAt })
            .ToListAsync();

        return Ok(prints);
    }

    [HttpDelete("fingerprints/{id}")]
    public async Task<IActionResult> DeleteFingerprint(int id)
    {
        var fp = await _context.EmployeeFingerprints.FindAsync(id);
        if (fp == null) return NotFound();

        _context.EmployeeFingerprints.Remove(fp);
        await _context.SaveChangesAsync();

        // Optional: We could also send a DELETE command to the ESP32 here to free up the slot in the sensor memory!
        
        return Ok(new { message = "Fingerprint mapping deleted." });
    }
}

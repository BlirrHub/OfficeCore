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
                EmployeeName = a.Employee != null ? $"{a.Employee.User.FirstName} {a.Employee.User.LastName}" : "Unknown",
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

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAttendanceRecord(Guid id)
    {
        var record = await _context.AttendanceRecords.FindAsync(id);
        if (record == null) return NotFound();

        _context.AttendanceRecords.Remove(record);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Attendance record deleted." });
    }

    [HttpPost("admin-create")]
    public async Task<IActionResult> AdminCreateAttendanceRecord([FromBody] AdminCreateAttendanceDto dto)
    {
        var employee = await _context.Users.FirstOrDefaultAsync(u => u.Id == dto.EmployeeId);
        if (employee == null) return BadRequest("Employee not found");

        var record = new AttendanceRecord
        {
            EmployeeId = dto.EmployeeId,
            Timestamp = new DateTimeOffset(DateTime.SpecifyKind(dto.Timestamp, DateTimeKind.Local)).ToUniversalTime(),
            Type = dto.Type,
            Source = AttendanceSource.ManualAdjustment
        };

        _context.AttendanceRecords.Add(record);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Attendance record created." });
    }
    [HttpPost("scan-event")]
    public async Task<IActionResult> LogScanEvent([FromBody] ScanEventDto dto)
    {
        var mapping = await _context.EmployeeFingerprints
            .FirstOrDefaultAsync(f => f.FingerprintId == dto.FingerprintId);

        if (mapping == null) return NotFound(new { message = "Unknown fingerprint." });

        var today = DateTimeOffset.UtcNow.Date;
        var latestRecord = await _context.AttendanceRecords
            .Where(a => a.EmployeeId == mapping.EmployeeId && a.Timestamp.Date == today)
            .OrderByDescending(a => a.Timestamp)
            .FirstOrDefaultAsync();

        var type = AttendanceType.ClockIn;
        if (latestRecord != null && latestRecord.Type == AttendanceType.ClockIn)
        {
            type = AttendanceType.ClockOut;
        }

        var record = new AttendanceRecord
        {
            EmployeeId = mapping.EmployeeId,
            Timestamp = DateTimeOffset.UtcNow,
            Type = type,
            Source = AttendanceSource.Device
        };

        _context.AttendanceRecords.Add(record);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Attendance logged." });
    }

    [HttpPost("enroll-success")]
    public async Task<IActionResult> EnrollSuccess([FromBody] EnrollSuccessDto dto)
    {
        var employee = await _context.Employees.FindAsync(dto.EmployeeId);
        if (employee == null) return BadRequest("Employee not found");

        _context.EmployeeFingerprints.Add(new EmployeeFingerprint
        {
            EmployeeId = dto.EmployeeId,
            FingerprintId = dto.FingerprintId,
            OrganizationId = employee.OrganizationId
        });

        await _context.SaveChangesAsync();
        return Ok(new { message = "Fingerprint enrolled successfully." });
    }
}

public class ScanEventDto
{
    public int FingerprintId { get; set; }
}

public class EnrollSuccessDto
{
    public string EmployeeId { get; set; } = string.Empty;
    public int FingerprintId { get; set; }
}

public class AdminCreateAttendanceDto
{
    public required string EmployeeId { get; set; }
    public DateTime Timestamp { get; set; }
    public AttendanceType Type { get; set; }
}

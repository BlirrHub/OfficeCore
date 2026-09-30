using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Models;
using OfficeCore.Api.Services;
using Microsoft.AspNetCore.SignalR;

namespace OfficeCore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AttendanceController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly FingerprintDeviceService _deviceService;
    private readonly IHubContext<OfficeCore.Api.Hubs.AttendanceHub> _hubContext;
    private readonly TenantService _tenant;
    private readonly IAuditService _audit;
    private readonly BiometricReportService _biometricReportService;

    public AttendanceController(
        AppDbContext context, 
        FingerprintDeviceService deviceService, 
        IHubContext<OfficeCore.Api.Hubs.AttendanceHub> hubContext,
        TenantService tenant,
        IAuditService audit,
        BiometricReportService biometricReportService)
    {
        _context = context;
        _deviceService = deviceService;
        _hubContext = hubContext;
        _tenant = tenant;
        _audit = audit;
        _biometricReportService = biometricReportService;
    }

    // =========================================================================
    // EMPLOYEE SELF-SERVICE (ESS) ENDPOINTS
    // =========================================================================

    [HttpGet("my")]
    public async Task<IActionResult> GetMyAttendance()
    {
        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Unauthorized("No active workspace selected.");

        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (employee == null) return Unauthorized("Employee profile not found in active workspace.");

        var todayUtc = DateTime.UtcNow.Date;
        var tomorrowUtc = todayUtc.AddDays(1);

        // Fetch employee with work schedule
        var empWithSchedule = await _context.Employees
            .Include(e => e.WorkSchedule)
                .ThenInclude(ws => ws!.Days)
            .FirstOrDefaultAsync(e => e.Id == employee.Id && e.OrganizationId == orgId.Value);

        // Fetch personal attendance records
        var records = await _context.AttendanceRecords
            .Where(a => a.OrganizationId == orgId.Value && a.EmployeeId == employee.Id)
            .OrderByDescending(a => a.Timestamp)
            .Take(200)
            .ToListAsync();

        // Calculate today's status
        var todayRecords = records
            .Where(a => a.Timestamp.UtcDateTime >= todayUtc && a.Timestamp.UtcDateTime < tomorrowUtc)
            .OrderBy(a => a.Timestamp)
            .ToList();

        var latestTodayRecord = todayRecords.LastOrDefault();
        bool isClockedIn = latestTodayRecord != null && latestTodayRecord.Type == AttendanceType.ClockIn;
        DateTimeOffset? lastPunchTime = latestTodayRecord?.Timestamp;

        // Calculate hours worked today by pairing ClockIn and ClockOut
        double todayTotalHours = 0;
        DateTimeOffset? currentClockIn = null;
        foreach (var r in todayRecords)
        {
            if (r.Type == AttendanceType.ClockIn)
            {
                currentClockIn = r.Timestamp;
            }
            else if (r.Type == AttendanceType.ClockOut && currentClockIn.HasValue)
            {
                todayTotalHours += (r.Timestamp - currentClockIn.Value).TotalHours;
                currentClockIn = null;
            }
        }
        if (currentClockIn.HasValue)
        {
            todayTotalHours += Math.Max(0, (DateTimeOffset.UtcNow - currentClockIn.Value).TotalHours);
        }

        // Scheduled shift for today
        var localNow = DateTime.Now;
        var dayOfWeek = localNow.DayOfWeek;
        string scheduledShiftText = "Flexible Schedule";
        TimeSpan? scheduledStartTime = null;
        TimeSpan? scheduledEndTime = null;

        if (empWithSchedule?.WorkSchedule?.Days != null)
        {
            var todaySched = empWithSchedule.WorkSchedule.Days.FirstOrDefault(d => d.DayOfWeek == dayOfWeek);
            if (todaySched != null)
            {
                if (todaySched.IsRestDay)
                {
                    scheduledShiftText = "Rest Day";
                }
                else
                {
                    scheduledStartTime = todaySched.StartTime;
                    scheduledEndTime = todaySched.EndTime;
                    if (todaySched.StartTime.HasValue && todaySched.EndTime.HasValue)
                    {
                        scheduledShiftText = $"{DateTime.Today.Add(todaySched.StartTime.Value):hh:mm tt} - {DateTime.Today.Add(todaySched.EndTime.Value):hh:mm tt}";
                    }
                    else
                    {
                        scheduledShiftText = "Flexible Schedule";
                    }
                }
            }
        }

        // Month-to-date stats
        var startOfMonth = new DateTime(localNow.Year, localNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthRecords = records
            .Where(a => a.Timestamp.UtcDateTime >= startOfMonth)
            .OrderBy(a => a.Timestamp)
            .ToList();

        int daysPresent = monthRecords.Select(r => r.Timestamp.ToLocalTime().Date).Distinct().Count();

        double monthTotalHours = 0;
        DateTimeOffset? monthClockIn = null;
        foreach (var r in monthRecords)
        {
            if (r.Type == AttendanceType.ClockIn)
            {
                monthClockIn = r.Timestamp;
            }
            else if (r.Type == AttendanceType.ClockOut && monthClockIn.HasValue)
            {
                monthTotalHours += (r.Timestamp - monthClockIn.Value).TotalHours;
                monthClockIn = null;
            }
        }

        var recordDtos = records.Select(a => new
        {
            a.Id,
            a.Timestamp,
            Type = a.Type.ToString(),
            Source = a.Source.ToString()
        }).ToList();

        return Ok(new
        {
            IsClockedIn = isClockedIn,
            LastPunchTime = lastPunchTime,
            TodayTotalHours = Math.Round(todayTotalHours, 2),
            ScheduledShiftText = scheduledShiftText,
            ScheduledStartTime = scheduledStartTime,
            ScheduledEndTime = scheduledEndTime,
            DaysPresent = daysPresent,
            MonthTotalHours = Math.Round(monthTotalHours, 2),
            Records = recordDtos
        });
    }

    [HttpPost("punch")]
    public async Task<IActionResult> Punch()
    {
        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Unauthorized("No active workspace selected.");

        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (employee == null) return Unauthorized("Employee profile not found in active workspace.");

        var todayUtc = DateTime.UtcNow.Date;
        var tomorrowUtc = todayUtc.AddDays(1);

        var latestRecord = await _context.AttendanceRecords
            .Where(a => a.OrganizationId == orgId.Value
                     && a.EmployeeId == employee.Id
                     && a.Timestamp >= todayUtc
                     && a.Timestamp < tomorrowUtc)
            .OrderByDescending(a => a.Timestamp)
            .FirstOrDefaultAsync();

        var type = AttendanceType.ClockIn;
        if (latestRecord != null && latestRecord.Type == AttendanceType.ClockIn)
        {
            type = AttendanceType.ClockOut;
        }

        var record = new AttendanceRecord
        {
            Id = Guid.NewGuid(),
            EmployeeId = employee.Id,
            Timestamp = DateTimeOffset.UtcNow,
            Type = type,
            Source = AttendanceSource.Web,
            OrganizationId = orgId.Value
        };

        _context.AttendanceRecords.Add(record);
        await _context.SaveChangesAsync();

        await _hubContext.Clients.Group($"org_{orgId.Value}").SendAsync("ReceiveAttendanceUpdate");

        var actionText = type == AttendanceType.ClockIn ? "Clocked In" : "Clocked Out";
        return Ok(new
        {
            message = $"Successfully {actionText} at {record.Timestamp.ToLocalTime():hh:mm tt}.",
            type = type.ToString(),
            timestamp = record.Timestamp
        });
    }

    // =========================================================================
    // MANAGEMENT SELF-SERVICE (MSS) ENDPOINTS — Policy: RequireAttendance
    // =========================================================================

    [HttpGet]
    [Authorize(Policy = "RequireAttendance")]
    public async Task<IActionResult> GetAttendanceRecords([FromQuery] string? employeeId = null, [FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null)
    {
        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Unauthorized("No active workspace selected.");

        var query = _context.AttendanceRecords
            .Include(a => a.Employee)
                .ThenInclude(e => e!.User)
            .Include(a => a.Employee)
                .ThenInclude(e => e!.WorkSchedule)
                    .ThenInclude(ws => ws!.Days)
            .Where(a => a.OrganizationId == orgId.Value);

        if (!string.IsNullOrWhiteSpace(employeeId))
        {
            query = query.Where(a => a.EmployeeId == employeeId);
        }

        if (startDate.HasValue)
        {
            var startUtc = DateTime.SpecifyKind(startDate.Value.Date, DateTimeKind.Utc);
            query = query.Where(a => a.Timestamp >= startUtc);
        }

        if (endDate.HasValue)
        {
            var endUtc = DateTime.SpecifyKind(endDate.Value.Date.AddDays(1), DateTimeKind.Utc);
            query = query.Where(a => a.Timestamp < endUtc);
        }

        var records = await query
            .OrderByDescending(a => a.Timestamp)
            .Take(250)
            .ToListAsync();

        var dtos = records.Select(a => {
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
                EmployeeName = a.Employee?.User != null ? $"{a.Employee.User.FirstName} {a.Employee.User.LastName}" : "Unknown",
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

    [HttpGet("export")]
    [Authorize(Policy = "RequireAttendance")]
    public async Task<IActionResult> ExportAttendance([FromQuery] string? employeeId = null, [FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null)
    {
        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Unauthorized("No active workspace selected.");

        var query = _context.AttendanceRecords
            .Include(a => a.Employee)
                .ThenInclude(e => e!.User)
            .Where(a => a.OrganizationId == orgId.Value);

        if (!string.IsNullOrWhiteSpace(employeeId))
        {
            query = query.Where(a => a.EmployeeId == employeeId);
        }

        if (startDate.HasValue)
        {
            var startUtc = DateTime.SpecifyKind(startDate.Value.Date, DateTimeKind.Utc);
            query = query.Where(a => a.Timestamp >= startUtc);
        }

        if (endDate.HasValue)
        {
            var endUtc = DateTime.SpecifyKind(endDate.Value.Date.AddDays(1), DateTimeKind.Utc);
            query = query.Where(a => a.Timestamp < endUtc);
        }

        var records = await query
            .OrderBy(a => a.Timestamp)
            .ToListAsync();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Employee Name,Timestamp,Type,Source");

        foreach (var r in records)
        {
            var empName = r.Employee?.User != null ? $"{r.Employee.User.FirstName} {r.Employee.User.LastName}".Trim() : "Unknown";
            empName = empName.Replace("\"", "\"\"");
            var localTime = r.Timestamp.ToLocalTime().ToString("yyyy-MM-dd hh:mm tt");
            sb.AppendLine($"\"{empName}\",{localTime},{r.Type},{r.Source}");
        }

        var fileName = $"AttendanceExport_{DateTime.Now:yyyyMMddHHmmss}.csv";
        return File(System.Text.Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", fileName);
    }

    [HttpPost("admin-create")]
    [Authorize(Policy = "RequireAttendance")]
    public async Task<IActionResult> AdminCreateAttendanceRecord([FromBody] AdminCreateAttendanceDto dto)
    {
        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Unauthorized("No active workspace selected.");

        var employee = await _context.Employees
            .Include(e => e.User)
            .FirstOrDefaultAsync(e => e.Id == dto.EmployeeId && e.OrganizationId == orgId.Value);
            
        if (employee == null) return BadRequest("Employee not found in active workspace.");

        var record = new AttendanceRecord
        {
            Id = Guid.NewGuid(),
            EmployeeId = dto.EmployeeId,
            Timestamp = new DateTimeOffset(DateTime.SpecifyKind(dto.Timestamp, DateTimeKind.Local)).ToUniversalTime(),
            Type = dto.Type,
            Source = AttendanceSource.ManualAdjustment,
            OrganizationId = orgId.Value
        };

        _context.AttendanceRecords.Add(record);
        await _context.SaveChangesAsync();

        await _hubContext.Clients.Group($"org_{orgId.Value}").SendAsync("ReceiveAttendanceUpdate");

        var empName = $"{employee.User?.FirstName} {employee.User?.LastName}".Trim();
        await _audit.LogAttendanceAsync(
            AuditActions.Create,
            $"Admin adjusted manual attendance punch ({record.Type} at {record.Timestamp:yyyy-MM-dd hh:mm tt}) for employee {empName}",
            record.Id);

        return Ok(new { message = "Attendance record created." });
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "RequireAttendance")]
    public async Task<IActionResult> DeleteAttendanceRecord(Guid id)
    {
        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Unauthorized("No active workspace selected.");

        var record = await _context.AttendanceRecords
            .Include(a => a.Employee)
                .ThenInclude(e => e!.User)
            .FirstOrDefaultAsync(a => a.Id == id && a.OrganizationId == orgId.Value);
        if (record == null) return NotFound("Attendance record not found.");

        var deletedEmpName = $"{record.Employee?.User?.FirstName} {record.Employee?.User?.LastName}".Trim();
        _context.AttendanceRecords.Remove(record);
        await _context.SaveChangesAsync();

        await _hubContext.Clients.Group($"org_{orgId.Value}").SendAsync("ReceiveAttendanceUpdate");

        await _audit.LogAttendanceAsync(
            AuditActions.Delete,
            $"Admin deleted {record.Type} attendance punch dated {record.Timestamp:yyyy-MM-dd hh:mm tt} for employee {deletedEmpName}",
            id);

        return Ok(new { message = "Attendance record deleted." });
    }

    // =========================================================================
    // BIOMETRIC SCANNER & ENROLLMENT ENDPOINTS
    // =========================================================================

    [HttpPost("enroll/{employeeId}")]
    [Authorize(Policy = "RequireAttendance")]
    public IActionResult StartEnrollment(string employeeId)
    {
        var started = _deviceService.StartEnrollment(employeeId);
        if (started)
        {
            return Ok(new { message = "Device is in enrollment mode. Please scan finger on the sensor." });
        }
        
        return BadRequest(new { message = "Could not start enrollment. Make sure the device is connected and not currently enrolling another user." });
    }

    [HttpPost("cancel-enroll")]
    [Authorize(Policy = "RequireAttendance")]
    public IActionResult CancelEnrollment()
    {
        _deviceService.CancelEnrollment();
        return Ok(new { message = "Enrollment cancelled." });
    }

    [HttpGet("fingerprints/{employeeId}")]
    [Authorize(Policy = "RequireAttendance")]
    public async Task<IActionResult> GetEmployeeFingerprints(string employeeId)
    {
        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Unauthorized("No active workspace selected.");

        var prints = await _context.EmployeeFingerprints
            .Where(f => f.EmployeeId == employeeId && f.OrganizationId == orgId.Value)
            .Select(f => new { f.Id, f.FingerprintId, f.EnrolledAt })
            .ToListAsync();

        return Ok(prints);
    }

    [HttpDelete("fingerprints/{id}")]
    [Authorize(Policy = "RequireAttendance")]
    public async Task<IActionResult> DeleteFingerprint(int id)
    {
        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Unauthorized("No active workspace selected.");

        var fp = await _context.EmployeeFingerprints
            .FirstOrDefaultAsync(f => f.Id == id && f.OrganizationId == orgId.Value);
        if (fp == null) return NotFound();

        _context.EmployeeFingerprints.Remove(fp);
        await _context.SaveChangesAsync();
        
        return Ok(new { message = "Fingerprint mapping deleted." });
    }

    [HttpPost("enroll-success")]
    [Authorize(Policy = "RequireAttendance")]
    public async Task<IActionResult> EnrollSuccess([FromBody] EnrollSuccessDto dto)
    {
        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Unauthorized("No active workspace selected.");

        var employee = await _context.Employees
            .FirstOrDefaultAsync(e => e.Id == dto.EmployeeId && e.OrganizationId == orgId.Value);
        if (employee == null) return BadRequest("Employee not found");

        _context.EmployeeFingerprints.Add(new EmployeeFingerprint
        {
            EmployeeId = dto.EmployeeId,
            FingerprintId = dto.FingerprintId,
            OrganizationId = orgId.Value
        });

        await _context.SaveChangesAsync();
        return Ok(new { message = "Fingerprint enrolled successfully." });
    }

    // Hardware Scanner scan callback endpoint
    [HttpPost("scan-event")]
    [AllowAnonymous] // ESP32 Wi-Fi hardware posts directly to this endpoint
    public async Task<IActionResult> LogScanEvent([FromBody] ScanEventDto dto)
    {
        var mapping = await _context.EmployeeFingerprints
            .FirstOrDefaultAsync(f => f.FingerprintId == dto.FingerprintId);

        if (mapping == null) return NotFound(new { message = "Unknown fingerprint." });

        var todayUtc = DateTime.UtcNow.Date;
        var tomorrowUtc = todayUtc.AddDays(1);

        var latestRecord = await _context.AttendanceRecords
            .Where(a => a.OrganizationId == mapping.OrganizationId
                     && a.EmployeeId == mapping.EmployeeId
                     && a.Timestamp >= todayUtc
                     && a.Timestamp < tomorrowUtc)
            .OrderByDescending(a => a.Timestamp)
            .FirstOrDefaultAsync();

        var type = AttendanceType.ClockIn;
        if (latestRecord != null && latestRecord.Type == AttendanceType.ClockIn)
        {
            type = AttendanceType.ClockOut;
        }

        var record = new AttendanceRecord
        {
            Id = Guid.NewGuid(),
            EmployeeId = mapping.EmployeeId,
            Timestamp = DateTimeOffset.UtcNow,
            Type = type,
            Source = AttendanceSource.Device,
            OrganizationId = mapping.OrganizationId
        };

        _context.AttendanceRecords.Add(record);
        await _context.SaveChangesAsync();

        await _hubContext.Clients.Group($"org_{mapping.OrganizationId}").SendAsync("ReceiveAttendanceUpdate");

        return Ok(new { message = "Attendance logged.", type = type.ToString() });
    }

    // =========================================================================
    // BIOMETRIC EXCEL / FILE REPORT IMPORT ENDPOINTS
    // =========================================================================

    [HttpPost("import-biometric-preview")]
    [Authorize(Policy = "RequireAttendance")]
    [RequestSizeLimit(50_000_000)] // Up to 50MB
    public async Task<IActionResult> PreviewBiometricImport([FromForm] IFormFile file)
    {
        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Unauthorized("No active workspace selected.");

        if (file == null || file.Length == 0)
        {
            return BadRequest("Please select a valid biometric report file (.xls, .xlsx).");
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".xls" && ext != ".xlsx" && ext != ".csv")
        {
            return BadRequest("Unsupported file type. Please upload an Excel biometric report (.xls or .xlsx).");
        }

        using var stream = file.OpenReadStream();
        var preview = await _biometricReportService.PreviewBiometricReportAsync(stream, orgId.Value);

        if (!preview.Success)
        {
            return BadRequest(preview.ErrorMessage ?? "Could not parse biometric report.");
        }

        return Ok(preview);
    }

    [HttpPost("import-biometric-commit")]
    [Authorize(Policy = "RequireAttendance")]
    public async Task<IActionResult> CommitBiometricImport([FromBody] BiometricCommitRequestDto dto)
    {
        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Unauthorized("No active workspace selected.");

        if (dto.Punches == null || dto.Punches.Count == 0)
        {
            return BadRequest("No punches selected for import.");
        }

        var (inserted, duplicates, updatedMappings) = await _biometricReportService.CommitBiometricAttendanceAsync(dto, orgId.Value);

        await _hubContext.Clients.Group($"org_{orgId.Value}").SendAsync("ReceiveAttendanceUpdate");

        await _audit.LogAttendanceAsync(
            AuditActions.Create,
            $"Imported {inserted} biometric attendance punches (skipped {duplicates} duplicates, updated {updatedMappings} employee device ID mappings)",
            Guid.Empty);

        return Ok(new
        {
            message = $"Successfully imported {inserted} attendance punch records.",
            insertedCount = inserted,
            skippedDuplicates = duplicates,
            updatedMappings
        });
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

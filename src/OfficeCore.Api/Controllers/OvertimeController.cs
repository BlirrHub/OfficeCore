using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Hubs;
using OfficeCore.Api.Models;
using OfficeCore.Api.Services;

namespace OfficeCore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OvertimeController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly TenantService _tenant;
    private readonly IHubContext<NotificationHub> _hub;
    private readonly IAuditService _audit;

    public OvertimeController(
        AppDbContext context,
        TenantService tenant,
        IHubContext<NotificationHub> hub,
        IAuditService audit)
    {
        _context = context;
        _tenant = tenant;
        _hub = hub;
        _audit = audit;
    }

    private async Task BroadcastOvertimeUpdated(Guid orgId)
    {
        await _hub.Clients.Group($"org_{orgId}").SendAsync("OvertimeUpdated");
    }

    // =========================================================================
    // EMPLOYEE SELF-SERVICE (ESS)
    // =========================================================================

    [HttpGet("my")]
    public async Task<IActionResult> GetMyOvertime()
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Unauthorized("No active workspace selected.");

        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (employee == null) return Unauthorized("Employee profile not found in active workspace.");

        var requests = await _context.OvertimeRequests
            .Include(r => r.ReviewedByUser)
            .Where(r => r.OrganizationId == orgId.Value && r.EmployeeId == employee.Id)
            .OrderByDescending(r => r.Date)
            .ThenByDescending(r => r.CreatedAt)
            .Take(250)
            .Select(r => new
            {
                r.Id,
                r.EmployeeId,
                r.Date,
                Type = (int)r.Type,
                TypeName = r.Type.ToString(),
                StartTime = r.StartTime.ToString(@"hh\:mm"),
                EndTime = r.EndTime.ToString(@"hh\:mm"),
                r.RequestedHours,
                r.ApprovedHours,
                r.Reason,
                Status = (int)r.Status,
                StatusName = r.Status.ToString(),
                r.ReviewedByUserId,
                ReviewedByName = r.ReviewedByUser != null ? $"{r.ReviewedByUser.FirstName} {r.ReviewedByUser.LastName}".Trim() : null,
                r.ActionTakenAt,
                r.RejectionReason,
                r.CreatedAt
            })
            .ToListAsync();

        return Ok(requests);
    }

    [HttpPost("request")]
    public async Task<IActionResult> CreateRequest([FromBody] CreateOvertimeRequestDto dto)
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Unauthorized("No active workspace selected.");

        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (employee == null) return Unauthorized("Employee profile not found in active workspace.");

        if (dto.EndTime <= dto.StartTime)
        {
            return BadRequest("End time must be after start time.");
        }

        var duration = (dto.EndTime - dto.StartTime).TotalHours;
        if (duration < 0.25) // Less than 15 mins
        {
            return BadRequest("Overtime duration must be at least 15 minutes.");
        }

        var shiftDateUtc = DateTime.SpecifyKind(dto.Date.Date, DateTimeKind.Utc);

        var request = new OvertimeRequest
        {
            Id = Guid.NewGuid(),
            EmployeeId = employee.Id,
            OrganizationId = orgId.Value,
            Date = shiftDateUtc,
            Type = OvertimeType.PreOvertime,
            StartTime = dto.StartTime,
            EndTime = dto.EndTime,
            RequestedHours = Math.Round(duration, 2),
            Reason = dto.Reason ?? string.Empty,
            Status = OvertimeStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _context.OvertimeRequests.Add(request);
        await _context.SaveChangesAsync();

        await BroadcastOvertimeUpdated(orgId.Value);

        await _audit.LogAsync(
            "Create",
            "OvertimeRequest",
            request.Id.ToString(),
            $"Employee submitted Pre-Overtime request for {dto.Date:yyyy-MM-dd} ({duration:0.##} hrs)");

        return Ok(new
        {
            message = "Pre-Overtime request filed successfully and is awaiting review.",
            id = request.Id
        });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> CancelRequest(Guid id)
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Unauthorized("No active workspace selected.");

        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (employee == null) return Unauthorized("Employee profile not found in active workspace.");

        var req = await _context.OvertimeRequests
            .FirstOrDefaultAsync(r => r.Id == id && r.OrganizationId == orgId.Value && r.EmployeeId == employee.Id);

        if (req == null) return NotFound("Overtime request not found.");

        if (req.Status != OvertimeStatus.Pending)
        {
            return BadRequest("Only pending overtime requests can be cancelled.");
        }

        req.Status = OvertimeStatus.Cancelled;
        req.ActionTakenAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        await BroadcastOvertimeUpdated(orgId.Value);

        await _audit.LogAsync(
            "Cancel",
            "OvertimeRequest",
            req.Id.ToString(),
            $"Employee cancelled their pending overtime request for {req.Date:yyyy-MM-dd}");

        return Ok(new { message = "Overtime request cancelled successfully." });
    }

    // =========================================================================
    // MANAGEMENT SELF-SERVICE (MSS) — Approvals
    // =========================================================================

    [HttpGet("admin")]
    [Authorize(Policy = "RequireOvertimeApprovals")]
    [Authorize(Policy = "Permission:overtime-approvals.view")]
    public async Task<IActionResult> GetAdminRequests(
        [FromQuery] string? employeeId = null,
        [FromQuery] int? status = null,
        [FromQuery] int? type = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Unauthorized("No active workspace selected.");

        var query = _context.OvertimeRequests
            .Include(r => r.Employee)
                .ThenInclude(e => e!.User)
            .Include(r => r.ReviewedByUser)
            .Where(r => r.OrganizationId == orgId.Value);

        if (!string.IsNullOrWhiteSpace(employeeId))
        {
            query = query.Where(r => r.EmployeeId == employeeId);
        }

        if (status.HasValue && status.Value >= 0)
        {
            query = query.Where(r => (int)r.Status == status.Value);
        }

        if (type.HasValue && type.Value >= 0)
        {
            query = query.Where(r => (int)r.Type == type.Value);
        }

        if (startDate.HasValue)
        {
            var startUtc = DateTime.SpecifyKind(startDate.Value.Date, DateTimeKind.Utc);
            query = query.Where(r => r.Date >= startUtc);
        }

        if (endDate.HasValue)
        {
            var endUtc = DateTime.SpecifyKind(endDate.Value.Date.AddDays(1), DateTimeKind.Utc);
            query = query.Where(r => r.Date < endUtc);
        }

        var requests = await query
            .OrderByDescending(r => r.Date)
            .ThenByDescending(r => r.CreatedAt)
            .Take(300)
            .ToListAsync();

        var result = requests.Select(r => new
        {
            r.Id,
            r.EmployeeId,
            EmployeeName = r.Employee?.User != null ? $"{r.Employee.User.FirstName} {r.Employee.User.LastName}".Trim() : "Unknown",
            EmployeeNumber = r.Employee?.EmployeeId ?? string.Empty,
            r.Date,
            Type = (int)r.Type,
            TypeName = r.Type == OvertimeType.PreOvertime ? "Pre-Overtime" : "Post-Overtime",
            StartTime = r.StartTime.ToString(@"hh\:mm"),
            EndTime = r.EndTime.ToString(@"hh\:mm"),
            r.RequestedHours,
            r.ApprovedHours,
            r.Reason,
            Status = (int)r.Status,
            StatusName = r.Status.ToString(),
            r.ReviewedByUserId,
            ReviewedByName = r.ReviewedByUser != null ? $"{r.ReviewedByUser.FirstName} {r.ReviewedByUser.LastName}".Trim() : null,
            r.ActionTakenAt,
            r.RejectionReason,
            r.CreatedAt
        }).ToList();

        return Ok(result);
    }

    [HttpPost("admin/{id}/approve")]
    [Authorize(Policy = "RequireOvertimeApprovals")]
    [Authorize(Policy = "Permission:overtime-approvals.manage")]
    public async Task<IActionResult> ApproveRequest(Guid id, [FromBody] ApproveOvertimeDto? dto)
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Unauthorized("No active workspace selected.");

        var req = await _context.OvertimeRequests
            .Include(r => r.Employee)
                .ThenInclude(e => e!.User)
            .FirstOrDefaultAsync(r => r.Id == id && r.OrganizationId == orgId.Value);

        if (req == null) return NotFound("Overtime request not found.");

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var hours = dto?.ApprovedHours ?? req.RequestedHours;
        if (hours <= 0) hours = req.RequestedHours;

        req.Status = OvertimeStatus.Approved;
        req.ApprovedHours = Math.Round(hours, 2);
        req.ReviewedByUserId = currentUserId;
        req.ActionTakenAt = DateTime.UtcNow;
        req.RejectionReason = null;

        await _context.SaveChangesAsync();
        await BroadcastOvertimeUpdated(orgId.Value);

        var empName = req.Employee?.User != null ? $"{req.Employee.User.FirstName} {req.Employee.User.LastName}".Trim() : "Employee";
        await _audit.LogAsync(
            "Approve",
            "OvertimeRequest",
            req.Id.ToString(),
            $"Approved {req.Type} for {empName} on {req.Date:yyyy-MM-dd} with {req.ApprovedHours:0.##} credited hours");

        return Ok(new
        {
            message = $"Overtime approved successfully ({req.ApprovedHours:0.##} hours credited).",
            status = req.Status.ToString(),
            approvedHours = req.ApprovedHours
        });
    }

    [HttpPost("admin/{id}/reject")]
    [Authorize(Policy = "RequireOvertimeApprovals")]
    [Authorize(Policy = "Permission:overtime-approvals.manage")]
    public async Task<IActionResult> RejectRequest(Guid id, [FromBody] RejectOvertimeDto? dto)
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Unauthorized("No active workspace selected.");

        var req = await _context.OvertimeRequests
            .Include(r => r.Employee)
                .ThenInclude(e => e!.User)
            .FirstOrDefaultAsync(r => r.Id == id && r.OrganizationId == orgId.Value);

        if (req == null) return NotFound("Overtime request not found.");

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        req.Status = OvertimeStatus.Rejected;
        req.ApprovedHours = 0;
        req.RejectionReason = dto?.Reason;
        req.ReviewedByUserId = currentUserId;
        req.ActionTakenAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        await BroadcastOvertimeUpdated(orgId.Value);

        var empName = req.Employee?.User != null ? $"{req.Employee.User.FirstName} {req.Employee.User.LastName}".Trim() : "Employee";
        await _audit.LogAsync(
            "Reject",
            "OvertimeRequest",
            req.Id.ToString(),
            $"Rejected {req.Type} for {empName} on {req.Date:yyyy-MM-dd}. Reason: {dto?.Reason ?? "None provided"}. Hours clamped down to scheduled shift end.");

        return Ok(new
        {
            message = "Overtime request rejected. Time will automatically clamp to scheduled shift end.",
            status = req.Status.ToString()
        });
    }

    [HttpPost("admin/create")]
    [Authorize(Policy = "RequireOvertimeApprovals")]
    [Authorize(Policy = "Permission:overtime-approvals.manage")]
    public async Task<IActionResult> AdminCreateOvertime([FromBody] AdminCreateOvertimeDto dto)
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Unauthorized("No active workspace selected.");

        var employee = await _context.Employees
            .Include(e => e.User)
            .FirstOrDefaultAsync(e => e.Id == dto.EmployeeId && e.OrganizationId == orgId.Value);

        if (employee == null) return BadRequest("Employee not found in active workspace.");

        if (dto.EndTime <= dto.StartTime)
        {
            return BadRequest("End time must be after start time.");
        }

        var duration = (dto.EndTime - dto.StartTime).TotalHours;
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var shiftDateUtc = DateTime.SpecifyKind(dto.Date.Date, DateTimeKind.Utc);

        var request = new OvertimeRequest
        {
            Id = Guid.NewGuid(),
            EmployeeId = dto.EmployeeId,
            OrganizationId = orgId.Value,
            Date = shiftDateUtc,
            Type = dto.Type,
            StartTime = dto.StartTime,
            EndTime = dto.EndTime,
            RequestedHours = Math.Round(duration, 2),
            ApprovedHours = dto.AutoApprove ? Math.Round(dto.ApprovedHours ?? duration, 2) : null,
            Reason = dto.Reason ?? "Administrative overtime entry",
            Status = dto.AutoApprove ? OvertimeStatus.Approved : OvertimeStatus.Pending,
            ReviewedByUserId = dto.AutoApprove ? currentUserId : null,
            ActionTakenAt = dto.AutoApprove ? DateTime.UtcNow : null,
            CreatedAt = DateTime.UtcNow
        };

        _context.OvertimeRequests.Add(request);
        await _context.SaveChangesAsync();

        await BroadcastOvertimeUpdated(orgId.Value);

        var empName = employee.User != null ? $"{employee.User.FirstName} {employee.User.LastName}".Trim() : dto.EmployeeId;
        await _audit.LogAsync(
            "Create",
            "OvertimeRequest",
            request.Id.ToString(),
            $"Admin created {request.Type} for {empName} on {dto.Date:yyyy-MM-dd} (Status: {request.Status})");

        return Ok(new
        {
            message = "Overtime record created successfully.",
            id = request.Id
        });
    }
}

public class CreateOvertimeRequestDto
{
    public DateTime Date { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string? Reason { get; set; }
}

public class ApproveOvertimeDto
{
    public double? ApprovedHours { get; set; }
}

public class RejectOvertimeDto
{
    public string? Reason { get; set; }
}

public class AdminCreateOvertimeDto
{
    public required string EmployeeId { get; set; }
    public DateTime Date { get; set; }
    public OvertimeType Type { get; set; } = OvertimeType.PreOvertime;
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public double? ApprovedHours { get; set; }
    public string? Reason { get; set; }
    public bool AutoApprove { get; set; } = true;
}

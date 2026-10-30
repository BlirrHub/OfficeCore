using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Models;
using System.Security.Claims;
using OfficeCore.Api.Services;
using OfficeCore.Api.Hubs;

namespace OfficeCore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LeaveRequestController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWebHostEnvironment _env;
    private readonly TenantService _tenant;
    private readonly IHubContext<NotificationHub> _hub;
    private readonly IAuditService _audit;

    public LeaveRequestController(
        AppDbContext context,
        UserManager<ApplicationUser> userManager,
        IWebHostEnvironment env,
        TenantService tenant,
        IHubContext<NotificationHub> hub,
        IAuditService audit)
    {
        _context = context;
        _userManager = userManager;
        _env = env;
        _tenant = tenant;
        _hub = hub;
        _audit = audit;
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMyRequests()
    {
        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (employee == null) return Unauthorized();
        var orgId = _tenant.GetActiveOrganizationId();

        var requests = await _context.LeaveRequests
            .Where(r => r.EmployeeId == employee.Id && r.OrganizationId == orgId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new
            {
                r.Id,
                r.Type,
                r.StartDate,
                r.EndDate,
                r.Reason,
                r.AttachmentPath,
                r.Status,
                r.CreatedAt,
                r.ActionTakenAt
            })
            .ToListAsync();

        return Ok(requests);
    }

    [HttpGet]
    [Authorize(Policy = "RequireLeaveApprovals")]
    public async Task<IActionResult> GetAllRequests()
    {
        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Forbid();

        var requests = await _context.LeaveRequests
            .Include(r => r.Employee)
                .ThenInclude(e => e!.User)
            .Where(r => r.OrganizationId == orgId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new
            {
                r.Id,
                r.EmployeeId,
                EmployeeName = r.Employee != null && r.Employee.User != null ? r.Employee.User.FirstName + " " + r.Employee.User.LastName : "Unknown",
                r.Type,
                r.StartDate,
                r.EndDate,
                r.Reason,
                r.AttachmentPath,
                r.Status,
                r.CreatedAt,
                r.ActionTakenAt
            })
            .ToListAsync();

        return Ok(requests);
    }

    [HttpPost]
    public async Task<IActionResult> SubmitRequest([FromForm] CreateLeaveRequestDto request)
    {
        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Unauthorized("No active workspace selected.");
        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (employee == null) return Unauthorized();

        string? attachmentPath = null;

        if (request.Attachment != null && request.Attachment.Length > 0)
        {
            var uploadsFolder = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads", "leaves");
            Directory.CreateDirectory(uploadsFolder);

            if (request.Attachment.Length > 15 * 1024 * 1024)
                return BadRequest("Attachment exceeds the 15MB limit.");
            var allowedLeaveExts = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".gif", ".webp", ".doc", ".docx", ".txt" };
            var safeLeaveFileName = Path.GetFileName(request.Attachment.FileName);
            var leaveExt = Path.GetExtension(safeLeaveFileName)?.ToLowerInvariant() ?? string.Empty;
            if (!allowedLeaveExts.Contains(leaveExt))
                return BadRequest("Unsupported attachment file type.");
            var uniqueFileName = Guid.NewGuid().ToString() + "_" + safeLeaveFileName;
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await request.Attachment.CopyToAsync(fileStream);
            }

            attachmentPath = $"/uploads/leaves/{uniqueFileName}";
        }

        var leaveRequest = new LeaveRequest
        {
            EmployeeId = employee.Id,
            Type = request.Type,
            StartDate = DateTime.SpecifyKind(request.StartDate.Date, DateTimeKind.Utc),
            EndDate = DateTime.SpecifyKind(request.EndDate.Date, DateTimeKind.Utc),
            Reason = request.Reason,
            AttachmentPath = attachmentPath,
            Status = LeaveRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            OrganizationId = orgId.Value
        };

        _context.LeaveRequests.Add(leaveRequest);
        await _context.SaveChangesAsync();

        // 1. Silent data refresh signal for admin tables & dashboard
        await _hub.Clients.Group($"org_{orgId.Value}").SendAsync("LeaveRequestCreated");

        // 2. Targeted notification for workspace admins / leave approvers
        var user = await _userManager.FindByIdAsync(employee.UserId);
        var requesterName = $"{user?.FirstName} {user?.LastName}".Trim();
        if (string.IsNullOrWhiteSpace(requesterName)) requesterName = user?.UserName ?? "An employee";

        var adminNotif = new NotificationPayload
        {
            Id = Guid.NewGuid().ToString(),
            Title = "New Leave Request Filed",
            Message = $"{requesterName} requested {leaveRequest.Type} leave ({leaveRequest.StartDate:MMM dd} - {leaveRequest.EndDate:MMM dd}).",
            Category = "Leave",
            Status = "Pending",
            TargetUrl = "/admin/leave-approvals",
            Timestamp = DateTime.UtcNow
        };
        await _hub.Clients.Group($"org_{orgId.Value}_admins").SendAsync("ReceiveNotification", adminNotif);

        return Ok(leaveRequest);
    }

    [HttpPut("{id}/status")]
    [Authorize(Policy = "RequireLeaveApprovals")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateLeaveStatusDto request)
    {
        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Forbid();

        var leaveRequest = await _context.LeaveRequests
            .Include(r => r.Employee)
                .ThenInclude(e => e!.User)
            .FirstOrDefaultAsync(r => r.Id == id && r.OrganizationId == orgId.Value);
        if (leaveRequest == null) return NotFound();

        var oldStatus = leaveRequest.Status;
        leaveRequest.Status = request.Status;
        leaveRequest.ActionTakenAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var empName = $"{leaveRequest.Employee?.User?.FirstName} {leaveRequest.Employee?.User?.LastName}".Trim();
        var isApproved = request.Status == LeaveRequestStatus.Approved;
        var isRejected = request.Status == LeaveRequestStatus.Rejected;
        var isCancelled = request.Status == LeaveRequestStatus.Cancelled;

        var action = isApproved ? AuditActions.Approve :
                     (isRejected ? AuditActions.Reject : AuditActions.Update);

        var auditDescription = isCancelled
            ? $"Cancelled leave request for {empName} ({leaveRequest.Type}, {leaveRequest.StartDate:yyyy-MM-dd} to {leaveRequest.EndDate:yyyy-MM-dd})"
            : $"{action} leave request for {empName} ({leaveRequest.Type}, {leaveRequest.StartDate:yyyy-MM-dd} to {leaveRequest.EndDate:yyyy-MM-dd})";

        await _audit.LogLeaveAsync(
            action,
            auditDescription,
            id,
            new { OldStatus = oldStatus.ToString(), NewStatus = request.Status.ToString() });

        // 1. Silent data refresh signal for pages
        if (orgId.HasValue)
        {
            await _hub.Clients.Group($"org_{orgId}").SendAsync("LeaveRequestUpdated");
        }

        // 2. Targeted notification for the requesting employee on the result
        if (!string.IsNullOrEmpty(leaveRequest.Employee?.UserId))
        {
            var statusWord = isApproved ? "approved" : (isRejected ? "rejected" : (isCancelled ? "cancelled" : "updated"));
            var statusTitle = isApproved ? "Leave Request Approved" : (isRejected ? "Leave Request Rejected" : (isCancelled ? "Leave Request Cancelled" : "Leave Request Updated"));

            var empNotif = new NotificationPayload
            {
                Id = Guid.NewGuid().ToString(),
                Title = statusTitle,
                Message = $"Your {leaveRequest.Type} leave request ({leaveRequest.StartDate:MMM dd} - {leaveRequest.EndDate:MMM dd}) has been {statusWord}.",
                Category = "Leave",
                Status = request.Status.ToString(),
                TargetUrl = "/myleaves",
                Timestamp = DateTime.UtcNow
            };
            await _hub.Clients.Group($"user_{leaveRequest.Employee.UserId}").SendAsync("ReceiveNotification", empNotif);
        }

        return Ok(new { message = "Status updated successfully" });
    }

    [HttpPost("admin-create")]
    [Authorize(Policy = "RequireLeaveApprovals")]
    public async Task<IActionResult> AdminCreateRequest(AdminCreateLeaveRequestDto request)
    {
        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Forbid();

        var employee = await _context.Employees
            .FirstOrDefaultAsync(e => (e.UserId == request.EmployeeId || e.Id == request.EmployeeId) && e.OrganizationId == orgId.Value);

        if (employee == null) return BadRequest("Employee not found");

        var leaveRequest = new LeaveRequest
        {
            EmployeeId = employee.Id,
            Type = request.Type,
            StartDate = DateTime.SpecifyKind(request.StartDate.Date, DateTimeKind.Utc),
            EndDate = DateTime.SpecifyKind(request.EndDate.Date, DateTimeKind.Utc),
            Reason = request.Reason,
            Status = LeaveRequestStatus.Approved,
            CreatedAt = DateTime.UtcNow,
            ActionTakenAt = DateTime.UtcNow,
            OrganizationId = orgId.Value
        };

        _context.LeaveRequests.Add(leaveRequest);
        await _context.SaveChangesAsync();

        var grantedEmpName = $"{employee.User?.FirstName} {employee.User?.LastName}".Trim();
        await _audit.LogLeaveAsync(
            AuditActions.Create,
            $"Admin issued approved {request.Type} leave for employee {grantedEmpName} ({request.StartDate:yyyy-MM-dd} to {request.EndDate:yyyy-MM-dd})",
            leaveRequest.Id);

        // Notify org members of the newly approved leave
        await _hub.Clients.Group($"org_{orgId.Value}").SendAsync("LeaveRequestCreated");

        if (!string.IsNullOrEmpty(employee.UserId))
        {
            var empNotif = new NotificationPayload
            {
                Id = Guid.NewGuid().ToString(),
                Title = "Leave Granted",
                Message = $"An approved {request.Type} leave ({request.StartDate:MMM dd} - {request.EndDate:MMM dd}) was added to your schedule.",
                Category = "Leave",
                Status = "Approved",
                TargetUrl = "/myleaves",
                Timestamp = DateTime.UtcNow
            };
            await _hub.Clients.Group($"user_{employee.UserId}").SendAsync("ReceiveNotification", empNotif);
        }

        return Ok(leaveRequest);
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "RequireLeaveApprovals")]
    public async Task<IActionResult> DeleteRequest(Guid id)
    {
        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Forbid();

        var leaveRequest = await _context.LeaveRequests.FirstOrDefaultAsync(r => r.Id == id && r.OrganizationId == orgId.Value);
        if (leaveRequest == null) return NotFound();

        var leaveOrgId = leaveRequest.OrganizationId;
        _context.LeaveRequests.Remove(leaveRequest);
        await _context.SaveChangesAsync();

        // Notify org members so the list updates live
        await _hub.Clients.Group($"org_{leaveOrgId}").SendAsync("LeaveRequestDeleted");

        return Ok(new { message = "Leave request deleted successfully" });
    }

    [HttpPut("{id}/cancel")]
    [HttpDelete("{id}/cancel")]
    public async Task<IActionResult> CancelRequest(Guid id)
    {
        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Unauthorized("No active workspace selected.");
        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (employee == null) return Unauthorized();

        var leaveRequest = await _context.LeaveRequests.FirstOrDefaultAsync(r => r.Id == id && r.OrganizationId == orgId.Value && r.EmployeeId == employee.Id);
        if (leaveRequest == null) return NotFound("Request not found.");

        if (leaveRequest.Status != LeaveRequestStatus.Pending)
            return BadRequest("Only pending requests can be cancelled.");

        leaveRequest.Status = LeaveRequestStatus.Cancelled;
        leaveRequest.ActionTakenAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var user = await _userManager.FindByIdAsync(employee.UserId);
        var empName = $"{user?.FirstName} {user?.LastName}".Trim();
        await _audit.LogLeaveAsync(
            AuditActions.Update,
            $"Cancelled leave request for {empName} ({leaveRequest.Type}, {leaveRequest.StartDate:yyyy-MM-dd} to {leaveRequest.EndDate:yyyy-MM-dd})",
            id,
            new { OldStatus = LeaveRequestStatus.Pending.ToString(), NewStatus = LeaveRequestStatus.Cancelled.ToString() });

        // Notify org members so the list updates live
        await _hub.Clients.Group($"org_{leaveRequest.OrganizationId}").SendAsync("LeaveRequestUpdated");

        return Ok(new { message = "Leave request cancelled successfully" });
    }
}

public class AdminCreateLeaveRequestDto
{
    public string EmployeeId { get; set; } = string.Empty;
    public LeaveType Type { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class CreateLeaveRequestDto
{
    public LeaveType Type { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public IFormFile? Attachment { get; set; }
}

public class UpdateLeaveStatusDto
{
    public LeaveRequestStatus Status { get; set; }
}

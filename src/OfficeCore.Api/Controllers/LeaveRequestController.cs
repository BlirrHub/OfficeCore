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
                .ThenInclude(e => e.User)
            .Where(r => r.OrganizationId == orgId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new
            {
                r.Id,
                r.EmployeeId,
                EmployeeName = r.Employee != null ? r.Employee.User.FirstName + " " + r.Employee.User.LastName : "Unknown",
                r.Type,
                r.StartDate,
                r.EndDate,
                r.Reason,
                r.AttachmentPath,
                r.Status,
                r.CreatedAt
            })
            .ToListAsync();

        return Ok(requests);
    }

    [HttpPost]
    public async Task<IActionResult> SubmitRequest([FromForm] CreateLeaveRequestDto request)
    {
        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (employee == null) return Unauthorized();
        var orgId = _tenant.GetActiveOrganizationId()!.Value;

        string? attachmentPath = null;

        if (request.Attachment != null && request.Attachment.Length > 0)
        {
            var uploadsFolder = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads", "leaves");
            Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = Guid.NewGuid().ToString() + "_" + request.Attachment.FileName;
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
            OrganizationId = orgId
        };

        _context.LeaveRequests.Add(leaveRequest);
        await _context.SaveChangesAsync();

        // Notify admins in this org so the Leave Approvals page updates live
        await _hub.Clients.Group($"org_{orgId}").SendAsync("LeaveRequestCreated", new
        {
            leaveRequest.Id,
            leaveRequest.EmployeeId,
            leaveRequest.Type,
            leaveRequest.StartDate,
            leaveRequest.EndDate,
            leaveRequest.Status,
            leaveRequest.CreatedAt
        });

        return Ok(leaveRequest);
    }

    [HttpPut("{id}/status")]
    [Authorize(Policy = "RequireLeaveApprovals")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateLeaveStatusDto request)
    {
        var orgId = _tenant.GetActiveOrganizationId();
        var leaveRequest = await _context.LeaveRequests
            .Include(r => r.Employee)
                .ThenInclude(e => e.User)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (leaveRequest == null) return NotFound();

        var oldStatus = leaveRequest.Status;
        leaveRequest.Status = request.Status;
        await _context.SaveChangesAsync();

        var empName = $"{leaveRequest.Employee?.User?.FirstName} {leaveRequest.Employee?.User?.LastName}".Trim();
        var action = request.Status == LeaveRequestStatus.Approved ? AuditActions.Approve :
                     (request.Status == LeaveRequestStatus.Rejected ? AuditActions.Reject : AuditActions.Update);

        await _audit.LogLeaveAsync(
            action,
            $"{action} leave request for {empName} ({leaveRequest.Type}, {leaveRequest.StartDate:yyyy-MM-dd} to {leaveRequest.EndDate:yyyy-MM-dd})",
            id,
            new { OldStatus = oldStatus.ToString(), NewStatus = request.Status.ToString() });

        // Notify all org members so the employee sees the status change live
        if (orgId.HasValue)
        {
            await _hub.Clients.Group($"org_{orgId}").SendAsync("LeaveRequestUpdated", new
            {
                Id = id,
                leaveRequest.Status
            });
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
        await _hub.Clients.Group($"org_{orgId.Value}").SendAsync("LeaveRequestCreated", new
        {
            leaveRequest.Id,
            leaveRequest.EmployeeId,
            leaveRequest.Type,
            leaveRequest.StartDate,
            leaveRequest.EndDate,
            leaveRequest.Status,
            leaveRequest.CreatedAt
        });

        return Ok(leaveRequest);
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "RequireLeaveApprovals")]
    public async Task<IActionResult> DeleteRequest(Guid id)
    {
        var leaveRequest = await _context.LeaveRequests.FindAsync(id);
        if (leaveRequest == null) return NotFound();

        var orgId = leaveRequest.OrganizationId;
        _context.LeaveRequests.Remove(leaveRequest);
        await _context.SaveChangesAsync();

        // Notify org members so the list updates live
        await _hub.Clients.Group($"org_{orgId}").SendAsync("LeaveRequestDeleted", id);

        return Ok(new { message = "Leave request deleted successfully" });
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

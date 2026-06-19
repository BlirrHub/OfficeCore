using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Models;
using System.Security.Claims;
using OfficeCore.Api.Services;

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

    public LeaveRequestController(AppDbContext context, UserManager<ApplicationUser> userManager, IWebHostEnvironment env, TenantService tenant)
    {
        _context = context;
        _userManager = userManager;
        _env = env;
        _tenant = tenant;
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
    [Authorize(Roles = "Admin,HR")]
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
            StartDate = request.StartDate.ToUniversalTime(),
            EndDate = request.EndDate.ToUniversalTime(),
            Reason = request.Reason,
            AttachmentPath = attachmentPath,
            Status = LeaveRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            OrganizationId = orgId
        };

        _context.LeaveRequests.Add(leaveRequest);
        await _context.SaveChangesAsync();

        return Ok(leaveRequest);
    }

    [HttpPut("{id}/status")]
    [Authorize(Roles = "Admin,HR")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateLeaveStatusDto request)
    {
        var leaveRequest = await _context.LeaveRequests.FindAsync(id);
        if (leaveRequest == null) return NotFound();

        leaveRequest.Status = request.Status;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Status updated successfully" });
    }

    [HttpPost("admin-create")]
    [Authorize(Roles = "Admin,HR")]
    public async Task<IActionResult> AdminCreateRequest(AdminCreateLeaveRequestDto request)
    {
        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Forbid();

        var leaveRequest = new LeaveRequest
        {
            EmployeeId = request.EmployeeId,
            Type = request.Type,
            StartDate = request.StartDate.ToUniversalTime(),
            EndDate = request.EndDate.ToUniversalTime(),
            Reason = request.Reason,
            Status = LeaveRequestStatus.Approved,
            CreatedAt = DateTime.UtcNow,
            OrganizationId = orgId.Value
        };

        _context.LeaveRequests.Add(leaveRequest);
        await _context.SaveChangesAsync();

        return Ok(leaveRequest);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin,HR")]
    public async Task<IActionResult> DeleteRequest(Guid id)
    {
        var leaveRequest = await _context.LeaveRequests.FindAsync(id);
        if (leaveRequest == null) return NotFound();

        _context.LeaveRequests.Remove(leaveRequest);
        await _context.SaveChangesAsync();

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

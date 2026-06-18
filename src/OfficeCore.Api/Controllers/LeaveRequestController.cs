using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Models;
using System.Security.Claims;

namespace OfficeCore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LeaveRequestController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWebHostEnvironment _env;

    public LeaveRequestController(AppDbContext context, UserManager<ApplicationUser> userManager, IWebHostEnvironment env)
    {
        _context = context;
        _userManager = userManager;
        _env = env;
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMyRequests()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Unauthorized();

        var requests = await _context.LeaveRequests
            .Where(r => r.EmployeeId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return Ok(requests);
    }

    [HttpGet]
    [Authorize(Roles = "Admin,HR")]
    public async Task<IActionResult> GetAllRequests()
    {
        var requests = await _context.LeaveRequests
            .Include(r => r.Employee)
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
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Unauthorized();

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
            EmployeeId = userId,
            Type = request.Type,
            StartDate = request.StartDate.ToUniversalTime(),
            EndDate = request.EndDate.ToUniversalTime(),
            Reason = request.Reason,
            AttachmentPath = attachmentPath,
            Status = LeaveRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow
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
        var leaveRequest = new LeaveRequest
        {
            EmployeeId = request.EmployeeId,
            Type = request.Type,
            StartDate = request.StartDate.ToUniversalTime(),
            EndDate = request.EndDate.ToUniversalTime(),
            Reason = request.Reason,
            Status = LeaveRequestStatus.Approved,
            CreatedAt = DateTime.UtcNow
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

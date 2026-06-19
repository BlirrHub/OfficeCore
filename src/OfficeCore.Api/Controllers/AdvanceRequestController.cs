using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Models;
using OfficeCore.Api.Services;

namespace OfficeCore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AdvanceRequestController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly TenantService _tenant;

    public AdvanceRequestController(AppDbContext context, UserManager<ApplicationUser> userManager, TenantService tenant)
    {
        _context = context;
        _userManager = userManager;
        _tenant = tenant;
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMyRequests()
    {
        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (employee == null) return Unauthorized();
        var orgId = _tenant.GetActiveOrganizationId()!.Value;

        var requests = await _context.AdvanceRequests
            .Where(r => r.EmployeeId == employee.Id && r.OrganizationId == orgId)
            .OrderByDescending(r => r.RequestDate)
            .ToListAsync();

        return Ok(requests);
    }

    [HttpGet]
    [Authorize(Policy = "RequireLedger")]
    public async Task<IActionResult> GetAllRequests()
    {
        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Forbid();

        var requests = await _context.AdvanceRequests
            .Include(r => r.Employee)
                .ThenInclude(e => e.User)
            .Where(r => r.OrganizationId == orgId)
            .OrderByDescending(r => r.RequestDate)
            .Select(r => new {
                r.Id,
                r.Type,
                r.RequestedAmount,
                r.Note,
                r.Status,
                r.RequestDate,
                r.RemainingBalance,
                r.InstallmentAmount,
                EmployeeName = r.Employee!.User.FirstName + " " + r.Employee.User.LastName
            })
            .ToListAsync();

        return Ok(requests);
    }

    [HttpPost]
    public async Task<IActionResult> CreateRequest(CreateAdvanceRequestDto dto)
    {
        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (employee == null) return Unauthorized();
        var orgId = _tenant.GetActiveOrganizationId()!.Value;

        var request = new AdvanceRequest
        {
            Id = Guid.NewGuid(),
            EmployeeId = employee.Id,
            Type = dto.Type,
            RequestedAmount = dto.RequestedAmount,
            Note = dto.Note ?? string.Empty,
            Status = AdvanceStatus.Pending,
            RequestDate = DateTime.UtcNow,
            OrganizationId = orgId
        };
        
        // ELoan initial balance is the requested amount. CA also needs a remaining balance to be deducted.
        request.RemainingBalance = request.RequestedAmount;
        if (request.Type != AdvanceType.ELoan)
        {
            request.InstallmentAmount = null;
        }

        _context.AdvanceRequests.Add(request);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetMyRequests), new { id = request.Id }, request);
    }

    [HttpPut("{id}/cancel")]
    public async Task<IActionResult> CancelRequest(Guid id)
    {
        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (employee == null) return Unauthorized();
        var orgId = _tenant.GetActiveOrganizationId()!.Value;

        var request = await _context.AdvanceRequests.FirstOrDefaultAsync(r => r.Id == id && r.EmployeeId == employee.Id && r.OrganizationId == orgId);
        
        if (request == null)
            return NotFound();

        if (request.Status != AdvanceStatus.Pending)
            return BadRequest("Only pending requests can be cancelled.");

        request.Status = AdvanceStatus.Cancelled;
        request.ResolutionDate = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpPut("{id}/approve")]
    [Authorize(Policy = "RequireLedger")]
    public async Task<IActionResult> ApproveRequest(Guid id, [FromBody] ApproveRequestDto dto)
    {
        var request = await _context.AdvanceRequests.FindAsync(id);
        if (request == null) return NotFound();

        if (request.Status != AdvanceStatus.Pending)
            return BadRequest("Only pending requests can be approved.");

        request.Status = AdvanceStatus.Approved;
        request.ResolutionDate = DateTime.UtcNow;

        if (request.Type == AdvanceType.ELoan)
        {
            if (!dto.InstallmentAmount.HasValue || dto.InstallmentAmount <= 0)
                return BadRequest("ELoans require a valid installment amount.");
            
            request.InstallmentAmount = dto.InstallmentAmount;
        }

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("{id}/reject")]
    [Authorize(Policy = "RequireLedger")]
    public async Task<IActionResult> RejectRequest(Guid id)
    {
        var request = await _context.AdvanceRequests.FindAsync(id);
        if (request == null) return NotFound();

        if (request.Status != AdvanceStatus.Pending)
            return BadRequest("Only pending requests can be rejected.");

        request.Status = AdvanceStatus.Rejected;
        request.ResolutionDate = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return NoContent();
    }
    
    [HttpPost("admin-create")]
    [Authorize(Policy = "RequireLedger")]
    public async Task<IActionResult> AdminCreateRequest(AdminCreateAdvanceRequestDto dto)
    {
        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Forbid();

        var request = new AdvanceRequest
        {
            Id = Guid.NewGuid(),
            EmployeeId = dto.EmployeeId,
            Type = dto.Type,
            RequestedAmount = dto.RequestedAmount,
            Note = dto.Note ?? string.Empty,
            Status = AdvanceStatus.Approved,
            RequestDate = DateTime.UtcNow,
            ResolutionDate = DateTime.UtcNow,
            OrganizationId = orgId.Value
        };
        
        request.RemainingBalance = request.RequestedAmount;
        if (request.Type == AdvanceType.ELoan)
        {
            if (!dto.InstallmentAmount.HasValue || dto.InstallmentAmount <= 0)
                return BadRequest("ELoans require a valid installment amount.");
            
            request.InstallmentAmount = dto.InstallmentAmount;
        }
        else
        {
            request.InstallmentAmount = null;
        }

        _context.AdvanceRequests.Add(request);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAllRequests), new { id = request.Id }, request);
    }
    
    public class ApproveRequestDto
    {
        public decimal? InstallmentAmount { get; set; }
    }

    [HttpDelete("admin-cleanup")]
    [AllowAnonymous]
    public async Task<IActionResult> AdminCleanup()
    {
        var adminAdvances = await _context.AdvanceRequests
            .Include(ar => ar.Employee)
            .Where(ar => ar.Employee != null && ar.Employee.User.Email == "admin@officecore.com")
            .ToListAsync();
            
        _context.AdvanceRequests.RemoveRange(adminAdvances);
        await _context.SaveChangesAsync();
        
        return Ok(new { Message = $"Deleted {adminAdvances.Count} records" });
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "RequirePayroll")] // Only payroll admins can delete
    public async Task<IActionResult> DeleteRequest(Guid id)
    {
        var advanceRequest = await _context.AdvanceRequests.FindAsync(id);
        if (advanceRequest == null) return NotFound();

        _context.AdvanceRequests.Remove(advanceRequest);
        await _context.SaveChangesAsync();

        return Ok(new { Message = "Advance/Loan request deleted successfully" });
    }
    
    public class CreateAdvanceRequestDto
    {
        public AdvanceType Type { get; set; }
        public decimal RequestedAmount { get; set; }
        public string? Note { get; set; }
    }

    public class AdminCreateAdvanceRequestDto
    {
        public string EmployeeId { get; set; } = string.Empty;
        public AdvanceType Type { get; set; }
        public decimal RequestedAmount { get; set; }
        public decimal? InstallmentAmount { get; set; }
        public string? Note { get; set; }
    }
}

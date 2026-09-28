using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Models;
using OfficeCore.Api.Services;
using OfficeCore.Api.Hubs;

namespace OfficeCore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AdvanceRequestController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly TenantService _tenant;
    private readonly IHubContext<NotificationHub> _hub;
    private readonly IAuditService _audit;

    public AdvanceRequestController(
        AppDbContext context,
        UserManager<ApplicationUser> userManager,
        TenantService tenant,
        IHubContext<NotificationHub> hub,
        IAuditService audit)
    {
        _context = context;
        _userManager = userManager;
        _tenant = tenant;
        _hub = hub;
        _audit = audit;
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

        // Fetch first, then project in-memory to avoid SQLite string concat translation issues
        var requests = await _context.AdvanceRequests
            .Include(r => r.Employee)
                .ThenInclude(e => e.User)
            .Where(r => r.OrganizationId == orgId)
            .OrderByDescending(r => r.RequestDate)
            .ToListAsync();

        var result = requests.Select(r => new {
            r.Id,
            r.Type,
            r.RequestedAmount,
            r.Note,
            r.Status,
            r.RequestDate,
            r.RemainingBalance,
            r.InstallmentAmount,
            EmployeeName = r.Employee != null
                ? $"{r.Employee.User?.FirstName} {r.Employee.User?.LastName}".Trim()
                : "Unknown"
        });

        return Ok(result);
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

        // Notify all org members (admins on Ledger, employee on Advances)
        await _hub.Clients.Group($"org_{orgId}").SendAsync("AdvanceRequestCreated", new
        {
            request.Id,
            request.EmployeeId,
            request.Type,
            request.RequestedAmount,
            request.Status,
            request.RequestDate
        });

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

        // Notify org so the employee's Advances page reflects the cancellation live
        await _hub.Clients.Group($"org_{orgId}").SendAsync("AdvanceRequestUpdated", new
        {
            Id = id,
            request.Status
        });

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

        var emp = await _context.Employees.Include(e => e.User).FirstOrDefaultAsync(e => e.Id == request.EmployeeId);
        var empName = $"{emp?.User?.FirstName} {emp?.User?.LastName}".Trim();
        await _audit.LogAdvanceAsync(
            AuditActions.Approve,
            $"Approved {request.Type} advance request of ₱{request.RequestedAmount:N2} for employee {empName}",
            id);

        // Notify org so the employee's Advances page reflects approval live
        var approvedOrgId = request.OrganizationId;
        await _hub.Clients.Group($"org_{approvedOrgId}").SendAsync("AdvanceRequestUpdated", new
        {
            Id = id,
            request.Status,
            request.InstallmentAmount
        });

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

        var rejEmp = await _context.Employees.Include(e => e.User).FirstOrDefaultAsync(e => e.Id == request.EmployeeId);
        var rejEmpName = $"{rejEmp?.User?.FirstName} {rejEmp?.User?.LastName}".Trim();
        await _audit.LogAdvanceAsync(
            AuditActions.Reject,
            $"Rejected {request.Type} advance request of ₱{request.RequestedAmount:N2} for employee {rejEmpName}",
            id);

        // Notify org so the employee's Advances page reflects rejection live
        var rejectedOrgId = request.OrganizationId;
        await _hub.Clients.Group($"org_{rejectedOrgId}").SendAsync("AdvanceRequestUpdated", new
        {
            Id = id,
            request.Status
        });

        return NoContent();
    }
    
    [HttpPost("admin-create")]
    [Authorize(Policy = "RequireLedger")]
    public async Task<IActionResult> AdminCreateRequest(AdminCreateAdvanceRequestDto dto)
    {
        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Forbid();

        var employee = await _context.Employees
            .FirstOrDefaultAsync(e => (e.UserId == dto.EmployeeId || e.Id == dto.EmployeeId) && e.OrganizationId == orgId.Value);

        if (employee == null) return BadRequest("Employee not found");

        var request = new AdvanceRequest
        {
            Id = Guid.NewGuid(),
            EmployeeId = employee.Id,
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

        // Notify org so both admin Ledger and employee Advances pages update live
        await _hub.Clients.Group($"org_{orgId.Value}").SendAsync("AdvanceRequestCreated", new
        {
            request.Id,
            request.EmployeeId,
            request.Type,
            request.RequestedAmount,
            request.Status,
            request.RequestDate
        });

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

        var advanceOrgId = advanceRequest.OrganizationId;
        _context.AdvanceRequests.Remove(advanceRequest);
        await _context.SaveChangesAsync();

        // Notify org members
        await _hub.Clients.Group($"org_{advanceOrgId}").SendAsync("AdvanceRequestDeleted", id);

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

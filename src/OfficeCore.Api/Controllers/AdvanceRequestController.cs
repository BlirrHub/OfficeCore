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
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Unauthorized("No active workspace selected.");
        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (employee == null) return Unauthorized();

        var requests = await _context.AdvanceRequests
            .Where(r => r.EmployeeId == employee.Id && r.OrganizationId == orgId.Value)
            .OrderByDescending(r => r.RequestDate)
            .ToListAsync();

        return Ok(requests);
    }

    [HttpGet]
    [Authorize(Policy = "RequireLedger")]
    [Authorize(Policy = "Permission:ledger.view")]
    public async Task<IActionResult> GetAllRequests()
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Forbid();

        // Fetch first, then project in-memory to avoid SQLite string concat translation issues
        var requests = await _context.AdvanceRequests
            .Include(r => r.Employee)
                .ThenInclude(e => e!.User)
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
            r.ResolutionDate,
            r.RemainingBalance,
            r.InstallmentAmount,
            EmployeeName = r.Employee?.User != null
                ? $"{r.Employee.User.FirstName} {r.Employee.User.LastName}".Trim()
                : "Unknown"
        });

        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateRequest(CreateAdvanceRequestDto dto)
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Unauthorized("No active workspace selected.");
        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (employee == null) return Unauthorized();

        var request = new AdvanceRequest
        {
            Id = Guid.NewGuid(),
            EmployeeId = employee.Id,
            Type = dto.Type,
            RequestedAmount = dto.RequestedAmount,
            Note = dto.Note ?? string.Empty,
            Status = AdvanceStatus.Pending,
            RequestDate = DateTime.UtcNow,
            OrganizationId = orgId.Value
        };
        
        // ELoan initial balance is the requested amount. CA also needs a remaining balance to be deducted.
        if (dto.RequestedAmount <= 0) return BadRequest("Requested amount must be greater than zero.");
        request.RemainingBalance = request.RequestedAmount;
        if (request.Type != AdvanceType.ELoan)
        {
            request.InstallmentAmount = null;
        }

        _context.AdvanceRequests.Add(request);
        await _context.SaveChangesAsync();

        // 1. Silent data refresh signal for pages
        await _hub.Clients.Group($"org_{orgId.Value}").SendAsync("AdvanceRequestCreated");

        // 2. Targeted notification for workspace admins / ledger approvers
        var user = await _userManager.FindByIdAsync(employee.UserId);
        var requesterName = $"{user?.FirstName} {user?.LastName}".Trim();
        if (string.IsNullOrWhiteSpace(requesterName)) requesterName = user?.UserName ?? "An employee";

        var typeTitle = request.Type == AdvanceType.ELoan ? "E-Loan" : "Cash Advance";
        var adminNotif = new NotificationPayload
        {
            Id = Guid.NewGuid().ToString(),
            Title = $"New {typeTitle} Request Filed",
            Message = $"{requesterName} filed a {typeTitle} request of ₱{request.RequestedAmount:N2}.",
            Category = "Advance",
            Status = "Pending",
            TargetUrl = "/ledger",
            Timestamp = DateTime.UtcNow
        };
        await _hub.Clients.Group($"org_{orgId.Value}_admins_ledger").SendAsync("ReceiveNotification", adminNotif);

        return CreatedAtAction(nameof(GetMyRequests), new { id = request.Id }, request);
    }

    [HttpPut("{id}/cancel")]
    public async Task<IActionResult> CancelRequest(Guid id)
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Unauthorized("No active workspace selected.");
        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (employee == null) return Unauthorized();

        var request = await _context.AdvanceRequests.FirstOrDefaultAsync(r => r.Id == id && r.EmployeeId == employee.Id && r.OrganizationId == orgId.Value);
        
        if (request == null)
            return NotFound();

        if (request.Status != AdvanceStatus.Pending)
            return BadRequest("Only pending requests can be cancelled.");

        request.Status = AdvanceStatus.Cancelled;
        request.ResolutionDate = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Notify org so the employee's Advances page reflects the cancellation live
        await _hub.Clients.Group($"org_{orgId}").SendAsync("AdvanceRequestUpdated");

        return NoContent();
    }

    [HttpPut("{id}/admin-cancel")]
    [Authorize(Policy = "RequireLedger")]
    [Authorize(Policy = "Permission:ledger.manage")]
    public async Task<IActionResult> AdminCancelRequest(Guid id)
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Forbid();

        var request = await _context.AdvanceRequests
            .Include(r => r.Employee)
                .ThenInclude(e => e!.User)
            .FirstOrDefaultAsync(r => r.Id == id && r.OrganizationId == orgId.Value);

        if (request == null) return NotFound();

        if (request.Status == AdvanceStatus.Cancelled)
            return BadRequest("Request is already cancelled.");

        var oldStatus = request.Status;
        request.Status = AdvanceStatus.Cancelled;
        request.ResolutionDate = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var empName = request.Employee?.User != null
            ? $"{request.Employee.User.FirstName} {request.Employee.User.LastName}".Trim()
            : "Employee";

        var typeTitle = request.Type == AdvanceType.ELoan ? "E-Loan" : "Cash Advance";

        await _audit.LogAdvanceAsync(
            AuditActions.Update,
            $"Cancelled {typeTitle} request of ₱{request.RequestedAmount:N2} for employee {empName} (previously {oldStatus})",
            id);

        // 1. Silent data refresh signal for pages
        await _hub.Clients.Group($"org_{orgId.Value}").SendAsync("AdvanceRequestUpdated");

        // 2. Targeted notification for the employee
        if (!string.IsNullOrEmpty(request.Employee?.UserId))
        {
            var empNotif = new NotificationPayload
            {
                Id = Guid.NewGuid().ToString(),
                Title = $"{typeTitle} Request Cancelled",
                Message = $"Your {typeTitle} request of ₱{request.RequestedAmount:N2} has been cancelled by administration.",
                Category = "Advance",
                Status = "Cancelled",
                TargetUrl = "/advances",
                Timestamp = DateTime.UtcNow
            };
            await _hub.Clients.Group($"user_{request.Employee.UserId}").SendAsync("ReceiveNotification", empNotif);
        }

        return NoContent();
    }

    [HttpPut("{id}/approve")]
    [Authorize(Policy = "RequireLedger")]
    [Authorize(Policy = "Permission:ledger.manage")]
    public async Task<IActionResult> ApproveRequest(Guid id, [FromBody] ApproveRequestDto dto)
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Forbid();

        var request = await _context.AdvanceRequests.FirstOrDefaultAsync(r => r.Id == id && r.OrganizationId == orgId.Value);
        if (request == null) return NotFound();

        if (request.Status != AdvanceStatus.Pending)
            return BadRequest("Only pending requests can be approved.");

        request.Status = AdvanceStatus.Approved;
        request.ResolutionDate = DateTime.UtcNow;
        request.RemainingBalance ??= request.RequestedAmount;

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

        // 1. Silent data refresh signal for pages
        var approvedOrgId = request.OrganizationId;
        await _hub.Clients.Group($"org_{approvedOrgId}").SendAsync("AdvanceRequestUpdated");

        // 2. Targeted notification for the requesting employee
        if (!string.IsNullOrEmpty(emp?.UserId))
        {
            var typeTitle = request.Type == AdvanceType.ELoan ? "E-Loan" : "Cash Advance";
            var empNotif = new NotificationPayload
            {
                Id = Guid.NewGuid().ToString(),
                Title = $"{typeTitle} Request Approved",
                Message = $"Your {typeTitle} request of ₱{request.RequestedAmount:N2} has been approved.",
                Category = "Advance",
                Status = "Approved",
                TargetUrl = "/advances",
                Timestamp = DateTime.UtcNow
            };
            await _hub.Clients.Group($"user_{emp.UserId}").SendAsync("ReceiveNotification", empNotif);
        }

        return NoContent();
    }

    [HttpPut("{id}/reject")]
    [Authorize(Policy = "RequireLedger")]
    [Authorize(Policy = "Permission:ledger.manage")]
    public async Task<IActionResult> RejectRequest(Guid id)
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Forbid();

        var request = await _context.AdvanceRequests.FirstOrDefaultAsync(r => r.Id == id && r.OrganizationId == orgId.Value);
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

        // 1. Silent data refresh signal for pages
        var rejectedOrgId = request.OrganizationId;
        await _hub.Clients.Group($"org_{rejectedOrgId}").SendAsync("AdvanceRequestUpdated");

        // 2. Targeted notification for the requesting employee
        if (!string.IsNullOrEmpty(rejEmp?.UserId))
        {
            var typeTitle = request.Type == AdvanceType.ELoan ? "E-Loan" : "Cash Advance";
            var empNotif = new NotificationPayload
            {
                Id = Guid.NewGuid().ToString(),
                Title = $"{typeTitle} Request Rejected",
                Message = $"Your {typeTitle} request of ₱{request.RequestedAmount:N2} was rejected.",
                Category = "Advance",
                Status = "Rejected",
                TargetUrl = "/advances",
                Timestamp = DateTime.UtcNow
            };
            await _hub.Clients.Group($"user_{rejEmp.UserId}").SendAsync("ReceiveNotification", empNotif);
        }

        return NoContent();
    }
    
    [HttpPost("admin-create")]
    [Authorize(Policy = "RequireLedger")]
    [Authorize(Policy = "Permission:ledger.manage")]
    public async Task<IActionResult> AdminCreateRequest(AdminCreateAdvanceRequestDto dto)
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Forbid();

        var employee = await _context.Employees
            .FirstOrDefaultAsync(e => (e.UserId == dto.EmployeeId || e.Id == dto.EmployeeId) && e.OrganizationId == orgId.Value);

        if (employee == null) return BadRequest("Employee not found");
        if (dto.RequestedAmount <= 0) return BadRequest("Requested amount must be greater than zero.");

        var requestDate = dto.RequestDate.HasValue
            ? (dto.RequestDate.Value.Kind == DateTimeKind.Utc 
                ? dto.RequestDate.Value 
                : DateTime.SpecifyKind(dto.RequestDate.Value, DateTimeKind.Utc))
            : DateTime.UtcNow;

        var request = new AdvanceRequest
        {
            Id = Guid.NewGuid(),
            EmployeeId = employee.Id,
            Type = dto.Type,
            RequestedAmount = dto.RequestedAmount,
            Note = dto.Note ?? string.Empty,
            Status = AdvanceStatus.Approved,
            RequestDate = requestDate,
            ResolutionDate = requestDate,
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

        // 1. Silent data refresh for admin Ledger and employee Advances pages
        await _hub.Clients.Group($"org_{orgId.Value}").SendAsync("AdvanceRequestCreated");

        // 2. Targeted notification for employee
        if (!string.IsNullOrEmpty(employee.UserId))
        {
            var typeTitle = request.Type == AdvanceType.ELoan ? "E-Loan" : "Cash Advance";
            var empNotif = new NotificationPayload
            {
                Id = Guid.NewGuid().ToString(),
                Title = $"{typeTitle} Granted",
                Message = $"An approved {typeTitle} of ₱{request.RequestedAmount:N2} has been issued to your account.",
                Category = "Advance",
                Status = "Approved",
                TargetUrl = "/advances",
                Timestamp = DateTime.UtcNow
            };
            await _hub.Clients.Group($"user_{employee.UserId}").SendAsync("ReceiveNotification", empNotif);
        }

        return CreatedAtAction(nameof(GetAllRequests), new { id = request.Id }, request);
    }
    
    public class ApproveRequestDto
    {
        public decimal? InstallmentAmount { get; set; }
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "RequireLedger")]
    [Authorize(Policy = "Permission:ledger.manage")]
    public async Task<IActionResult> DeleteRequest(Guid id)
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Forbid();

        var advanceRequest = await _context.AdvanceRequests.FirstOrDefaultAsync(r => r.Id == id && r.OrganizationId == orgId.Value);
        if (advanceRequest == null) return NotFound();

        var advanceOrgId = advanceRequest.OrganizationId;
        _context.AdvanceRequests.Remove(advanceRequest);
        await _context.SaveChangesAsync();

        // Notify org members
        await _hub.Clients.Group($"org_{advanceOrgId}").SendAsync("AdvanceRequestDeleted");

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
        public DateTime? RequestDate { get; set; }
    }
}

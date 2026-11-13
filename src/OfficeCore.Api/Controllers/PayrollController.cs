using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Models;
using OfficeCore.Api.Services;
using OfficeCore.Shared.Authorization;
using System.Security.Claims;

namespace OfficeCore.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class PayrollController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IPayrollComputationService _computationService;
    private readonly TenantService _tenant;
    private readonly IAuditService _audit;

    public PayrollController(
        AppDbContext context,
        IPayrollComputationService computationService,
        TenantService tenant,
        IAuditService audit)
    {
        _context = context;
        _computationService = computationService;
        _tenant = tenant;
        _audit = audit;
    }

    [HttpGet("periods")]
    [Authorize(Policy = "Permission:payroll.view")]
    public async Task<IActionResult> GetPayrollPeriods()
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Forbid();

        var periods = await _context.PayrollPeriods
            .Where(p => !p.IsArchived && p.OrganizationId == orgId)
            .OrderByDescending(p => p.StartDate)
            .ToListAsync();
        return Ok(periods);
    }

    [HttpDelete("periods/{id}")]
    [Authorize(Policy = "Permission:payroll.calculate")]
    public async Task<IActionResult> DeletePayrollPeriod(Guid id)
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Forbid();

        var period = await _context.PayrollPeriods.FirstOrDefaultAsync(p => p.Id == id && p.OrganizationId == orgId.Value);
        if (period == null) return NotFound();

        if (period.Status == PayrollStatus.Finalized || period.Status == PayrollStatus.Paid)
        {
            return BadRequest("Cannot delete a finalized or paid payroll period.");
        }

        period.IsArchived = true;
        await _context.SaveChangesAsync();
        
        await _audit.LogPayrollAsync(AuditActions.Delete, $"Archived payroll period {period.StartDate:yyyy-MM-dd} to {period.EndDate:yyyy-MM-dd}", id);

        return Ok();
    }

    public sealed class CreatePayrollPeriodRequest
    {
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        [System.ComponentModel.DataAnnotations.MaxLength(500)]
        public string? Remarks { get; set; }
        [System.ComponentModel.DataAnnotations.Range(0, 10)]
        public decimal DeductionMultiplier { get; set; } = 1.0m;
    }

    [HttpPost("periods")]
    [Authorize(Policy = "Permission:payroll.calculate")]
    public async Task<IActionResult> CreatePayrollPeriod([FromBody] CreatePayrollPeriodRequest request)
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Forbid();

        if (request.EndDate < request.StartDate)
            return BadRequest(new { message = "End date cannot be earlier than start date." });

        var period = new PayrollPeriod
        {
            Id = Guid.NewGuid(),
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Remarks = request.Remarks,
            DeductionMultiplier = request.DeductionMultiplier,
            Status = PayrollStatus.Draft,
            IsArchived = false,
            CreatedAt = DateTime.UtcNow,
            OrganizationId = orgId.Value
        };

        _context.PayrollPeriods.Add(period);
        await _context.SaveChangesAsync();

        await _audit.LogPayrollAsync(AuditActions.Create, $"Created draft payroll period {period.StartDate:yyyy-MM-dd} to {period.EndDate:yyyy-MM-dd}", period.Id);

        return Ok(period);
    }
    
    [HttpGet("periods/{id}")]
    [Authorize(Policy = "Permission:payroll.view")]
    public async Task<IActionResult> GetPayrollPeriod(Guid id)
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        var period = await _context.PayrollPeriods.FirstOrDefaultAsync(p => p.Id == id && p.OrganizationId == orgId);
        if (period == null) return NotFound();
        return Ok(period);
    }

    [HttpPost("periods/{id}/compute")]
    [Authorize(Policy = "Permission:payroll.calculate")]
    public async Task<IActionResult> ComputePayroll(Guid id)
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Forbid();

        var period = await _context.PayrollPeriods.FirstOrDefaultAsync(p => p.Id == id && p.OrganizationId == orgId.Value);
        if (period == null) return NotFound();

        try
        {
            await _computationService.ComputePayrollPeriodAsync(id);
            var updatedPeriod = await _context.PayrollPeriods.FindAsync(id);
            await _audit.LogPayrollAsync(AuditActions.Calculate, $"Computed payroll calculations for period {updatedPeriod?.StartDate:yyyy-MM-dd} to {updatedPeriod?.EndDate:yyyy-MM-dd}", id);
            return Ok();
        }
        catch (Exception ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    [HttpGet("periods/{id}/payslips")]
    [Authorize(Policy = "Permission:payroll.view")]
    public async Task<IActionResult> GetPayslips(Guid id)
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Forbid();

        var periodExists = await _context.PayrollPeriods.AnyAsync(p => p.Id == id && p.OrganizationId == orgId.Value);
        if (!periodExists) return NotFound();

        var payslips = await _context.Payslips
            .Include(p => p.Employee)
                .ThenInclude(e => e!.User)
            .Include(p => p.Employee)
                .ThenInclude(e => e!.EmployeeRoles)
                    .ThenInclude(er => er.Role)
            .Include(p => p.Organization)
            .Include(p => p.PayrollPeriod)
            .Where(p => p.PayrollPeriodId == id && p.OrganizationId == orgId.Value)
            .ToListAsync();
        
        return Ok(payslips.Select(p => {
            double totalHours = p.TotalWorkedHours;
            if (totalHours == 0 && !string.IsNullOrWhiteSpace(p.CalculationDetailsJson))
            {
                try {
                    var jsonDoc = System.Text.Json.JsonDocument.Parse(p.CalculationDetailsJson);
                    if (jsonDoc.RootElement.TryGetProperty("TotalTrackedHours", out var hrsProp) && hrsProp.TryGetDouble(out var hrs))
                    {
                        totalHours = hrs;
                    }
                } catch { }
            }

            var assignedRole = p.AssignedRole;
            if (string.IsNullOrWhiteSpace(assignedRole) && p.Employee?.EmployeeRoles != null)
            {
                assignedRole = string.Join(", ", p.Employee.EmployeeRoles.Select(er => er.Role?.Name).Where(n => !string.IsNullOrWhiteSpace(n)));
            }

            return new {
                p.Id,
                p.EmployeeId,
                EmployeeName = p.Employee?.User != null ? $"{p.Employee.User.FirstName} {p.Employee.User.LastName}".Trim() : "Unknown",
                p.BasicPay,
                p.TotalLeavePay,
                p.TotalOvertimePay,
                p.GrossPay,
                p.SssDeduction,
                p.PhilHealthDeduction,
                p.PagIbigDeduction,
                p.TaxDeduction,
                p.AdvancesDeduction,
                p.OtherAllowances,
                p.OtherDeductions,
                p.NetPay,
                OrganizationName = p.Organization?.Name ?? "OfficeCore",
                OrganizationAddress = p.Organization?.Address,
                TotalWorkedHours = totalHours,
                AssignedRole = assignedRole,
                p.CalculationDetailsJson,
                PayrollPeriod = new {
                    p.PayrollPeriod?.StartDate,
                    p.PayrollPeriod?.EndDate
                }
            };
        }));
    }

    [HttpGet("periods/{id}/export/summary")]
    [Authorize(Policy = "Permission:payroll.export")]
    public async Task<IActionResult> ExportPayrollSummaryCsv(Guid id)
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Forbid();

        var period = await _context.PayrollPeriods.FirstOrDefaultAsync(p => p.Id == id && p.OrganizationId == orgId.Value);
        if (period == null) return NotFound();

        var payslips = await _context.Payslips
            .Include(p => p.Employee)
                .ThenInclude(e => e!.User)
            .Where(p => p.PayrollPeriodId == id && p.OrganizationId == orgId.Value)
            .ToListAsync();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Employee Name,Basic Pay,Leave Pay,Overtime Pay,Gross Pay,SSS,PhilHealth,Pag-IBIG,Tax,Advances,Other Allowances,Other Deductions,Net Pay");

        foreach (var p in payslips)
        {
            var empName = p.Employee?.User != null ? $"{p.Employee.User.FirstName} {p.Employee.User.LastName}".Trim() : "Unknown";
            empName = empName.Replace("\"", "\"\"");
            sb.AppendLine($"\"{empName}\",{p.BasicPay},{p.TotalLeavePay},{p.TotalOvertimePay},{p.GrossPay},{p.SssDeduction},{p.PhilHealthDeduction},{p.PagIbigDeduction},{p.TaxDeduction},{p.AdvancesDeduction},{p.OtherAllowances},{p.OtherDeductions},{p.NetPay}");
        }

        var fileName = $"PayrollSummary_{period.StartDate:yyyyMMdd}_{period.EndDate:yyyyMMdd}.csv";
        return File(System.Text.Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", fileName);
    }

    [HttpGet("payslips/{id}")]
    public async Task<IActionResult> GetPayslip(Guid id)
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Forbid();

        var payslip = await _context.Payslips
            .Include(p => p.PayrollPeriod)
            .Include(p => p.Organization)
            .Include(p => p.Employee)
                .ThenInclude(e => e!.User)
            .Include(p => p.Employee)
                .ThenInclude(e => e!.EmployeeRoles)
                    .ThenInclude(er => er.Role)
            .FirstOrDefaultAsync(p => p.Id == id && p.OrganizationId == orgId.Value);

        if (payslip == null) return NotFound();

        var callerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isOwner = await _tenant.IsCurrentUserWorkspaceOwnerAsync(orgId);
        if (!isOwner && payslip.Employee?.UserId != callerId && !User.HasClaim(PermissionCatalog.ClaimType, AccessPermissions.PayrollView) && !User.HasClaim("ModuleAccess", AccessModules.Payroll))
            return Forbid();
        double totalHours = payslip.TotalWorkedHours;
        if (totalHours == 0 && !string.IsNullOrWhiteSpace(payslip.CalculationDetailsJson))
        {
            try {
                var jsonDoc = System.Text.Json.JsonDocument.Parse(payslip.CalculationDetailsJson);
                if (jsonDoc.RootElement.TryGetProperty("TotalTrackedHours", out var hrsProp) && hrsProp.TryGetDouble(out var hrs))
                {
                    totalHours = hrs;
                }
            } catch { }
        }

        var assignedRole = payslip.AssignedRole;
        if (string.IsNullOrWhiteSpace(assignedRole) && payslip.Employee?.EmployeeRoles != null)
        {
            assignedRole = string.Join(", ", payslip.Employee.EmployeeRoles.Select(er => er.Role?.Name).Where(n => !string.IsNullOrWhiteSpace(n)));
        }

        return Ok(new
        {
            payslip.Id,
            payslip.EmployeeId,
            EmployeeName = payslip.Employee?.User != null ? $"{payslip.Employee.User.FirstName} {payslip.Employee.User.LastName}".Trim() : "Unknown",
            OrganizationName = payslip.Organization?.Name ?? "OfficeCore",
            OrganizationAddress = payslip.Organization?.Address,
            TotalWorkedHours = totalHours,
            AssignedRole = assignedRole,
            payslip.BasicPay,
            payslip.TotalLeavePay,
            payslip.TotalOvertimePay,
            payslip.GrossPay,
            payslip.SssDeduction,
            payslip.PhilHealthDeduction,
            payslip.PagIbigDeduction,
            payslip.TaxDeduction,
            payslip.AdvancesDeduction,
            payslip.OtherAllowances,
            payslip.OtherDeductions,
            payslip.NetPay,
            payslip.CalculationDetailsJson,
            PayrollPeriod = new {
                payslip.PayrollPeriod?.StartDate,
                payslip.PayrollPeriod?.EndDate
            }
        });
    }

    [HttpGet("my-payslips")]
    [Authorize]
    public async Task<IActionResult> GetMyPayslips()
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Forbid();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Unauthorized();

        var employee = await _context.Employees.FirstOrDefaultAsync(e => e.UserId == userId && e.OrganizationId == orgId.Value && e.IsActive);
        if (employee == null) return NotFound("Employee profile not found");

        var payslips = await _context.Payslips
            .Include(p => p.PayrollPeriod)
            .Where(p => p.EmployeeId == employee.Id && p.OrganizationId == orgId.Value)
            .OrderByDescending(p => p.PayrollPeriod!.EndDate)
            .Select(p => new
            {
                p.Id,
                PayrollPeriodStart = p.PayrollPeriod!.StartDate,
                PayrollPeriodEnd = p.PayrollPeriod!.EndDate,
                p.GrossPay,
                p.NetPay
            })
            .ToListAsync();

        return Ok(payslips);
    }

    [HttpPut("payslips/{id}")]
    [Authorize(Policy = "Permission:payroll.calculate")]
    public async Task<IActionResult> UpdatePayslip(Guid id, [FromBody] PayslipUpdateRequest request)
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Forbid();

        var payslip = await _context.Payslips
            .Include(p => p.PayrollPeriod)
            .Include(p => p.Employee)
                .ThenInclude(e => e!.User)
            .FirstOrDefaultAsync(p => p.Id == id && p.OrganizationId == orgId.Value);
        if (payslip == null) return NotFound();
        if (payslip.PayrollPeriod?.Status == PayrollStatus.Finalized || payslip.PayrollPeriod?.Status == PayrollStatus.Paid)
            return BadRequest("Cannot update a finalized payslip.");

        var oldState = new
        {
            payslip.OtherAllowances,
            payslip.OtherDeductions,
            payslip.NetPay
        };

        payslip.OtherAllowances = request.OtherAllowances;
        payslip.OtherDeductions = request.OtherDeductions;
        
        payslip.NetPay = payslip.GrossPay 
            - payslip.SssDeduction 
            - payslip.PhilHealthDeduction 
            - payslip.PagIbigDeduction 
            - payslip.TaxDeduction 
            - payslip.AdvancesDeduction 
            - payslip.LateUndertimeDeduction
            + payslip.OtherAllowances
            - payslip.OtherDeductions;

        await _context.SaveChangesAsync();

        var newState = new
        {
            payslip.OtherAllowances,
            payslip.OtherDeductions,
            payslip.NetPay
        };

        var empName = $"{payslip.Employee?.User?.FirstName} {payslip.Employee?.User?.LastName}".Trim();
        await _audit.LogAsync(
            AuditActions.Update,
            AuditModules.Payroll,
            payslip.Id.ToString(),
            $"Modified payslip allowances/deductions for employee {empName} (Net Pay adjusted: ₱{oldState.NetPay:N2} -> ₱{newState.NetPay:N2})",
            oldState,
            newState);

        return Ok();
    }

    [HttpPost("periods/{id}/finalize")]
    [Authorize(Policy = "Permission:payroll.finalize")]
    public async Task<IActionResult> FinalizePeriod(Guid id)
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Forbid();

        var period = await _context.PayrollPeriods.FirstOrDefaultAsync(p => p.Id == id && p.OrganizationId == orgId.Value);
        if (period == null) return NotFound();

        if (period.Status == PayrollStatus.Finalized || period.Status == PayrollStatus.Paid)
            return BadRequest("Period is already finalized");

        period.Status = PayrollStatus.Finalized;
        period.FinalizedAt = DateTime.UtcNow;

        // Deduct remaining balance from active advances
        var payslips = await _context.Payslips.Where(p => p.PayrollPeriodId == id && p.OrganizationId == orgId.Value && p.AdvancesDeduction > 0).ToListAsync();
        foreach (var payslip in payslips)
        {
            var activeAdvances = await _context.AdvanceRequests
                .Where(ar => ar.EmployeeId == payslip.EmployeeId && ar.OrganizationId == orgId.Value && ar.Status == AdvanceStatus.Approved && (ar.RemainingBalance == null || ar.RemainingBalance > 0))
                .OrderBy(ar => ar.RequestDate)
                .ToListAsync();

            decimal deductionToApply = payslip.AdvancesDeduction;
            foreach (var advance in activeAdvances)
            {
                if (deductionToApply <= 0) break;
                
                decimal deduction = 0m;
                if (advance.Type == AdvanceType.ELoan)
                {
                    // Delay E-Loan deduction to periods that start after approval
                    if (period.StartDate > DateOnly.FromDateTime(advance.ResolutionDate ?? advance.RequestDate))
                    {
                        deduction = (advance.InstallmentAmount ?? 0m) / 2m; // Assuming 1 cutoff deduction
                    }
                    else
                    {
                        continue;
                    }
                }
                else
                {
                    deduction = advance.RemainingBalance ?? advance.RequestedAmount;
                }
                
                if (deduction > advance.RemainingBalance) deduction = advance.RemainingBalance ?? 0m;
                
                if (deduction > deductionToApply) deduction = deductionToApply;

                advance.RemainingBalance -= deduction;
                deductionToApply -= deduction;
            }
        }

        await _context.SaveChangesAsync();

        var totalNetPay = await _context.Payslips.Where(p => p.PayrollPeriodId == id && p.OrganizationId == orgId.Value).SumAsync(p => p.NetPay);
        var payslipCount = await _context.Payslips.CountAsync(p => p.PayrollPeriodId == id && p.OrganizationId == orgId.Value);
        await _audit.LogPayrollAsync(AuditActions.Approve, $"Finalized and locked payroll period {period.StartDate:yyyy-MM-dd} to {period.EndDate:yyyy-MM-dd} ({payslipCount} employees, Total Net Payout: ₱{totalNetPay:N2})", id);

        return Ok();
    }
}

public class PayslipUpdateRequest
{
    public decimal OtherAllowances { get; set; }
    public decimal OtherDeductions { get; set; }
}

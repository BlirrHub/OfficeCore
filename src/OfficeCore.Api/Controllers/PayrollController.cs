using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Models;
using OfficeCore.Api.Services;

namespace OfficeCore.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Policy = "RequirePayroll")]
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
    public async Task<IActionResult> GetPayrollPeriods()
    {
        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Forbid();

        var periods = await _context.PayrollPeriods
            .Where(p => !p.IsArchived && p.OrganizationId == orgId)
            .OrderByDescending(p => p.StartDate)
            .ToListAsync();
        return Ok(periods);
    }

    [HttpDelete("periods/{id}")]
    public async Task<IActionResult> DeletePayrollPeriod(Guid id)
    {
        var orgId = _tenant.GetActiveOrganizationId();
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

    [HttpPost("periods")]
    public async Task<IActionResult> CreatePayrollPeriod(PayrollPeriod period)
    {
        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Forbid();

        period.Id = Guid.NewGuid();
        period.Status = PayrollStatus.Draft;
        period.CreatedAt = DateTime.UtcNow;
        period.OrganizationId = orgId.Value;

        _context.PayrollPeriods.Add(period);
        await _context.SaveChangesAsync();

        await _audit.LogPayrollAsync(AuditActions.Create, $"Created draft payroll period {period.StartDate:yyyy-MM-dd} to {period.EndDate:yyyy-MM-dd}", period.Id);

        return Ok(period);
    }
    
    [HttpGet("periods/{id}")]
    public async Task<IActionResult> GetPayrollPeriod(Guid id)
    {
        var orgId = _tenant.GetActiveOrganizationId();
        var period = await _context.PayrollPeriods.FirstOrDefaultAsync(p => p.Id == id && p.OrganizationId == orgId);
        if (period == null) return NotFound();
        return Ok(period);
    }

    [HttpPost("periods/{id}/compute")]
    public async Task<IActionResult> ComputePayroll(Guid id)
    {
        var orgId = _tenant.GetActiveOrganizationId();
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
            return BadRequest(new { Message = ex.ToString() });
        }
    }

    [HttpGet("periods/{id}/payslips")]
    public async Task<IActionResult> GetPayslips(Guid id)
    {
        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Forbid();

        var periodExists = await _context.PayrollPeriods.AnyAsync(p => p.Id == id && p.OrganizationId == orgId.Value);
        if (!periodExists) return NotFound();

        var payslips = await _context.Payslips
            .Include(p => p.Employee)
                .ThenInclude(e => e!.User)
            .Where(p => p.PayrollPeriodId == id && p.OrganizationId == orgId.Value)
            .ToListAsync();
        
        return Ok(payslips.Select(p => new {
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
            p.CalculationDetailsJson
        }));
    }

    [HttpPut("payslips/{id}")]
    public async Task<IActionResult> UpdatePayslip(Guid id, [FromBody] PayslipUpdateRequest request)
    {
        var orgId = _tenant.GetActiveOrganizationId();
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
    public async Task<IActionResult> FinalizePeriod(Guid id)
    {
        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Forbid();

        var period = await _context.PayrollPeriods.FirstOrDefaultAsync(p => p.Id == id && p.OrganizationId == orgId.Value);
        if (period == null) return NotFound();

        if (period.Status == PayrollStatus.Finalized)
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

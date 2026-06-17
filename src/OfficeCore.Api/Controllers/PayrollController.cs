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

    public PayrollController(AppDbContext context, IPayrollComputationService computationService)
    {
        _context = context;
        _computationService = computationService;
    }

    [HttpGet("periods")]
    public async Task<IActionResult> GetPayrollPeriods()
    {
        var periods = await _context.PayrollPeriods
            .Where(p => !p.IsArchived)
            .OrderByDescending(p => p.StartDate)
            .ToListAsync();
        return Ok(periods);
    }

    [HttpDelete("periods/{id}")]
    public async Task<IActionResult> DeletePayrollPeriod(Guid id)
    {
        var period = await _context.PayrollPeriods.FindAsync(id);
        if (period == null) return NotFound();

        if (period.Status == PayrollStatus.Finalized || period.Status == PayrollStatus.Paid)
        {
            return BadRequest("Cannot delete a finalized or paid payroll period.");
        }

        period.IsArchived = true;
        await _context.SaveChangesAsync();
        
        return Ok();
    }

    [HttpPost("periods")]
    public async Task<IActionResult> CreatePayrollPeriod(PayrollPeriod period)
    {
        period.Id = Guid.NewGuid();
        period.Status = PayrollStatus.Draft;
        period.CreatedAt = DateTime.UtcNow;

        _context.PayrollPeriods.Add(period);
        await _context.SaveChangesAsync();

        return Ok(period);
    }
    
    [HttpGet("periods/{id}")]
    public async Task<IActionResult> GetPayrollPeriod(Guid id)
    {
        var period = await _context.PayrollPeriods.FindAsync(id);
        if (period == null) return NotFound();
        return Ok(period);
    }

    [HttpPost("periods/{id}/compute")]
    public async Task<IActionResult> ComputePayroll(Guid id)
    {
        try
        {
            await _computationService.ComputePayrollPeriodAsync(id);
            return Ok();
        }
        catch (Exception ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    [HttpGet("periods/{id}/payslips")]
    public async Task<IActionResult> GetPayslips(Guid id)
    {
        var payslips = await _context.Payslips
            .Include(p => p.Employee)
            .Where(p => p.PayrollPeriodId == id)
            .ToListAsync();
        
        return Ok(payslips.Select(p => new {
            p.Id,
            p.EmployeeId,
            EmployeeName = $"{p.Employee?.FirstName} {p.Employee?.LastName}",
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
        var payslip = await _context.Payslips.Include(p => p.PayrollPeriod).FirstOrDefaultAsync(p => p.Id == id);
        if (payslip == null) return NotFound();
        if (payslip.PayrollPeriod?.Status == PayrollStatus.Finalized || payslip.PayrollPeriod?.Status == PayrollStatus.Paid)
            return BadRequest("Cannot update a finalized payslip.");

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
        return Ok();
    }

    [HttpPost("periods/{id}/finalize")]
    public async Task<IActionResult> FinalizePeriod(Guid id)
    {
        var period = await _context.PayrollPeriods.FindAsync(id);
        if (period == null) return NotFound();

        if (period.Status == PayrollStatus.Finalized)
            return BadRequest("Period is already finalized");

        period.Status = PayrollStatus.Finalized;
        period.FinalizedAt = DateTime.UtcNow;

        // Deduct remaining balance from active advances
        var payslips = await _context.Payslips.Where(p => p.PayrollPeriodId == id && p.AdvancesDeduction > 0).ToListAsync();
        foreach (var payslip in payslips)
        {
            var activeAdvances = await _context.AdvanceRequests
                .Where(ar => ar.EmployeeId == payslip.EmployeeId && ar.Status == AdvanceStatus.Approved && ar.RemainingBalance > 0)
                .ToListAsync();

            decimal deductionToApply = payslip.AdvancesDeduction;
            foreach (var advance in activeAdvances)
            {
                if (deductionToApply <= 0) break;
                
                decimal deduction = (advance.InstallmentAmount ?? 0m) / 2m; // Assuming 1 cutoff deduction
                if (deduction > advance.RemainingBalance) deduction = advance.RemainingBalance ?? 0m;
                
                if (deduction > deductionToApply) deduction = deductionToApply;

                advance.RemainingBalance -= deduction;
                deductionToApply -= deduction;

                // If balance is 0, we can just leave it or mark as a custom status if needed. 
                // Since there's no Paid status, we rely on RemainingBalance == 0.
            }
        }

        await _context.SaveChangesAsync();
        return Ok();
    }
}

public class PayslipUpdateRequest
{
    public decimal OtherAllowances { get; set; }
    public decimal OtherDeductions { get; set; }
}

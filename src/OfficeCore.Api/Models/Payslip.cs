using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OfficeCore.Api.Models;

public class Payslip
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PayrollPeriodId { get; set; }
    
    [ForeignKey(nameof(PayrollPeriodId))]
    public PayrollPeriod? PayrollPeriod { get; set; }

    public string EmployeeId { get; set; } = string.Empty;
    
    [ForeignKey(nameof(EmployeeId))]
    public Employee? Employee { get; set; }

    // Summary fields for UI display
    public decimal BasicPay { get; set; }
    public decimal TotalOvertimePay { get; set; }
    public decimal TotalLeavePay { get; set; }
    public decimal TotalHolidayPay { get; set; }
    
    public decimal GrossPay { get; set; }

    // Deductions
    public decimal SssDeduction { get; set; }
    public decimal PhilHealthDeduction { get; set; }
    public decimal PagIbigDeduction { get; set; }
    public decimal TaxDeduction { get; set; }
    public decimal LateUndertimeDeduction { get; set; }
    public decimal AdvancesDeduction { get; set; }

    // Manual Overrides / Adjustments
    public decimal OtherAllowances { get; set; }
    public decimal OtherDeductions { get; set; }

    public decimal NetPay { get; set; }

    // JSON blob to store the exact mathematical breakdown for historical auditing
    public string? CalculationDetailsJson { get; set; }
}

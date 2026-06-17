using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OfficeCore.Api.Models;

public enum PayrollStatus
{
    Draft,
    Computed,
    Finalized,
    Paid
}

public class PayrollPeriod
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateOnly StartDate { get; set; }
    
    public DateOnly EndDate { get; set; }
    

    public PayrollStatus Status { get; set; } = PayrollStatus.Draft;

    public string? Remarks { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? FinalizedAt { get; set; }
}

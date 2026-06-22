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

public class PayrollPeriod : ITenantEntity{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateOnly StartDate { get; set; }
    
    public DateOnly EndDate { get; set; }
    

    public PayrollStatus Status { get; set; } = PayrollStatus.Draft;
    
    public bool IsArchived { get; set; }

    public string? Remarks { get; set; }

    public decimal DeductionMultiplier { get; set; } = 1.0m;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? FinalizedAt { get; set; }

    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }
}

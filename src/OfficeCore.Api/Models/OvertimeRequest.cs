using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OfficeCore.Api.Models;

public enum OvertimeType
{
    PreOvertime = 0,
    PostOvertime = 1
}

public enum OvertimeStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Cancelled = 3
}

public class OvertimeRequest : ITenantEntity
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public required string EmployeeId { get; set; }
    
    [ForeignKey(nameof(EmployeeId))]
    public Employee? Employee { get; set; }

    /// <summary>
    /// The date of the shift/workday this overtime applies to.
    /// </summary>
    public DateTime Date { get; set; }

    public OvertimeType Type { get; set; }

    public TimeSpan StartTime { get; set; }

    public TimeSpan EndTime { get; set; }

    public double RequestedHours { get; set; }

    public double? ApprovedHours { get; set; }

    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;

    public OvertimeStatus Status { get; set; } = OvertimeStatus.Pending;

    public string? ReviewedByUserId { get; set; }
    
    [ForeignKey(nameof(ReviewedByUserId))]
    public ApplicationUser? ReviewedByUser { get; set; }

    public DateTime? ActionTakenAt { get; set; }

    [MaxLength(500)]
    public string? RejectionReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Guid OrganizationId { get; set; }
    
    [ForeignKey(nameof(OrganizationId))]
    public Organization? Organization { get; set; }
}

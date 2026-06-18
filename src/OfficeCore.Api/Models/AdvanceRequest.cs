using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OfficeCore.Api.Models;

public enum AdvanceType
{
    CashAdvance,
    ELoan
}

public enum AdvanceStatus
{
    Pending,
    Approved,
    Rejected,
    Cancelled
}

public class AdvanceRequest : ITenantEntity{
    public Guid Id { get; set; }

    [Required]
    public required string EmployeeId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public Employee? Employee { get; set; }

    public AdvanceType Type { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal RequestedAmount { get; set; }

    [MaxLength(500)]
    public string Note { get; set; } = string.Empty;

    public AdvanceStatus Status { get; set; } = AdvanceStatus.Pending;

    public DateTime RequestDate { get; set; }

    public DateTime? ResolutionDate { get; set; }

    // For ELoans
    [Column(TypeName = "decimal(18,2)")]
    public decimal? RemainingBalance { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? InstallmentAmount { get; set; }

    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }
}

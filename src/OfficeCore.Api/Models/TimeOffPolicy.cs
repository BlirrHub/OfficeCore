using System;
using System.ComponentModel.DataAnnotations;

namespace OfficeCore.Api.Models;

public class TimeOffPolicy : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public LeaveType LeaveType { get; set; }

    public decimal DefaultDaysPerYear { get; set; } = 15;

    public bool IsPaid { get; set; } = true;

    public bool RequiresAttachment { get; set; } = false;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }
}

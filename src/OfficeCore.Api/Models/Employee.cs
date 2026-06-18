using System;

namespace OfficeCore.Api.Models;

public class Employee : ITenantEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    // Links to the global ApplicationUser
    public required string UserId { get; set; }
    public ApplicationUser? User { get; set; }
    
    // Links to the Organization
    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }
    
    // Tenant-specific properties
    public string? EmployeeId { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsEmployee { get; set; } = true;
    public decimal? DailyRate { get; set; }
    public bool UseCustomDailyRate { get; set; } = false;
    public DateTime? DateHired { get; set; }
    public DateTime? ResignedDate { get; set; }
    public bool IsEligibleForLeave { get; set; } = true;

    // Work schedule assignment
    public Guid? WorkScheduleId { get; set; }
    public WorkSchedule? WorkSchedule { get; set; }

    // Government Records
    public EmployeeGovernmentRecord? GovernmentRecord { get; set; }
}

using Microsoft.AspNetCore.Identity;

namespace OfficeCore.Api.Models;

public class Employee : IdentityUser
{
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    
    // Optional internal company ID
    public string? EmployeeId { get; set; }
    // Profile image
    public string? ProfilePictureUrl { get; set; }
    
    // To soft-disable users
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; } = true;

    /// <summary>When true, this user is included in payroll processing and requires payroll settings.</summary>
    public bool IsEmployee { get; set; } = true;

    // Daily rate — used when UseCustomDailyRate is true; otherwise global minimum rate applies
    public decimal? DailyRate { get; set; }
    
    /// <summary>When true, DailyRate is used for payroll. When false, the global minimum rate is used.</summary>
    public bool UseCustomDailyRate { get; set; } = false;

    // Employment dates
    public DateTime? DateHired { get; set; }
    public DateTime? ResignedDate { get; set; }

    // Leave eligibility
    public bool IsEligibleForLeave { get; set; } = true;

    // Work schedule assignment
    public Guid? WorkScheduleId { get; set; }
    public WorkSchedule? WorkSchedule { get; set; }

    // Government Records
    public EmployeeGovernmentRecord? GovernmentRecord { get; set; }
}

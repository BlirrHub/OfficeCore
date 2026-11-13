namespace OfficeCore.Api.Models;

public class LeaveRequest : ITenantEntity{
    public Guid Id { get; set; }
    
    public required string EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    
    public LeaveType Type { get; set; }
    
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    
    public string Reason { get; set; } = string.Empty;
    public string? AttachmentPath { get; set; } // Optional, typically used for Sick Leave
    
    public LeaveRequestStatus Status { get; set; } = LeaveRequestStatus.Pending;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ActionTakenAt { get; set; }
    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }
}

public enum LeaveType
{
    Sick = 0,
    Vacation = 1,
    Emergency = 2,
    Maternity = 3,
    Other = 4
}

public enum LeaveRequestStatus
{
    Pending,
    Approved,
    Rejected,
    Cancelled
}

namespace OfficeCore.Api.Models;

public class LeaveRequest
{
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
}

public enum LeaveType
{
    Sick,
    Vacation
}

public enum LeaveRequestStatus
{
    Pending,
    Approved,
    Rejected
}

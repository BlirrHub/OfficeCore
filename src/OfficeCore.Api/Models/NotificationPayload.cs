using System;

namespace OfficeCore.Api.Models;

public class NotificationPayload
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Category { get; set; } = "System"; // Leave, Advance, Task, System
    public string? Status { get; set; } // Pending, Approved, Rejected, Assigned, DueSoon, Overdue
    public string? TargetUrl { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; } = false;
}

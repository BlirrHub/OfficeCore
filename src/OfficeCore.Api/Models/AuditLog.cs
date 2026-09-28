using System;

namespace OfficeCore.Api.Models;

public class AuditLog : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    public string? UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? UserEmail { get; set; }

    /// <summary>
    /// Action verb: CREATE, UPDATE, DELETE, APPROVE, REJECT, CALCULATE, RESTORE, SECURITY
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Domain entity category: Payroll, Employee, Attendance, Leave, Advance, Roles, Backup, Organization
    /// </summary>
    public string EntityName { get; set; } = string.Empty;

    /// <summary>
    /// Stringified identifier of the affected entity
    /// </summary>
    public string? EntityId { get; set; }

    /// <summary>
    /// Human-readable description of what changed or what operation was performed
    /// </summary>
    public string Details { get; set; } = string.Empty;

    /// <summary>
    /// Serialized JSON of modified fields prior to change
    /// </summary>
    public string? OldValues { get; set; }

    /// <summary>
    /// Serialized JSON of modified fields after change
    /// </summary>
    public string? NewValues { get; set; }

    /// <summary>
    /// Workstation or remote IP address
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// Client app signature or browser user agent
    /// </summary>
    public string? ClientInfo { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public static class AuditActions
{
    public const string Create = "CREATE";
    public const string Update = "UPDATE";
    public const string Delete = "DELETE";
    public const string Approve = "APPROVE";
    public const string Reject = "REJECT";
    public const string Calculate = "CALCULATE";
    public const string Restore = "RESTORE";
    public const string Security = "SECURITY";
}

public static class AuditModules
{
    public const string Payroll = "Payroll";
    public const string Employee = "Employee";
    public const string Attendance = "Attendance";
    public const string Leave = "Leave";
    public const string Advance = "Advance";
    public const string Roles = "Roles";
    public const string Organization = "Organization";
    public const string Backup = "Backup";
}

namespace OfficeCore.Api.Models;

/// <summary>
/// Stores global single-value payroll configuration (e.g., minimum daily rate).
/// Uses a Key-Value pattern so new settings can be added without schema changes.
/// </summary>
public class GeneralPayrollSetting : ITenantEntity{
    public Guid Id { get; set; }
    public required string Key { get; set; }
    public decimal Value { get; set; }
    public string? Description { get; set; }

    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }
}

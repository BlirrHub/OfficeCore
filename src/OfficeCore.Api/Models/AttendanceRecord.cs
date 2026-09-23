using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OfficeCore.Api.Models;

public enum AttendanceType
{
    ClockIn,
    ClockOut
}

public enum AttendanceSource
{
    Device,
    ManualAdjustment,
    Web
}

public class AttendanceRecord : ITenantEntity{
    public Guid Id { get; set; }
    
    [Required]
    public string EmployeeId { get; set; } = string.Empty;
    
    [ForeignKey(nameof(EmployeeId))]
    public Employee? Employee { get; set; }
    
    public DateTimeOffset Timestamp { get; set; }
    
    public AttendanceType Type { get; set; }
    
    public AttendanceSource Source { get; set; }

    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }
}

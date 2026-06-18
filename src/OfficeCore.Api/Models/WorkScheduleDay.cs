using System.ComponentModel.DataAnnotations.Schema;

namespace OfficeCore.Api.Models;

public class WorkScheduleDay : ITenantEntity{
    public int Id { get; set; }
    
    public Guid WorkScheduleId { get; set; }
    
    [ForeignKey(nameof(WorkScheduleId))]
    [System.Text.Json.Serialization.JsonIgnore]
    public WorkSchedule? WorkSchedule { get; set; }
    
    public DayOfWeek DayOfWeek { get; set; }
    
    public bool IsRestDay { get; set; }
    
    public TimeSpan? StartTime { get; set; }
    
    public TimeSpan? EndTime { get; set; }
    
    public double? WorkingHours { get; set; }

    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }
}

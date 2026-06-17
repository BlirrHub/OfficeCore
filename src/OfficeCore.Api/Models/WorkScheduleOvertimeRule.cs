using System.Text.Json.Serialization;

namespace OfficeCore.Api.Models;

public enum OvertimeTriggerMode
{
    AfterHours,
    AfterTime
}

public class WorkScheduleOvertimeRule
{
    public int Id { get; set; }
    
    public Guid WorkScheduleId { get; set; }
    
    public OvertimeTriggerMode TriggerMode { get; set; } = OvertimeTriggerMode.AfterHours;
    
    /// <summary>
    /// If TriggerMode is AfterHours, overtime starts after working this many hours (e.g. 8.0).
    /// </summary>
    public double? AfterHours { get; set; }
    
    /// <summary>
    /// If TriggerMode is AfterTime, overtime starts after this time of day (e.g. 17:00 or 5 PM).
    /// </summary>
    public TimeSpan? AfterTime { get; set; }
    
    /// <summary>
    /// The multiplier for overtime hours (e.g. 1.5).
    /// </summary>
    public double Multiplier { get; set; } = 1.5;
    
    [JsonIgnore]
    public WorkSchedule? WorkSchedule { get; set; }
}

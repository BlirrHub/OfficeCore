using System.ComponentModel.DataAnnotations;

namespace OfficeCore.Api.Models;

public enum WorkArrangement
{
    Fixed,
    Flexible,
    Weekly
}

public class WorkSchedule
{
    public Guid Id { get; set; }
    
    [Required]
    [MaxLength(100)]
    public required string Name { get; set; }
    
    public WorkArrangement WorkArrangement { get; set; } = WorkArrangement.Fixed;
    
    public bool IncludeTimeTrackedBeforeScheduledStartTime { get; set; }
    
    public TimeSpan SplitTimesheetsAt { get; set; } = TimeSpan.Zero;
    
    public ICollection<WorkScheduleDay> Days { get; set; } = new List<WorkScheduleDay>();
}

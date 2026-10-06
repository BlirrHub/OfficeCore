using System.ComponentModel.DataAnnotations;

namespace OfficeCore.Api.Models;

public enum WorkArrangement
{
    Fixed,
    Flexible,
    Weekly,
    NoSchedule
}

public class WorkSchedule : ITenantEntity{
    public Guid Id { get; set; }
    
    [Required]
    [MaxLength(100)]
    public required string Name { get; set; }
    
    public WorkArrangement WorkArrangement { get; set; } = WorkArrangement.Fixed;
    
    public bool IsFixedWeeklyRate { get; set; } = false;
    
    public bool IncludeTimeTrackedBeforeScheduledStartTime { get; set; }
    
    public bool AutoClockOut { get; set; }
    
    public TimeSpan SplitTimesheetsAt { get; set; } = TimeSpan.Zero;
    
    public double? TotalWeeklyHours { get; set; }
    
    public int LateGracePeriodMinutes { get; set; } = 0;
    
    public int OvertimeThresholdMinutes { get; set; } = 15;
    
    public bool AutoNormalizeHalfDay { get; set; } = true;
    
    public bool AutoSubmitOvertimeApproval { get; set; } = true;
    
    public ICollection<WorkScheduleDay> Days { get; set; } = new List<WorkScheduleDay>();
    
    public ICollection<WorkScheduleBreak> Breaks { get; set; } = new List<WorkScheduleBreak>();
    
    public ICollection<WorkScheduleAutoDeduction> AutoDeductions { get; set; } = new List<WorkScheduleAutoDeduction>();
    
    public ICollection<WorkScheduleOvertimeRule> OvertimeRules { get; set; } = new List<WorkScheduleOvertimeRule>();

    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }
}

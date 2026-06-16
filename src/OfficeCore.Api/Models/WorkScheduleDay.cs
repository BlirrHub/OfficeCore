using System.ComponentModel.DataAnnotations.Schema;

namespace OfficeCore.Api.Models;

public class WorkScheduleDay
{
    public int Id { get; set; }
    
    public Guid WorkScheduleId { get; set; }
    
    [ForeignKey(nameof(WorkScheduleId))]
    public WorkSchedule? WorkSchedule { get; set; }
    
    public DayOfWeek DayOfWeek { get; set; }
    
    public bool IsRestDay { get; set; }
    
    public TimeSpan? StartTime { get; set; }
    
    public TimeSpan? EndTime { get; set; }
}

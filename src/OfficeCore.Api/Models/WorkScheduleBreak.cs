using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace OfficeCore.Api.Models;

public class WorkScheduleBreak
{
    public int Id { get; set; }
    
    public Guid WorkScheduleId { get; set; }
    
    [ForeignKey(nameof(WorkScheduleId))]
    [JsonIgnore]
    public WorkSchedule? WorkSchedule { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    public bool IsPaid { get; set; }
    
    public TimeSpan StartTime { get; set; }
    
    public TimeSpan EndTime { get; set; }
}

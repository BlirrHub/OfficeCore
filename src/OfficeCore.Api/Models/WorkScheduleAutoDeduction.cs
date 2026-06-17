using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace OfficeCore.Api.Models;

public class WorkScheduleAutoDeduction
{
    public int Id { get; set; }
    
    public Guid WorkScheduleId { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// The number of hours to deduct (e.g. 1.0 for 1 hour lunch).
    /// </summary>
    public double DeductHours { get; set; }
    
    /// <summary>
    /// The deduction applies after the employee works this many hours (e.g. after 4.0 hours).
    /// </summary>
    public double AfterWorkHours { get; set; }
    
    [JsonIgnore]
    public WorkSchedule? WorkSchedule { get; set; }
}

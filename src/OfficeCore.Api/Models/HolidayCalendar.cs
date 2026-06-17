using System.ComponentModel.DataAnnotations;

namespace OfficeCore.Api.Models;

public class HolidayCalendar
{
    public Guid Id { get; set; }
    
    [Required]
    [MaxLength(100)]
    public required string Name { get; set; }
    
    public bool IsDefault { get; set; }
    
    public ICollection<Holiday> Holidays { get; set; } = new List<Holiday>();
}

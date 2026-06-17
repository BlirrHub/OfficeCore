using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace OfficeCore.Api.Models;

public class Holiday
{
    public int Id { get; set; }
    
    public Guid HolidayCalendarId { get; set; }
    
    [ForeignKey(nameof(HolidayCalendarId))]
    [JsonIgnore]
    public HolidayCalendar? HolidayCalendar { get; set; }
    
    [Required]
    [MaxLength(100)]
    public required string Name { get; set; }
    
    public DateTime Date { get; set; }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OfficeCore.Api.Models;

public class EmployeeFingerprint
{
    public int Id { get; set; }
    
    [Required]
    public string EmployeeId { get; set; } = string.Empty;
    
    [ForeignKey(nameof(EmployeeId))]
    public Employee? Employee { get; set; }
    
    [Required]
    public int FingerprintId { get; set; }
    
    public DateTimeOffset EnrolledAt { get; set; } = DateTimeOffset.UtcNow;
}

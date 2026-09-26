using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OfficeCore.Api.Models;

public class BoardCardAssignee : ITenantEntity
{
    public Guid CardId { get; set; }
    
    [ForeignKey("CardId")]
    public BoardCard Card { get; set; } = null!;

    [Required]
    public string EmployeeId { get; set; } = string.Empty;
    
    [ForeignKey("EmployeeId")]
    public Employee Employee { get; set; } = null!;

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }
}

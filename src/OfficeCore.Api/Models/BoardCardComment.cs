using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OfficeCore.Api.Models;

public class BoardCardComment : ITenantEntity{
    [Key]
    public Guid Id { get; set; }

    public Guid CardId { get; set; }
    
    [ForeignKey("CardId")]
    public BoardCard Card { get; set; } = null!;

    [Required]
    public string UserId { get; set; } = string.Empty;
    
    [ForeignKey("UserId")]
    public Employee User { get; set; } = null!;

    [Required]
    [MaxLength(2000)]
    public string Text { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }
}

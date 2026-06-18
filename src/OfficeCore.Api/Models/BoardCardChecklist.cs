using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OfficeCore.Api.Models;

public class BoardCardChecklist : ITenantEntity{
    [Key]
    public Guid Id { get; set; }

    public Guid CardId { get; set; }
    
    [ForeignKey("CardId")]
    public BoardCard Card { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string Title { get; set; } = string.Empty;

    public List<BoardCardChecklistItem> Items { get; set; } = new();

    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }
}

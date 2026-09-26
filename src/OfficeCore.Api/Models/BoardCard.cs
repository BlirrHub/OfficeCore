using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OfficeCore.Api.Models;

public class BoardCard : ITenantEntity{
    [Key]
    public Guid Id { get; set; }

    public Guid ListId { get; set; }
    
    [ForeignKey("ListId")]
    public BoardList List { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string? Description { get; set; }

    public int OrderIndex { get; set; }

    public string? AssignedToUserId { get; set; }
    
    [ForeignKey("AssignedToUserId")]
    public Employee? AssignedToUser { get; set; }

    public DateTime? DueDate { get; set; }

    public List<BoardCardLabel> Labels { get; set; } = new();
    public List<BoardCardAssignee> Assignees { get; set; } = new();
    public List<BoardCardComment> Comments { get; set; } = new();
    public List<BoardCardChecklist> Checklists { get; set; } = new();
    public List<BoardCardAttachment> Attachments { get; set; } = new();

    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }
}

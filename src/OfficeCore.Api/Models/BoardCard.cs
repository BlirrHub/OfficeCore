using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OfficeCore.Api.Models;

public class BoardCard
{
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
}

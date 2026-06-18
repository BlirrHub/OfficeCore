using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OfficeCore.Api.Models;

public class BoardCardChecklistItem
{
    [Key]
    public Guid Id { get; set; }

    public Guid ChecklistId { get; set; }
    
    [ForeignKey("ChecklistId")]
    public BoardCardChecklist Checklist { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string Text { get; set; } = string.Empty;

    public bool IsCompleted { get; set; }
}

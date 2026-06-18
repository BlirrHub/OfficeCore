using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OfficeCore.Api.Models;

public class Board
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }

    public string CreatedById { get; set; } = string.Empty;
    
    [ForeignKey("CreatedById")]
    public Employee CreatedBy { get; set; } = null!;

    public List<BoardMember> Members { get; set; } = new();
    public List<BoardList> Lists { get; set; } = new();
    public List<BoardLabel> Labels { get; set; } = new();
}

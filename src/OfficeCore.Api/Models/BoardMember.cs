using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OfficeCore.Api.Models;

public enum BoardRole
{
    Viewer = 0,
    Member = 1,
    Owner = 2
}

public class BoardMember
{
    [Key]
    public Guid Id { get; set; }

    public Guid BoardId { get; set; }
    
    [ForeignKey("BoardId")]
    public Board Board { get; set; } = null!;

    [Required]
    public string UserId { get; set; } = string.Empty;
    
    [ForeignKey("UserId")]
    public Employee User { get; set; } = null!;

    public BoardRole Role { get; set; }
}

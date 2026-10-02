using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OfficeCore.Api.Models;

public enum BoardRole
{
    Viewer = 0,
    Member = 1,
    Owner = 2,
    Admin = 3
}

public class BoardMember : ITenantEntity{
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

    public bool IsStarred { get; set; }

    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OfficeCore.Api.Models;

public class BoardList : ITenantEntity{
    [Key]
    public Guid Id { get; set; }

    public Guid BoardId { get; set; }
    
    [ForeignKey("BoardId")]
    public Board Board { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public int OrderIndex { get; set; }

    [MaxLength(7)]
    public string? ColorHex { get; set; }

    public List<BoardCard> Cards { get; set; } = new();

    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }
}

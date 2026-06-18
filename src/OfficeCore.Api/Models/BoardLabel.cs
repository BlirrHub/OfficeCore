using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OfficeCore.Api.Models;

public class BoardLabel : ITenantEntity{
    [Key]
    public Guid Id { get; set; }

    public Guid BoardId { get; set; }
    
    [ForeignKey("BoardId")]
    public Board Board { get; set; } = null!;

    [Required]
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string ColorHex { get; set; } = "#71717a"; // Default zinc-500

    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }
}

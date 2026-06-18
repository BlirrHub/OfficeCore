using System.ComponentModel.DataAnnotations.Schema;

namespace OfficeCore.Api.Models;

public class BoardCardLabel
{
    public Guid CardId { get; set; }
    
    [ForeignKey("CardId")]
    public BoardCard Card { get; set; } = null!;

    public Guid LabelId { get; set; }
    
    [ForeignKey("LabelId")]
    public BoardLabel Label { get; set; } = null!;
}

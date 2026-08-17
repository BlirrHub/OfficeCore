using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace OfficeCore.Api.Models;

public class OrganizationPhone
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    
    [Required]
    public required string Number { get; set; }
    
    [Required]
    public required string Type { get; set; } // "Mobile" or "Telephone"

    [JsonIgnore]
    public Organization? Organization { get; set; }
}

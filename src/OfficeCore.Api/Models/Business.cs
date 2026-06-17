using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace OfficeCore.Api.Models;

public class Business
{
    public Guid Id { get; set; }
    
    public Guid ClientId { get; set; }
    
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string? Address { get; set; }
    
    [Phone]
    [MaxLength(50)]
    public string? PhoneNumber { get; set; }
    
    public bool IsVatRegistered { get; set; }
    
    // Navigation property
    [JsonIgnore]
    public Client? Client { get; set; }
}

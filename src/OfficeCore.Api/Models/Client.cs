using System.ComponentModel.DataAnnotations;

namespace OfficeCore.Api.Models;

public class Client
{
    public Guid Id { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;
    
    [EmailAddress]
    [MaxLength(100)]
    public string? Email { get; set; }
    
    [Phone]
    [MaxLength(50)]
    public string? PhoneNumber { get; set; }
    
    public DateTime DateAdded { get; set; } = DateTime.UtcNow;
    
    public bool IsActive { get; set; } = true;
    
    // Navigation property
    public ICollection<Business> Businesses { get; set; } = new List<Business>();
}

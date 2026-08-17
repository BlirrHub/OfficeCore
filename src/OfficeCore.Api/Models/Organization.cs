namespace OfficeCore.Api.Models;

public class Organization
{
    public Guid Id { get; set; }
    
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string? LogoUrl { get; set; }
    public string? Address { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedByUserId { get; set; }
    public ApplicationUser? CreatedBy { get; set; }

    // Navigation property for employees belonging to this organization
    public ICollection<Employee> Employees { get; set; } = new List<Employee>();
    
    // Additional contact details
    public ICollection<OrganizationPhone> Phones { get; set; } = new List<OrganizationPhone>();
}

using Microsoft.AspNetCore.Identity;

namespace OfficeCore.Api.Models;

public class ApplicationUser : IdentityUser
{
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    
    // Profile image
    public string? ProfilePictureUrl { get; set; }
    
    public bool MustChangePassword { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public bool IsEmployee { get; set; } = true;
    public DateTime? ResignedDate { get; set; }
    
    // Navigation property for employee records (can belong to multiple organizations)
    public ICollection<Employee> EmployeeProfiles { get; set; } = new List<Employee>();
}

using Microsoft.AspNetCore.Identity;

namespace OfficeCore.Api.Models;

public class Employee : IdentityUser
{
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    
    // Optional internal company ID
    public string? EmployeeId { get; set; }
    
    // To soft-disable users
    public bool IsActive { get; set; } = true;
}

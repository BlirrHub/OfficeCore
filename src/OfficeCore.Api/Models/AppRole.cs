using Microsoft.AspNetCore.Identity;

namespace OfficeCore.Api.Models;

public class AppRole : IdentityRole
{
    public string? Description { get; set; }
    public bool ShowAsTitle { get; set; }
    
    public AppRole() : base() { }
    
    public AppRole(string roleName) : base(roleName) { }
}

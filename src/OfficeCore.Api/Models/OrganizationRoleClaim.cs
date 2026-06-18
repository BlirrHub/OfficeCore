using System;

namespace OfficeCore.Api.Models;

public class OrganizationRoleClaim
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid OrganizationRoleId { get; set; }
    public OrganizationRole? Role { get; set; }
    
    public required string ClaimType { get; set; }
    public required string ClaimValue { get; set; }
}

using System;
using System.Collections.Generic;

namespace OfficeCore.Api.Models;

public class OrganizationRole : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }
    
    public required string Name { get; set; }
    public string? Description { get; set; }
    public bool ShowAsTitle { get; set; }
    
    public ICollection<OrganizationRoleClaim> Claims { get; set; } = new List<OrganizationRoleClaim>();
    public ICollection<EmployeeRole> EmployeeRoles { get; set; } = new List<EmployeeRole>();
}

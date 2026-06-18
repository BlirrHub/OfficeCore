using System;

namespace OfficeCore.Api.Models;

public class EmployeeRole
{
    public string EmployeeId { get; set; } = string.Empty;
    public Employee? Employee { get; set; }
    
    public Guid OrganizationRoleId { get; set; }
    public OrganizationRole? Role { get; set; }
}

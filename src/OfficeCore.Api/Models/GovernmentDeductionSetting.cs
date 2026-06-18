namespace OfficeCore.Api.Models;

public class GovernmentDeductionSetting : ITenantEntity{
    public Guid Id { get; set; }
    public required string AgencyName { get; set; } // e.g. "SSS", "PhilHealth", "Pag-IBIG"
    
    // Monthly deduction amounts
    public decimal EmployeeShare { get; set; }
    public decimal EmployerShare { get; set; }

    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }
}

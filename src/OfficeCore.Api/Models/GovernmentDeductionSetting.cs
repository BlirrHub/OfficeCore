namespace OfficeCore.Api.Models;

public class GovernmentDeductionSetting
{
    public Guid Id { get; set; }
    public required string AgencyName { get; set; } // e.g. "SSS", "PhilHealth", "Pag-IBIG"
    
    // Monthly deduction amounts
    public decimal EmployeeShare { get; set; }
    public decimal EmployerShare { get; set; }
}

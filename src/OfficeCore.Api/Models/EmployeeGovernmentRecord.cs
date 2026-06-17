namespace OfficeCore.Api.Models;

public class EmployeeGovernmentRecord
{
    public Guid Id { get; set; }
    
    // Foreign key to Employee
    public required string EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    
    // Eligibility
    public bool IsEligibleForDeductions { get; set; } = true;
    
    // Government IDs
    public string? SssNumber { get; set; }
    public string? PagIbigNumber { get; set; }
    public string? PhilHealthNumber { get; set; }
    public string? TinNumber { get; set; }
}

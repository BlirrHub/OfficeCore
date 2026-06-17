using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Models;

namespace OfficeCore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class PayrollSettingsController : ControllerBase
{
    private readonly AppDbContext _context;

    public PayrollSettingsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("deductions")]
    public async Task<IActionResult> GetGovernmentDeductionSettings()
    {
        var settings = await _context.GovernmentDeductionSettings.ToListAsync();
        
        // If settings are empty, we can create the default ones (SSS, PhilHealth, Pag-IBIG)
        if (!settings.Any())
        {
            settings = new List<GovernmentDeductionSetting>
            {
                new() { AgencyName = "SSS", EmployeeShare = 250m, EmployerShare = 510m },
                new() { AgencyName = "PhilHealth", EmployeeShare = 250m, EmployerShare = 200m },
                new() { AgencyName = "Pag-IBIG", EmployeeShare = 100m, EmployerShare = 100m }
            };
            _context.GovernmentDeductionSettings.AddRange(settings);
            await _context.SaveChangesAsync();
        }

        return Ok(settings);
    }

    [HttpPut("deductions")]
    public async Task<IActionResult> UpdateGovernmentDeductionSettings([FromBody] List<GovernmentDeductionSettingDto> updatedSettings)
    {
        foreach (var dto in updatedSettings)
        {
            var setting = await _context.GovernmentDeductionSettings.FindAsync(dto.Id);
            if (setting != null)
            {
                setting.EmployeeShare = dto.EmployeeShare;
                setting.EmployerShare = dto.EmployerShare;
            }
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = "Settings updated successfully" });
    }
}

public class GovernmentDeductionSettingDto
{
    public Guid Id { get; set; }
    public string AgencyName { get; set; } = string.Empty;
    public decimal EmployeeShare { get; set; }
    public decimal EmployerShare { get; set; }
}

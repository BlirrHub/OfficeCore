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

    // ─── Government Deductions ────────────────────────────────────────────────

    [HttpGet("deductions")]
    public async Task<IActionResult> GetGovernmentDeductionSettings()
    {
        var settings = await _context.GovernmentDeductionSettings.ToListAsync();

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

    // ─── General Settings (e.g. Minimum Daily Rate) ───────────────────────────

    private const string MinDailyRateKey = "MinimumDailyRate";

    [HttpGet("general")]
    [Authorize] // All authenticated users can read
    public async Task<IActionResult> GetGeneralSettings()
    {
        var setting = await _context.GeneralPayrollSettings
            .FirstOrDefaultAsync(s => s.Key == MinDailyRateKey);

        if (setting == null)
        {
            setting = new GeneralPayrollSetting
            {
                Key = MinDailyRateKey,
                Value = 0m,
                Description = "Default minimum daily rate used when employee has no custom rate."
            };
            _context.GeneralPayrollSettings.Add(setting);
            await _context.SaveChangesAsync();
        }

        return Ok(new GeneralSettingsDto { MinimumDailyRate = setting.Value });
    }

    [HttpPut("general")]
    public async Task<IActionResult> UpdateGeneralSettings([FromBody] GeneralSettingsDto dto)
    {
        var setting = await _context.GeneralPayrollSettings
            .FirstOrDefaultAsync(s => s.Key == MinDailyRateKey);

        if (setting == null)
        {
            setting = new GeneralPayrollSetting
            {
                Key = MinDailyRateKey,
                Description = "Default minimum daily rate used when employee has no custom rate."
            };
            _context.GeneralPayrollSettings.Add(setting);
        }

        setting.Value = dto.MinimumDailyRate;
        await _context.SaveChangesAsync();

        return Ok(new { message = "General settings updated successfully" });
    }
}

public class GovernmentDeductionSettingDto
{
    public Guid Id { get; set; }
    public string AgencyName { get; set; } = string.Empty;
    public decimal EmployeeShare { get; set; }
    public decimal EmployerShare { get; set; }
}

public class GeneralSettingsDto
{
    public decimal MinimumDailyRate { get; set; }
}

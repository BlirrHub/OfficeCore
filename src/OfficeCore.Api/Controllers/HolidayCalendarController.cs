using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Models;
using OfficeCore.Api.Services;

namespace OfficeCore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "RequireTimeOff")]
public class HolidayCalendarController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly TenantService _tenant;

    public HolidayCalendarController(AppDbContext context, TenantService tenant)
    {
        _context = context;
        _tenant = tenant;
    }

    public sealed class HolidayItemDto
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
        public DateTime Date { get; set; }
    }

    public sealed class HolidayCalendarRequest
    {
        public Guid Id { get; set; }
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
        public List<HolidayItemDto>? Holidays { get; set; }
    }

    [HttpGet]
    [Authorize(Policy = "Permission:time-off.view")]
    public async Task<IActionResult> GetCalendars()
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Forbid();

        var calendars = await _context.HolidayCalendars
            .Where(c => c.OrganizationId == orgId)
            .Include(c => c.Holidays)
            .ToListAsync();
            
        return Ok(calendars);
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "Permission:time-off.view")]
    public async Task<IActionResult> GetCalendar(Guid id)
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Forbid();
        var calendar = await _context.HolidayCalendars
            .Where(c => c.OrganizationId == orgId)
            .Include(c => c.Holidays)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (calendar == null)
            return NotFound();

        return Ok(calendar);
    }

    [HttpPost]
    [Authorize(Policy = "RequireTimeOff")]
    [Authorize(Policy = "Permission:time-off.manage")]
    public async Task<IActionResult> CreateCalendar([FromBody] HolidayCalendarRequest request)
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Forbid();

        var calendar = new HolidayCalendar
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            IsDefault = request.IsDefault,
            OrganizationId = orgId.Value
        };
        
        if (calendar.IsDefault)
        {
            var defaultCalendars = await _context.HolidayCalendars.Where(c => c.IsDefault && c.OrganizationId == orgId).ToListAsync();
            foreach (var defaultCalendar in defaultCalendars)
            {
                defaultCalendar.IsDefault = false;
            }
        }
        
        if (request.Holidays != null)
        {
            foreach (var h in request.Holidays)
            {
                calendar.Holidays.Add(new Holiday
                {
                    HolidayCalendarId = calendar.Id,
                    Name = h.Name.Trim(),
                    Date = DateTime.SpecifyKind(h.Date, DateTimeKind.Utc),
                    OrganizationId = orgId.Value
                });
            }
        }

        _context.HolidayCalendars.Add(calendar);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetCalendar), new { id = calendar.Id }, calendar);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "RequireTimeOff")]
    [Authorize(Policy = "Permission:time-off.manage")]
    public async Task<IActionResult> UpdateCalendar(Guid id, [FromBody] HolidayCalendarRequest updatedCalendar)
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Forbid();

        if (updatedCalendar.Id != Guid.Empty && id != updatedCalendar.Id)
            return BadRequest();

        var existingCalendar = await _context.HolidayCalendars
            .Where(c => c.OrganizationId == orgId)
            .Include(c => c.Holidays)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (existingCalendar == null)
            return NotFound();

        if (updatedCalendar.IsDefault && !existingCalendar.IsDefault)
        {
            var defaultCalendars = await _context.HolidayCalendars.Where(c => c.IsDefault && c.Id != id && c.OrganizationId == orgId).ToListAsync();
            foreach (var defaultCalendar in defaultCalendars)
            {
                defaultCalendar.IsDefault = false;
            }
        }

        existingCalendar.Name = updatedCalendar.Name.Trim();
        existingCalendar.IsDefault = updatedCalendar.IsDefault;

        _context.Holidays.RemoveRange(existingCalendar.Holidays);
        
        if (updatedCalendar.Holidays != null)
        {
            foreach (var h in updatedCalendar.Holidays)
            {
                existingCalendar.Holidays.Add(new Holiday
                {
                    HolidayCalendarId = id,
                    Name = h.Name.Trim(),
                    Date = DateTime.SpecifyKind(h.Date, DateTimeKind.Utc),
                    OrganizationId = orgId.Value
                });
            }
        }

        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "RequireTimeOff")]
    [Authorize(Policy = "Permission:time-off.manage")]
    public async Task<IActionResult> DeleteCalendar(Guid id)
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Forbid();
        var calendar = await _context.HolidayCalendars.FirstOrDefaultAsync(c => c.Id == id && c.OrganizationId == orgId);
        if (calendar == null)
            return NotFound();

        _context.HolidayCalendars.Remove(calendar);
        await _context.SaveChangesAsync();

        return NoContent();
    }
    
    // Import Endpoints
    [HttpGet("import/countries")]
    public IActionResult GetImportCountries()
    {
        return Ok(new[] 
        { 
            new { Id = "PH", Name = "Philippines" },
            new { Id = "US", Name = "United States" }
        });
    }

    [HttpGet("import/{countryCode}")]
    public IActionResult GetHolidaysForCountry(string countryCode, int year)
    {
        // Mock data for prototype
        var holidays = new List<Holiday>();
        
        if (countryCode == "PH")
        {
            holidays.Add(new Holiday { Name = "New Year's Day", Date = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc) });
            holidays.Add(new Holiday { Name = "Maundy Thursday", Date = new DateTime(year, 4, 2, 0, 0, 0, DateTimeKind.Utc) });
            holidays.Add(new Holiday { Name = "Good Friday", Date = new DateTime(year, 4, 3, 0, 0, 0, DateTimeKind.Utc) });
            holidays.Add(new Holiday { Name = "The Day of Valor", Date = new DateTime(year, 4, 9, 0, 0, 0, DateTimeKind.Utc) });
            holidays.Add(new Holiday { Name = "Labor Day", Date = new DateTime(year, 5, 1, 0, 0, 0, DateTimeKind.Utc) });
            holidays.Add(new Holiday { Name = "Independence Day", Date = new DateTime(year, 6, 12, 0, 0, 0, DateTimeKind.Utc) });
            holidays.Add(new Holiday { Name = "National Heroes Day", Date = new DateTime(year, 8, 31, 0, 0, 0, DateTimeKind.Utc) });
            holidays.Add(new Holiday { Name = "Bonifacio Day", Date = new DateTime(year, 11, 30, 0, 0, 0, DateTimeKind.Utc) });
            holidays.Add(new Holiday { Name = "Christmas Day", Date = new DateTime(year, 12, 25, 0, 0, 0, DateTimeKind.Utc) });
            holidays.Add(new Holiday { Name = "Rizal Day", Date = new DateTime(year, 12, 30, 0, 0, 0, DateTimeKind.Utc) });
        }
        else if (countryCode == "US")
        {
            holidays.Add(new Holiday { Name = "New Year's Day", Date = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc) });
            holidays.Add(new Holiday { Name = "Martin Luther King Jr. Day", Date = new DateTime(year, 1, 19, 0, 0, 0, DateTimeKind.Utc) });
            holidays.Add(new Holiday { Name = "Memorial Day", Date = new DateTime(year, 5, 25, 0, 0, 0, DateTimeKind.Utc) });
            holidays.Add(new Holiday { Name = "Juneteenth", Date = new DateTime(year, 6, 19, 0, 0, 0, DateTimeKind.Utc) });
            holidays.Add(new Holiday { Name = "Independence Day", Date = new DateTime(year, 7, 4, 0, 0, 0, DateTimeKind.Utc) });
            holidays.Add(new Holiday { Name = "Labor Day", Date = new DateTime(year, 9, 7, 0, 0, 0, DateTimeKind.Utc) });
            holidays.Add(new Holiday { Name = "Veterans Day", Date = new DateTime(year, 11, 11, 0, 0, 0, DateTimeKind.Utc) });
            holidays.Add(new Holiday { Name = "Thanksgiving Day", Date = new DateTime(year, 11, 26, 0, 0, 0, DateTimeKind.Utc) });
            holidays.Add(new Holiday { Name = "Christmas Day", Date = new DateTime(year, 12, 25, 0, 0, 0, DateTimeKind.Utc) });
        }
        
        return Ok(holidays);
    }
}

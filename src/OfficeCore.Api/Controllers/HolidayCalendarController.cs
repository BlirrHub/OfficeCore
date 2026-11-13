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
        if (year < 1900 || year > 2100)
        {
            return BadRequest("Year must be between 1900 and 2100.");
        }

        var code = (countryCode ?? string.Empty).Trim().ToUpperInvariant();
        var holidays = new List<Holiday>();

        if (code == "PH")
        {
            var easter = GetEasterSunday(year);
            holidays.Add(new Holiday { Name = "New Year's Day", Date = UtcDate(year, 1, 1) });
            holidays.Add(new Holiday { Name = "Maundy Thursday", Date = UtcDate(easter.AddDays(-3)) });
            holidays.Add(new Holiday { Name = "Good Friday", Date = UtcDate(easter.AddDays(-2)) });
            holidays.Add(new Holiday { Name = "The Day of Valor", Date = UtcDate(year, 4, 9) });
            holidays.Add(new Holiday { Name = "Labor Day", Date = UtcDate(year, 5, 1) });
            holidays.Add(new Holiday { Name = "Independence Day", Date = UtcDate(year, 6, 12) });
            holidays.Add(new Holiday { Name = "National Heroes Day", Date = UtcDate(GetLastDayOfWeekInMonth(year, 8, DayOfWeek.Monday)) });
            holidays.Add(new Holiday { Name = "Bonifacio Day", Date = UtcDate(year, 11, 30) });
            holidays.Add(new Holiday { Name = "Christmas Day", Date = UtcDate(year, 12, 25) });
            holidays.Add(new Holiday { Name = "Rizal Day", Date = UtcDate(year, 12, 30) });
        }
        else if (code == "US")
        {
            holidays.Add(new Holiday { Name = "New Year's Day", Date = UtcDate(year, 1, 1) });
            holidays.Add(new Holiday { Name = "Martin Luther King Jr. Day", Date = UtcDate(GetNthDayOfWeekInMonth(year, 1, DayOfWeek.Monday, 3)) });
            holidays.Add(new Holiday { Name = "Memorial Day", Date = UtcDate(GetLastDayOfWeekInMonth(year, 5, DayOfWeek.Monday)) });
            holidays.Add(new Holiday { Name = "Juneteenth", Date = UtcDate(year, 6, 19) });
            holidays.Add(new Holiday { Name = "Independence Day", Date = UtcDate(year, 7, 4) });
            holidays.Add(new Holiday { Name = "Labor Day", Date = UtcDate(GetNthDayOfWeekInMonth(year, 9, DayOfWeek.Monday, 1)) });
            holidays.Add(new Holiday { Name = "Veterans Day", Date = UtcDate(year, 11, 11) });
            holidays.Add(new Holiday { Name = "Thanksgiving Day", Date = UtcDate(GetNthDayOfWeekInMonth(year, 11, DayOfWeek.Thursday, 4)) });
            holidays.Add(new Holiday { Name = "Christmas Day", Date = UtcDate(year, 12, 25) });
        }

        return Ok(holidays);
    }

    private static DateTime UtcDate(int year, int month, int day) =>
        new(year, month, day, 0, 0, 0, DateTimeKind.Utc);

    private static DateTime UtcDate(DateTime date) =>
        DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);

    // Anonymous Gregorian computus for Easter Sunday
    private static DateTime GetEasterSunday(int year)
    {
        int a = year % 19;
        int b = year / 100;
        int c = year % 100;
        int d = b / 4;
        int e = b % 4;
        int f = (b + 8) / 25;
        int g = (b - f + 1) / 3;
        int h = (19 * a + b - d - g + 15) % 30;
        int i = c / 4;
        int k = c % 4;
        int l = (32 + 2 * e + 2 * i - h - k) % 7;
        int m = (a + 11 * h + 22 * l) / 451;
        int month = (h + l - 7 * m + 114) / 31;
        int day = ((h + l - 7 * m + 114) % 31) + 1;
        return new DateTime(year, month, day);
    }

    private static DateTime GetNthDayOfWeekInMonth(int year, int month, DayOfWeek targetDay, int nth)
    {
        var first = new DateTime(year, month, 1);
        int offset = ((int)targetDay - (int)first.DayOfWeek + 7) % 7;
        return first.AddDays(offset + (nth - 1) * 7);
    }

    private static DateTime GetLastDayOfWeekInMonth(int year, int month, DayOfWeek targetDay)
    {
        var last = new DateTime(year, month, DateTime.DaysInMonth(year, month));
        int offset = ((int)last.DayOfWeek - (int)targetDay + 7) % 7;
        return last.AddDays(-offset);
    }
}

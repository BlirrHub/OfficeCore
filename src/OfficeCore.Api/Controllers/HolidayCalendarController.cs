using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Models;

namespace OfficeCore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class HolidayCalendarController : ControllerBase
{
    private readonly AppDbContext _context;

    public HolidayCalendarController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetCalendars()
    {
        var calendars = await _context.HolidayCalendars
            .Include(c => c.Holidays)
            .ToListAsync();
            
        return Ok(calendars);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetCalendar(Guid id)
    {
        var calendar = await _context.HolidayCalendars
            .Include(c => c.Holidays)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (calendar == null)
            return NotFound();

        return Ok(calendar);
    }

    [HttpPost]
    [Authorize(Policy = "RequireTimeOff")]
    public async Task<IActionResult> CreateCalendar(HolidayCalendar calendar)
    {
        calendar.Id = Guid.NewGuid();
        
        if (calendar.IsDefault)
        {
            // Set all other calendars to non-default
            var defaultCalendars = await _context.HolidayCalendars.Where(c => c.IsDefault).ToListAsync();
            foreach (var defaultCalendar in defaultCalendars)
            {
                defaultCalendar.IsDefault = false;
            }
        }
        
        if (calendar.Holidays != null)
        {
            foreach (var holiday in calendar.Holidays)
            {
                holiday.HolidayCalendarId = calendar.Id;
                holiday.Id = 0; // Let DB generate ID
                holiday.Date = DateTime.SpecifyKind(holiday.Date, DateTimeKind.Utc);
            }
        }

        _context.HolidayCalendars.Add(calendar);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetCalendar), new { id = calendar.Id }, calendar);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "RequireTimeOff")]
    public async Task<IActionResult> UpdateCalendar(Guid id, HolidayCalendar updatedCalendar)
    {
        if (id != updatedCalendar.Id)
            return BadRequest();

        var existingCalendar = await _context.HolidayCalendars
            .Include(c => c.Holidays)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (existingCalendar == null)
            return NotFound();

        if (updatedCalendar.IsDefault && !existingCalendar.IsDefault)
        {
            var defaultCalendars = await _context.HolidayCalendars.Where(c => c.IsDefault && c.Id != id).ToListAsync();
            foreach (var defaultCalendar in defaultCalendars)
            {
                defaultCalendar.IsDefault = false;
            }
        }

        existingCalendar.Name = updatedCalendar.Name;
        existingCalendar.IsDefault = updatedCalendar.IsDefault;

        // Update holidays
        _context.Holidays.RemoveRange(existingCalendar.Holidays);
        
        if (updatedCalendar.Holidays != null)
        {
            foreach (var h in updatedCalendar.Holidays)
            {
                h.Id = 0;
                h.HolidayCalendarId = id;
                h.Date = DateTime.SpecifyKind(h.Date, DateTimeKind.Utc);
                existingCalendar.Holidays.Add(h);
            }
        }

        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "RequireTimeOff")]
    public async Task<IActionResult> DeleteCalendar(Guid id)
    {
        var calendar = await _context.HolidayCalendars.FindAsync(id);
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

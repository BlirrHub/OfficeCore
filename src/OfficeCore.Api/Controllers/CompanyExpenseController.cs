using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Models;
using OfficeCore.Api.Services;
using Microsoft.AspNetCore.SignalR;
using OfficeCore.Api.Hubs;

namespace OfficeCore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "RequireLedger")]
public class CompanyExpenseController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly TenantService _tenant;
    private readonly IHubContext<NotificationHub> _hub;

    public CompanyExpenseController(AppDbContext context, TenantService tenant, IHubContext<NotificationHub> hub)
    {
        _context = context;
        _tenant = tenant;
        _hub = hub;
    }

    [HttpGet]
    public async Task<IActionResult> GetExpenses()
    {
        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Forbid();

        var expenses = await _context.CompanyExpenses
            .Where(e => e.OrganizationId == orgId)
            .OrderByDescending(e => e.DateIncurred)
            .ToListAsync();

        return Ok(expenses);
    }

    [HttpPost]
    public async Task<IActionResult> CreateExpense(CreateCompanyExpenseDto dto)
    {
        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Forbid();

        var expense = new CompanyExpense
        {
            Id = Guid.NewGuid(),
            Description = dto.Description,
            Amount = dto.Amount,
            Category = dto.Category,
            DateIncurred = dto.DateIncurred.ToUniversalTime(),
            OrganizationId = orgId.Value
        };

        _context.CompanyExpenses.Add(expense);
        await _context.SaveChangesAsync();

        await _hub.Clients.Group($"org_{orgId.Value}").SendAsync("ExpenseCreated");

        return Ok(expense);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteExpense(Guid id)
    {
        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Forbid();

        var expense = await _context.CompanyExpenses
            .FirstOrDefaultAsync(e => e.Id == id && e.OrganizationId == orgId.Value);
        if (expense == null) return NotFound();

        _context.CompanyExpenses.Remove(expense);
        await _context.SaveChangesAsync();

        await _hub.Clients.Group($"org_{orgId.Value}").SendAsync("ExpenseDeleted");

        return NoContent();
    }
}

public class CreateCompanyExpenseDto
{
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public ExpenseCategory Category { get; set; }
    public DateTime DateIncurred { get; set; }
}

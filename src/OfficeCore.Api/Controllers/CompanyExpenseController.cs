using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Models;

namespace OfficeCore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "RequireLedger")]
public class CompanyExpenseController : ControllerBase
{
    private readonly AppDbContext _context;

    public CompanyExpenseController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetExpenses()
    {
        var expenses = await _context.CompanyExpenses
            .OrderByDescending(e => e.DateIncurred)
            .ToListAsync();

        return Ok(expenses);
    }

    [HttpPost]
    public async Task<IActionResult> CreateExpense(CreateCompanyExpenseDto dto)
    {
        var expense = new CompanyExpense
        {
            Id = Guid.NewGuid(),
            Description = dto.Description,
            Amount = dto.Amount,
            Category = dto.Category,
            DateIncurred = dto.DateIncurred.ToUniversalTime()
        };

        _context.CompanyExpenses.Add(expense);
        await _context.SaveChangesAsync();

        return Ok(expense);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteExpense(Guid id)
    {
        var expense = await _context.CompanyExpenses.FindAsync(id);
        if (expense == null) return NotFound();

        _context.CompanyExpenses.Remove(expense);
        await _context.SaveChangesAsync();

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

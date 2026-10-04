using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Models;
using OfficeCore.Api.Services;

namespace OfficeCore.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuditController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly TenantService _tenant;
    private readonly UserManager<ApplicationUser> _userManager;

    public AuditController(
        AppDbContext context,
        TenantService tenant,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _tenant = tenant;
        _userManager = userManager;
    }

    private async Task<bool> IsAuthorizedForAuditAsync()
    {
        if (User.Identity?.IsAuthenticated != true)
            return false;

        var userId = _userManager.GetUserId(User);
        if (userId == null) return false;

        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null)
        {
            return await _context.Organizations.AnyAsync(o => o.CreatedByUserId == userId);
        }

        var org = await _context.Organizations.FirstOrDefaultAsync(o => o.Id == orgId.Value);
        if (org != null && org.CreatedByUserId == userId)
            return true;

        var isAdmin = await _context.EmployeeRoles
            .AnyAsync(er => er.Employee != null && er.Employee.UserId == userId && er.Employee.OrganizationId == orgId.Value &&
                            (er.Role != null && (er.Role.Name == "Admin" || er.Role.Name == "Owner")));

        return isAdmin;
    }

    [HttpGet]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        [FromQuery] string? action = null,
        [FromQuery] string? entity = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        if (!await IsAuthorizedForAuditAsync())
            return Forbid("Administrative privileges required to access audit trail records.");

        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return BadRequest("Active workspace is required.");

        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;
        if (page < 1) page = 1;

        var query = _context.AuditLogs
            .AsNoTracking()
            .Where(a => a.OrganizationId == orgId.Value);

        if (!string.IsNullOrWhiteSpace(action))
        {
            var act = action.Trim().ToUpperInvariant();
            query = query.Where(a => a.Action == act);
        }

        if (!string.IsNullOrWhiteSpace(entity))
        {
            var ent = entity.Trim();
            query = query.Where(a => a.EntityName == ent);
        }

        if (startDate.HasValue)
        {
            var startUtc = DateTime.SpecifyKind(startDate.Value.Date, DateTimeKind.Utc);
            query = query.Where(a => a.CreatedAt >= startUtc);
        }

        if (endDate.HasValue)
        {
            var endUtc = DateTime.SpecifyKind(endDate.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
            query = query.Where(a => a.CreatedAt <= endUtc);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(a =>
                a.Details.ToLower().Contains(term) ||
                a.UserName.ToLower().Contains(term) ||
                (a.UserEmail != null && a.UserEmail.ToLower().Contains(term)) ||
                (a.EntityId != null && a.EntityId.ToLower().Contains(term)) ||
                (a.IpAddress != null && a.IpAddress.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.Id,
                a.UserId,
                a.UserName,
                a.UserEmail,
                a.Action,
                a.EntityName,
                a.EntityId,
                a.Details,
                a.OldValues,
                a.NewValues,
                a.IpAddress,
                a.ClientInfo,
                a.CreatedAt
            })
            .ToListAsync();

        return Ok(new
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
        });
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetAuditSummary()
    {
        if (!await IsAuthorizedForAuditAsync())
            return Forbid("Administrative privileges required to access audit summary.");

        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return BadRequest("Active workspace is required.");

        var nowUtc = DateTime.UtcNow;
        var todayStartUtc = DateTime.SpecifyKind(nowUtc.Date, DateTimeKind.Utc);

        var query = _context.AuditLogs
            .AsNoTracking()
            .Where(a => a.OrganizationId == orgId.Value);

        var totalEvents = await query.CountAsync();
        var todayCount = await query.CountAsync(a => a.CreatedAt >= todayStartUtc);

        var financialCount = await query.CountAsync(a =>
            a.EntityName == AuditModules.Payroll ||
            a.EntityName == AuditModules.Advance);

        var securityCount = await query.CountAsync(a =>
            a.EntityName == AuditModules.Roles ||
            a.EntityName == AuditModules.Organization);

        // Group action breakdown
        var actionCounts = await query
            .GroupBy(a => a.Action)
            .Select(g => new { Action = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Action, x => x.Count);

        // Group entity breakdown
        var entityCounts = await query
            .GroupBy(a => a.EntityName)
            .Select(g => new { Entity = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Entity, x => x.Count);

        // Recent high-impact critical events (Payroll, Delete, Security)
        var recentCritical = await query
            .Where(a => a.Action == AuditActions.Delete ||
                        a.Action == AuditActions.Security ||
                        a.EntityName == AuditModules.Payroll)
            .OrderByDescending(a => a.CreatedAt)
            .Take(5)
            .Select(a => new
            {
                a.Id,
                a.UserName,
                a.Action,
                a.EntityName,
                a.Details,
                a.CreatedAt
            })
            .ToListAsync();

        return Ok(new
        {
            TotalEvents = totalEvents,
            TodayCount = todayCount,
            FinancialCount = financialCount,
            SecurityCount = securityCount,
            ActionCounts = actionCounts,
            EntityCounts = entityCounts,
            RecentCritical = recentCritical
        });
    }

    [HttpGet("export")]
    public async Task<IActionResult> ExportAuditLogs(
        [FromQuery] string? search = null,
        [FromQuery] string? action = null,
        [FromQuery] string? entity = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        if (!await IsAuthorizedForAuditAsync())
            return Forbid("Administrative privileges required to export audit logs.");

        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return BadRequest("Active workspace is required.");

        var query = _context.AuditLogs
            .AsNoTracking()
            .Where(a => a.OrganizationId == orgId.Value);

        if (!string.IsNullOrWhiteSpace(action))
        {
            var act = action.Trim().ToUpperInvariant();
            query = query.Where(a => a.Action == act);
        }

        if (!string.IsNullOrWhiteSpace(entity))
        {
            var ent = entity.Trim();
            query = query.Where(a => a.EntityName == ent);
        }

        if (startDate.HasValue)
        {
            var startUtc = DateTime.SpecifyKind(startDate.Value.Date, DateTimeKind.Utc);
            query = query.Where(a => a.CreatedAt >= startUtc);
        }

        if (endDate.HasValue)
        {
            var endUtc = DateTime.SpecifyKind(endDate.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
            query = query.Where(a => a.CreatedAt <= endUtc);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(a =>
                a.Details.ToLower().Contains(term) ||
                a.UserName.ToLower().Contains(term) ||
                (a.UserEmail != null && a.UserEmail.ToLower().Contains(term)));
        }

        // Limit export to latest 50,000 records to prevent memory pressure
        var logs = await query
            .OrderByDescending(a => a.CreatedAt)
            .Take(50000)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("Timestamp (UTC),Actor Name,User Email,Action,Category,Entity ID,Details,IP Address,Workstation Info");

        foreach (var l in logs)
        {
            sb.AppendLine(string.Join(",",
                EscapeCsv(l.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
                EscapeCsv(l.UserName),
                EscapeCsv(l.UserEmail ?? ""),
                EscapeCsv(l.Action),
                EscapeCsv(l.EntityName),
                EscapeCsv(l.EntityId ?? ""),
                EscapeCsv(l.Details),
                EscapeCsv(l.IpAddress ?? ""),
                EscapeCsv(l.ClientInfo ?? "")
            ));
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        var fileName = $"OfficeCore_AuditLedger_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";

        return File(bytes, "text/csv", fileName);
    }

    private static string EscapeCsv(string field)
    {
        if (string.IsNullOrEmpty(field)) return "\"\"";
        if (field.Contains(",") || field.Contains("\"") || field.Contains("\n") || field.Contains("\r"))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }
        return $"\"{field}\"";
    }
}

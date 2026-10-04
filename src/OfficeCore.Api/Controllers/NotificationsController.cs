using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Models;
using OfficeCore.Api.Services;

namespace OfficeCore.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly TenantService _tenant;
    private readonly UserManager<ApplicationUser> _userManager;

    public NotificationsController(
        AppDbContext context,
        TenantService tenant,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _tenant = tenant;
        _userManager = userManager;
    }

    [HttpGet("recent")]
    public async Task<IActionResult> GetRecentNotifications()
    {
        var orgId = _tenant.GetActiveOrganizationId();
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? _tenant.GetUserId();

        if (orgId == null || string.IsNullOrEmpty(userId))
        {
            return Ok(new List<NotificationPayload>());
        }

        var notifications = new List<NotificationPayload>();
        var now = DateTime.UtcNow;

        // Resolve current employee
        var employee = await _context.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.UserId == userId && e.OrganizationId == orgId.Value);

        // Check if user is an admin / manager in this workspace
        var isAdmin = false;
        var org = await _context.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orgId.Value);
        if (org != null && org.CreatedByUserId == userId)
        {
            isAdmin = true;
        }
        else if (employee != null)
        {
            isAdmin = await _context.EmployeeRoles
                .AsNoTracking()
                .AnyAsync(er => er.EmployeeId == employee.Id &&
                                er.Role != null && (er.Role.Name == "Admin" || er.Role.Name == "Owner" ||
                                 er.Role.Claims.Any(c => c.ClaimType == "Permission" && (c.ClaimValue == "LeaveApprovals" || c.ClaimValue == "Ledger"))));
        }

        // 1. ADMIN NOTIFICATIONS: Pending leave and advance requests
        if (isAdmin)
        {
            var pendingLeaves = await _context.LeaveRequests
                .AsNoTracking()
                .Include(r => r.Employee)
                    .ThenInclude(e => e!.User)
                .Where(r => r.OrganizationId == orgId.Value && r.Status == LeaveRequestStatus.Pending)
                .OrderByDescending(r => r.CreatedAt)
                .Take(10)
                .ToListAsync();

            foreach (var l in pendingLeaves)
            {
                var empName = $"{l.Employee?.User?.FirstName} {l.Employee?.User?.LastName}".Trim();
                if (string.IsNullOrWhiteSpace(empName)) empName = "An employee";

                notifications.Add(new NotificationPayload
                {
                    Id = $"leave_pending_{l.Id}",
                    Title = "Pending Leave Review",
                    Message = $"{empName} requested {l.Type} leave ({l.StartDate:MMM dd} - {l.EndDate:MMM dd}).",
                    Category = "Leave",
                    Status = "Pending",
                    TargetUrl = "/admin/leave-approvals",
                    Timestamp = l.CreatedAt
                });
            }

            var pendingAdvances = await _context.AdvanceRequests
                .AsNoTracking()
                .Include(r => r.Employee)
                    .ThenInclude(e => e!.User)
                .Where(r => r.OrganizationId == orgId.Value && r.Status == AdvanceStatus.Pending)
                .OrderByDescending(r => r.RequestDate)
                .Take(10)
                .ToListAsync();

            foreach (var a in pendingAdvances)
            {
                var empName = $"{a.Employee?.User?.FirstName} {a.Employee?.User?.LastName}".Trim();
                if (string.IsNullOrWhiteSpace(empName)) empName = "An employee";
                var typeTitle = a.Type == AdvanceType.ELoan ? "E-Loan" : "Cash Advance";

                notifications.Add(new NotificationPayload
                {
                    Id = $"advance_pending_{a.Id}",
                    Title = $"Pending {typeTitle} Review",
                    Message = $"{empName} filed a {typeTitle} request of ₱{a.RequestedAmount:N2}.",
                    Category = "Advance",
                    Status = "Pending",
                    TargetUrl = "/ledger",
                    Timestamp = a.RequestDate
                });
            }
        }

        // 2. EMPLOYEE REQUEST RESULTS: Approved or Rejected requests from the last 14 days
        if (employee != null)
        {
            var resolvedSince = now.AddDays(-14);

            var myResolvedLeaves = await _context.LeaveRequests
                .AsNoTracking()
                .Where(r => r.EmployeeId == employee.Id &&
                            r.OrganizationId == orgId.Value &&
                            r.Status != LeaveRequestStatus.Pending &&
                            r.ActionTakenAt != null &&
                            r.ActionTakenAt >= resolvedSince)
                .OrderByDescending(r => r.ActionTakenAt)
                .Take(5)
                .ToListAsync();

            foreach (var l in myResolvedLeaves)
            {
                var isApproved = l.Status == LeaveRequestStatus.Approved;
                var statusText = isApproved ? "approved" : "rejected";
                var titleText = isApproved ? "Leave Request Approved" : "Leave Request Rejected";

                notifications.Add(new NotificationPayload
                {
                    Id = $"leave_resolved_{l.Id}",
                    Title = titleText,
                    Message = $"Your {l.Type} leave request for {l.StartDate:MMM dd} - {l.EndDate:MMM dd} was {statusText}.",
                    Category = "Leave",
                    Status = l.Status.ToString(),
                    TargetUrl = "/myleaves",
                    Timestamp = l.ActionTakenAt ?? l.CreatedAt
                });
            }

            var myResolvedAdvances = await _context.AdvanceRequests
                .AsNoTracking()
                .Where(r => r.EmployeeId == employee.Id &&
                            r.OrganizationId == orgId.Value &&
                            r.Status != AdvanceStatus.Pending &&
                            r.ResolutionDate != null &&
                            r.ResolutionDate >= resolvedSince)
                .OrderByDescending(r => r.ResolutionDate)
                .Take(5)
                .ToListAsync();

            foreach (var a in myResolvedAdvances)
            {
                var isApproved = a.Status == AdvanceStatus.Approved;
                var statusText = isApproved ? "approved" : "rejected";
                var typeTitle = a.Type == AdvanceType.ELoan ? "E-Loan" : "Cash Advance";
                var titleText = isApproved ? $"{typeTitle} Request Approved" : $"{typeTitle} Request Rejected";

                notifications.Add(new NotificationPayload
                {
                    Id = $"advance_resolved_{a.Id}",
                    Title = titleText,
                    Message = $"Your {typeTitle} request of ₱{a.RequestedAmount:N2} was {statusText}.",
                    Category = "Advance",
                    Status = a.Status.ToString(),
                    TargetUrl = "/advances",
                    Timestamp = a.ResolutionDate ?? a.RequestDate
                });
            }

            // 3. TASK NOTIFICATIONS: Assigned tasks and tasks closing due date
            var myCards = await _context.BoardCards
                .AsNoTracking()
                .Include(c => c.List)
                    .ThenInclude(l => l.Board)
                .Include(c => c.Assignees)
                .Where(c => c.OrganizationId == orgId.Value &&
                            (c.AssignedToUserId == employee.Id ||
                             c.AssignedToUserId == employee.UserId ||
                             c.Assignees.Any(a => a.EmployeeId == employee.Id || a.EmployeeId == employee.UserId)))
                .ToListAsync();

            foreach (var card in myCards)
            {
                var boardName = card.List?.Board?.Name ?? "Task Board";
                var boardId = card.List?.BoardId ?? Guid.Empty;

                // Due date checks
                if (card.DueDate.HasValue)
                {
                    var dueUtc = card.DueDate.Value;
                    var diff = dueUtc - now;

                    if (diff.TotalHours < 0 && diff.TotalDays > -14)
                    {
                        // Overdue
                        notifications.Add(new NotificationPayload
                        {
                            Id = $"task_overdue_{card.Id}",
                            Title = "Task Overdue",
                            Message = $"Task \"{card.Title}\" in {boardName} was due on {dueUtc:MMM dd, yyyy}.",
                            Category = "Task",
                            Status = "Overdue",
                            TargetUrl = $"/boards/{boardId}",
                            Timestamp = dueUtc
                        });
                    }
                    else if (diff.TotalHours >= 0 && diff.TotalHours <= 48)
                    {
                        // Closing due date (within 48 hours)
                        var hoursLeft = Math.Max(1, (int)diff.TotalHours);
                        var dueLabel = hoursLeft <= 24 ? $"{hoursLeft}h remaining" : "due tomorrow";
                        notifications.Add(new NotificationPayload
                        {
                            Id = $"task_due_{card.Id}",
                            Title = "Task Closing Due Date",
                            Message = $"Task \"{card.Title}\" in {boardName} is due soon ({dueLabel}).",
                            Category = "Task",
                            Status = "DueSoon",
                            TargetUrl = $"/boards/{boardId}",
                            Timestamp = now
                        });
                    }
                }

                // Recent assignments (assigned within the last 7 days)
                var assignment = card.Assignees.FirstOrDefault(a => a.EmployeeId == employee.Id || a.EmployeeId == employee.UserId);
                if (assignment != null && assignment.AssignedAt >= now.AddDays(-7))
                {
                    notifications.Add(new NotificationPayload
                    {
                        Id = $"task_assigned_{card.Id}",
                        Title = "Task Assigned",
                        Message = $"You were assigned to \"{card.Title}\" on board \"{boardName}\".",
                        Category = "Task",
                        Status = "Assigned",
                        TargetUrl = $"/boards/{boardId}",
                        Timestamp = assignment.AssignedAt
                    });
                }
            }
        }

        // Order by timestamp descending
        var sorted = notifications
            .OrderByDescending(n => n.Timestamp)
            .Take(30)
            .ToList();

        return Ok(sorted);
    }
}

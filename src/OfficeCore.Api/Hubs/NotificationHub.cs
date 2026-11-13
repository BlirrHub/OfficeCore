using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Models;
using OfficeCore.Api.Services;
using OfficeCore.Shared.Authorization;

namespace OfficeCore.Api.Hubs;

/// <summary>
/// Hub for real-time org-scoped and user-targeted notifications:
/// - "org_{orgId}": Workspace-wide broadcasts (data sync, room updates)
/// - "org_{orgId}_admins_leaves": Leave approval managers and workspace owners
/// - "org_{orgId}_admins_ledger": Ledger/advance approval managers and workspace owners
/// - "org_{orgId}_admins_overtime": Overtime approval managers and workspace owners
/// - "user_{userId}": Targeted user notifications (approval/rejection outcomes, task assignments, due dates)
/// </summary>
[Authorize]
public class NotificationHub : Hub
{
    private readonly AppDbContext _context;
    private readonly TenantService _tenant;

    public NotificationHub(AppDbContext context, TenantService tenant)
    {
        _context = context;
        _tenant = tenant;
    }

    public override async Task OnConnectedAsync()
    {
        var requestedOrgId = _tenant.GetActiveOrganizationId();
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? _tenant.GetUserId();

        // A selected workspace must belong to an active membership. Never fall back
        // to another workspace when a client explicitly requests an unauthorized one.
        if (!requestedOrgId.HasValue && !string.IsNullOrEmpty(userId))
        {
            var employee = await _context.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.UserId == userId && e.IsActive);
            if (employee != null)
            {
                orgId = employee.OrganizationId;
            }
        }

        if (!orgId.HasValue || string.IsNullOrEmpty(userId))
        {
            Context.Abort();
            return;
        }

        Console.WriteLine($"[SignalR] NotificationHub connected. ConnectionId: {Context.ConnectionId}, UserId: {userId}, OrgId: {orgId}");

        if (!string.IsNullOrEmpty(userId))
        {
            // Join user-specific group for targeted alerts (request resolutions, task assignments, task due dates)
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
            Console.WriteLine($"[SignalR] Added to group user_{userId}");
        }

        if (orgId.HasValue)
        {
            // Join the org-wide group so all members of this org receive relevant events
            await Groups.AddToGroupAsync(Context.ConnectionId, $"org_{orgId.Value}");
            Console.WriteLine($"[SignalR] Added to group org_{orgId.Value}");

            // Subscribe user only to the admin notification groups their workspace role actually authorizes
            if (!string.IsNullOrEmpty(userId))
            {
                var org = await _context.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orgId.Value);
                bool isOwner = org != null && org.CreatedByUserId == userId;

                bool canLeaveApprovals = isOwner;
                bool canLedger = isOwner;
                bool canOvertimeApprovals = isOwner;

                if (!isOwner)
                {
                    var roleClaims = await _context.EmployeeRoles
                        .AsNoTracking()
                        .Where(er => er.Employee != null && er.Employee.UserId == userId && er.Employee.OrganizationId == orgId.Value && er.Role != null)
                        .SelectMany(er => er.Role!.Claims.Select(c => new { c.ClaimType, c.ClaimValue }))
                        .ToListAsync();

                    canLeaveApprovals = roleClaims.Any(c =>
                        (c.ClaimType == PermissionCatalog.ClaimType && c.ClaimValue == AccessPermissions.LeaveApprovalsManage) ||
                        (c.ClaimType == "ModuleAccess" && c.ClaimValue == AccessModules.LeaveApprovals));
                    canLedger = roleClaims.Any(c =>
                        (c.ClaimType == PermissionCatalog.ClaimType && c.ClaimValue == AccessPermissions.LedgerManage) ||
                        (c.ClaimType == "ModuleAccess" && c.ClaimValue == AccessModules.Ledger));
                    canOvertimeApprovals = roleClaims.Any(c =>
                        (c.ClaimType == PermissionCatalog.ClaimType && c.ClaimValue == AccessPermissions.OvertimeApprovalsManage) ||
                        (c.ClaimType == "ModuleAccess" && c.ClaimValue == AccessModules.OvertimeApprovals));
                }

                if (canLeaveApprovals)
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"org_{orgId.Value}_admins_leaves");
                if (canLedger)
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"org_{orgId.Value}_admins_ledger");
                if (canOvertimeApprovals)
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"org_{orgId.Value}_admins_overtime");
            }
        }
        else
        {
            Console.WriteLine($"[SignalR] WARNING: No active organization ID found for connection {Context.ConnectionId}");
        }

        await base.OnConnectedAsync();
    }
}

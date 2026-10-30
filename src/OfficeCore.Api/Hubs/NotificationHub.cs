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
/// - "org_{orgId}_admins": Workspace admins and approvers (new leave/loan submissions)
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

            // Check if this user is an admin or approver in this organization
            if (!string.IsNullOrEmpty(userId))
            {
                var isWorkspaceAdmin = false;
                var org = await _context.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orgId.Value);
                if (org != null && org.CreatedByUserId == userId)
                {
                    isWorkspaceAdmin = true;
                }
                else
                {
                    isWorkspaceAdmin = await _context.EmployeeRoles
                        .AsNoTracking()
                        .AnyAsync(er => er.Employee != null && er.Employee.UserId == userId && er.Employee.OrganizationId == orgId.Value &&
                                        er.Role != null && er.Role.Claims.Any(c =>
                                            (c.ClaimType == PermissionCatalog.ClaimType &&
                                             (c.ClaimValue == AccessPermissions.LeaveApprovalsManage ||
                                              c.ClaimValue == AccessPermissions.LedgerManage ||
                                              c.ClaimValue == AccessPermissions.OvertimeApprovalsManage)) ||
                                            (c.ClaimType == "ModuleAccess" &&
                                             (c.ClaimValue == AccessModules.LeaveApprovals ||
                                              c.ClaimValue == AccessModules.Ledger ||
                                              c.ClaimValue == AccessModules.OvertimeApprovals))));
                }

                if (isWorkspaceAdmin)
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"org_{orgId.Value}_admins");
                    Console.WriteLine($"[SignalR] Added to group org_{orgId.Value}_admins");
                }
            }
        }
        else
        {
            Console.WriteLine($"[SignalR] WARNING: No active organization ID found for connection {Context.ConnectionId}");
        }

        await base.OnConnectedAsync();
    }
}

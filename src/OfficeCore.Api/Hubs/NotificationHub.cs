using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Models;
using OfficeCore.Api.Services;

namespace OfficeCore.Api.Hubs;

/// <summary>
/// Hub for real-time org-scoped notifications (leave approvals, advance/loan events).
/// Each authenticated connection automatically joins a group keyed to their active organization.
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
        var orgId = _tenant.GetActiveOrganizationId();
        var userId = _tenant.GetUserId();

        if (!orgId.HasValue && !string.IsNullOrEmpty(userId))
        {
            var employee = await _context.Employees.FirstOrDefaultAsync(e => e.UserId == userId);
            if (employee != null)
            {
                orgId = employee.OrganizationId;
            }
        }
        
        Console.WriteLine($"[SignalR] NotificationHub connected. ConnectionId: {Context.ConnectionId}, UserId: {userId}, OrgId: {orgId}");
        
        if (orgId.HasValue)
        {
            // Join the org-wide group so all members of this org receive relevant events
            await Groups.AddToGroupAsync(Context.ConnectionId, $"org_{orgId}");
            Console.WriteLine($"[SignalR] Added to group org_{orgId}");
        }
        else
        {
            Console.WriteLine($"[SignalR] WARNING: No active organization ID found for connection {Context.ConnectionId}");
        }

        await base.OnConnectedAsync();
    }
}

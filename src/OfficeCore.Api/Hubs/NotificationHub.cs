using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using OfficeCore.Api.Services;

namespace OfficeCore.Api.Hubs;

/// <summary>
/// Hub for real-time org-scoped notifications (leave approvals, advance/loan events).
/// Each authenticated connection automatically joins a group keyed to their active organization.
/// </summary>
[Authorize]
public class NotificationHub : Hub
{
    private readonly TenantService _tenant;

    public NotificationHub(TenantService tenant)
    {
        _tenant = tenant;
    }

    public override async Task OnConnectedAsync()
    {
        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId.HasValue)
        {
            // Join the org-wide group so all members of this org receive relevant events
            await Groups.AddToGroupAsync(Context.ConnectionId, $"org_{orgId}");
        }

        await base.OnConnectedAsync();
    }
}

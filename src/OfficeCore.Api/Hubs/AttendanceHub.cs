using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using OfficeCore.Api.Services;

namespace OfficeCore.Api.Hubs;

[Authorize]
public class AttendanceHub : Hub
{
    private readonly TenantService _tenant;

    public AttendanceHub(TenantService tenant)
    {
        _tenant = tenant;
    }

    public override async Task OnConnectedAsync()
    {
        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId != null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"org_{orgId.Value}");
        }
        await base.OnConnectedAsync();
    }

    public async Task JoinOrgGroup(string orgId)
    {
        var activeOrgId = _tenant.GetActiveOrganizationId();
        if (activeOrgId != null && activeOrgId.Value.ToString() == orgId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"org_{orgId}");
        }
    }
}

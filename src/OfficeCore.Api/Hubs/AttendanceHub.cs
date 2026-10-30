using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Models;
using OfficeCore.Api.Services;

namespace OfficeCore.Api.Hubs;

[Authorize]
public class AttendanceHub : Hub
{
    private readonly AppDbContext _context;
    private readonly TenantService _tenant;

    public AttendanceHub(AppDbContext context, TenantService tenant)
    {
        _context = context;
        _tenant = tenant;
    }

    public override async Task OnConnectedAsync()
    {
        var requestedOrgId = _tenant.GetActiveOrganizationId();
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? _tenant.GetUserId();

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

        await Groups.AddToGroupAsync(Context.ConnectionId, $"org_{orgId.Value}");
        await base.OnConnectedAsync();
    }

    public async Task JoinOrgGroup(string orgId)
    {
        var activeOrgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (activeOrgId != null && activeOrgId.Value.ToString() == orgId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"org_{orgId}");
        }
    }
}

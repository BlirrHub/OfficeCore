using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Models;
using OfficeCore.Api.Services;

namespace OfficeCore.Api.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly AppDbContext _context;
    private readonly TenantService _tenant;

    public ChatHub(AppDbContext context, TenantService tenant)
    {
        _context = context;
        _tenant = tenant;
    }

    public override async Task OnConnectedAsync()
    {
        // Each connection joins a per-org per-employee group so messages are isolated.
        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (employee != null)
        {
            var orgId = _tenant.GetActiveOrganizationId();
            await Groups.AddToGroupAsync(Context.ConnectionId, $"org_{orgId}_employee_{employee.Id}");
        }

        await base.OnConnectedAsync();
    }

    public async Task JoinRoom(string roomId)
    {
        if (!Guid.TryParse(roomId, out var roomGuid)) return;

        // Verify the user is actually a member of this room before allowing them to subscribe
        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (employee == null) return;

        var isMember = await _context.ChatRoomMembers
            .AnyAsync(m => m.ChatRoomId == roomGuid && m.UserId == employee.Id);

        if (isMember)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"room_{roomId}");
        }
    }

    public async Task LeaveRoom(string roomId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"room_{roomId}");
    }

    public async Task SendTypingIndicator(string roomId)
    {
        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (employee == null) return;
        
        await Clients.Group($"room_{roomId}").SendAsync("UserTyping", roomId, employee.Id);
    }
}

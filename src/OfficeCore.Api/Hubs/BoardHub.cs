using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Models;
using OfficeCore.Api.Services;

namespace OfficeCore.Api.Hubs;

[Authorize]
public class BoardHub : Hub
{
    private readonly AppDbContext _context;
    private readonly TenantService _tenant;

    public BoardHub(AppDbContext context, TenantService tenant)
    {
        _context = context;
        _tenant = tenant;
    }

    public async Task JoinBoardGroup(string boardId)
    {
        if (!Guid.TryParse(boardId, out var parsedBoardId)) return;

        var orgId = _tenant.GetActiveOrganizationId();
        bool boardExists;
        if (orgId.HasValue)
        {
            boardExists = await _context.Boards
                .AnyAsync(b => b.Id == parsedBoardId && b.OrganizationId == orgId.Value);
        }
        else
        {
            boardExists = await _context.Boards
                .AnyAsync(b => b.Id == parsedBoardId);
        }

        if (boardExists)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, boardId);
        }
    }

    public async Task LeaveBoardGroup(string boardId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, boardId);
    }
}


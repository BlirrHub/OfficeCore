using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Hubs;
using OfficeCore.Api.Models;
using OfficeCore.Api.Services;

namespace OfficeCore.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IHubContext<ChatHub> _chatHub;
    private readonly TenantService _tenant;

    public ChatController(AppDbContext context, UserManager<ApplicationUser> userManager, IHubContext<ChatHub> chatHub, TenantService tenant)
    {
        _context = context;
        _userManager = userManager;
        _chatHub = chatHub;
        _tenant = tenant;
    }

    [HttpGet("rooms")]
    public async Task<IActionResult> GetRooms()
    {
        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (employee == null) return Forbid();
        
        var orgId = _tenant.GetActiveOrganizationId();

        // SQLite cannot translate correlated sub-queries (LastMessage, UnreadCount) inside a .Select().
        // Fetch all required data into memory first, then project.
        var rooms = await _context.ChatRooms
            .Include(r => r.Members)
                .ThenInclude(m => m.User)
                    .ThenInclude(e => e.User)
            .Where(r => r.OrganizationId == orgId && r.Members.Any(m => m.UserId == employee.Id))
            .ToListAsync();

        // Fetch last messages for each room in bulk (one query)
        var roomIds = rooms.Select(r => r.Id).ToList();
        var lastMessages = await _context.ChatMessages
            .Where(m => roomIds.Contains(m.ChatRoomId))
            .GroupBy(m => m.ChatRoomId)
            .Select(g => g.OrderByDescending(m => m.CreatedAt).First())
            .ToListAsync();

        var lastMessageByRoom = lastMessages.ToDictionary(m => m.ChatRoomId);

        // Fetch unread counts in bulk (one query)
        var memberReadTimes = rooms
            .SelectMany(r => r.Members)
            .Where(m => m.UserId == employee.Id)
            .ToDictionary(m => m.ChatRoomId, m => m.LastReadAt);

        var unreadCounts = await _context.ChatMessages
            .Where(m => roomIds.Contains(m.ChatRoomId))
            .GroupBy(m => m.ChatRoomId)
            .Select(g => new { RoomId = g.Key, Count = g.Count() })
            .ToListAsync();

        // Build unread count per room in-memory (comparing against LastReadAt)
        var unreadByRoom = new Dictionary<Guid, int>();
        foreach (var room in rooms)
        {
            var lastRead = room.Members.FirstOrDefault(m => m.UserId == employee.Id)?.LastReadAt;
            unreadByRoom[room.Id] = await _context.ChatMessages
                .CountAsync(m => m.ChatRoomId == room.Id && (lastRead == null || m.CreatedAt > lastRead));
        }

        var result = rooms
            .Select(r =>
            {
                lastMessageByRoom.TryGetValue(r.Id, out var lastMsg);
                unreadByRoom.TryGetValue(r.Id, out var unread);
                return new
                {
                    r.Id,
                    r.Name,
                    r.Type,
                    r.CreatedAt,
                    Members = r.Members.Select(m => new
                    {
                        m.UserId,
                        m.User.User.FirstName,
                        m.User.User.LastName,
                        m.User.User.ProfilePictureUrl,
                        m.LastReadAt
                    }),
                    LastMessage = lastMsg == null ? null : new
                    {
                        lastMsg.Id,
                        lastMsg.SenderId,
                        lastMsg.Content,
                        lastMsg.CreatedAt
                    },
                    UnreadCount = unread
                };
            })
            .OrderByDescending(r => r.LastMessage != null ? r.LastMessage.CreatedAt : r.CreatedAt)
            .ToList();

        return Ok(result);
    }

    [HttpGet("rooms/{roomId}/messages")]
    public async Task<IActionResult> GetMessages(Guid roomId, [FromQuery] int skip = 0, [FromQuery] int take = 50)
    {
        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (employee == null) return Forbid();

        var isMember = await _context.ChatRoomMembers.AnyAsync(m => m.ChatRoomId == roomId && m.UserId == employee.Id);
        if (!isMember) return Forbid();

        var messages = await _context.ChatMessages
            .Include(m => m.Sender)
            .Where(m => m.ChatRoomId == roomId)
            .OrderByDescending(m => m.CreatedAt)
            .Skip(skip)
            .Take(take)
            .Select(m => new
            {
                m.Id,
                m.ChatRoomId,
                m.SenderId,
                SenderName = m.Sender.User.FirstName + " " + m.Sender.User.LastName,
                m.Sender.User.ProfilePictureUrl,
                m.Content,
                m.CreatedAt
            })
            .ToListAsync();

        return Ok(messages);
    }

    public class SendMessageRequest
    {
        public string Content { get; set; } = string.Empty;
    }

    [HttpPost("rooms/{roomId}/messages")]
    public async Task<IActionResult> SendMessage(Guid roomId, [FromBody] SendMessageRequest req)
    {
        var currentEmployee = await _tenant.GetCurrentEmployeeAsync();
        if (currentEmployee == null) return Forbid();
        var employee = await _context.Employees.Include(e => e.User).FirstOrDefaultAsync(e => e.Id == currentEmployee.Id);
        if (employee == null) return Forbid();
        
        var orgId = _tenant.GetActiveOrganizationId()!.Value;

        var isMember = await _context.ChatRoomMembers.AnyAsync(m => m.ChatRoomId == roomId && m.UserId == employee.Id);
        if (!isMember) return Forbid();

        var message = new ChatMessage
        {
            Id = Guid.NewGuid(),
            ChatRoomId = roomId,
            SenderId = employee.Id,
            Content = req.Content,
            CreatedAt = DateTime.UtcNow,
            OrganizationId = orgId
        };

        _context.ChatMessages.Add(message);
        
        // Update sender's LastReadAt
        var member = await _context.ChatRoomMembers.FirstAsync(m => m.ChatRoomId == roomId && m.UserId == employee.Id);
        member.LastReadAt = message.CreatedAt;

        await _context.SaveChangesAsync();

        var msgDto = new
        {
            message.Id,
            message.ChatRoomId,
            message.SenderId,
            SenderName = employee.User!.FirstName + " " + employee.User.LastName,
            employee.User.ProfilePictureUrl,
            message.Content,
            message.CreatedAt
        };

        // Notify all members of the room via org-scoped SignalR group
        var roomMembers = await _context.ChatRoomMembers.Where(m => m.ChatRoomId == roomId).Select(m => m.UserId).ToListAsync();
        foreach (var memberId in roomMembers)
        {
            await _chatHub.Clients.Group($"org_{orgId}_employee_{memberId}").SendAsync("ReceiveMessage", roomId, msgDto);
        }

        return Ok(msgDto);
    }

    [HttpPost("rooms/{roomId}/read")]
    public async Task<IActionResult> MarkAsRead(Guid roomId)
    {
        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (employee == null) return Forbid();
        
        var orgId = _tenant.GetActiveOrganizationId()!.Value;

        var member = await _context.ChatRoomMembers.FirstOrDefaultAsync(m => m.ChatRoomId == roomId && m.UserId == employee.Id);
        if (member == null) return Forbid();

        member.LastReadAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        
        await _chatHub.Clients.Group($"org_{orgId}_employee_{employee.Id}").SendAsync("RoomRead", roomId);

        return Ok();
    }

    [HttpPost("direct/{targetEmployeeId}")]
    public async Task<IActionResult> GetOrCreateDirectMessage(string targetEmployeeId)
    {
        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (employee == null) return Forbid();
        
        var orgId = _tenant.GetActiveOrganizationId()!.Value;

        if (employee.Id == targetEmployeeId) return BadRequest("Cannot start DM with yourself.");

        var targetEmployee = await _context.Employees.Include(e => e.User).FirstOrDefaultAsync(e => e.Id == targetEmployeeId && e.OrganizationId == orgId);
        if (targetEmployee == null) return NotFound("Member not found in this organization.");

        // Check if DM already exists within this org
        var existingRoom = await _context.ChatRooms
            .Where(r => r.OrganizationId == orgId && r.Type == ChatRoomType.DirectMessage)
            .Where(r => r.Members.Any(m => m.UserId == employee.Id) && r.Members.Any(m => m.UserId == targetEmployeeId))
            .FirstOrDefaultAsync();

        if (existingRoom != null) return Ok(new { RoomId = existingRoom.Id });

        var newRoom = new ChatRoom
        {
            Id = Guid.NewGuid(),
            Type = ChatRoomType.DirectMessage,
            CreatedAt = DateTime.UtcNow,
            OrganizationId = orgId
        };

        newRoom.Members.Add(new ChatRoomMember { Id = Guid.NewGuid(), UserId = employee.Id, JoinedAt = DateTime.UtcNow, LastReadAt = DateTime.UtcNow, OrganizationId = orgId });
        newRoom.Members.Add(new ChatRoomMember { Id = Guid.NewGuid(), UserId = targetEmployeeId, JoinedAt = DateTime.UtcNow, LastReadAt = DateTime.UtcNow, OrganizationId = orgId });

        _context.ChatRooms.Add(newRoom);
        await _context.SaveChangesAsync();

        // Notify both members that a new room was created
        await _chatHub.Clients.Group($"org_{orgId}_employee_{targetEmployeeId}").SendAsync("RoomCreated", newRoom.Id);
        await _chatHub.Clients.Group($"org_{orgId}_employee_{employee.Id}").SendAsync("RoomCreated", newRoom.Id);

        return Ok(new { RoomId = newRoom.Id });
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsersForChat()
    {
        var orgId = _tenant.GetActiveOrganizationId();
        if (orgId == null) return Forbid();

        // Only return employees in the current organization
        var employees = await _context.Employees
            .Include(e => e.User)
            .Where(e => e.OrganizationId == orgId && e.IsActive)
            .Select(e => new
            {
                Id = e.Id,           // Employee.Id (used for chat membership)
                ApplicationUserId = e.UserId, // ApplicationUser.Id (used to identify self)
                e.User!.FirstName,
                e.User.LastName,
                e.User.ProfilePictureUrl
            })
            .ToListAsync();

        return Ok(employees);
    }
}

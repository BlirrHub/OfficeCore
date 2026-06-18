using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Data;
using OfficeCore.Api.Hubs;
using OfficeCore.Api.Models;

namespace OfficeCore.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IHubContext<ChatHub> _chatHub;

    public ChatController(AppDbContext context, UserManager<ApplicationUser> userManager, IHubContext<ChatHub> chatHub)
    {
        _context = context;
        _userManager = userManager;
        _chatHub = chatHub;
    }

    [HttpGet("rooms")]
    public async Task<IActionResult> GetRooms()
    {
        var userId = _userManager.GetUserId(User);

        var rooms = await _context.ChatRooms
            .Include(r => r.Members)
                .ThenInclude(m => m.User)
            .Where(r => r.Members.Any(m => m.UserId == userId))
            .Select(r => new
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
                LastMessage = _context.ChatMessages
                    .Where(m => m.ChatRoomId == r.Id)
                    .OrderByDescending(m => m.CreatedAt)
                    .Select(m => new
                    {
                        m.Id,
                        m.SenderId,
                        m.Content,
                        m.CreatedAt
                    })
                    .FirstOrDefault(),
                UnreadCount = _context.ChatMessages
                    .Where(m => m.ChatRoomId == r.Id && m.CreatedAt > r.Members.First(me => me.UserId == userId).LastReadAt)
                    .Count()
            })
            .OrderByDescending(r => r.LastMessage != null ? r.LastMessage.CreatedAt : r.CreatedAt)
            .ToListAsync();

        return Ok(rooms);
    }

    [HttpGet("rooms/{roomId}/messages")]
    public async Task<IActionResult> GetMessages(Guid roomId, [FromQuery] int skip = 0, [FromQuery] int take = 50)
    {
        var userId = _userManager.GetUserId(User);

        var isMember = await _context.ChatRoomMembers.AnyAsync(m => m.ChatRoomId == roomId && m.UserId == userId);
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
        var userId = _userManager.GetUserId(User);

        var isMember = await _context.ChatRoomMembers.AnyAsync(m => m.ChatRoomId == roomId && m.UserId == userId);
        if (!isMember) return Forbid();

        var message = new ChatMessage
        {
            Id = Guid.NewGuid(),
            ChatRoomId = roomId,
            SenderId = userId!,
            Content = req.Content,
            CreatedAt = DateTime.UtcNow
        };

        _context.ChatMessages.Add(message);
        
        // Update sender's LastReadAt
        var member = await _context.ChatRoomMembers.FirstAsync(m => m.ChatRoomId == roomId && m.UserId == userId);
        member.LastReadAt = message.CreatedAt;

        await _context.SaveChangesAsync();

        var sender = await _context.Users.FindAsync(userId);

        var msgDto = new
        {
            message.Id,
            message.ChatRoomId,
            message.SenderId,
            SenderName = sender!.FirstName + " " + sender.LastName,
            sender.ProfilePictureUrl,
            message.Content,
            message.CreatedAt
        };

        // Notify all members of the room
        var roomMembers = await _context.ChatRoomMembers.Where(m => m.ChatRoomId == roomId).Select(m => m.UserId).ToListAsync();
        foreach (var memberId in roomMembers)
        {
            await _chatHub.Clients.Group($"user_{memberId}").SendAsync("ReceiveMessage", roomId, msgDto);
        }

        return Ok(msgDto);
    }

    [HttpPost("rooms/{roomId}/read")]
    public async Task<IActionResult> MarkAsRead(Guid roomId)
    {
        var userId = _userManager.GetUserId(User);
        var member = await _context.ChatRoomMembers.FirstOrDefaultAsync(m => m.ChatRoomId == roomId && m.UserId == userId);
        if (member == null) return Forbid();

        member.LastReadAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        
        await _chatHub.Clients.Group($"user_{userId}").SendAsync("RoomRead", roomId);

        return Ok();
    }

    [HttpPost("direct/{targetUserId}")]
    public async Task<IActionResult> GetOrCreateDirectMessage(string targetUserId)
    {
        var userId = _userManager.GetUserId(User);
        if (userId == targetUserId) return BadRequest("Cannot start DM with yourself.");

        var targetUser = await _context.Users.FindAsync(targetUserId);
        if (targetUser == null) return NotFound("User not found.");

        // Check if DM already exists
        var existingRoom = await _context.ChatRooms
            .Where(r => r.Type == ChatRoomType.DirectMessage)
            .Where(r => r.Members.Any(m => m.UserId == userId) && r.Members.Any(m => m.UserId == targetUserId))
            .FirstOrDefaultAsync();

        if (existingRoom != null) return Ok(new { RoomId = existingRoom.Id });

        var newRoom = new ChatRoom
        {
            Id = Guid.NewGuid(),
            Type = ChatRoomType.DirectMessage,
            CreatedAt = DateTime.UtcNow
        };

        newRoom.Members.Add(new ChatRoomMember { Id = Guid.NewGuid(), UserId = userId!, JoinedAt = DateTime.UtcNow, LastReadAt = DateTime.UtcNow });
        newRoom.Members.Add(new ChatRoomMember { Id = Guid.NewGuid(), UserId = targetUserId, JoinedAt = DateTime.UtcNow, LastReadAt = DateTime.UtcNow });

        _context.ChatRooms.Add(newRoom);
        await _context.SaveChangesAsync();

        // Notify target user that a room was created
        await _chatHub.Clients.Group($"user_{targetUserId}").SendAsync("RoomCreated", newRoom.Id);
        await _chatHub.Clients.Group($"user_{userId}").SendAsync("RoomCreated", newRoom.Id);

        return Ok(new { RoomId = newRoom.Id });
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsersForChat()
    {
        var users = await _context.Users
            .Where(u => u.IsActive)
            .Select(u => new
            {
                u.Id,
                u.FirstName,
                u.LastName,
                u.ProfilePictureUrl
            })
            .ToListAsync();

        return Ok(users);
    }
}

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
        
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();

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

        // Fetch unread counts in bulk (one single SQL query instead of N+1)
        var unreadCounts = await _context.ChatMessages
            .Where(m => roomIds.Contains(m.ChatRoomId))
            .Join(_context.ChatRoomMembers.Where(crm => crm.UserId == employee.Id),
                  m => m.ChatRoomId,
                  crm => crm.ChatRoomId,
                  (m, crm) => new { m.ChatRoomId, m.CreatedAt, crm.LastReadAt })
            .Where(x => x.LastReadAt == null || x.CreatedAt > x.LastReadAt)
            .GroupBy(x => x.ChatRoomId)
            .Select(g => new { RoomId = g.Key, Count = g.Count() })
            .ToListAsync();

        var unreadByRoom = unreadCounts.ToDictionary(x => x.RoomId, x => x.Count);

        var result = rooms
            .Select(r =>
            {
                lastMessageByRoom.TryGetValue(r.Id, out var lastMsg);
                unreadByRoom.TryGetValue(r.Id, out var unread);
                return new
                {
                    r.Id,
                    r.Name,
                    r.PictureUrl,
                    r.Type,
                    r.CreatedAt,
                    Members = r.Members.Select(m => new
                    {
                        m.UserId,
                        FirstName = m.User?.User?.FirstName ?? "Unknown",
                        LastName = m.User?.User?.LastName ?? "",
                        ProfilePictureUrl = m.User?.User?.ProfilePictureUrl,
                        m.LastReadAt
                    }),
                    LastMessage = lastMsg == null ? null : new
                    {
                        lastMsg.Id,
                        lastMsg.SenderId,
                        lastMsg.Content,
                        lastMsg.AttachmentUrl,
                        lastMsg.AttachmentName,
                        lastMsg.AttachmentSize,
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

        var recentMessages = await _context.ChatMessages
            .Include(m => m.Sender)
                .ThenInclude(s => s.User)
            .Include(m => m.Reactions)
            .Where(m => m.ChatRoomId == roomId)
            .OrderByDescending(m => m.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();

        // Also fetch any pinned messages for this room that might be outside the recent window
        var pinnedMessages = await _context.ChatMessages
            .Include(m => m.Sender)
                .ThenInclude(s => s.User)
            .Include(m => m.Reactions)
            .Where(m => m.ChatRoomId == roomId && m.IsPinned)
            .ToListAsync();

        var combinedMessages = recentMessages.Concat(pinnedMessages)
            .DistinctBy(m => m.Id)
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new
            {
                m.Id,
                m.ChatRoomId,
                m.SenderId,
                SenderName = m.Sender.User != null ? m.Sender.User.FirstName + " " + m.Sender.User.LastName : "Unknown",
                ProfilePictureUrl = m.Sender.User != null ? m.Sender.User.ProfilePictureUrl : null,
                m.Content,
                m.AttachmentUrl,
                m.AttachmentName,
                m.AttachmentSize,
                m.CreatedAt,
                m.IsPinned,
                Reactions = m.Reactions.Select(r => new
                {
                    r.UserId,
                    r.Emoji
                }).ToList()
            })
            .ToList();

        return Ok(combinedMessages);
    }

    public class SendMessageRequest
    {
        public string Content { get; set; } = string.Empty;
        public string? AttachmentUrl { get; set; }
        public string? AttachmentName { get; set; }
        public long? AttachmentSize { get; set; }
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
            Content = req.Content ?? string.Empty,
            AttachmentUrl = req.AttachmentUrl,
            AttachmentName = req.AttachmentName,
            AttachmentSize = req.AttachmentSize,
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
            SenderName = employee.User != null ? employee.User.FirstName + " " + employee.User.LastName : "Me",
            ProfilePictureUrl = employee.User?.ProfilePictureUrl,
            message.Content,
            message.AttachmentUrl,
            message.AttachmentName,
            message.AttachmentSize,
            message.CreatedAt,
            IsPinned = false,
            Reactions = new List<object>()
        };

        // Notify all members of the room via org-scoped SignalR group
        var roomMembers = await _context.ChatRoomMembers.Where(m => m.ChatRoomId == roomId).Select(m => m.UserId).ToListAsync();
        foreach (var memberId in roomMembers)
        {
            await _chatHub.Clients.Group($"org_{orgId}_employee_{memberId}").SendAsync("ReceiveMessage", roomId, msgDto);
        }

        return Ok(msgDto);
    }

    [HttpPut("messages/{messageId}/pin")]
    public async Task<IActionResult> TogglePin(Guid messageId)
    {
        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (employee == null) return Forbid();
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Forbid();

        var message = await _context.ChatMessages.FirstOrDefaultAsync(m => m.Id == messageId && m.OrganizationId == orgId.Value);
        if (message == null) return NotFound("Message not found.");

        var isMember = await _context.ChatRoomMembers.AnyAsync(m => m.ChatRoomId == message.ChatRoomId && m.UserId == employee.Id);
        if (!isMember) return Forbid();

        message.IsPinned = !message.IsPinned;
        if (message.IsPinned)
        {
            message.PinnedAt = DateTime.UtcNow;
            message.PinnedById = employee.Id;
        }
        else
        {
            message.PinnedAt = null;
            message.PinnedById = null;
        }

        await _context.SaveChangesAsync();

        var roomId = message.ChatRoomId;
        var roomMembers = await _context.ChatRoomMembers.Where(m => m.ChatRoomId == roomId).Select(m => m.UserId).ToListAsync();
        foreach (var memberId in roomMembers)
        {
            await _chatHub.Clients.Group($"org_{orgId}_employee_{memberId}").SendAsync("MessagePinUpdated", roomId, messageId, message.IsPinned);
        }

        return Ok(new { message.Id, message.IsPinned, message.PinnedAt, message.PinnedById });
    }

    public class ReactMessageRequest
    {
        public string Emoji { get; set; } = string.Empty;
    }

    [HttpPost("messages/{messageId}/react")]
    public async Task<IActionResult> ToggleReaction(Guid messageId, [FromBody] ReactMessageRequest req)
    {
        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (employee == null) return Forbid();
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Forbid();

        if (string.IsNullOrWhiteSpace(req.Emoji))
            return BadRequest("Emoji is required.");

        var message = await _context.ChatMessages.FirstOrDefaultAsync(m => m.Id == messageId && m.OrganizationId == orgId.Value);
        if (message == null) return NotFound("Message not found.");

        var isMember = await _context.ChatRoomMembers.AnyAsync(m => m.ChatRoomId == message.ChatRoomId && m.UserId == employee.Id);
        if (!isMember) return Forbid();

        var existingReaction = await _context.ChatMessageReactions
            .FirstOrDefaultAsync(r => r.ChatMessageId == messageId && r.UserId == employee.Id);

        string? resultingEmoji = null;
        if (existingReaction != null && existingReaction.Emoji == req.Emoji)
        {
            _context.ChatMessageReactions.Remove(existingReaction);
            resultingEmoji = null;
        }
        else if (existingReaction != null)
        {
            existingReaction.Emoji = req.Emoji;
            existingReaction.CreatedAt = DateTime.UtcNow;
            resultingEmoji = req.Emoji;
        }
        else
        {
            var newReaction = new ChatMessageReaction
            {
                Id = Guid.NewGuid(),
                ChatMessageId = messageId,
                UserId = employee.Id,
                Emoji = req.Emoji,
                CreatedAt = DateTime.UtcNow,
                OrganizationId = orgId.Value
            };
            _context.ChatMessageReactions.Add(newReaction);
            resultingEmoji = req.Emoji;
        }

        await _context.SaveChangesAsync();

        var roomId = message.ChatRoomId;
        var roomMembers = await _context.ChatRoomMembers.Where(m => m.ChatRoomId == roomId).Select(m => m.UserId).ToListAsync();
        foreach (var memberId in roomMembers)
        {
            await _chatHub.Clients.Group($"org_{orgId}_employee_{memberId}").SendAsync("MessageReactionUpdated", roomId, messageId, employee.Id, resultingEmoji);
        }

        return Ok(new { messageId, userId = employee.Id, emoji = resultingEmoji });
    }

    [HttpDelete("messages/{messageId}")]
    public async Task<IActionResult> UnsendMessage(Guid messageId)
    {
        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (employee == null) return Forbid();
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Forbid();

        var message = await _context.ChatMessages.FirstOrDefaultAsync(m => m.Id == messageId && m.OrganizationId == orgId.Value);
        if (message == null) return NotFound("Message not found.");

        if (message.SenderId != employee.Id) return Forbid("You can only unsend your own messages.");

        var roomId = message.ChatRoomId;
        _context.ChatMessages.Remove(message);
        await _context.SaveChangesAsync();

        var roomMembers = await _context.ChatRoomMembers.Where(m => m.ChatRoomId == roomId).Select(m => m.UserId).ToListAsync();
        foreach (var memberId in roomMembers)
        {
            await _chatHub.Clients.Group($"org_{orgId}_employee_{memberId}").SendAsync("MessageDeleted", roomId, messageId);
        }

        return Ok(new { message = "Message unsent successfully." });
    }

    [HttpPost("rooms/{roomId}/attachment")]
    public async Task<IActionResult> UploadAttachment(Guid roomId, IFormFile file)
    {
        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (employee == null) return Forbid();

        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Forbid();

        var isMember = await _context.ChatRoomMembers.AnyAsync(m => m.ChatRoomId == roomId && m.UserId == employee.Id);
        if (!isMember) return Forbid();

        if (file == null || file.Length == 0)
            return BadRequest("No file was uploaded.");

        // Allow images, pdfs, docs, text files up to 25MB
        if (file.Length > 25 * 1024 * 1024)
            return BadRequest("File size cannot exceed 25MB.");

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif", ".pdf", ".txt", ".doc", ".docx", ".xls", ".xlsx" };
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(ext))
            return BadRequest("File type is not supported.");

        var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "chat");
        Directory.CreateDirectory(uploadDir);

        var safeFileName = $"chat_{roomId}_{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(uploadDir, safeFileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        return Ok(new
        {
            AttachmentUrl = $"/uploads/chat/{safeFileName}",
            AttachmentName = Path.GetFileName(file.FileName),
            AttachmentSize = file.Length
        });
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

    public class CreateGroupChatRequest
    {
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.StringLength(100, MinimumLength = 1)]
        public string Name { get; set; } = string.Empty;

        public string? PictureUrl { get; set; }

        public List<string> MemberIds { get; set; } = new();
    }

    [HttpPost("group")]
    public async Task<IActionResult> CreateGroupChat([FromBody] CreateGroupChatRequest req)
    {
        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (employee == null) return Forbid();

        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Forbid();

        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest("Group name is required.");

        var trimmedName = req.Name.Trim();

        var memberIds = (req.MemberIds ?? new List<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();

        if (!memberIds.Contains(employee.Id))
        {
            memberIds.Add(employee.Id);
        }

        if (memberIds.Count < 2)
        {
            return BadRequest("A group chat must contain at least 2 members.");
        }

        var validEmployees = await _context.Employees
            .Include(e => e.User)
            .Where(e => e.OrganizationId == orgId.Value && e.IsActive && memberIds.Contains(e.Id))
            .ToListAsync();

        if (validEmployees.Count < 2)
        {
            return BadRequest("Not enough valid active organization members to create group chat.");
        }

        var newRoom = new ChatRoom
        {
            Id = Guid.NewGuid(),
            Name = trimmedName,
            PictureUrl = !string.IsNullOrWhiteSpace(req.PictureUrl) ? req.PictureUrl.Trim() : null,
            Type = ChatRoomType.Group,
            CreatedAt = DateTime.UtcNow,
            OrganizationId = orgId.Value
        };

        foreach (var emp in validEmployees)
        {
            newRoom.Members.Add(new ChatRoomMember
            {
                Id = Guid.NewGuid(),
                UserId = emp.Id,
                JoinedAt = DateTime.UtcNow,
                LastReadAt = DateTime.UtcNow,
                OrganizationId = orgId.Value
            });
        }

        _context.ChatRooms.Add(newRoom);
        await _context.SaveChangesAsync();

        foreach (var emp in validEmployees)
        {
            await _chatHub.Clients.Group($"org_{orgId}_employee_{emp.Id}").SendAsync("RoomCreated", newRoom.Id);
        }

        return Ok(new { RoomId = newRoom.Id, Name = newRoom.Name });
    }

    public class AddGroupMembersRequest
    {
        public List<string> MemberIds { get; set; } = new();
    }

    [HttpPost("rooms/{roomId}/members")]
    public async Task<IActionResult> AddGroupMembers(Guid roomId, [FromBody] AddGroupMembersRequest req)
    {
        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (employee == null) return Forbid();

        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Forbid();

        var room = await _context.ChatRooms
            .Include(r => r.Members)
            .FirstOrDefaultAsync(r => r.Id == roomId && r.OrganizationId == orgId.Value && r.Type == ChatRoomType.Group);

        if (room == null) return NotFound("Group chat not found.");

        if (!room.Members.Any(m => m.UserId == employee.Id))
            return Forbid("You are not a member of this group.");

        var existingIds = room.Members.Select(m => m.UserId).ToHashSet();
        var candidateIds = (req.MemberIds ?? new List<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id) && !existingIds.Contains(id))
            .Distinct()
            .ToList();

        if (!candidateIds.Any())
            return BadRequest("No new members specified to add.");

        var validEmployees = await _context.Employees
            .Where(e => e.OrganizationId == orgId.Value && e.IsActive && candidateIds.Contains(e.Id))
            .ToListAsync();

        if (!validEmployees.Any())
            return BadRequest("None of the specified members were found in this organization.");

        foreach (var emp in validEmployees)
        {
            _context.ChatRoomMembers.Add(new ChatRoomMember
            {
                Id = Guid.NewGuid(),
                ChatRoomId = room.Id,
                UserId = emp.Id,
                JoinedAt = DateTime.UtcNow,
                LastReadAt = DateTime.UtcNow,
                OrganizationId = orgId.Value
            });
        }

        await _context.SaveChangesAsync();

        var allMemberIds = room.Members.Select(m => m.UserId).Concat(validEmployees.Select(e => e.Id)).Distinct();
        foreach (var memberId in allMemberIds)
        {
            await _chatHub.Clients.Group($"org_{orgId}_employee_{memberId}").SendAsync("RoomCreated", room.Id);
        }

        return Ok(new { message = "Members added successfully", Count = validEmployees.Count });
    }

    [HttpPost("rooms/{roomId}/leave")]
    public async Task<IActionResult> LeaveGroup(Guid roomId)
    {
        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (employee == null) return Forbid();

        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Forbid();

        var room = await _context.ChatRooms
            .Include(r => r.Members)
            .FirstOrDefaultAsync(r => r.Id == roomId && r.OrganizationId == orgId.Value && r.Type == ChatRoomType.Group);

        if (room == null) return NotFound("Group chat not found.");

        var member = room.Members.FirstOrDefault(m => m.UserId == employee.Id);
        if (member == null) return BadRequest("You are not a member of this group.");

        _context.ChatRoomMembers.Remove(member);
        await _context.SaveChangesAsync();

        foreach (var remaining in room.Members.Where(m => m.UserId != employee.Id))
        {
            await _chatHub.Clients.Group($"org_{orgId}_employee_{remaining.UserId}").SendAsync("RoomCreated", room.Id);
        }

        return Ok(new { message = "Left group successfully" });
    }

    public class UpdateGroupNameRequest
    {
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.StringLength(100, MinimumLength = 1)]
        public string Name { get; set; } = string.Empty;
    }

    [HttpPut("rooms/{roomId}/name")]
    public async Task<IActionResult> RenameGroup(Guid roomId, [FromBody] UpdateGroupNameRequest req)
    {
        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (employee == null) return Forbid();

        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Forbid();

        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest("Group name is required.");

        var room = await _context.ChatRooms
            .Include(r => r.Members)
            .FirstOrDefaultAsync(r => r.Id == roomId && r.OrganizationId == orgId.Value && r.Type == ChatRoomType.Group);

        if (room == null) return NotFound("Group chat not found.");

        if (!room.Members.Any(m => m.UserId == employee.Id))
            return Forbid("You are not a member of this group.");

        room.Name = req.Name.Trim();
        await _context.SaveChangesAsync();

        foreach (var member in room.Members)
        {
            await _chatHub.Clients.Group($"org_{orgId}_employee_{member.UserId}").SendAsync("RoomCreated", room.Id);
        }

        return Ok(new { Name = room.Name });
    }

    [HttpPost("rooms/{roomId}/picture")]
    public async Task<IActionResult> UploadGroupPicture(Guid roomId, IFormFile file)
    {
        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (employee == null) return Forbid();

        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Forbid();

        var room = await _context.ChatRooms
            .Include(r => r.Members)
            .FirstOrDefaultAsync(r => r.Id == roomId && r.OrganizationId == orgId.Value && r.Type == ChatRoomType.Group);

        if (room == null) return NotFound("Group chat not found.");

        if (!room.Members.Any(m => m.UserId == employee.Id))
            return Forbid("You are not a member of this group.");

        if (file == null || file.Length == 0)
            return BadRequest("File is empty.");

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(ext))
            return BadRequest("Invalid image format. Allowed formats: JPG, PNG, WEBP, GIF.");

        if (file.Length > 5 * 1024 * 1024)
            return BadRequest("Image file size must be less than 5MB.");

        var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "chat");
        Directory.CreateDirectory(uploadDir);

        var fileName = $"group_{room.Id}_{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(uploadDir, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        room.PictureUrl = $"/uploads/chat/{fileName}";
        await _context.SaveChangesAsync();

        foreach (var member in room.Members)
        {
            await _chatHub.Clients.Group($"org_{orgId}_employee_{member.UserId}").SendAsync("RoomCreated", room.Id);
            await _chatHub.Clients.Group($"org_{orgId}_employee_{member.UserId}").SendAsync("RoomPictureUpdated", room.Id, room.PictureUrl);
        }

        return Ok(new { PictureUrl = room.PictureUrl });
    }

    [HttpDelete("rooms/{roomId}/picture")]
    public async Task<IActionResult> RemoveGroupPicture(Guid roomId)
    {
        var employee = await _tenant.GetCurrentEmployeeAsync();
        if (employee == null) return Forbid();

        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Forbid();

        var room = await _context.ChatRooms
            .Include(r => r.Members)
            .FirstOrDefaultAsync(r => r.Id == roomId && r.OrganizationId == orgId.Value && r.Type == ChatRoomType.Group);

        if (room == null) return NotFound("Group chat not found.");

        if (!room.Members.Any(m => m.UserId == employee.Id))
            return Forbid("You are not a member of this group.");

        room.PictureUrl = null;
        await _context.SaveChangesAsync();

        foreach (var member in room.Members)
        {
            await _chatHub.Clients.Group($"org_{orgId}_employee_{member.UserId}").SendAsync("RoomCreated", room.Id);
            await _chatHub.Clients.Group($"org_{orgId}_employee_{member.UserId}").SendAsync("RoomPictureUpdated", room.Id, (string?)null);
        }

        return Ok(new { message = "Group picture removed successfully" });
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsersForChat()
    {
        var orgId = await _tenant.GetValidatedOrganizationIdAsync();
        if (orgId == null) return Forbid();

        // Only return employees in the current organization
        var employees = await _context.Employees
            .Include(e => e.User)
            .Where(e => e.OrganizationId == orgId && e.IsActive)
            .Select(e => new
            {
                Id = e.Id,           // Employee.Id (used for chat membership)
                ApplicationUserId = e.UserId, // ApplicationUser.Id (used to identify self)
                FirstName = e.User != null ? e.User.FirstName : "",
                LastName = e.User != null ? e.User.LastName : "",
                ProfilePictureUrl = e.User != null ? e.User.ProfilePictureUrl : null
            })
            .ToListAsync();

        return Ok(employees);
    }
}


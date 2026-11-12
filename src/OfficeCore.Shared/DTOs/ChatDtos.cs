namespace OfficeCore.Shared.DTOs;

public class ChatRoomDto
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? PictureUrl { get; set; }
    public int Type { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<ChatRoomMemberDto> Members { get; set; } = new();
    public ChatMessageDto? LastMessage { get; set; }
    public int UnreadCount { get; set; }
}

public class ChatRoomMemberDto
{
    public string UserId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? ProfilePictureUrl { get; set; }
    public DateTime? LastReadAt { get; set; }
}

public class ChatReactionDto
{
    public string UserId { get; set; } = string.Empty;
    public string Emoji { get; set; } = string.Empty;
}

public class ChatMessageDto
{
    public Guid Id { get; set; }
    public Guid ChatRoomId { get; set; }
    public string SenderId { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public string? ProfilePictureUrl { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? AttachmentUrl { get; set; }
    public string? AttachmentName { get; set; }
    public long? AttachmentSize { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsOptimistic { get; set; }
    public bool IsPinned { get; set; }
    public List<ChatReactionDto> Reactions { get; set; } = new();
}

public class ChatUserDto
{
    public string Id { get; set; } = string.Empty;
    public string ApplicationUserId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? ProfilePictureUrl { get; set; }
}

public class ChatSearchTagDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? BoardName { get; set; }
    public string? BoardId { get; set; }
}

public class ChatSearchTagsResponse
{
    public List<ChatSearchTagDto> Boards { get; set; } = new();
    public List<ChatSearchTagDto> Tasks { get; set; } = new();
}

public class ChatRoomCreationResponse
{
    public Guid RoomId { get; set; }
    public string? Name { get; set; }
    public string? PictureUrl { get; set; }
}

public class ChatPictureUploadResponse
{
    public string? PictureUrl { get; set; }
}

public class ChatAttachmentUploadResponse
{
    public string? AttachmentUrl { get; set; }
    public string? AttachmentName { get; set; }
    public long? AttachmentSize { get; set; }
}

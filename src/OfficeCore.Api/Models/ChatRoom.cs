using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace OfficeCore.Api.Models;

public enum ChatRoomType
{
    DirectMessage = 0,
    Group = 1
}

public class ChatRoom : ITenantEntity{
    [Key]
    public Guid Id { get; set; }
    
    public string? Name { get; set; }
    
    public string? PictureUrl { get; set; }
    
    public ChatRoomType Type { get; set; }
    
    public DateTime CreatedAt { get; set; }

    public ICollection<ChatRoomMember> Members { get; set; } = new List<ChatRoomMember>();
    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();

    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }
}

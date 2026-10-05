using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OfficeCore.Api.Models;

public class ChatMessage : ITenantEntity{
    [Key]
    public Guid Id { get; set; }

    public Guid ChatRoomId { get; set; }
    
    [ForeignKey("ChatRoomId")]
    public ChatRoom ChatRoom { get; set; } = null!;

    [Required]
    public string SenderId { get; set; } = string.Empty;

    [ForeignKey("SenderId")]
    public Employee Sender { get; set; } = null!;

    [Required]
    public string Content { get; set; } = string.Empty;

    public string? AttachmentUrl { get; set; }
    public string? AttachmentName { get; set; }
    public long? AttachmentSize { get; set; }

    public bool IsPinned { get; set; } = false;
    public DateTime? PinnedAt { get; set; }
    public string? PinnedById { get; set; }

    [ForeignKey("PinnedById")]
    public Employee? PinnedBy { get; set; }

    public ICollection<ChatMessageReaction> Reactions { get; set; } = new List<ChatMessageReaction>();

    public DateTime CreatedAt { get; set; }

    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }
}

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

    public DateTime CreatedAt { get; set; }

    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }
}

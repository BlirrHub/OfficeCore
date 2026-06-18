using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OfficeCore.Api.Models;

public class ChatRoomMember : ITenantEntity{
    [Key]
    public Guid Id { get; set; }

    public Guid ChatRoomId { get; set; }
    
    [ForeignKey("ChatRoomId")]
    public ChatRoom ChatRoom { get; set; } = null!;

    [Required]
    public string UserId { get; set; } = string.Empty;
    
    [ForeignKey("UserId")]
    public Employee User { get; set; } = null!;

    public DateTime JoinedAt { get; set; }
    
    public DateTime? LastReadAt { get; set; }

    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }
}

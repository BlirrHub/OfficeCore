using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OfficeCore.Api.Models;

public class ChatMessageReaction : ITenantEntity
{
    [Key]
    public Guid Id { get; set; }

    public Guid ChatMessageId { get; set; }

    [ForeignKey("ChatMessageId")]
    public ChatMessage ChatMessage { get; set; } = null!;

    [Required]
    public string UserId { get; set; } = string.Empty; // Employee.Id

    [ForeignKey("UserId")]
    public Employee User { get; set; } = null!;

    [Required]
    [MaxLength(16)]
    public string Emoji { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }
}

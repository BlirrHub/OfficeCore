using System;

namespace OfficeCore.Api.Models;

public class OrganizationInvitation : ITenantEntity
{
    public Guid Id { get; set; }
    
    // ITenantEntity
    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }
    
    public string? Email { get; set; }
    public required string Token { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    
    // Whether it has been accepted
    public bool IsAccepted { get; set; }
    public DateTime? AcceptedAt { get; set; }
    
    // Who invited them
    public string? InvitedByUserId { get; set; }
    public ApplicationUser? InvitedBy { get; set; }
}

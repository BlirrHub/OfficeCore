using System;
using System.ComponentModel.DataAnnotations;

namespace OfficeCore.Api.Models;

public class FingerprintDevice : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    public FingerprintConnectionType ConnectionType { get; set; }
    
    // For Wifi/TCP Client
    [MaxLength(50)]
    public string? IpAddress { get; set; }
    
    public bool IsActive { get; set; } = true;

    // Last time the device pinged the server (useful for Wifi)
    public DateTimeOffset? LastSeen { get; set; }

    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }
}

public enum FingerprintConnectionType
{
    Wired, // Handled via Web Serial API on client side
    WiFi   // Handled via TCP Server on backend
}

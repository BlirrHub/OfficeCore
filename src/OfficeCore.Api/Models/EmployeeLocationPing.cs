using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OfficeCore.Api.Models;

public class EmployeeLocationPing : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string EmployeeId { get; set; } = string.Empty;

    [ForeignKey(nameof(EmployeeId))]
    public Employee? Employee { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public double? AccuracyMeters { get; set; }

    public double? SpeedKmh { get; set; }

    [MaxLength(250)]
    public string? StatusNote { get; set; } // e.g. "En route to Client A", "Delivering parcel #120"

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }
}

namespace OfficeCore.Api.Models;

public interface ITenantEntity
{
    Guid OrganizationId { get; set; }
    Organization? Organization { get; set; }
}

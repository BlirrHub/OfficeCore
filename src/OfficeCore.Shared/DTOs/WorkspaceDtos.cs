namespace OfficeCore.Shared.DTOs;

public class WorkspaceSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public DateTime JoinedAt { get; set; }
}

public class InvitationDetailsDto
{
    public string Token { get; set; } = string.Empty;
    public string OrganizationName { get; set; } = string.Empty;
    public string InvitedBy { get; set; } = string.Empty;
    public bool HasEmail { get; set; }
    public string? TargetEmail { get; set; }
}

public class JoinWorkspaceResponseDto
{
    public string Message { get; set; } = string.Empty;
    public Guid OrganizationId { get; set; }
    public string OrganizationName { get; set; } = string.Empty;
}

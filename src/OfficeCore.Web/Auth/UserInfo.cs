namespace OfficeCore.Web.Client.Auth;

/// <summary>
/// Represents the authenticated user's basic information returned from /api/auth/me.
/// Shared between Server and WASM Blazor contexts.
/// </summary>
public class UserInfo
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? ProfilePictureUrl { get; set; }
    public string? TitleRole { get; set; }
    public IEnumerable<string> Roles { get; set; } = [];
    public IEnumerable<string> Modules { get; set; } = [];
    public bool MustChangePassword { get; set; }

    public string FullName => $"{FirstName} {LastName}".Trim();

    public bool HasModule(string module) =>
        Modules.Contains(module, StringComparer.OrdinalIgnoreCase);

    public bool IsAdmin() =>
        Roles.Contains("Admin", StringComparer.OrdinalIgnoreCase);
}

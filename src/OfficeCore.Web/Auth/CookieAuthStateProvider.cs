using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;

namespace OfficeCore.Web.Client.Auth;

/// <summary>
/// Used in both the Server-Side (SignalR) and WASM contexts.
/// Calls /api/auth/me to determine if the user is authenticated, and builds
/// a ClaimsPrincipal from the returned UserInfo.
/// </summary>
public class CookieAuthStateProvider : AuthenticationStateProvider
{
    private readonly HttpClient _httpClient;

    private static readonly AuthenticationState _anonymous =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    public CookieAuthStateProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/auth/me");

            if (!response.IsSuccessStatusCode)
                return _anonymous;

            var json = await response.Content.ReadAsStringAsync();
            var userInfo = JsonSerializer.Deserialize<UserInfo>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (userInfo == null)
                return _anonymous;

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, userInfo.Id),
                new(ClaimTypes.Name,           userInfo.Email),
                new(ClaimTypes.Email,          userInfo.Email),
                new("FirstName",               userInfo.FirstName),
                new("LastName",                userInfo.LastName),
                new("MustChangePassword",      userInfo.MustChangePassword.ToString())
            };

            if (!string.IsNullOrEmpty(userInfo.TitleRole))
                claims.Add(new Claim("TitleRole", userInfo.TitleRole));

            if (!string.IsNullOrEmpty(userInfo.ProfilePictureUrl))
                claims.Add(new Claim("ProfilePictureUrl", userInfo.ProfilePictureUrl));

            foreach (var role in userInfo.Roles)
                claims.Add(new Claim(ClaimTypes.Role, role));

            foreach (var module in userInfo.Modules)
                claims.Add(new Claim("ModuleAccess", module));

            // Ensure default modules are always granted for authenticated accounts
            var defaultModules = new[] { "Dashboard", "MyAttendance", "MyLeaves", "Advances" };
            foreach (var dm in defaultModules)
            {
                if (!userInfo.Modules.Any(m => string.Equals(m, dm, StringComparison.OrdinalIgnoreCase)))
                {
                    claims.Add(new Claim("ModuleAccess", dm));
                }
            }

            var identity = new ClaimsIdentity(claims, "Cookie");
            var principal = new ClaimsPrincipal(identity);
            return new AuthenticationState(principal);
        }
        catch
        {
            return _anonymous;
        }
    }

    /// <summary>Call this after login/logout to refresh the auth state in the UI.</summary>
    public void NotifyAuthStateChanged() =>
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
}

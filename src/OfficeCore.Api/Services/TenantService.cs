using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Models;

namespace OfficeCore.Api.Services;

/// <summary>
/// Provides tenant (organization) context resolution for the current HTTP request.
/// Reads the active OrganizationId from the cookie and resolves the current Employee record.
/// </summary>
public class TenantService
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantService(AppDbContext context, UserManager<ApplicationUser> userManager, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _userManager = userManager;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Gets the active OrganizationId from the cookie, or null if none is set.
    /// </summary>
    public Guid? GetActiveOrganizationId()
    {
        var request = _httpContextAccessor.HttpContext?.Request;
        if (request != null && request.Cookies.TryGetValue("ActiveOrganizationId", out var orgIdString) && Guid.TryParse(orgIdString, out var orgId))
        {
            return orgId;
        }
        return null;
    }

    /// <summary>
    /// Gets the active OrganizationId or Guid.Empty if not resolved.
    /// </summary>
    public Guid CurrentOrganizationId => GetActiveOrganizationId() ?? Guid.Empty;

    /// <summary>
    /// Gets the current user's ApplicationUser ID from claims.
    /// </summary>
    public string? GetUserId()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        return user == null ? null : _userManager.GetUserId(user);
    }

    /// <summary>
    /// Gets the current user's Employee record for the active organization.
    /// Returns null if the user is not a member of the active org or there's no active org.
    /// </summary>
    public async Task<Employee?> GetCurrentEmployeeAsync()
    {
        var userId = GetUserId();
        var orgId = GetActiveOrganizationId();

        if (userId == null || orgId == null) return null;

        return await _context.Employees
            .FirstOrDefaultAsync(e => e.UserId == userId && e.OrganizationId == orgId);
    }

    /// <summary>
    /// Resolves a global user's Employee record for the active organization by their ApplicationUser ID.
    /// </summary>
    public async Task<Employee?> GetEmployeeByUserIdAsync(string applicationUserId)
    {
        var orgId = GetActiveOrganizationId();
        if (orgId == null) return null;

        return await _context.Employees
            .FirstOrDefaultAsync(e => e.UserId == applicationUserId && e.OrganizationId == orgId);
    }
}

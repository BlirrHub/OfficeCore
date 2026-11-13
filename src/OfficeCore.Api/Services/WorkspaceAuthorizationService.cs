using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using OfficeCore.Api.Models;
using OfficeCore.Shared.Authorization;

namespace OfficeCore.Api.Services;

public sealed record WorkspaceAccessContext(
    bool IsActiveMember,
    bool IsOwner,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> TitleRoles,
    IReadOnlyList<string> Modules,
    IReadOnlyList<string> Permissions)
{
    public static readonly WorkspaceAccessContext Empty = new(
        IsActiveMember: false,
        IsOwner: false,
        Roles: [],
        TitleRoles: [],
        Modules: [],
        Permissions: []);
}

public interface IWorkspaceAuthorizationService
{
    Task<WorkspaceAccessContext> GetAccessAsync(string userId, Guid orgId, CancellationToken cancellationToken = default);
    void InvalidateWorkspace(Guid orgId);
    void InvalidateUser(Guid orgId, string userId);
}

public sealed class WorkspaceAuthorizationService : IWorkspaceAuthorizationService
{
    private readonly AppDbContext _context;
    private readonly IMemoryCache? _cache;

    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public WorkspaceAuthorizationService(AppDbContext context, IMemoryCache? cache = null)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<WorkspaceAccessContext> GetAccessAsync(string userId, Guid orgId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || orgId == Guid.Empty)
            return WorkspaceAccessContext.Empty;

        if (_cache == null)
            return await ComputeAccessAsync(userId, orgId, cancellationToken);

        var epoch = _cache.GetOrCreate(VersionKey(orgId), entry =>
        {
            entry.Priority = CacheItemPriority.NeverRemove;
            return Guid.NewGuid().ToString("N");
        })!;

        var cacheKey = EntryKey(orgId, epoch, userId);
        if (_cache.TryGetValue<WorkspaceAccessContext>(cacheKey, out var cached) && cached != null)
            return cached;

        var computed = await ComputeAccessAsync(userId, orgId, cancellationToken);
        _cache.Set(cacheKey, computed, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheDuration
        });

        return computed;
    }

    public void InvalidateWorkspace(Guid orgId)
    {
        if (_cache == null || orgId == Guid.Empty)
            return;

        _cache.Set(VersionKey(orgId), Guid.NewGuid().ToString("N"), new MemoryCacheEntryOptions
        {
            Priority = CacheItemPriority.NeverRemove
        });
    }

    public void InvalidateUser(Guid orgId, string userId)
    {
        if (_cache == null || orgId == Guid.Empty || string.IsNullOrWhiteSpace(userId))
            return;

        if (_cache.TryGetValue<string>(VersionKey(orgId), out var epoch) && !string.IsNullOrEmpty(epoch))
        {
            _cache.Remove(EntryKey(orgId, epoch, userId));
        }
    }

    private async Task<WorkspaceAccessContext> ComputeAccessAsync(string userId, Guid orgId, CancellationToken cancellationToken)
    {
        var employee = await _context.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.UserId == userId && e.OrganizationId == orgId && e.IsActive, cancellationToken);

        if (employee == null)
            return WorkspaceAccessContext.Empty;

        var org = await _context.Organizations
            .AsNoTracking()
            .Where(o => o.Id == orgId)
            .Select(o => new { o.CreatedByUserId })
            .FirstOrDefaultAsync(cancellationToken);

        bool isOwner = org != null && string.Equals(org.CreatedByUserId, userId, StringComparison.Ordinal);

        var roles = new List<string>();
        var titleRoles = new List<string>();
        var permissionSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var moduleSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (isOwner)
        {
            foreach (var module in ModuleCatalog.AllKeys)
                moduleSet.Add(module);
            foreach (var permission in PermissionCatalog.AllKeys)
                permissionSet.Add(permission);
        }
        else
        {
            foreach (var module in ModuleCatalog.DefaultKeys)
                moduleSet.Add(module);
            foreach (var permission in PermissionCatalog.ExpandModules(ModuleCatalog.DefaultKeys))
                permissionSet.Add(permission);
        }

        var employeeRoles = await _context.EmployeeRoles
            .AsNoTracking()
            .Include(er => er.Role)
                .ThenInclude(r => r!.Claims)
            .Where(er => er.EmployeeId == employee.Id)
            .ToListAsync(cancellationToken);

        foreach (var er in employeeRoles)
        {
            if (er.Role == null)
                continue;

            if (!string.IsNullOrEmpty(er.Role.Name))
            {
                roles.Add(er.Role.Name);
                if (er.Role.ShowAsTitle)
                    titleRoles.Add(er.Role.Name);
            }

            var explicitPermissions = er.Role.Claims
                .Where(c => c.ClaimType == PermissionCatalog.ClaimType)
                .Select(c => c.ClaimValue)
                .ToArray();

            var legacyModules = er.Role.Claims
                .Where(c => c.ClaimType == "ModuleAccess")
                .Select(c => c.ClaimValue)
                .ToArray();

            // Granular Permission claims are the source of truth; expand legacy ModuleAccess only if no explicit permissions exist.
            var resolvedPermissions = explicitPermissions.Length > 0
                ? explicitPermissions
                : PermissionCatalog.ExpandModules(legacyModules);

            foreach (var perm in resolvedPermissions)
                permissionSet.Add(perm);

            foreach (var mod in PermissionCatalog.DeriveModules(resolvedPermissions))
                moduleSet.Add(mod);

            foreach (var mod in legacyModules)
                moduleSet.Add(mod);
        }

        return new WorkspaceAccessContext(
            IsActiveMember: true,
            IsOwner: isOwner,
            Roles: roles,
            TitleRoles: titleRoles,
            Modules: moduleSet.OrderBy(m => m, StringComparer.Ordinal).ToArray(),
            Permissions: permissionSet.OrderBy(p => p, StringComparer.Ordinal).ToArray());
    }

    private static string VersionKey(Guid orgId) => $"ws-auth-ver:{orgId:N}";
    private static string EntryKey(Guid orgId, string epoch, string userId) => $"ws-auth:{orgId:N}:{epoch}:{userId}";
}

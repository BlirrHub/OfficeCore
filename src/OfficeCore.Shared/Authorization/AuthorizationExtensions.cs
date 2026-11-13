using System.Security.Claims;

namespace OfficeCore.Shared.Authorization;

public static class AuthorizationExtensions
{
    public static HashSet<string> GetEffectivePermissions(this ClaimsPrincipal? user)
    {
        if (user == null)
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var explicitPermissions = user.FindAll(PermissionCatalog.ClaimType)
            .Select(c => c.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToList();

        if (explicitPermissions.Count > 0)
            return new HashSet<string>(explicitPermissions, StringComparer.OrdinalIgnoreCase);

        var legacyModules = user.FindAll("ModuleAccess")
            .Select(c => c.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v));

        return new HashSet<string>(PermissionCatalog.ExpandModules(legacyModules), StringComparer.OrdinalIgnoreCase);
    }

    public static bool HasPermission(this ClaimsPrincipal? user, string permission)
    {
        if (user?.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(permission))
            return false;

        if (user.HasClaim(c => c.Type == PermissionCatalog.ClaimType
            && string.Equals(c.Value, permission, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        // Support legacy principals populated only with ModuleAccess claims
        return !user.HasClaim(c => c.Type == PermissionCatalog.ClaimType)
            && user.GetEffectivePermissions().Contains(permission);
    }

    public static bool HasModuleAccess(this ClaimsPrincipal? user, string module)
    {
        if (user?.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(module))
            return false;

        // Granular permissions for the module are the authoritative source of truth.
        var modulePermissionKeys = PermissionCatalog.KeysForModule(module);
        if (modulePermissionKeys.Count > 0)
        {
            var effectivePermissions = user.GetEffectivePermissions();
            if (modulePermissionKeys.Any(k => effectivePermissions.Contains(k)))
                return true;
        }

        // Default self-service modules (e.g. Dashboard, MyAttendance) and explicit legacy claims.
        return user.HasClaim(c => c.Type == "ModuleAccess"
            && string.Equals(c.Value, module, StringComparison.OrdinalIgnoreCase));
    }

    public static HashSet<string> GetEffectiveModules(this ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var modules = new HashSet<string>(
            user.FindAll("ModuleAccess").Select(c => c.Value),
            StringComparer.OrdinalIgnoreCase);

        foreach (var mod in PermissionCatalog.DeriveModules(user.GetEffectivePermissions()))
        {
            modules.Add(mod);
        }

        return modules;
    }
}

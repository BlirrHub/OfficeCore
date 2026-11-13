using System.Security.Claims;
using OfficeCore.Shared.Authorization;
using Xunit;

namespace OfficeCore.Authorization.Tests;

public class ModuleCatalogTests
{
    [Fact]
    public void Catalog_keys_are_unique_and_nonempty()
    {
        Assert.All(ModuleCatalog.All, module => Assert.False(string.IsNullOrWhiteSpace(module.Key)));
        Assert.Equal(ModuleCatalog.All.Count, ModuleCatalog.AllKeys.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Default_modules_are_part_of_the_catalog()
    {
        Assert.NotEmpty(ModuleCatalog.DefaultKeys);
        Assert.All(ModuleCatalog.DefaultKeys, key => Assert.Contains(key, ModuleCatalog.AllKeys));
    }

    [Fact]
    public void Normalize_many_canonicalizes_and_removes_duplicates()
    {
        var valid = ModuleCatalog.TryNormalizeMany([" payroll ", "PAYROLL", "Users"], out var normalized, out var invalid);

        Assert.True(valid);
        Assert.Empty(invalid);
        Assert.Equal([AccessModules.Payroll, AccessModules.Users], normalized);
    }

    [Fact]
    public void Normalize_many_rejects_unknown_modules()
    {
        var valid = ModuleCatalog.TryNormalizeMany([AccessModules.Payroll, "SuperAdmin"], out var normalized, out var invalid);

        Assert.False(valid);
        Assert.Contains(AccessModules.Payroll, normalized);
        Assert.Equal(["SuperAdmin"], invalid);
    }

    [Fact]
    public void Permission_keys_are_unique_known_and_grouped_by_module()
    {
        Assert.Equal(PermissionCatalog.All.Count, PermissionCatalog.AllKeys.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(PermissionCatalog.All, permission =>
        {
            Assert.False(string.IsNullOrWhiteSpace(permission.Key));
            Assert.Contains(permission.Module, ModuleCatalog.AllKeys);
        });
    }

    [Fact]
    public void Module_expansion_provides_legacy_roles_with_action_permissions()
    {
        var permissions = PermissionCatalog.ExpandModules([AccessModules.Roles, AccessModules.AuditLogs]);

        Assert.Contains(AccessPermissions.RolesEdit, permissions);
        Assert.Contains(AccessPermissions.RolesDelete, permissions);
        Assert.Contains(AccessPermissions.AuditView, permissions);
        Assert.Contains(AccessPermissions.AuditExport, permissions);
    }

    [Fact]
    public void Permission_normalization_rejects_unknown_values()
    {
        var valid = PermissionCatalog.TryNormalizeMany(
            [" AUDIT.VIEW ", "audit.view", "system.root"], out var normalized, out var invalid);

        Assert.False(valid);
        Assert.Equal([AccessPermissions.AuditView], normalized);
        Assert.Equal(["system.root"], invalid);
    }

    [Fact]
    public void Keys_for_module_and_derive_modules_map_permissions_back_to_modules()
    {
        var payrollKeys = PermissionCatalog.KeysForModule(AccessModules.Payroll);

        Assert.Contains(AccessPermissions.PayrollView, payrollKeys);
        Assert.Contains(AccessPermissions.PayrollFinalize, payrollKeys);
        Assert.DoesNotContain(AccessPermissions.ClientsView, payrollKeys);

        var derived = PermissionCatalog.DeriveModules([AccessPermissions.PayrollView, AccessPermissions.AuditExport]);

        Assert.Contains(AccessModules.Payroll, derived);
        Assert.Contains(AccessModules.AuditLogs, derived);
        Assert.DoesNotContain(AccessModules.Users, derived);
    }

    [Fact]
    public void Claims_principal_derives_module_access_from_permission_claims_without_module_claims()
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "user-1"),
            new Claim(PermissionCatalog.ClaimType, AccessPermissions.ClientsView)
        ], "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        Assert.True(principal.HasPermission(AccessPermissions.ClientsView));
        Assert.False(principal.HasPermission(AccessPermissions.ClientsManage));
        Assert.True(principal.HasModuleAccess(AccessModules.Clients));
        Assert.False(principal.HasModuleAccess(AccessModules.Payroll));
        Assert.Contains(AccessModules.Clients, principal.GetEffectiveModules());

        // Unauthenticated principal gets neither permissions nor modules
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
        Assert.False(anonymous.HasModuleAccess(AccessModules.Clients));
        Assert.False(anonymous.HasModuleAccess(AccessModules.Dashboard));
    }
}

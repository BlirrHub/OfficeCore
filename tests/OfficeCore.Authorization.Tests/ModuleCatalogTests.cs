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
}

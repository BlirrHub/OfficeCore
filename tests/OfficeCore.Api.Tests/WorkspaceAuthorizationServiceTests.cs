using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using OfficeCore.Api.Controllers;
using OfficeCore.Api.Models;
using OfficeCore.Api.Services;
using OfficeCore.Shared.Authorization;
using System.Security.Claims;
using Xunit;

namespace OfficeCore.Api.Tests;

public class WorkspaceAuthorizationServiceTests
{
    private static ServiceProvider CreateProvider(out AppDbContext db, out IMemoryCache cache)
    {
        var services = new ServiceCollection();
        var dbName = $"ws-auth-{Guid.NewGuid():N}";
        services.AddLogging();
        services.AddMemoryCache();
        services.AddDbContext<AppDbContext>(o =>
            o.UseInMemoryDatabase(dbName).ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        services.AddIdentityCore<ApplicationUser>()
            .AddEntityFrameworkStores<AppDbContext>();
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = new DefaultHttpContext() });
        services.AddScoped<TenantService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IWorkspaceAuthorizationService, WorkspaceAuthorizationService>();

        var provider = services.BuildServiceProvider();
        db = provider.GetRequiredService<AppDbContext>();
        cache = provider.GetRequiredService<IMemoryCache>();
        return provider;
    }

    [Fact]
    public async Task Derives_modules_from_permission_claims_and_expands_legacy_module_only_roles()
    {
        using var provider = CreateProvider(out var db, out var cache);
        var orgId = Guid.NewGuid();
        var owner = new ApplicationUser { Id = "owner-1", UserName = "owner@test.com", Email = "owner@test.com", FirstName = "O", LastName = "W" };
        var granularUser = new ApplicationUser { Id = "granular-1", UserName = "granular@test.com", Email = "granular@test.com", FirstName = "G", LastName = "U" };
        var legacyUser = new ApplicationUser { Id = "legacy-1", UserName = "legacy@test.com", Email = "legacy@test.com", FirstName = "L", LastName = "U" };

        var org = new Organization { Id = orgId, Name = "Workspace A", CreatedByUserId = owner.Id };

        var granularEmp = new Employee { Id = "emp-granular", UserId = granularUser.Id, OrganizationId = orgId, IsActive = true };
        var legacyEmp = new Employee { Id = "emp-legacy", UserId = legacyUser.Id, OrganizationId = orgId, IsActive = true };

        var granularRole = new OrganizationRole { Id = Guid.NewGuid(), OrganizationId = orgId, Name = "Payroll Reader" };
        var legacyRole = new OrganizationRole { Id = Guid.NewGuid(), OrganizationId = orgId, Name = "Legacy Clients" };

        db.AddRange(owner, granularUser, legacyUser, org, granularEmp, legacyEmp, granularRole, legacyRole);
        db.OrganizationRoleClaims.Add(new OrganizationRoleClaim
        {
            OrganizationRoleId = granularRole.Id,
            ClaimType = PermissionCatalog.ClaimType,
            ClaimValue = AccessPermissions.PayrollView
        });
        // Legacy role has only ModuleAccess claim, zero Permission claims
        db.OrganizationRoleClaims.Add(new OrganizationRoleClaim
        {
            OrganizationRoleId = legacyRole.Id,
            ClaimType = "ModuleAccess",
            ClaimValue = AccessModules.Clients
        });
        db.EmployeeRoles.AddRange(
            new EmployeeRole { EmployeeId = granularEmp.Id, OrganizationRoleId = granularRole.Id },
            new EmployeeRole { EmployeeId = legacyEmp.Id, OrganizationRoleId = legacyRole.Id }
        );
        await db.SaveChangesAsync();

        var service = new WorkspaceAuthorizationService(db, cache);

        var granularAccess = await service.GetAccessAsync(granularUser.Id, orgId);
        Assert.True(granularAccess.IsActiveMember);
        Assert.Contains(AccessPermissions.PayrollView, granularAccess.Permissions);
        Assert.Contains(AccessModules.Payroll, granularAccess.Modules);
        Assert.DoesNotContain(AccessPermissions.PayrollCalculate, granularAccess.Permissions);

        var legacyAccess = await service.GetAccessAsync(legacyUser.Id, orgId);
        Assert.True(legacyAccess.IsActiveMember);
        Assert.Contains(AccessModules.Clients, legacyAccess.Modules);
        Assert.Contains(AccessPermissions.ClientsView, legacyAccess.Permissions);
        Assert.Contains(AccessPermissions.ClientsManage, legacyAccess.Permissions);
    }

    [Fact]
    public async Task Cache_invalidates_per_user_and_per_workspace()
    {
        using var provider = CreateProvider(out var db, out var cache);
        var orgId = Guid.NewGuid();
        var user = new ApplicationUser { Id = "member-1", UserName = "member@test.com", Email = "member@test.com", FirstName = "M", LastName = "One" };
        var org = new Organization { Id = orgId, Name = "Workspace B", CreatedByUserId = "owner-other" };
        db.AddRange(user, org);
        await db.SaveChangesAsync();

        var service = new WorkspaceAuthorizationService(db, cache);

        // First call caches non-member Empty result
        var beforeJoin = await service.GetAccessAsync(user.Id, orgId);
        Assert.False(beforeJoin.IsActiveMember);

        // Add employee directly to DB (cache still holds stale Empty)
        var emp = new Employee { Id = "emp-1", UserId = user.Id, OrganizationId = orgId, IsActive = true };
        var role = new OrganizationRole { Id = Guid.NewGuid(), OrganizationId = orgId, Name = "Auditor" };
        db.AddRange(emp, role);
        db.EmployeeRoles.Add(new EmployeeRole { EmployeeId = emp.Id, OrganizationRoleId = role.Id });
        await db.SaveChangesAsync();

        var stillCached = await service.GetAccessAsync(user.Id, orgId);
        Assert.False(stillCached.IsActiveMember);

        // Per-user invalidation refreshes immediately
        service.InvalidateUser(orgId, user.Id);
        var afterUserInvalidate = await service.GetAccessAsync(user.Id, orgId);
        Assert.True(afterUserInvalidate.IsActiveMember);
        Assert.DoesNotContain(AccessPermissions.AuditView, afterUserInvalidate.Permissions);

        // Grant permission on role -> still cached until workspace invalidation
        db.OrganizationRoleClaims.Add(new OrganizationRoleClaim
        {
            OrganizationRoleId = role.Id,
            ClaimType = PermissionCatalog.ClaimType,
            ClaimValue = AccessPermissions.AuditView
        });
        await db.SaveChangesAsync();

        var beforeWorkspaceInvalidate = await service.GetAccessAsync(user.Id, orgId);
        Assert.DoesNotContain(AccessPermissions.AuditView, beforeWorkspaceInvalidate.Permissions);

        service.InvalidateWorkspace(orgId);
        var afterWorkspaceInvalidate = await service.GetAccessAsync(user.Id, orgId);
        Assert.Contains(AccessPermissions.AuditView, afterWorkspaceInvalidate.Permissions);
        Assert.Contains(AccessModules.AuditLogs, afterWorkspaceInvalidate.Modules);
    }

    [Fact]
    public async Task Create_and_update_role_persist_only_permission_claims_and_derive_modules_on_read()
    {
        using var provider = CreateProvider(out var db, out var cache);
        var orgId = Guid.NewGuid();
        var owner = new ApplicationUser { Id = "owner-role-1", UserName = "owner@test.com", Email = "owner@test.com", FirstName = "O", LastName = "R" };
        var org = new Organization { Id = orgId, Name = "Role Workspace", CreatedByUserId = owner.Id };
        var ownerEmp = new Employee { Id = "emp-owner-role", UserId = owner.Id, OrganizationId = orgId, IsActive = true };
        db.AddRange(owner, org, ownerEmp);
        await db.SaveChangesAsync();

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, owner.Id),
                new Claim(PermissionCatalog.ClaimType, AccessPermissions.RolesView),
                new Claim(PermissionCatalog.ClaimType, AccessPermissions.RolesCreate),
                new Claim(PermissionCatalog.ClaimType, AccessPermissions.RolesEdit),
                new Claim(PermissionCatalog.ClaimType, AccessPermissions.ClientsView),
                new Claim(PermissionCatalog.ClaimType, AccessPermissions.ClientsManage),
                new Claim(PermissionCatalog.ClaimType, AccessPermissions.PayrollView)
            ], "TestAuth"))
        };
        httpContext.Request.Headers["Cookie"] = $"ActiveOrganizationId={orgId}";
        provider.GetRequiredService<IHttpContextAccessor>().HttpContext = httpContext;

        var tenant = provider.GetRequiredService<TenantService>();
        var audit = provider.GetRequiredService<IAuditService>();
        var workspaceAuth = provider.GetRequiredService<IWorkspaceAuthorizationService>();
        var controller = new RoleController(db, tenant, audit, workspaceAuth)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        var createResult = await controller.CreateRole(new CreateRoleRequest
        {
            Name = "Billing Specialist",
            Modules = [AccessModules.Payroll], // Deliberately different from Permissions to verify Permissions rule
            Permissions = [AccessPermissions.ClientsView]
        });
        Assert.IsType<OkObjectResult>(createResult);

        var roleEntity = await db.OrganizationRoles.Include(r => r.Claims).SingleAsync(r => r.Name == "Billing Specialist");
        Assert.DoesNotContain(roleEntity.Claims, c => c.ClaimType == "ModuleAccess");
        Assert.Equal([AccessPermissions.ClientsView], roleEntity.Claims.Where(c => c.ClaimType == PermissionCatalog.ClaimType).Select(c => c.ClaimValue));

        var getResult = Assert.IsType<OkObjectResult>(await controller.GetRoles());
        var rolesPayload = Assert.IsAssignableFrom<IEnumerable<object>>(getResult.Value).ToList();
        var serialized = System.Text.Json.JsonSerializer.Serialize(rolesPayload.Single());
        Assert.Contains(AccessModules.Clients, serialized);
        Assert.DoesNotContain(AccessModules.Payroll, serialized);
    }
}

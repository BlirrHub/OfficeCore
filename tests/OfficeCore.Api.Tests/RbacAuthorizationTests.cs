using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OfficeCore.Api.Controllers;
using OfficeCore.Api.Models;
using OfficeCore.Api.Services;
using OfficeCore.Shared.Authorization;
using Xunit;

namespace OfficeCore.Api.Tests;

public class RbacAuthorizationTests
{
    [Fact]
    public async Task Delegated_role_manager_can_create_subset_role_but_not_escalate_or_create_admin()
    {
        var services = new ServiceCollection();
        var dbName = $"rbac-roles-{Guid.NewGuid():N}";
        services.AddLogging();
        services.AddDbContext<AppDbContext>(o =>
            o.UseInMemoryDatabase(dbName).ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        services.AddIdentityCore<ApplicationUser>()
            .AddEntityFrameworkStores<AppDbContext>();

        var http = new DefaultHttpContext();
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = http });
        services.AddScoped<TenantService>();
        services.AddScoped<IAuditService, AuditService>();

        await using var provider = services.BuildServiceProvider();
        var db = provider.GetRequiredService<AppDbContext>();
        var tenant = provider.GetRequiredService<TenantService>();

        var owner = new ApplicationUser { Id = Guid.NewGuid().ToString(), UserName = "owner@test.com", Email = "owner@test.com", FirstName = "Owner", LastName = "User" };
        var subAdmin = new ApplicationUser { Id = Guid.NewGuid().ToString(), UserName = "sub@test.com", Email = "sub@test.com", FirstName = "Sub", LastName = "Admin" };
        var org = new Organization { Id = Guid.NewGuid(), Name = "Org", CreatedByUserId = owner.Id };
        var subEmp = new Employee { Id = Guid.NewGuid().ToString(), UserId = subAdmin.Id, User = subAdmin, OrganizationId = org.Id, IsActive = true };
        db.AddRange(owner, subAdmin, org, subEmp);
        await db.SaveChangesAsync();

        http.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, subAdmin.Id),
            new Claim("ModuleAccess", AccessModules.Roles),
            new Claim("ModuleAccess", AccessModules.Clients),
            new Claim(PermissionCatalog.ClaimType, AccessPermissions.RolesCreate),
            new Claim(PermissionCatalog.ClaimType, AccessPermissions.RolesEdit),
            new Claim(PermissionCatalog.ClaimType, AccessPermissions.ClientsView)
        }, "Test"));
        http.Request.Headers.Cookie = $"ActiveOrganizationId={org.Id}";

        var controller = new RoleController(db, tenant, provider.GetRequiredService<IAuditService>())
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };

        // 1. Can create a role with subset permissions (ClientsView)
        var allowedResult = await controller.CreateRole(new CreateRoleRequest
        {
            Name = "Client Reader",
            Modules = [AccessModules.Clients],
            Permissions = [AccessPermissions.ClientsView]
        });
        Assert.IsType<OkObjectResult>(allowedResult);

        // 2. Cannot create a role with permissions the caller doesn't hold (ClientsManage)
        var escalateResult = await controller.CreateRole(new CreateRoleRequest
        {
            Name = "Client Manager",
            Modules = [AccessModules.Clients],
            Permissions = [AccessPermissions.ClientsManage]
        });
        Assert.IsType<ForbidResult>(escalateResult);

        // 3. Cannot create a role named "Admin"
        var adminNameResult = await controller.CreateRole(new CreateRoleRequest
        {
            Name = "Admin",
            Modules = [AccessModules.Clients],
            Permissions = [AccessPermissions.ClientsView]
        });
        Assert.IsType<ForbidResult>(adminNameResult);
    }

    [Fact]
    public async Task Invite_targeted_to_email_rejects_different_user_and_accepts_matching_email()
    {
        var services = new ServiceCollection();
        var dbName = $"rbac-invite-{Guid.NewGuid():N}";
        services.AddLogging();
        services.AddDbContext<AppDbContext>(o =>
            o.UseInMemoryDatabase(dbName).ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        services.AddIdentityCore<ApplicationUser>()
            .AddEntityFrameworkStores<AppDbContext>();

        var http = new DefaultHttpContext();
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = http });
        services.AddScoped<TenantService>();
        services.AddScoped<IAuditService, AuditService>();

        await using var provider = services.BuildServiceProvider();
        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var tenant = provider.GetRequiredService<TenantService>();

        var owner = new ApplicationUser { Id = Guid.NewGuid().ToString(), UserName = "owner@test.com", Email = "owner@test.com", FirstName = "Owner", LastName = "User" };
        var invitedUser = new ApplicationUser { Id = Guid.NewGuid().ToString(), UserName = "invited@test.com", Email = "invited@test.com", FirstName = "Invited", LastName = "User" };
        var wrongUser = new ApplicationUser { Id = Guid.NewGuid().ToString(), UserName = "wrong@test.com", Email = "wrong@test.com", FirstName = "Wrong", LastName = "User" };
        var org = new Organization { Id = Guid.NewGuid(), Name = "Org", CreatedByUserId = owner.Id };
        var invite = new OrganizationInvitation
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            Organization = org,
            Email = "invited@test.com",
            Token = "INVITE123",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            InvitedByUserId = owner.Id
        };
        db.AddRange(owner, invitedUser, wrongUser, org, invite);
        await db.SaveChangesAsync();

        var controller = new OrganizationsController(db, userManager, null!, tenant)
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };

        // Wrong user attempts to accept targeted invite -> Forbid
        http.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, wrongUser.Id) }, "Test"));
        var wrongResult = await controller.Join(new JoinRequest { Token = "INVITE123" });
        var wrongForbidden = Assert.IsType<ObjectResult>(wrongResult);
        Assert.Equal(403, wrongForbidden.StatusCode);

        // Matching user accepts targeted invite -> Ok
        http.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, invitedUser.Id) }, "Test"));
        var okResult = await controller.Join(new JoinRequest { Token = "INVITE123" });
        Assert.IsType<OkObjectResult>(okResult);
    }

    [Theory]
    [InlineData(typeof(ClientController), nameof(ClientController.GetClients), "Permission:clients.view")]
    [InlineData(typeof(ClientController), nameof(ClientController.CreateClient), "Permission:clients.manage")]
    [InlineData(typeof(CompanyExpenseController), nameof(CompanyExpenseController.GetExpenses), "Permission:ledger.view")]
    [InlineData(typeof(CompanyExpenseController), nameof(CompanyExpenseController.CreateExpense), "Permission:ledger.manage")]
    [InlineData(typeof(AdvanceRequestController), nameof(AdvanceRequestController.GetAllRequests), "Permission:ledger.view")]
    [InlineData(typeof(AdvanceRequestController), nameof(AdvanceRequestController.ApproveRequest), "Permission:ledger.manage")]
    [InlineData(typeof(AdvanceRequestController), nameof(AdvanceRequestController.DeleteRequest), "RequireLedger")]
    [InlineData(typeof(AdvanceRequestController), nameof(AdvanceRequestController.DeleteRequest), "Permission:ledger.manage")]
    [InlineData(typeof(PayrollController), nameof(PayrollController.GetPayrollPeriods), "Permission:payroll.view")]
    [InlineData(typeof(PayrollController), nameof(PayrollController.ComputePayroll), "Permission:payroll.calculate")]
    [InlineData(typeof(PayrollController), nameof(PayrollController.FinalizePeriod), "Permission:payroll.finalize")]
    [InlineData(typeof(PayrollController), nameof(PayrollController.ExportPayrollSummaryCsv), "Permission:payroll.export")]
    [InlineData(typeof(PayrollSettingsController), nameof(PayrollSettingsController.UpdateGeneralSettings), "Permission:payroll.settings.manage")]
    [InlineData(typeof(AttendanceController), nameof(AttendanceController.GetAttendanceRecords), "Permission:attendance.view")]
    [InlineData(typeof(AttendanceController), nameof(AttendanceController.AdminCreateAttendanceRecord), "Permission:attendance.adjust")]
    [InlineData(typeof(LeaveRequestController), nameof(LeaveRequestController.GetAllRequests), "Permission:leave-approvals.view")]
    [InlineData(typeof(LeaveRequestController), nameof(LeaveRequestController.UpdateStatus), "Permission:leave-approvals.manage")]
    [InlineData(typeof(OvertimeController), nameof(OvertimeController.GetAdminRequests), "Permission:overtime-approvals.view")]
    [InlineData(typeof(OvertimeController), nameof(OvertimeController.ApproveRequest), "Permission:overtime-approvals.manage")]
    [InlineData(typeof(WorkScheduleController), nameof(WorkScheduleController.CreateWorkSchedule), "Permission:work-schedules.manage")]
    [InlineData(typeof(TimeOffPoliciesController), nameof(TimeOffPoliciesController.CreatePolicy), "Permission:time-off.manage")]
    [InlineData(typeof(HolidayCalendarController), nameof(HolidayCalendarController.CreateCalendar), "Permission:time-off.manage")]
    [InlineData(typeof(LocationsController), nameof(LocationsController.GetActiveFieldEmployees), "Permission:locations.view")]
    [InlineData(typeof(UserController), nameof(UserController.GetUsers), "Permission:users.view")]
    [InlineData(typeof(UserController), nameof(UserController.CreateUser), "Permission:users.manage")]
    public void Controller_action_enforces_expected_action_permission_policy(Type controllerType, string methodName, string expectedPolicy)
    {
        var method = controllerType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(method);
        var policies = method!.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Select(a => a.Policy).ToList();
        Assert.Contains(expectedPolicy, policies);
    }

    [Fact]
    public async Task Non_owner_manager_cannot_resign_or_remove_admin_while_owner_can_and_history_blocks_delete()
    {
        var services = new ServiceCollection();
        var dbName = $"rbac-users-{Guid.NewGuid():N}";
        services.AddLogging();
        services.AddDbContext<AppDbContext>(o =>
            o.UseInMemoryDatabase(dbName).ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        services.AddIdentityCore<ApplicationUser>()
            .AddEntityFrameworkStores<AppDbContext>();

        var http = new DefaultHttpContext();
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = http });
        services.AddScoped<TenantService>();
        services.AddScoped<IAuditService, AuditService>();

        await using var provider = services.BuildServiceProvider();
        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var tenant = provider.GetRequiredService<TenantService>();
        var audit = provider.GetRequiredService<IAuditService>();

        var owner = new ApplicationUser { Id = Guid.NewGuid().ToString(), UserName = "owner@test.com", Email = "owner@test.com", FirstName = "Owner", LastName = "User" };
        var manager = new ApplicationUser { Id = Guid.NewGuid().ToString(), UserName = "mgr@test.com", Email = "mgr@test.com", FirstName = "Mgr", LastName = "User" };
        var adminUser = new ApplicationUser { Id = Guid.NewGuid().ToString(), UserName = "admin@test.com", Email = "admin@test.com", FirstName = "Admin", LastName = "User" };
        var staffUser = new ApplicationUser { Id = Guid.NewGuid().ToString(), UserName = "staff@test.com", Email = "staff@test.com", FirstName = "Staff", LastName = "User" };

        var org = new Organization { Id = Guid.NewGuid(), Name = "Org", CreatedByUserId = owner.Id };
        var adminRole = new OrganizationRole { Id = Guid.NewGuid(), Name = "Admin", OrganizationId = org.Id };
        var ownerEmp = new Employee { Id = Guid.NewGuid().ToString(), UserId = owner.Id, User = owner, OrganizationId = org.Id, IsActive = true };
        var mgrEmp = new Employee { Id = Guid.NewGuid().ToString(), UserId = manager.Id, User = manager, OrganizationId = org.Id, IsActive = true };
        var adminEmp = new Employee
        {
            Id = Guid.NewGuid().ToString(),
            UserId = adminUser.Id,
            User = adminUser,
            OrganizationId = org.Id,
            IsActive = true,
            EmployeeRoles = new List<EmployeeRole> { new() { Role = adminRole, OrganizationRoleId = adminRole.Id } }
        };
        var staffEmp = new Employee { Id = Guid.NewGuid().ToString(), UserId = staffUser.Id, User = staffUser, OrganizationId = org.Id, IsActive = true };

        db.AddRange(owner, manager, adminUser, staffUser, org, adminRole, ownerEmp, mgrEmp, adminEmp, staffEmp);
        db.AttendanceRecords.Add(new AttendanceRecord
        {
            Id = Guid.NewGuid(),
            EmployeeId = staffEmp.Id,
            OrganizationId = org.Id,
            Timestamp = DateTimeOffset.UtcNow,
            Type = AttendanceType.ClockIn
        });
        await db.SaveChangesAsync();

        var controller = new UserController(userManager, db, tenant, audit, null!)
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };
        http.Request.Headers.Cookie = $"ActiveOrganizationId={org.Id}";

        // Non-owner manager cannot resign or delete privileged Admin
        http.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, manager.Id) }, "Test"));
        Assert.IsType<ForbidResult>(await controller.ResignUser(adminUser.Id, new ResignRequest { ResignedDate = DateTime.UtcNow.Date }));
        Assert.IsType<ForbidResult>(await controller.RemoveUserFromOrganization(adminUser.Id));

        // Employee with historical attendance records cannot be hard-deleted (prevents FK crash)
        Assert.IsType<BadRequestObjectResult>(await controller.RemoveUserFromOrganization(staffUser.Id));

        // Workspace owner CAN resign the Admin
        http.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, owner.Id) }, "Test"));
        Assert.IsType<OkObjectResult>(await controller.ResignUser(adminUser.Id, new ResignRequest { ResignedDate = DateTime.UtcNow.Date }));
        Assert.False((await db.Employees.FindAsync(adminEmp.Id))!.IsActive);
    }

    [Fact]
    public async Task Roleless_employee_claims_include_default_boards_view_permission()
    {
        var services = new ServiceCollection();
        var dbName = $"rbac-claims-{Guid.NewGuid():N}";
        services.AddLogging();
        services.AddDbContext<AppDbContext>(o =>
            o.UseInMemoryDatabase(dbName).ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        services.AddIdentityCore<ApplicationUser>()
            .AddEntityFrameworkStores<AppDbContext>();

        var http = new DefaultHttpContext();
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = http });
        services.AddScoped<TenantService>();

        await using var provider = services.BuildServiceProvider();
        var db = provider.GetRequiredService<AppDbContext>();
        var tenant = provider.GetRequiredService<TenantService>();

        var ownerId = Guid.NewGuid().ToString();
        var staffId = Guid.NewGuid().ToString();
        var org = new Organization { Id = Guid.NewGuid(), Name = "Org", CreatedByUserId = ownerId };
        db.Organizations.Add(org);
        db.Employees.Add(new Employee { Id = Guid.NewGuid().ToString(), UserId = staffId, OrganizationId = org.Id, IsActive = true });
        await db.SaveChangesAsync();

        http.Request.Headers.Cookie = $"ActiveOrganizationId={org.Id}";
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, staffId) }, "Test"));
        var transform = new OrganizationClaimsTransformation(db, tenant);

        var transformed = await transform.TransformAsync(principal);
        Assert.True(transformed.HasClaim(PermissionCatalog.ClaimType, AccessPermissions.BoardsView));
    }

    [Fact]
    public async Task BoardAdmin_cannot_grant_BoardRole_Owner()
    {
        var services = new ServiceCollection();
        var dbName = $"rbac-board-{Guid.NewGuid():N}";
        services.AddLogging();
        services.AddDbContext<AppDbContext>(o =>
            o.UseInMemoryDatabase(dbName).ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        services.AddIdentityCore<ApplicationUser>()
            .AddEntityFrameworkStores<AppDbContext>();

        var http = new DefaultHttpContext();
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = http });
        services.AddScoped<TenantService>();

        await using var provider = services.BuildServiceProvider();
        var db = provider.GetRequiredService<AppDbContext>();
        var tenant = provider.GetRequiredService<TenantService>();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();

        var ownerId = Guid.NewGuid().ToString();
        var boardAdminUserId = Guid.NewGuid().ToString();
        var targetUserId = Guid.NewGuid().ToString();
        var org = new Organization { Id = Guid.NewGuid(), Name = "Org", CreatedByUserId = ownerId };
        var boardAdminEmp = new Employee { Id = Guid.NewGuid().ToString(), UserId = boardAdminUserId, OrganizationId = org.Id, IsActive = true };
        var targetEmp = new Employee { Id = Guid.NewGuid().ToString(), UserId = targetUserId, OrganizationId = org.Id, IsActive = true };
        var board = new Board { Id = Guid.NewGuid(), Name = "Test Board", OrganizationId = org.Id, CreatedById = boardAdminEmp.Id };
        board.Members.Add(new BoardMember { Id = Guid.NewGuid(), BoardId = board.Id, UserId = boardAdminEmp.Id, Role = BoardRole.Admin, OrganizationId = org.Id });
        db.AddRange(org, boardAdminEmp, targetEmp, board);
        await db.SaveChangesAsync();

        http.Request.Headers.Cookie = $"ActiveOrganizationId={org.Id}";
        http.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, boardAdminUserId) }, "Test"));

        var controller = new BoardsController(db, userManager, null!, null!, tenant, null!)
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };

        var result = await controller.ManageMember(board.Id, new BoardsController.ManageMemberRequest
        {
            UserId = targetEmp.Id,
            Role = BoardRole.Owner
        });
        Assert.IsType<ForbidResult>(result);
    }

}

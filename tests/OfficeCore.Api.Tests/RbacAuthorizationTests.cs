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
        Assert.IsType<ForbidResult>(wrongResult);

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

}

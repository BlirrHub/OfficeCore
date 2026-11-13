using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OfficeCore.Api.Controllers;
using OfficeCore.Api.Hubs;
using OfficeCore.Api.Models;
using OfficeCore.Api.Services;
using Xunit;

namespace OfficeCore.Api.Tests;

public class TenantIsolationTests
{
    [Fact]
    public async Task Spoofed_cookie_cannot_read_other_workspace_chat_users_or_payroll_settings_or_calendars()
    {
        await using var fixture = new TenantTestFixture();
        fixture.SelectWorkspace(fixture.OtherOrganization.Id);

        var chatController = new ChatController(fixture.Db, fixture.UserManager, null!, fixture.Tenant)
        {
            ControllerContext = fixture.ControllerContext
        };
        Assert.IsType<ForbidResult>(await chatController.GetUsersForChat());

        var payrollSettingsController = new PayrollSettingsController(fixture.Db, fixture.Tenant)
        {
            ControllerContext = fixture.ControllerContext
        };
        Assert.IsType<ForbidResult>(await payrollSettingsController.GetGeneralSettings());
        Assert.Empty(await fixture.Db.GeneralPayrollSettings.Where(s => s.OrganizationId == fixture.OtherOrganization.Id).ToListAsync());

        var holidayController = new HolidayCalendarController(fixture.Db, fixture.Tenant)
        {
            ControllerContext = fixture.ControllerContext
        };
        Assert.IsType<ForbidResult>(await holidayController.GetCalendars());

        var timeOffController = new TimeOffPoliciesController(fixture.Db, fixture.Tenant)
        {
            ControllerContext = fixture.ControllerContext
        };
        var policyResult = await timeOffController.GetPolicies();
        Assert.IsType<ForbidResult>(policyResult.Result);

        var dashboardController = new DashboardController(fixture.Db, fixture.Tenant)
        {
            ControllerContext = fixture.ControllerContext
        };
        var statsResult = await dashboardController.GetStats();
        Assert.IsType<ForbidResult>(statsResult.Result);
    }

    [Fact]
    public async Task AttendanceHub_and_BoardHub_reject_spoofed_workspace_subscriptions()
    {
        await using var fixture = new TenantTestFixture();
        var otherBoard = new Board
        {
            Id = Guid.NewGuid(),
            Name = "Other Org Board",
            OrganizationId = fixture.OtherOrganization.Id
        };
        fixture.Db.Boards.Add(otherBoard);
        await fixture.Db.SaveChangesAsync();

        fixture.SelectWorkspace(fixture.OtherOrganization.Id);

        var attendanceHub = new AttendanceHub(fixture.Db, fixture.Tenant)
        {
            Context = fixture.Connection,
            Groups = fixture.Groups
        };
        await attendanceHub.OnConnectedAsync();
        Assert.True(fixture.Connection.Aborted);
        Assert.Empty(fixture.Groups.Joined);

        var boardHub = new BoardHub(fixture.Db, fixture.Tenant)
        {
            Context = fixture.Connection,
            Groups = fixture.Groups
        };
        await boardHub.JoinBoardGroup(otherBoard.Id.ToString());
        Assert.Empty(fixture.Groups.Joined);
    }

    [Fact]
    public async Task Inactive_employee_cannot_receive_recent_notifications_or_payslips()
    {
        await using var fixture = new TenantTestFixture();
        fixture.Employee.IsActive = false;
        await fixture.Db.SaveChangesAsync();
        fixture.SelectWorkspace(fixture.Organization.Id);

        var notificationsController = new NotificationsController(fixture.Db, fixture.Tenant, fixture.UserManager)
        {
            ControllerContext = fixture.ControllerContext
        };
        var notifResult = Assert.IsType<OkObjectResult>(await notificationsController.GetRecentNotifications());
        var list = Assert.IsAssignableFrom<IEnumerable<NotificationPayload>>(notifResult.Value);
        Assert.Empty(list);

        var payrollController = new PayrollController(fixture.Db, null!, fixture.Tenant, null!)
        {
            ControllerContext = fixture.ControllerContext
        };
        Assert.IsType<ForbidResult>(await payrollController.GetMyPayslips());
    }

    [Fact]
    public async Task GetMyOrganizations_excludes_inactive_memberships()
    {
        await using var fixture = new TenantTestFixture();
        // Add user from Org One as an inactive (resigned) employee in OtherOrganization
        fixture.Db.Employees.Add(new Employee
        {
            Id = Guid.NewGuid().ToString(),
            UserId = fixture.Employee.UserId,
            OrganizationId = fixture.OtherOrganization.Id,
            IsActive = false
        });
        await fixture.Db.SaveChangesAsync();

        var orgsController = new OrganizationsController(fixture.Db, fixture.UserManager, null!, fixture.Tenant)
        {
            ControllerContext = fixture.ControllerContext
        };
        var ok = Assert.IsType<OkObjectResult>(await orgsController.GetMyOrganizations());
        var list = Assert.IsAssignableFrom<IEnumerable<object>>(ok.Value);
        var single = Assert.Single(list);
        var idProp = single.GetType().GetProperty("id") ?? single.GetType().GetProperty("Id");
        Assert.Equal(fixture.Organization.Id, (Guid)idProp!.GetValue(single)!);
    }

    [Fact]
    public async Task Attendance_LogScanEvent_and_StartEnrollment_enforce_auth_and_tenant_scoping()
    {
        var scanMethod = typeof(AttendanceController).GetMethod(nameof(AttendanceController.LogScanEvent));
        Assert.NotNull(scanMethod);
        Assert.Null(scanMethod!.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.NotNull(scanMethod.GetCustomAttribute<AuthorizeAttribute>());

        await using var fixture = new TenantTestFixture();
        fixture.SelectWorkspace(fixture.Organization.Id);

        // Add fingerprint in OtherOrganization (same FingerprintId 77) and an inactive employee fingerprint in active Organization (FingerprintId 88)
        var inactiveUser = new ApplicationUser { Id = Guid.NewGuid().ToString(), UserName = "resigned@test.com", Email = "resigned@test.com", FirstName = "Res", LastName = "Signed" };
        var inactiveEmp = new Employee { Id = Guid.NewGuid().ToString(), UserId = inactiveUser.Id, User = inactiveUser, OrganizationId = fixture.Organization.Id, IsActive = false };
        fixture.Db.AddRange(inactiveUser, inactiveEmp);
        fixture.Db.EmployeeFingerprints.AddRange(
            new EmployeeFingerprint { EmployeeId = fixture.OtherEmployee.Id, OrganizationId = fixture.OtherOrganization.Id, FingerprintId = 77 },
            new EmployeeFingerprint { EmployeeId = inactiveEmp.Id, OrganizationId = fixture.Organization.Id, FingerprintId = 88 });
        await fixture.Db.SaveChangesAsync();

        var attendanceController = new AttendanceController(
            fixture.Db,
            null!,
            null!,
            null!,
            fixture.Tenant,
            null!,
            new BiometricReportService(fixture.Db))
        {
            ControllerContext = fixture.ControllerContext
        };

        // Cross-tenant fingerprint cannot be scanned from active org
        Assert.IsType<NotFoundObjectResult>(await attendanceController.LogScanEvent(new ScanEventDto { FingerprintId = 77 }));
        // Inactive employee fingerprint cannot be scanned
        Assert.IsType<NotFoundObjectResult>(await attendanceController.LogScanEvent(new ScanEventDto { FingerprintId = 88 }));
        // Cross-tenant enrollment target is rejected before touching TCP device service
        Assert.IsType<NotFoundObjectResult>(await attendanceController.StartEnrollment(fixture.OtherEmployee.Id));
    }

    [Fact]
    public async Task AuditService_ignores_unvalidated_organization_cookie()
    {
        await using var fixture = new TenantTestFixture();
        // Spoof OtherOrganization cookie while authenticated as Employee (only belongs to Organization)
        fixture.SelectWorkspace(fixture.OtherOrganization.Id);

        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<AuditService>.Instance;
        var httpAccessor = new HttpContextAccessor { HttpContext = fixture.ControllerContext.HttpContext };
        var auditService = new AuditService(fixture.Db, httpAccessor, fixture.Tenant, logger);

        await auditService.LogAsync("Spoof", "Board", "1", "Should not be written to OtherOrganization");
        Assert.Empty(await fixture.Db.AuditLogs.ToListAsync());

        // Valid org selected -> audit row is persisted cleanly
        fixture.SelectWorkspace(fixture.Organization.Id);
        await auditService.LogAsync("Valid", "Board", "1", "Belongs to active workspace");
        var log = Assert.Single(await fixture.Db.AuditLogs.ToListAsync());
        Assert.Equal(fixture.Organization.Id, log.OrganizationId);
    }

    private sealed class TenantTestFixture : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        public AppDbContext Db { get; }
        public TenantService Tenant { get; }
        public UserManager<ApplicationUser> UserManager { get; }
        public Organization Organization { get; }
        public Organization OtherOrganization { get; }
        public Employee Employee { get; }
        public Employee OtherEmployee { get; }
        public ControllerContext ControllerContext { get; }
        public RecordingConnection Connection { get; }
        public RecordingGroups Groups { get; } = new();
        private readonly DefaultHttpContext _http;

        public TenantTestFixture()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
            services.AddIdentityCore<ApplicationUser>().AddRoles<AppRole>().AddEntityFrameworkStores<AppDbContext>();
            _http = new DefaultHttpContext();
            services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = _http });
            services.AddScoped<TenantService>();
            _provider = services.BuildServiceProvider();

            Db = _provider.GetRequiredService<AppDbContext>();
            Tenant = _provider.GetRequiredService<TenantService>();
            UserManager = _provider.GetRequiredService<UserManager<ApplicationUser>>();

            var user = new ApplicationUser { Id = Guid.NewGuid().ToString(), UserName = "user1@test.com", Email = "user1@test.com", FirstName = "Test", LastName = "One" };
            var otherUser = new ApplicationUser { Id = Guid.NewGuid().ToString(), UserName = "user2@test.com", Email = "user2@test.com", FirstName = "Test", LastName = "Two" };
            Organization = new Organization { Id = Guid.NewGuid(), Name = "Org One", CreatedByUserId = user.Id };
            OtherOrganization = new Organization { Id = Guid.NewGuid(), Name = "Org Two", CreatedByUserId = otherUser.Id };
            Employee = new Employee { Id = Guid.NewGuid().ToString(), UserId = user.Id, User = user, OrganizationId = Organization.Id, IsActive = true };
            OtherEmployee = new Employee { Id = Guid.NewGuid().ToString(), UserId = otherUser.Id, User = otherUser, OrganizationId = OtherOrganization.Id, IsActive = true };

            Db.AddRange(user, otherUser, Organization, OtherOrganization, Employee, OtherEmployee);
            Db.SaveChanges();

            _http.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, user.Id) }, "Test"));
            ControllerContext = new ControllerContext { HttpContext = _http };
            Connection = new RecordingConnection(_http.User);
        }

        public void SelectWorkspace(Guid id) => _http.Request.Headers.Cookie = $"ActiveOrganizationId={id}";

        public async ValueTask DisposeAsync() => await _provider.DisposeAsync();
    }

    private sealed class RecordingGroups : IGroupManager
    {
        public HashSet<string> Joined { get; } = new();
        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            Joined.Add(groupName);
            return Task.CompletedTask;
        }
        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class RecordingConnection(ClaimsPrincipal user) : HubCallerContext
    {
        public bool Aborted { get; private set; }
        public override string ConnectionId => "test-connection";
        public override string? UserIdentifier => user.FindFirstValue(ClaimTypes.NameIdentifier);
        public override ClaimsPrincipal User => user;
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override IFeatureCollection Features { get; } = new FeatureCollection();
        public override CancellationToken ConnectionAborted => CancellationToken.None;
        public override void Abort() => Aborted = true;
    }
}

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OfficeCore.Api.Hubs;
using OfficeCore.Api.Models;
using OfficeCore.Api.Services;
using Xunit;

namespace OfficeCore.Api.Tests;

public class NotificationAccessTests
{
    [Fact]
    public async Task Spoofed_workspace_cookie_cannot_subscribe_to_another_workspace()
    {
        await using var fixture = new HubFixture();
        var otherWorkspace = new Organization { Id = Guid.NewGuid(), Name = "Other test workspace" };
        fixture.Db.Organizations.Add(otherWorkspace);
        fixture.SelectWorkspace(otherWorkspace.Id);
        await fixture.Connect();
        Assert.True(fixture.Connection.Aborted);
        Assert.Empty(fixture.Groups.Joined);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Inactive_membership_cannot_receive_workspace_notifications(bool selectWorkspace)
    {
        await using var fixture = new HubFixture();
        fixture.Employee.IsActive = false;
        if (selectWorkspace) fixture.SelectWorkspace(fixture.Organization.Id);
        await fixture.Connect();
        Assert.True(fixture.Connection.Aborted);
        Assert.Empty(fixture.Groups.Joined);
    }

    [Fact]
    public async Task Active_employee_joins_own_workspace_without_admin_notifications()
    {
        await using var fixture = new HubFixture();
        fixture.SelectWorkspace(fixture.Organization.Id);
        await fixture.Connect();
        Assert.False(fixture.Connection.Aborted);
        Assert.Contains($"org_{fixture.Organization.Id}", fixture.Groups.Joined);
        Assert.Contains($"user_{fixture.Employee.UserId}", fixture.Groups.Joined);
        Assert.DoesNotContain($"org_{fixture.Organization.Id}_admins_leaves", fixture.Groups.Joined);
        Assert.DoesNotContain($"org_{fixture.Organization.Id}_admins_ledger", fixture.Groups.Joined);
        Assert.DoesNotContain($"org_{fixture.Organization.Id}_admins_overtime", fixture.Groups.Joined);
    }

    [Fact]
    public async Task Owner_receives_admin_notifications_for_own_workspace()
    {
        await using var fixture = new HubFixture();
        fixture.Organization.CreatedByUserId = fixture.Employee.UserId;
        fixture.SelectWorkspace(fixture.Organization.Id);
        await fixture.Connect();
        Assert.Contains($"org_{fixture.Organization.Id}_admins_leaves", fixture.Groups.Joined);
        Assert.Contains($"org_{fixture.Organization.Id}_admins_ledger", fixture.Groups.Joined);
        Assert.Contains($"org_{fixture.Organization.Id}_admins_overtime", fixture.Groups.Joined);
    }

    [Fact]
    public async Task Missing_workspace_cookie_can_fall_back_to_active_membership()
    {
        await using var fixture = new HubFixture();
        await fixture.Connect();
        Assert.False(fixture.Connection.Aborted);
        Assert.Contains($"org_{fixture.Organization.Id}", fixture.Groups.Joined);
    }

    [Fact]
    public async Task Leave_manager_joins_only_leave_admin_group_not_ledger_or_overtime()
    {
        await using var fixture = new HubFixture();
        var role = new OrganizationRole
        {
            Id = Guid.NewGuid(),
            Name = "LeaveManager",
            OrganizationId = fixture.Organization.Id
        };
        fixture.Db.OrganizationRoles.Add(role);
        fixture.Db.OrganizationRoleClaims.Add(new OrganizationRoleClaim
        {
            OrganizationRoleId = role.Id,
            ClaimType = OfficeCore.Shared.Authorization.PermissionCatalog.ClaimType,
            ClaimValue = OfficeCore.Shared.Authorization.AccessPermissions.LeaveApprovalsManage
        });
        fixture.Db.EmployeeRoles.Add(new EmployeeRole
        {
            EmployeeId = fixture.Employee.Id,
            OrganizationRoleId = role.Id
        });

        fixture.SelectWorkspace(fixture.Organization.Id);
        await fixture.Connect();

        Assert.Contains($"org_{fixture.Organization.Id}_admins_leaves", fixture.Groups.Joined);
        Assert.DoesNotContain($"org_{fixture.Organization.Id}_admins_ledger", fixture.Groups.Joined);
        Assert.DoesNotContain($"org_{fixture.Organization.Id}_admins_overtime", fixture.Groups.Joined);
    }

    private sealed class HubFixture : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        public AppDbContext Db { get; }
        public Organization Organization { get; }
        public Employee Employee { get; }
        public RecordingConnection Connection { get; }
        public RecordingGroups Groups { get; } = new();
        private readonly DefaultHttpContext _http;
        private readonly NotificationHub _hub;

        public HubFixture()
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
            var user = new ApplicationUser { Id = Guid.NewGuid().ToString(), FirstName = "Test", LastName = "User" };
            Organization = new Organization { Id = Guid.NewGuid(), Name = "Test workspace" };
            Employee = new Employee { UserId = user.Id, User = user, OrganizationId = Organization.Id };
            Db.AddRange(Organization, Employee);
            _http.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, user.Id) }, "Test"));
            Connection = new RecordingConnection(_http.User);
            _hub = new NotificationHub(Db, _provider.GetRequiredService<TenantService>()) { Context = Connection, Groups = Groups };
        }

        public void SelectWorkspace(Guid id) => _http.Request.Headers.Cookie = $"ActiveOrganizationId={id}";

        public async Task Connect()
        {
            await Db.SaveChangesAsync();
            await _hub.OnConnectedAsync();
        }

        public async ValueTask DisposeAsync()
        {
            _hub.Dispose();
            await _provider.DisposeAsync();
        }
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

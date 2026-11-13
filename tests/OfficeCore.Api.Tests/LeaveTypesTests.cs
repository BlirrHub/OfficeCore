using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OfficeCore.Api.Controllers;
using OfficeCore.Api.Data;
using OfficeCore.Api.Hubs;
using OfficeCore.Api.Models;
using OfficeCore.Api.Services;
using OfficeCore.Shared.DTOs;
using Xunit;

namespace OfficeCore.Api.Tests;

public class LeaveTypesTests
{
    [Fact]
    public void LeaveTypeCatalog_contains_all_supported_leave_types_matching_enum()
    {
        var enumValues = Enum.GetValues<LeaveType>().Select(x => (int)x).OrderBy(x => x).ToArray();
        var catalogValues = LeaveTypeCatalog.All.Select(x => x.Value).OrderBy(x => x).ToArray();

        Assert.Equal(enumValues, catalogValues);
        Assert.Equal("Sick Leave", LeaveTypeCatalog.Get((int)LeaveType.Sick).Label);
        Assert.Equal("Vacation Leave", LeaveTypeCatalog.Get((int)LeaveType.Vacation).Label);
        Assert.Equal("Emergency Leave", LeaveTypeCatalog.Get((int)LeaveType.Emergency).Label);
        Assert.Equal("Maternity Leave", LeaveTypeCatalog.Get((int)LeaveType.Maternity).Label);
        Assert.Equal("Other", LeaveTypeCatalog.Get((int)LeaveType.Other).Label);
        Assert.False(LeaveTypeCatalog.IsDefined(99));
    }

    [Theory]
    [InlineData(LeaveType.Emergency)]
    [InlineData(LeaveType.Maternity)]
    [InlineData(LeaveType.Other)]
    public async Task SubmitRequest_and_TimeOffPolicy_accept_new_leave_types(LeaveType leaveType)
    {
        await using var fixture = new LeaveFixture();

        var leaveController = new LeaveRequestController(
            fixture.Db,
            fixture.UserManager,
            fixture.Tenant,
            new NoOpHubContext<NotificationHub>(),
            fixture.Audit)
        {
            ControllerContext = fixture.ControllerContext
        };

        var submitResult = await leaveController.SubmitRequest(new CreateLeaveRequestDto
        {
            Type = leaveType,
            StartDate = new DateTime(2026, 11, 20),
            EndDate = new DateTime(2026, 11, 21),
            Reason = $"Test {leaveType}"
        });

        var createdLeave = Assert.IsType<LeaveRequest>(Assert.IsType<OkObjectResult>(submitResult).Value);
        Assert.Equal(leaveType, createdLeave.Type);

        var policyController = new TimeOffPoliciesController(fixture.Db, fixture.Tenant)
        {
            ControllerContext = fixture.ControllerContext
        };

        var policyResult = await policyController.CreatePolicy(new TimeOffPoliciesController.TimeOffPolicyRequest
        {
            Name = $"{leaveType} Policy",
            LeaveType = leaveType,
            DefaultDaysPerYear = 5,
            IsPaid = true
        });

        var createdResult = Assert.IsType<CreatedAtActionResult>(policyResult.Result);
        var createdPolicy = Assert.IsType<TimeOffPolicy>(createdResult.Value);
        Assert.Equal(leaveType, createdPolicy.LeaveType);
    }

    [Fact]
    public async Task Invalid_leave_type_is_rejected_with_BadRequest()
    {
        await using var fixture = new LeaveFixture();

        var leaveController = new LeaveRequestController(
            fixture.Db,
            fixture.UserManager,
            fixture.Tenant,
            new NoOpHubContext<NotificationHub>(),
            fixture.Audit)
        {
            ControllerContext = fixture.ControllerContext
        };

        var leaveResult = await leaveController.SubmitRequest(new CreateLeaveRequestDto
        {
            Type = (LeaveType)99,
            StartDate = new DateTime(2026, 11, 20),
            EndDate = new DateTime(2026, 11, 20),
            Reason = "Invalid"
        });
        Assert.IsType<BadRequestObjectResult>(leaveResult);

        var policyController = new TimeOffPoliciesController(fixture.Db, fixture.Tenant)
        {
            ControllerContext = fixture.ControllerContext
        };

        var policyResult = await policyController.CreatePolicy(new TimeOffPoliciesController.TimeOffPolicyRequest
        {
            Name = "Invalid",
            LeaveType = (LeaveType)99,
            DefaultDaysPerYear = 5
        });
        Assert.IsType<BadRequestObjectResult>(policyResult.Result);
    }

    private sealed class NoOpHubContext<THub> : IHubContext<THub> where THub : Hub
    {
        public IHubClients Clients { get; } = new NoOpClients();
        public IGroupManager Groups { get; } = new NoOpGroups();

        private sealed class NoOpClients : IHubClients
        {
            private static readonly IClientProxy Proxy = new NoOpClientProxy();
            public IClientProxy All => Proxy;
            public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => Proxy;
            public IClientProxy Client(string connectionId) => Proxy;
            public IClientProxy Clients(IReadOnlyList<string> connectionIds) => Proxy;
            public IClientProxy Group(string groupName) => Proxy;
            public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => Proxy;
            public IClientProxy Groups(IReadOnlyList<string> groupNames) => Proxy;
            public IClientProxy User(string userId) => Proxy;
            public IClientProxy Users(IReadOnlyList<string> userIds) => Proxy;
        }

        private sealed class NoOpClientProxy : IClientProxy
        {
            public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
                => Task.CompletedTask;
        }

        private sealed class NoOpGroups : IGroupManager
        {
            public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
        }
    }

    private sealed class LeaveFixture : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly DefaultHttpContext _http;
        public AppDbContext Db { get; }
        public TenantService Tenant { get; }
        public UserManager<ApplicationUser> UserManager { get; }
        public AuditService Audit { get; }
        public ControllerContext ControllerContext => new() { HttpContext = _http };

        public LeaveFixture()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
            services.AddIdentityCore<ApplicationUser>().AddRoles<AppRole>().AddEntityFrameworkStores<AppDbContext>();
            _http = new DefaultHttpContext();
            var accessor = new HttpContextAccessor { HttpContext = _http };
            services.AddSingleton<IHttpContextAccessor>(accessor);
            services.AddScoped<TenantService>();
            _provider = services.BuildServiceProvider();

            Db = _provider.GetRequiredService<AppDbContext>();
            Tenant = _provider.GetRequiredService<TenantService>();
            UserManager = _provider.GetRequiredService<UserManager<ApplicationUser>>();
            Audit = new AuditService(Db, accessor, Tenant, NullLogger<AuditService>.Instance);

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = "emp@test.local",
                Email = "emp@test.local",
                NormalizedEmail = "EMP@TEST.LOCAL",
                FirstName = "Test",
                LastName = "Employee"
            };
            var org = new Organization { Id = Guid.NewGuid(), Name = "ACBA", CreatedByUserId = user.Id };
            var employee = new Employee
            {
                Id = Guid.NewGuid().ToString(),
                UserId = user.Id,
                User = user,
                OrganizationId = org.Id,
                IsActive = true,
                IsEmployee = true
            };

            Db.AddRange(user, org, employee);
            Db.SaveChanges();

            _http.User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, user.Id) },
                "TestAuth"));
            _http.Request.Headers.Cookie = $"ActiveOrganizationId={org.Id}";
        }

        public ValueTask DisposeAsync()
        {
            _provider.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}

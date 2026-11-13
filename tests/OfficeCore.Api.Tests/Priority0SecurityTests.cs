using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using OfficeCore.Api.Controllers;
using OfficeCore.Api.Models;
using OfficeCore.Api.Services;
using OfficeCore.Shared.Authorization;
using Xunit;

namespace OfficeCore.Api.Tests;

public class Priority0SecurityTests
{
    [Fact]
    public void No_string_scheme_Forbid_calls_remain_in_api_controllers()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "src", "OfficeCore.Api", "Controllers")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        var controllersDir = Path.Combine(dir!.FullName, "src", "OfficeCore.Api", "Controllers");
        var files = Directory.GetFiles(controllersDir, "*.cs", SearchOption.AllDirectories);
        Assert.NotEmpty(files);

        var badCallPattern = new Regex(@"\bForbid\s*\(\s*""[^""]+""", RegexOptions.Compiled);
        var violations = new List<string>();

        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            if (badCallPattern.IsMatch(content))
            {
                violations.Add(Path.GetFileName(file));
            }
        }

        Assert.Empty(violations);
    }

    [Fact]
    public void Attendance_LogScanEvent_requires_attendance_adjust_permission()
    {
        var method = typeof(AttendanceController).GetMethod(nameof(AttendanceController.LogScanEvent));
        Assert.NotNull(method);

        var authorizeAttributes = method!.GetCustomAttributes<AuthorizeAttribute>().ToList();
        Assert.Contains(authorizeAttributes, a => a.Policy == "RequireAttendance");
        Assert.Contains(authorizeAttributes, a => a.Policy == PermissionPolicies.For(AccessPermissions.AttendanceAdjust));
    }

    [Fact]
    public async Task LocalFileStorageService_rejects_spoofed_magic_bytes_and_path_traversal_and_stores_valid_png_outside_wwwroot()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"officecore-p0-storage-{Guid.NewGuid():N}");
        try
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Uploads:RootPath"] = tempRoot })
                .Build();
            var storage = new LocalFileStorageService(config, NullLogger<LocalFileStorageService>.Instance);

            // 1. HTML script spoofed as .jpg must fail magic-byte validation
            var spoofBytes = Encoding.UTF8.GetBytes("<html><script>alert(1)</script></html>");
            var spoofFile = CreateFormFile("avatar.jpg", "image/jpeg", spoofBytes);
            var spoofResult = await storage.ValidateAndSaveAsync(spoofFile, "profiles", "user1_test.jpg", new[] { ".jpg", ".png" }, 1024 * 1024);
            Assert.False(spoofResult.Success);

            // 2. Traversal targetFileName must be rejected
            var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D };
            var pngFile = CreateFormFile("avatar.png", "image/png", pngBytes);
            var traversalResult = await storage.ValidateAndSaveAsync(pngFile, "profiles", "../evil.png", new[] { ".jpg", ".png" }, 1024 * 1024);
            Assert.False(traversalResult.Success);

            // 3. Valid PNG succeeds outside wwwroot and resolves properly
            var validResult = await storage.ValidateAndSaveAsync(pngFile, "profiles", "user1_avatar.png", new[] { ".jpg", ".png" }, 1024 * 1024);
            Assert.True(validResult.Success);
            Assert.Equal("/uploads/profiles/user1_avatar.png", validResult.RelativeUrl);

            var resolved = storage.ResolveFile("profiles", "user1_avatar.png");
            Assert.NotNull(resolved);
            Assert.True(File.Exists(resolved!.FullPath));
            Assert.Equal("image/png", resolved.ContentType);
            Assert.Null(storage.ResolveFile("profiles", "../user1_avatar.png"));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task FilesController_enforces_tenant_and_room_acls_with_nosniff_header()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"officecore-p0-files-{Guid.NewGuid():N}");
        try
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Uploads:RootPath"] = tempRoot })
                .Build();
            var storage = new LocalFileStorageService(config, NullLogger<LocalFileStorageService>.Instance);

            var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D };
            var chatRoomId = Guid.NewGuid();
            var chatFileName = $"chat_{chatRoomId:N}_{Guid.NewGuid():N}.png";
            var chatRelativeUrl = $"/uploads/chat/{chatFileName}";
            var saveChat = await storage.ValidateAndSaveAsync(
                CreateFormFile("chart.png", "image/png", pngBytes),
                "chat",
                chatFileName,
                new[] { ".png" },
                1024 * 1024);
            Assert.True(saveChat.Success);

            await using var fixture = new TestApiFixture();
            var orgA = new Organization { Id = Guid.NewGuid(), Name = "Org A" };
            var orgB = new Organization { Id = Guid.NewGuid(), Name = "Org B" };

            var userInRoom = new ApplicationUser { Id = Guid.NewGuid().ToString(), UserName = "roomuser@test.local", FirstName = "Room", LastName = "Member" };
            var userOutsideRoom = new ApplicationUser { Id = Guid.NewGuid().ToString(), UserName = "nonroom@test.local", FirstName = "SameOrg", LastName = "Outsider" };
            var otherOrgUser = new ApplicationUser { Id = Guid.NewGuid().ToString(), UserName = "otherorg@test.local", FirstName = "Other", LastName = "Tenant" };

            var empInRoom = new Employee { Id = Guid.NewGuid().ToString(), UserId = userInRoom.Id, OrganizationId = orgA.Id, IsActive = true };
            var empOutsideRoom = new Employee { Id = Guid.NewGuid().ToString(), UserId = userOutsideRoom.Id, OrganizationId = orgA.Id, IsActive = true };
            var empOtherOrg = new Employee { Id = Guid.NewGuid().ToString(), UserId = otherOrgUser.Id, OrganizationId = orgB.Id, IsActive = true };

            var room = new ChatRoom { Id = chatRoomId, Name = "Secret Finance Room", Type = ChatRoomType.Group, OrganizationId = orgA.Id, CreatedAt = DateTime.UtcNow };
            var member = new ChatRoomMember { Id = Guid.NewGuid(), ChatRoomId = chatRoomId, UserId = empInRoom.Id, OrganizationId = orgA.Id, JoinedAt = DateTime.UtcNow };
            var msg = new ChatMessage
            {
                Id = Guid.NewGuid(),
                ChatRoomId = chatRoomId,
                SenderId = empInRoom.Id,
                Content = "Attachment",
                AttachmentUrl = chatRelativeUrl,
                OrganizationId = orgA.Id,
                CreatedAt = DateTime.UtcNow
            };

            fixture.Db.AddRange(orgA, orgB, userInRoom, userOutsideRoom, otherOrgUser, empInRoom, empOutsideRoom, empOtherOrg, room, member, msg);
            await fixture.Db.SaveChangesAsync();

            var controller = new FilesController(fixture.Db, fixture.Tenant, storage)
            {
                ControllerContext = new ControllerContext { HttpContext = fixture.Http }
            };

            // Same-org non-room member -> 403
            fixture.SetCaller(userOutsideRoom.Id, orgA.Id);
            var nonMemberResult = Assert.IsType<ObjectResult>(await controller.GetUploadedFile("chat", chatFileName));
            Assert.Equal(403, nonMemberResult.StatusCode);

            // Different-org user -> 403
            fixture.SetCaller(otherOrgUser.Id, orgB.Id);
            var crossOrgResult = Assert.IsType<ObjectResult>(await controller.GetUploadedFile("chat", chatFileName));
            Assert.Equal(403, crossOrgResult.StatusCode);

            // Room member -> 200 PhysicalFile + X-Content-Type-Options: nosniff
            fixture.SetCaller(userInRoom.Id, orgA.Id);
            var okResult = Assert.IsType<PhysicalFileResult>(await controller.GetUploadedFile("chat", chatFileName));
            Assert.Equal("image/png", okResult.ContentType);
            Assert.Equal("nosniff", fixture.Http.Response.Headers["X-Content-Type-Options"].ToString());
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task MustChangePasswordFilter_blocks_api_calls_until_password_changed_and_allows_exempt_routes()
    {
        await using var fixture = new TestApiFixture();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "temp@test.local",
            FirstName = "Temp",
            LastName = "User",
            MustChangePassword = true
        };
        fixture.Db.Users.Add(user);
        await fixture.Db.SaveChangesAsync();

        var filter = new MustChangePasswordFilter(fixture.Db);

        // Non-exempt route must be blocked with 403
        fixture.SetCaller(user.Id, null);
        fixture.Http.Request.Path = "/api/dashboard/stats";
        var blockedCtx = CreateExecutingContext(fixture.Http);
        bool nextCalled = false;
        await filter.OnActionExecutionAsync(blockedCtx, () =>
        {
            nextCalled = true;
            return Task.FromResult(CreateExecutedContext(blockedCtx));
        });
        Assert.False(nextCalled);
        var forbidden = Assert.IsType<ObjectResult>(blockedCtx.Result);
        Assert.Equal(403, forbidden.StatusCode);

        // Exempt /api/auth/change-password route must proceed
        fixture.Http.Request.Path = "/api/auth/change-password";
        var allowedCtx = CreateExecutingContext(fixture.Http);
        await filter.OnActionExecutionAsync(allowedCtx, () =>
        {
            nextCalled = true;
            return Task.FromResult(CreateExecutedContext(allowedCtx));
        });
        Assert.True(nextCalled);
        Assert.Null(allowedCtx.Result);
    }

    [Fact]
    public async Task EmailOtpService_fails_closed_outside_development_when_smtp_unconfigured()
    {
        var config = new ConfigurationBuilder().Build();
        var prodEnv = new StubHostEnvironment { EnvironmentName = Environments.Production };
        var service = new EmailOtpService(NullLogger<EmailOtpService>.Instance, config, prodEnv);

        var (success, message, debugCode) = await service.SendOtpAsync("employee@acba.local");

        Assert.False(success);
        Assert.Null(debugCode);
        Assert.Contains("not configured", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PayrollController_CreatePayrollPeriod_rejects_end_date_before_start_date()
    {
        await using var fixture = new TestApiFixture();
        var org = new Organization { Id = Guid.NewGuid(), Name = "ACBA", CreatedByUserId = "owner-1" };
        var emp = new Employee { Id = Guid.NewGuid().ToString(), UserId = "owner-1", OrganizationId = org.Id, IsActive = true };
        fixture.Db.AddRange(org, emp);
        await fixture.Db.SaveChangesAsync();

        fixture.SetCaller("owner-1", org.Id);
        var audit = new AuditService(fixture.Db, fixture.HttpAccessor, fixture.Tenant, NullLogger<AuditService>.Instance);
        var computation = new PayrollComputationService(fixture.Db);
        var controller = new PayrollController(fixture.Db, computation, fixture.Tenant, audit)
        {
            ControllerContext = new ControllerContext { HttpContext = fixture.Http }
        };

        var result = await controller.CreatePayrollPeriod(new PayrollController.CreatePayrollPeriodRequest
        {
            StartDate = new DateOnly(2026, 11, 15),
            EndDate = new DateOnly(2026, 11, 1),
            DeductionMultiplier = 1.0m
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task DashboardController_only_populates_counters_for_modules_caller_is_authorized_to_manage()
    {
        await using var fixture = new TestApiFixture();
        var org = new Organization { Id = Guid.NewGuid(), Name = "ACBA", CreatedByUserId = "owner-user" };
        var leaveManagerUser = new ApplicationUser { Id = "leave-mgr", UserName = "leave@test.local", FirstName = "Leave", LastName = "Mgr" };
        var emp = new Employee { Id = "emp-leave-mgr", UserId = "leave-mgr", OrganizationId = org.Id, IsActive = true };
        var otherEmp = new Employee { Id = "emp-other", UserId = "other-user", OrganizationId = org.Id, IsActive = true };

        fixture.Db.AddRange(
            org,
            leaveManagerUser,
            emp,
            otherEmp,
            new LeaveRequest
            {
                Id = Guid.NewGuid(),
                OrganizationId = org.Id,
                EmployeeId = otherEmp.Id,
                Type = LeaveType.Vacation,
                Status = LeaveRequestStatus.Pending,
                StartDate = DateTime.UtcNow.Date,
                EndDate = DateTime.UtcNow.Date
            },
            new AdvanceRequest
            {
                Id = Guid.NewGuid(),
                OrganizationId = org.Id,
                EmployeeId = otherEmp.Id,
                Type = AdvanceType.CashAdvance,
                RequestedAmount = 1000m,
                Note = "Need cash",
                Status = AdvanceStatus.Pending
            });
        await fixture.Db.SaveChangesAsync();

        // Caller ONLY has LeaveApprovalsView (no Users/Ledger/Overtime permissions)
        fixture.SetCaller("leave-mgr", org.Id, new Claim(PermissionCatalog.ClaimType, AccessPermissions.LeaveApprovalsView));
        var controller = new DashboardController(fixture.Db, fixture.Tenant)
        {
            ControllerContext = new ControllerContext { HttpContext = fixture.Http }
        };

        var actionResult = await controller.GetStats();
        var ok = Assert.IsType<OkObjectResult>(actionResult.Result);
        var stats = Assert.IsType<DashboardStatsDto>(ok.Value);

        Assert.Equal(1, stats.PendingLeavesCount);
        Assert.Null(stats.PendingAdvancesCount);
        Assert.Null(stats.TotalEmployees);
        Assert.True(stats.Permissions.CanManageLeaves);
        Assert.False(stats.Permissions.CanManageAdvances);
        Assert.False(stats.Permissions.CanManageUsers);
    }

    private static IFormFile CreateFormFile(string fileName, string contentType, byte[] bytes)
    {
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    private static ActionExecutingContext CreateExecutingContext(HttpContext http)
    {
        var actionContext = new ActionContext(http, new RouteData(), new ActionDescriptor());
        return new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), new Dictionary<string, object?>(), new object());
    }

    private static ActionExecutedContext CreateExecutedContext(ActionExecutingContext executing)
    {
        return new ActionExecutedContext(executing, new List<IFilterMetadata>(), new object());
    }

    private sealed class StubHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "OfficeCore.Api";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class TestApiFixture : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        public AppDbContext Db { get; }
        public TenantService Tenant { get; }
        public DefaultHttpContext Http { get; }
        public IHttpContextAccessor HttpAccessor { get; }

        public TestApiFixture()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase($"p0-{Guid.NewGuid():N}"));
            services.AddIdentityCore<ApplicationUser>().AddRoles<AppRole>().AddEntityFrameworkStores<AppDbContext>();
            Http = new DefaultHttpContext();
            HttpAccessor = new HttpContextAccessor { HttpContext = Http };
            services.AddSingleton(HttpAccessor);
            services.AddScoped<TenantService>();
            _provider = services.BuildServiceProvider();

            Db = _provider.GetRequiredService<AppDbContext>();
            Tenant = _provider.GetRequiredService<TenantService>();
        }

        public void SetCaller(string userId, Guid? orgId, params Claim[] extraClaims)
        {
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId) };
            claims.AddRange(extraClaims);
            Http.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
            Http.Request.Headers.Cookie = orgId.HasValue ? $"ActiveOrganizationId={orgId.Value}" : string.Empty;
        }

        public async ValueTask DisposeAsync()
        {
            await _provider.DisposeAsync();
        }
    }
}

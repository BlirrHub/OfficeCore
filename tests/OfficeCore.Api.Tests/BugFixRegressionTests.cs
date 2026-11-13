using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using OfficeCore.Api.Controllers;
using OfficeCore.Api.Hubs;
using OfficeCore.Api.Models;
using OfficeCore.Api.Services;
using Xunit;

namespace OfficeCore.Api.Tests;

public class BugFixRegressionTests
{
    [Fact]
    public async Task AdvanceRequest_null_RemainingBalance_is_initialized_decremented_and_not_re_deducted()
    {
        await using var fixture = new RegressionFixture();

        // 1. ApproveRequest initializes RemainingBalance to RequestedAmount
        var pendingLoan = new AdvanceRequest
        {
            Id = Guid.NewGuid(),
            OrganizationId = fixture.Organization.Id,
            EmployeeId = fixture.Employee.Id,
            Type = AdvanceType.ELoan,
            RequestedAmount = 2000m,
            Status = AdvanceStatus.Pending,
            RequestDate = DateTime.UtcNow
        };
        // 2. Legacy/seeded Approved CashAdvance with null RemainingBalance
        var legacyAdvance = new AdvanceRequest
        {
            Id = Guid.NewGuid(),
            OrganizationId = fixture.Organization.Id,
            EmployeeId = fixture.Employee.Id,
            Type = AdvanceType.CashAdvance,
            RequestedAmount = 500m,
            RemainingBalance = null,
            Status = AdvanceStatus.Approved,
            RequestDate = DateTime.UtcNow
        };
        fixture.Db.AdvanceRequests.AddRange(pendingLoan, legacyAdvance);

        var period1 = new PayrollPeriod
        {
            Id = Guid.NewGuid(),
            OrganizationId = fixture.Organization.Id,
            StartDate = new DateOnly(2030, 1, 5),
            EndDate = new DateOnly(2030, 1, 5)
        };
        var period2 = new PayrollPeriod
        {
            Id = Guid.NewGuid(),
            OrganizationId = fixture.Organization.Id,
            StartDate = new DateOnly(2030, 1, 12),
            EndDate = new DateOnly(2030, 1, 12)
        };
        fixture.Db.PayrollPeriods.AddRange(period1, period2);
        await fixture.Db.SaveChangesAsync();

        var advController = new AdvanceRequestController(
            fixture.Db,
            fixture.UserManager,
            fixture.Tenant,
            new NoOpHubContext<NotificationHub>(),
            fixture.Audit)
        {
            ControllerContext = fixture.ControllerContext
        };

        Assert.IsType<NoContentResult>(await advController.ApproveRequest(
            pendingLoan.Id,
            new AdvanceRequestController.ApproveRequestDto { InstallmentAmount = 400m }));

        var approvedLoan = await fixture.Db.AdvanceRequests.SingleAsync(a => a.Id == pendingLoan.Id);
        Assert.Equal(2000m, approvedLoan.RemainingBalance);

        // Compute & finalize period 1 -> deducts 200 (half-month ELoan installment) + 500 (legacy CashAdvance) = 700
        var computation = new PayrollComputationService(fixture.Db);
        await computation.ComputePayrollPeriodAsync(period1.Id);
        var payslip1 = await fixture.Db.Payslips.SingleAsync(p => p.PayrollPeriodId == period1.Id);
        Assert.Equal(700m, payslip1.AdvancesDeduction);

        var payrollController = new PayrollController(fixture.Db, computation, fixture.Tenant, fixture.Audit)
        {
            ControllerContext = fixture.ControllerContext
        };
        Assert.IsType<OkResult>(await payrollController.FinalizePeriod(period1.Id));

        var afterLoan = await fixture.Db.AdvanceRequests.SingleAsync(a => a.Id == pendingLoan.Id);
        var afterLegacy = await fixture.Db.AdvanceRequests.SingleAsync(a => a.Id == legacyAdvance.Id);
        Assert.Equal(1800m, afterLoan.RemainingBalance);
        Assert.Equal(0m, afterLegacy.RemainingBalance);

        // Compute period 2 -> legacy CashAdvance (balance 0) must NOT be deducted again; only 200 ELoan remains
        await computation.ComputePayrollPeriodAsync(period2.Id);
        var payslip2 = await fixture.Db.Payslips.SingleAsync(p => p.PayrollPeriodId == period2.Id);
        Assert.Equal(200m, payslip2.AdvancesDeduction);
    }

    [Fact]
    public async Task BoardsController_clamps_negative_order_indices_and_deletes_stored_attachments()
    {
        await using var fixture = new RegressionFixture();
        var tempRoot = Path.Combine(Path.GetTempPath(), $"oc-board-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        try
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Uploads:RootPath"] = tempRoot })
                .Build();
            var storage = new LocalFileStorageService(config, NullLogger<LocalFileStorageService>.Instance);

            // Seed a valid PNG file in IFileStorageService storage (outside wwwroot)
            var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00 };
            await using var ms = new MemoryStream(pngBytes);
            var formFile = new FormFile(ms, 0, pngBytes.Length, "file", "shot.png");
            var saved = await storage.ValidateAndSaveAsync(formFile, "boards", "test-shot.png", new[] { ".png" }, 1024 * 1024);
            Assert.True(saved.Success);
            Assert.NotNull(storage.ResolveFile("boards", "test-shot.png"));

            var board = new Board
            {
                Id = Guid.NewGuid(),
                Name = "Engineering",
                OrganizationId = fixture.Organization.Id,
                Members = new List<BoardMember>
                {
                    new() { Id = Guid.NewGuid(), BoardId = Guid.Empty, UserId = fixture.Employee.Id, Role = BoardRole.Owner, OrganizationId = fixture.Organization.Id }
                }
            };
            board.Members.First().BoardId = board.Id;

            var list1 = new BoardList { Id = Guid.NewGuid(), BoardId = board.Id, Name = "Todo", OrderIndex = 0, OrganizationId = fixture.Organization.Id };
            var list2 = new BoardList { Id = Guid.NewGuid(), BoardId = board.Id, Name = "Done", OrderIndex = 1, OrganizationId = fixture.Organization.Id };
            var card = new BoardCard { Id = Guid.NewGuid(), ListId = list1.Id, Title = "Task A", OrderIndex = 0, OrganizationId = fixture.Organization.Id };
            var attachment = new BoardCardAttachment
            {
                Id = Guid.NewGuid(),
                CardId = card.Id,
                UserId = fixture.Employee.Id,
                FileName = "shot.png",
                FilePath = saved.RelativeUrl!,
                OrganizationId = fixture.Organization.Id
            };
            fixture.Db.AddRange(board, list1, list2, card, attachment);
            await fixture.Db.SaveChangesAsync();

            var controller = new BoardsController(
                fixture.Db,
                fixture.UserManager,
                new NoOpHubContext<BoardHub>(),
                new NoOpHubContext<NotificationHub>(),
                fixture.Tenant,
                new StubWebHostEnvironment(),
                storage)
            {
                ControllerContext = fixture.ControllerContext
            };

            // Negative NewOrderIndex must clamp to 0 instead of throwing ArgumentOutOfRangeException
            Assert.IsType<OkResult>(await controller.MoveCard(card.Id, new BoardsController.MoveCardRequest
            {
                NewListId = list2.Id,
                NewOrderIndex = -5
            }));
            Assert.Equal(0, (await fixture.Db.BoardCards.SingleAsync(c => c.Id == card.Id)).OrderIndex);

            Assert.IsType<OkResult>(await controller.MoveList(board.Id, list2.Id, new BoardsController.MoveListRequest
            {
                NewOrderIndex = -10
            }));
            Assert.Equal(0, (await fixture.Db.BoardLists.SingleAsync(l => l.Id == list2.Id)).OrderIndex);

            // DeleteAttachment must delete file via IFileStorageService even though it is not in wwwroot
            Assert.IsType<OkObjectResult>(await controller.DeleteAttachment(attachment.Id));
            Assert.Null(storage.ResolveFile("boards", "test-shot.png"));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ClientController_UpdateClient_preserves_existing_business_ids()
    {
        await using var fixture = new RegressionFixture();
        var biz1Id = Guid.NewGuid();
        var biz2Id = Guid.NewGuid();
        var client = new Client
        {
            Id = Guid.NewGuid(),
            OrganizationId = fixture.Organization.Id,
            FullName = "Maria Santos",
            IsActive = true,
            Businesses = new List<Business>
            {
                new() { Id = biz1Id, OrganizationId = fixture.Organization.Id, Name = "Santos Trading" },
                new() { Id = biz2Id, OrganizationId = fixture.Organization.Id, Name = "Removed Venture" }
            }
        };
        fixture.Db.Clients.Add(client);
        await fixture.Db.SaveChangesAsync();

        var controller = new ClientController(fixture.Db, fixture.Tenant)
        {
            ControllerContext = fixture.ControllerContext
        };

        var result = await controller.UpdateClient(client.Id, new ClientController.ClientRequest
        {
            Id = client.Id,
            FullName = "Maria Santos Updated",
            IsActive = true,
            Businesses = new List<ClientController.BusinessRequest>
            {
                new() { Id = biz1Id, Name = "Santos Trading Corp", TinId = "123-456-789" },
                new() { Name = "New Bakery" }
            }
        });

        Assert.IsType<NoContentResult>(result);
        var updated = await fixture.Db.Clients.Include(c => c.Businesses).SingleAsync(c => c.Id == client.Id);
        Assert.Equal("Maria Santos Updated", updated.FullName);
        Assert.Equal(2, updated.Businesses.Count);
        var preserved = Assert.Single(updated.Businesses, b => b.Id == biz1Id);
        Assert.Equal("Santos Trading Corp", preserved.Name);
        Assert.Equal("123-456-789", preserved.TinId);
        Assert.DoesNotContain(updated.Businesses, b => b.Id == biz2Id);
    }

    [Fact]
    public async Task OrganizationsController_invitations_match_email_case_insensitively()
    {
        await using var fixture = new RegressionFixture("Invitee@Example.com");
        var invite = new OrganizationInvitation
        {
            Id = Guid.NewGuid(),
            OrganizationId = fixture.Organization.Id,
            Email = "invitee@example.com",
            Token = "tok-case-1",
            IsAccepted = false,
            ExpiresAt = DateTime.UtcNow.AddDays(5),
            InvitedByUserId = fixture.User.Id
        };
        fixture.Db.OrganizationInvitations.Add(invite);
        await fixture.Db.SaveChangesAsync();

        var controller = new OrganizationsController(
            fixture.Db,
            fixture.UserManager,
            new NoOpHubContext<ChatHub>(),
            fixture.Tenant)
        {
            ControllerContext = fixture.ControllerContext
        };

        // Pending invitations match despite different casing ("Invitee@Example.com" vs "invitee@example.com")
        var pendingOk = Assert.IsType<OkObjectResult>(await controller.GetPendingInvitations());
        var list = Assert.IsAssignableFrom<IEnumerable<object>>(pendingOk.Value);
        Assert.Single(list);

        // Declining invitation also matches case-insensitively
        Assert.IsType<OkObjectResult>(await controller.DeclineInvitation("tok-case-1"));
        Assert.False(await fixture.Db.OrganizationInvitations.AnyAsync(i => i.Id == invite.Id));
    }

    [Fact]
    public void HolidayCalendarController_validates_year_and_computes_dynamic_movable_holidays()
    {
        var controller = new HolidayCalendarController(null!, null!);

        Assert.IsType<BadRequestObjectResult>(controller.GetHolidaysForCountry("PH", 1800));
        Assert.IsType<BadRequestObjectResult>(controller.GetHolidaysForCountry("PH", 2200));

        // In 2027, Easter Sunday is March 28 -> Maundy Thursday = March 25, Good Friday = March 26;
        // Last Monday of August 2027 is August 30.
        var phOk = Assert.IsType<OkObjectResult>(controller.GetHolidaysForCountry("ph", 2027));
        var phHolidays = Assert.IsType<List<Holiday>>(phOk.Value);
        Assert.Equal(new DateTime(2027, 3, 25, 0, 0, 0, DateTimeKind.Utc), phHolidays.Single(h => h.Name == "Maundy Thursday").Date);
        Assert.Equal(new DateTime(2027, 3, 26, 0, 0, 0, DateTimeKind.Utc), phHolidays.Single(h => h.Name == "Good Friday").Date);
        Assert.Equal(new DateTime(2027, 8, 30, 0, 0, 0, DateTimeKind.Utc), phHolidays.Single(h => h.Name == "National Heroes Day").Date);

        // US 2027: Thanksgiving is 4th Thursday of Nov = Nov 25
        var usOk = Assert.IsType<OkObjectResult>(controller.GetHolidaysForCountry("US", 2027));
        var usHolidays = Assert.IsType<List<Holiday>>(usOk.Value);
        Assert.Equal(new DateTime(2027, 11, 25, 0, 0, 0, DateTimeKind.Utc), usHolidays.Single(h => h.Name == "Thanksgiving Day").Date);
    }

    [Fact]
    public async Task WorkScheduleController_DeleteWorkSchedule_returns_Forbid_when_workspace_missing()
    {
        await using var fixture = new RegressionFixture();
        fixture.SelectWorkspace(Guid.NewGuid()); // spoofed / non-member workspace

        var controller = new WorkScheduleController(fixture.Db, fixture.Tenant)
        {
            ControllerContext = fixture.ControllerContext
        };

        Assert.IsType<ForbidResult>(await controller.DeleteWorkSchedule(Guid.NewGuid()));
    }

    [Fact]
    public async Task AttendanceController_ExportAttendance_filters_by_search_and_formats_pht()
    {
        await using var fixture = new RegressionFixture();
        fixture.Db.AttendanceRecords.Add(new AttendanceRecord
        {
            Id = Guid.NewGuid(),
            OrganizationId = fixture.Organization.Id,
            EmployeeId = fixture.Employee.Id,
            Timestamp = new DateTimeOffset(2026, 11, 10, 0, 0, 0, TimeSpan.Zero), // 08:00 AM PHT
            Type = AttendanceType.ClockIn,
            Source = AttendanceSource.Web
        });
        await fixture.Db.SaveChangesAsync();

        var controller = new AttendanceController(
            fixture.Db,
            null!,
            new NoOpHubContext<AttendanceHub>(),
            new NoOpHubContext<NotificationHub>(),
            fixture.Tenant,
            fixture.Audit,
            new BiometricReportService(fixture.Db))
        {
            ControllerContext = fixture.ControllerContext
        };

        var matchFile = Assert.IsType<FileContentResult>(await controller.ExportAttendance(search: "juan"));
        var csv = Encoding.UTF8.GetString(matchFile.FileContents);
        Assert.Contains("Juan Dela Cruz", csv);
        Assert.Contains("2026-11-10 08:00 AM", csv);

        var missFile = Assert.IsType<FileContentResult>(await controller.ExportAttendance(search: "nobody"));
        var emptyCsv = Encoding.UTF8.GetString(missFile.FileContents);
        Assert.DoesNotContain("Juan Dela Cruz", emptyCsv);
    }

    private sealed class RegressionFixture : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly DefaultHttpContext _http;
        public AppDbContext Db { get; }
        public TenantService Tenant { get; }
        public UserManager<ApplicationUser> UserManager { get; }
        public AuditService Audit { get; }
        public ApplicationUser User { get; }
        public Organization Organization { get; }
        public Employee Employee { get; }
        public ControllerContext ControllerContext => new() { HttpContext = _http };

        public RegressionFixture(string userEmail = "juan@test.com")
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

            User = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = userEmail,
                Email = userEmail,
                NormalizedEmail = userEmail.ToUpperInvariant(),
                FirstName = "Juan",
                LastName = "Dela Cruz"
            };
            Organization = new Organization { Id = Guid.NewGuid(), Name = "ACBA Test", CreatedByUserId = User.Id };
            Employee = new Employee
            {
                Id = Guid.NewGuid().ToString(),
                UserId = User.Id,
                User = User,
                OrganizationId = Organization.Id,
                IsActive = true,
                IsEmployee = true,
                DailyRate = 800m
            };

            Db.AddRange(User, Organization, Employee);
            Db.SaveChanges();
            SelectWorkspace(Organization.Id);
        }

        public void SelectWorkspace(Guid orgId)
        {
            _http.User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, User.Id) },
                "TestAuth"));
            _http.Request.Headers.Cookie = $"ActiveOrganizationId={orgId}";
        }

        public ValueTask DisposeAsync()
        {
            _provider.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class StubWebHostEnvironment : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider WebRootFileProvider { get; set; } = null!;
        public string ApplicationName { get; set; } = "OfficeCore.Api";
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public string EnvironmentName { get; set; } = "Development";
    }

    private sealed class NoOpHubContext<THub> : IHubContext<THub> where THub : Hub
    {
        public IHubClients Clients { get; } = new NoOpHubClients();
        public IGroupManager Groups { get; } = null!;

        private sealed class NoOpHubClients : IHubClients
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
            public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;
        }
    }
}

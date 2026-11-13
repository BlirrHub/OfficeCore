using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OfficeCore.Api.Controllers;
using OfficeCore.Api.Models;
using OfficeCore.Api.Services;
using Xunit;

namespace OfficeCore.Api.Tests;

public class SecurityHardeningTests
{
    [Fact]
    public async Task BackupController_requires_valid_station_token_and_rejects_proxied_or_secondary_org_requests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddIdentityCore<ApplicationUser>().AddRoles<AppRole>().AddEntityFrameworkStores<AppDbContext>();
        var http = new DefaultHttpContext();
        http.Connection.RemoteIpAddress = IPAddress.Loopback;
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = http });
        services.AddScoped<TenantService>();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=officecoreacbadevdb;Username=postgres;Password=password",
                ["OfficeCore:StationSecret"] = "test-secret-station-token-0123456789abcdef"
            })
            .Build();

        await using var provider = services.BuildServiceProvider();
        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var tenant = provider.GetRequiredService<TenantService>();
        var logger = provider.GetRequiredService<ILogger<BackupService>>();
        var backupService = new BackupService(config, logger);

        var primaryOwner = new ApplicationUser { Id = "owner-1", UserName = "o1@test.com", Email = "o1@test.com", FirstName = "Primary", LastName = "Owner" };
        var secondaryOwner = new ApplicationUser { Id = "owner-2", UserName = "o2@test.com", Email = "o2@test.com", FirstName = "Secondary", LastName = "Owner" };
        db.Users.AddRange(primaryOwner, secondaryOwner);
        db.Organizations.AddRange(
            new Organization { Id = Guid.NewGuid(), Name = "Primary Org", CreatedByUserId = primaryOwner.Id, CreatedAt = DateTime.UtcNow.AddDays(-10) },
            new Organization { Id = Guid.NewGuid(), Name = "Secondary Org", CreatedByUserId = secondaryOwner.Id, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var controller = new BackupController(backupService, tenant, userManager, db)
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };

        // 1. Spoofed X-OfficeCore-Station without token -> Forbid
        http.Request.Headers["X-OfficeCore-Station"] = "ServerStationOrchestrator";
        var spoofForbidden = Assert.IsType<ObjectResult>(await controller.GetBackups());
        Assert.Equal(403, spoofForbidden.StatusCode);

        // 2. Valid token -> Ok
        http.Request.Headers["X-OfficeCore-Station-Token"] = "test-secret-station-token-0123456789abcdef";
        Assert.IsType<OkObjectResult>(await controller.GetBackups());

        // 3. Valid token but forwarded via Cloudflare Tunnel -> Forbid
        http.Request.Headers["CF-Connecting-IP"] = "203.0.113.5";
        var tunnelForbidden = Assert.IsType<ObjectResult>(await controller.GetBackups());
        Assert.Equal(403, tunnelForbidden.StatusCode);

        // 4. Secondary workspace creator on loopback without station token -> Forbid
        http.Request.Headers.Clear();
        http.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, secondaryOwner.Id) }, "Test"));
        var secondaryForbidden = Assert.IsType<ObjectResult>(await controller.GetBackups());
        Assert.Equal(403, secondaryForbidden.StatusCode);

        // 5. Primary host workspace owner on loopback -> Ok
        http.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, primaryOwner.Id) }, "Test"));
        Assert.IsType<OkObjectResult>(await controller.GetBackups());
    }

    [Theory]
    [InlineData("exploit.html")]
    [InlineData("xss.svg")]
    [InlineData("payload.exe")]
    public async Task Upload_endpoints_reject_executable_and_script_extensions(string fileName)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddIdentityCore<ApplicationUser>().AddRoles<AppRole>().AddEntityFrameworkStores<AppDbContext>();
        var http = new DefaultHttpContext();
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = http });
        services.AddScoped<TenantService>();

        await using var provider = services.BuildServiceProvider();
        var db = provider.GetRequiredService<AppDbContext>();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var tenant = provider.GetRequiredService<TenantService>();

        var user = new ApplicationUser { Id = Guid.NewGuid().ToString(), UserName = "u@test.com", Email = "u@test.com", FirstName = "Test", LastName = "User" };
        var org = new Organization { Id = Guid.NewGuid(), Name = "Test Org", CreatedByUserId = user.Id };
        var emp = new Employee { Id = Guid.NewGuid().ToString(), UserId = user.Id, User = user, OrganizationId = org.Id, IsActive = true };
        var board = new Board { Id = Guid.NewGuid(), Name = "Board", OrganizationId = org.Id };
        var member = new BoardMember { Id = Guid.NewGuid(), BoardId = board.Id, UserId = emp.Id, Role = BoardRole.Owner, OrganizationId = org.Id };
        var list = new BoardList { Id = Guid.NewGuid(), BoardId = board.Id, Board = board, Name = "Todo", OrganizationId = org.Id };
        var card = new BoardCard { Id = Guid.NewGuid(), ListId = list.Id, List = list, Title = "Card", OrganizationId = org.Id };
        db.AddRange(user, org, emp, board, member, list, card);
        await db.SaveChangesAsync();

        http.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, user.Id) }, "Test"));
        http.Request.Headers.Cookie = $"ActiveOrganizationId={org.Id}";

        using var ms = new MemoryStream(Encoding.UTF8.GetBytes("<script>alert(1)</script>"));
        IFormFile badFile = new FormFile(ms, 0, ms.Length, "file", fileName);

        var orgController = new OrganizationsController(db, userManager, null!, tenant)
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };
        Assert.IsType<BadRequestObjectResult>(await orgController.UploadLogo(org.Id, badFile));

        var boardsController = new BoardsController(db, userManager, null!, null!, tenant, new StubWebHostEnvironment())
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };
        Assert.IsType<BadRequestObjectResult>(await boardsController.AddAttachment(card.Id, badFile));
    }

    private sealed class StubWebHostEnvironment : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!;
        public string ApplicationName { get; set; } = "OfficeCore.Api";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public string EnvironmentName { get; set; } = "Development";
    }
}

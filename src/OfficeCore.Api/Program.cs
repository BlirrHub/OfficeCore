using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using System.Threading.RateLimiting;
using OfficeCore.Api.Data;
using OfficeCore.Api.Models;
using OfficeCore.Api.Services;
using OfficeCore.Shared.Authorization;

using System.Globalization;

var cultureInfo = new CultureInfo("en-PH");
CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;

var builder = WebApplication.CreateBuilder(args);

// Load optional local override configuration (gitignored, for private developer credentials & secrets)
builder.Configuration
    .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.Local.json", optional: true, reloadOnChange: true);

// ─── CORS ─────────────────────────────────────────────────────────────────────
// Allow any origin that reaches this server — needed for LAN clients connecting
// via the desktop workstation app from a different machine on the network.
// Explicitly configured origins in appsettings.json are also included.
var allowedOrigins = builder.Configuration
    .GetSection("AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();
var legacyBlazorOrigin = builder.Configuration["BlazorOrigin"];
if (!string.IsNullOrWhiteSpace(legacyBlazorOrigin))
{
    allowedOrigins = allowedOrigins.Append(legacyBlazorOrigin).ToArray();
}
var allowLanOrigins = builder.Configuration.GetValue("Cors:AllowLanOrigins", true);

var devOrigins = new[]
{
    "http://localhost:5261",
    "https://localhost:5261",
    "http://localhost:5026",
    "https://localhost:7174",
};

var allOrigins = allowedOrigins.Union(devOrigins).ToArray();

builder.Services.AddCors(opt =>
    opt.AddPolicy("BlazorPolicy", p => p
        .SetIsOriginAllowed(origin =>
        {
            if (string.IsNullOrWhiteSpace(origin)) return false;
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)) return false;
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return false;

            var host = uri.Host;

            // 1. Localhost and loopback
            if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || host == "127.0.0.1" || host == "::1")
                return true;

            // 2. Local LAN office RFC1918 subnets (explicitly configurable)
            if (allowLanOrigins && System.Net.IPAddress.TryParse(host, out var ip))
            {
                var bytes = ip.GetAddressBytes();
                if (bytes.Length == 4)
                {
                    if (bytes[0] == 10) return true;
                    if (bytes[0] == 192 && bytes[1] == 168) return true;
                    if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;
                }
            }

            // 3. Explicitly allowed origins from configuration
            return allOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase);
        })
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

// ─── Rate Limiting ────────────────────────────────────────────────────────────
// Per-IP partitioned fixed-window rate limit for auth endpoints: 5 attempts per minute per IP.
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("LoginPolicy", httpContext =>
    {
        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"login-ip:{clientIp}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, _) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsync(
            "{\"message\":\"Too many authentication attempts. Please wait a minute and try again.\"}");
    };
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

// ─── Data Protection ─────────────────────────────────────────────────────────
// Persist keys to disk so auth cookies survive API process restarts.
// Without this, ASP.NET uses ephemeral in-memory keys — any restart invalidates
// all active sessions, causing users to be logged out immediately after login.
var dpKeysPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "OfficeCore", "DataProtection");
Directory.CreateDirectory(dpKeysPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dpKeysPath))
    .SetApplicationName("OfficeCore");

// ─── Database ─────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ─── Identity ─────────────────────────────────────────────────────────────────
builder.Services.AddIdentityCore<ApplicationUser>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 8;

    // Enable account lockout after 5 failed attempts for 5 minutes
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
})
.AddRoles<AppRole>()
.AddEntityFrameworkStores<AppDbContext>()
.AddSignInManager()
.AddDefaultTokenProviders();

builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddCookie(IdentityConstants.ApplicationScheme, options =>
    {
        options.Cookie.Name = "OfficeCoreAuth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization(options =>
{
    foreach (var module in ModuleCatalog.AllKeys)
    {
        options.AddPolicy($"Require{module}", policy =>
            policy.RequireAuthenticatedUser().RequireAssertion(ctx => ctx.User.HasModuleAccess(module)));
    }
    foreach (var permission in PermissionCatalog.AllKeys)
    {
        options.AddPolicy(PermissionPolicies.For(permission), policy =>
            policy.RequireAuthenticatedUser().RequireAssertion(ctx => ctx.User.HasPermission(permission)));
    }
});

// ─── Services ─────────────────────────────────────────────────────────────────
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddMemoryCache();
builder.Services.AddOpenApi();
builder.Services.AddControllers(options =>
{
    options.Filters.Add<MustChangePasswordFilter>();
}).AddJsonOptions(options =>
{
    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});
builder.Services.AddSignalR();

// Register Background Services
builder.Services.AddSingleton<IFileStorageService, LocalFileStorageService>();
builder.Services.AddSingleton<OfficeCore.Api.Services.FingerprintDeviceService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<OfficeCore.Api.Services.FingerprintDeviceService>());
builder.Services.AddSingleton<OfficeCore.Api.Services.BackupService>();
builder.Services.AddHostedService<OfficeCore.Api.Services.BackupHostedService>();

// Register Application Services
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<OfficeCore.Api.Services.TenantService>();
builder.Services.AddScoped<OfficeCore.Api.Services.IWorkspaceAuthorizationService, OfficeCore.Api.Services.WorkspaceAuthorizationService>();
builder.Services.AddScoped<OfficeCore.Api.Services.IPayrollComputationService, OfficeCore.Api.Services.PayrollComputationService>();
builder.Services.AddScoped<Microsoft.AspNetCore.Authentication.IClaimsTransformation, OfficeCore.Api.Services.OrganizationClaimsTransformation>();
builder.Services.AddScoped<OfficeCore.Api.Services.IAuditService, OfficeCore.Api.Services.AuditService>();
builder.Services.AddScoped<OfficeCore.Api.Services.BiometricReportService>();
builder.Services.AddSingleton<OfficeCore.Api.Services.ISmsOtpService, OfficeCore.Api.Services.SmsOtpService>();
builder.Services.AddSingleton<OfficeCore.Api.Services.IEmailOtpService, OfficeCore.Api.Services.EmailOtpService>();


// ─── Build ────────────────────────────────────────────────────────────────────
var app = builder.Build();

// Seed the database and move legacy public uploads outside wwwroot
await DbSeeder.SeedAsync(app.Services);
app.Services.GetRequiredService<IFileStorageService>().MigrateLegacyWebRootUploads(app.Environment.WebRootPath);

// Configure the HTTP request pipeline.
app.UseForwardedHeaders();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=()";
    await next();
});

// NOTE: Do NOT use UseHttpsRedirection() — the server runs on plain HTTP (http://0.0.0.0:5026).
// HTTPS redirects cause Blazor WASM fetch requests from LAN clients to silently fail
// because a credentialed fetch cannot follow a cross-scheme 307 redirect.
app.UseCors("BlazorPolicy");
app.UseRateLimiter();

// ─── Workstation / Desktop Only Enforcer ───────────────────────────────────────
// Blocks direct web browser access (e.g. Chrome, Edge) unless requested by the
// official OfficeCore desktop workstation client or mobile application.


app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<OfficeCore.Api.Hubs.AttendanceHub>("/attendanceHub");
app.MapHub<OfficeCore.Api.Hubs.BoardHub>("/hubs/boards");
app.MapHub<OfficeCore.Api.Hubs.ChatHub>("/hubs/chat");
app.MapHub<OfficeCore.Api.Hubs.NotificationHub>("/hubs/notifications");
app.MapFallbackToFile("index.html");

app.Run("http://0.0.0.0:5026");

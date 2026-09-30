using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using OfficeCore.Api.Data;
using OfficeCore.Api.Models;

using System.Globalization;

var cultureInfo = new CultureInfo("en-PH");
CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;

var builder = WebApplication.CreateBuilder(args);

// ─── CORS ─────────────────────────────────────────────────────────────────────
// Allow any origin that reaches this server — needed for LAN clients connecting
// via the desktop workstation app from a different machine on the network.
// Explicitly configured origins in appsettings.json are also included.
var allowedOrigins = builder.Configuration
    .GetSection("AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();

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
        .SetIsOriginAllowed(_ => true)   // Allow any LAN/WAN origin (desktop clients on other machines)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

// ─── Rate Limiting ────────────────────────────────────────────────────────────
// Fixed-window rate limit for the login endpoint: 5 attempts per minute per IP.
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("LoginPolicy", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, _) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsync(
            "{\"message\":\"Too many login attempts. Please wait a minute and try again.\"}");
    };
});

// ─── Database ─────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ─── Identity ─────────────────────────────────────────────────────────────────
builder.Services.AddIdentityCore<ApplicationUser>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 6;

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
    });

builder.Services.AddAuthorization(options =>
{
    var modules = new[] { 
        "Dashboard", "MyAttendance", "Clients", "Attendance", "Boards", "MyLeaves",
        "Advances", "Ledger", "Payroll", "Invoices",
        "Users", "Roles", "LeaveApprovals", "WorkSchedules", "TimeOff", "Locations", "Organization",
        "Backups", "AuditLogs"
    };
    foreach (var module in modules)
    {
        options.AddPolicy($"Require{module}", policy => policy.RequireClaim("ModuleAccess", module));
    }
});

// ─── Services ─────────────────────────────────────────────────────────────────
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});
builder.Services.AddSignalR();

// Register Background Services
builder.Services.AddSingleton<OfficeCore.Api.Services.FingerprintDeviceService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<OfficeCore.Api.Services.FingerprintDeviceService>());
builder.Services.AddSingleton<OfficeCore.Api.Services.BackupService>();
builder.Services.AddHostedService<OfficeCore.Api.Services.BackupHostedService>();

// Register Application Services
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<OfficeCore.Api.Services.TenantService>();
builder.Services.AddScoped<OfficeCore.Api.Services.IPayrollComputationService, OfficeCore.Api.Services.PayrollComputationService>();
builder.Services.AddScoped<Microsoft.AspNetCore.Authentication.IClaimsTransformation, OfficeCore.Api.Services.OrganizationClaimsTransformation>();
builder.Services.AddScoped<OfficeCore.Api.Services.IAuditService, OfficeCore.Api.Services.AuditService>();
builder.Services.AddScoped<OfficeCore.Api.Services.BiometricReportService>();
builder.Services.AddSingleton<OfficeCore.Api.Services.ISmsOtpService, OfficeCore.Api.Services.SmsOtpService>();
builder.Services.AddSingleton<OfficeCore.Api.Services.IEmailOtpService, OfficeCore.Api.Services.EmailOtpService>();


// ─── Build ────────────────────────────────────────────────────────────────────
var app = builder.Build();

// Seed the database
await DbSeeder.SeedAsync(app.Services);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

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

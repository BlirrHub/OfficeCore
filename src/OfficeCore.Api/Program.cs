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
// Restrict to known origins. In production, add your real domain(s) to
// "AllowedOrigins" in appsettings.json. Dev origins are always included.
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
        .WithOrigins(allOrigins)
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
        "Dashboard", "Clients", "Attendance", "Boards", "MyLeaves",
        "Advances", "Ledger", "Payroll", "Invoices",
        "Users", "Roles", "LeaveApprovals", "WorkSchedules", "TimeOff", "Locations", "Organization" 
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

// Register Application Services
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<OfficeCore.Api.Services.TenantService>();
builder.Services.AddScoped<OfficeCore.Api.Services.IPayrollComputationService, OfficeCore.Api.Services.PayrollComputationService>();
builder.Services.AddScoped<Microsoft.AspNetCore.Authentication.IClaimsTransformation, OfficeCore.Api.Services.OrganizationClaimsTransformation>();

// ─── Build ────────────────────────────────────────────────────────────────────
var app = builder.Build();

// Seed the database
await DbSeeder.SeedAsync(app.Services);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.UseCors("BlazorPolicy");
app.UseRateLimiter();

// ─── Workstation / Desktop Only Enforcer ───────────────────────────────────────
// Blocks direct web browser access (e.g. Chrome, Edge) unless requested by the
// official OfficeCore desktop workstation client or mobile application.
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? "";

    // Allow API endpoints, SignalR hubs, static assets, and OpenAPI
    if (path.StartsWith("/api", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/hubs", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/attendanceHub", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/openapi", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/_framework", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/_content", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".js", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".css", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".ico", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".woff", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".woff2", StringComparison.OrdinalIgnoreCase))
    {
        await next();
        return;
    }

    // Verify desktop / mobile client header or loopback development bypass
    var clientHeader = context.Request.Headers["X-OfficeCore-Client"].ToString();
    var isAuthorizedApp = clientHeader.Contains("OfficeCore-Desktop-Workstation", StringComparison.OrdinalIgnoreCase);

    // If accessed via a generic browser (no app header), block access
    if (!isAuthorizedApp)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "text/html";
        await context.Response.WriteAsync("""
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="utf-8"/>
                <meta name="viewport" content="width=device-width, initial-scale=1.0"/>
                <title>Access Restricted — OfficeCore</title>
                <style>
                    body { margin: 0; background: #09090b; color: #f4f4f5; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; display: flex; align-items: center; justify-content: center; height: 100vh; }
                    .card { background: #18181b; border: 1px solid #27272a; border-radius: 16px; padding: 40px; max-width: 480px; text-align: center; box-shadow: 0 25px 50px -12px rgba(0,0,0,0.5); }
                    .badge { display: inline-block; background: #7f1d1d; color: #fca5a5; font-size: 11px; font-weight: 700; text-transform: uppercase; padding: 4px 10px; border-radius: 9999px; margin-bottom: 16px; }
                    h1 { font-size: 22px; font-weight: 700; margin: 0 0 10px 0; color: #ffffff; }
                    p { font-size: 14px; line-height: 1.6; color: #a1a1aa; margin: 0 0 24px 0; }
                    .instruction { background: #09090b; border: 1px solid #27272a; border-radius: 8px; padding: 14px; font-size: 13px; color: #38bdf8; text-align: left; }
                </style>
            </head>
            <body>
                <div class="card">
                    <div class="badge">Browser Access Restricted</div>
                    <h1>Official Client Required</h1>
                    <p>Direct web browser access to OfficeCore has been restricted for security and licensing compliance.</p>
                    <div class="instruction">
                        <strong>To connect:</strong><br/>
                        1. Launch the official <strong>OfficeCore Workstation</strong> desktop application.<br/>
                        2. Use your assigned server address to log into your account.
                    </div>
                </div>
            </body>
            </html>
            """);
        return;
    }

    await next();
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<OfficeCore.Api.Hubs.AttendanceHub>("/attendanceHub");
app.MapHub<OfficeCore.Api.Hubs.BoardHub>("/hubs/boards");
app.MapHub<OfficeCore.Api.Hubs.ChatHub>("/hubs/chat");
app.MapHub<OfficeCore.Api.Hubs.NotificationHub>("/hubs/notifications");
app.MapFallbackToFile("index.html");

app.Run();

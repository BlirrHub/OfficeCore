using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Data;
using OfficeCore.Api.Models;

using System.Globalization;

var cultureInfo = new CultureInfo("en-PH");
CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;

var builder = WebApplication.CreateBuilder(args);

// CORS – allow the Blazor frontend
builder.Services.AddCors(opt =>
    opt.AddPolicy("BlazorPolicy", p => p
        .SetIsOriginAllowed(origin => true)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

// Add services to the container.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentityCore<ApplicationUser>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 6;
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
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<OfficeCore.Api.Hubs.AttendanceHub>("/attendanceHub");
app.MapHub<OfficeCore.Api.Hubs.BoardHub>("/hubs/boards");
app.MapHub<OfficeCore.Api.Hubs.ChatHub>("/hubs/chat");
app.MapFallbackToFile("index.html");

app.Run();

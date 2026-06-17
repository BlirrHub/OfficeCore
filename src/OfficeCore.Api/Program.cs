using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Data;
using OfficeCore.Api.Models;

var builder = WebApplication.CreateBuilder(args);

// CORS – allow the Blazor frontend
var blazorOrigin = builder.Configuration["BlazorOrigin"] ?? "https://localhost:7057";
builder.Services.AddCors(opt =>
    opt.AddPolicy("BlazorPolicy", p => p
        .WithOrigins(blazorOrigin)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

// Add services to the container.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentityCore<Employee>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 8;
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
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;
    });

builder.Services.AddAuthorization(options =>
{
    var modules = new[] { "Dashboard", "Clients", "Attendance", "Tasks", "Payroll", "Invoices", "Ledger", "WorkSchedules", "TimeOff", "Locations", "Users", "Roles", "Organization" };
    foreach (var module in modules)
    {
        options.AddPolicy($"Require{module}", policy => policy.RequireClaim("ModuleAccess", module));
    }
});

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddSignalR();

// Register Background Services
builder.Services.AddSingleton<OfficeCore.Api.Services.FingerprintDeviceService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<OfficeCore.Api.Services.FingerprintDeviceService>());

var app = builder.Build();

// Seed the database
await DbSeeder.SeedAsync(app.Services);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCors("BlazorPolicy");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<OfficeCore.Api.Hubs.AttendanceHub>("/attendanceHub");

app.Run();

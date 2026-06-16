using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Models;

namespace OfficeCore.Api.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        // Ensure database is created and migrations applied
        await context.Database.MigrateAsync();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Employee>>();

        // 1. Seed Roles
        string adminRoleName = "Admin";
        var adminRole = await roleManager.FindByNameAsync(adminRoleName);
        if (adminRole == null)
        {
            adminRole = new AppRole(adminRoleName) { Description = "Super Administrator with full access" };
            await roleManager.CreateAsync(adminRole);
        }

        // Ensure Admin has all module claims
        var existingClaims = await roleManager.GetClaimsAsync(adminRole);
        var modules = new[]
        {
            "Dashboard", "Clients", "Attendance", "Tasks",
            "Payroll", "Invoices", "Ledger",
            "WorkSchedules", "TimeOff", "Locations",
            "Users", "Roles", "Organization"
        };

        foreach (var module in modules)
        {
            if (!existingClaims.Any(c => c.Type == "ModuleAccess" && c.Value == module))
            {
                await roleManager.AddClaimAsync(adminRole, new Claim("ModuleAccess", module));
            }
        }

        // 2. Seed Default Admin User
        string adminEmail = "admin@company.com";
        if (await userManager.FindByEmailAsync(adminEmail) == null)
        {
            var adminUser = new Employee
            {
                UserName = adminEmail,
                Email = adminEmail,
                FirstName = "System",
                LastName = "Administrator",
                EmailConfirmed = true,
                IsActive = true
            };

            var result = await userManager.CreateAsync(adminUser, "Admin123!");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, adminRoleName);
            }
        }
    }
}

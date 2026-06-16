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
        if (!await roleManager.RoleExistsAsync(adminRoleName))
        {
            var adminRole = new AppRole(adminRoleName) { Description = "Super Administrator with full access" };
            await roleManager.CreateAsync(adminRole);

            // Assign all module claims to Admin
            var modules = new[]
            {
                "Dashboard", "Clients", "Attendance", "Tasks",
                "Payroll", "Invoices", "Ledger",
                "WorkSchedule", "TimeOff", "Locations"
            };
            foreach (var module in modules)
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

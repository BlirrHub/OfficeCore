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

        // Production only applies schema migrations. Customer accounts,
        // organizations, roles, and employees are provisioned through the app.
        await context.Database.MigrateAsync();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();
        const string adminRoleName = "Admin";
        if (!await roleManager.RoleExistsAsync(adminRoleName))
        {
            await roleManager.CreateAsync(new AppRole(adminRoleName)
            {
                Description = "Administrator with full access"
            });
        }
    }
}

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
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        // 1. Ensure Global Admin Role exists in Identity
        const string adminRoleName = "Admin";
        if (!await roleManager.RoleExistsAsync(adminRoleName))
        {
            await roleManager.CreateAsync(new AppRole(adminRoleName)
            {
                Description = "Administrator with full access"
            });
        }

        // 2. Ensure Default Demo User exists
        const string demoEmail = "demo@officecore.com";
        var demoUser = await userManager.FindByEmailAsync(demoEmail);
        if (demoUser == null)
        {
            demoUser = new ApplicationUser
            {
                UserName = demoEmail,
                Email = demoEmail,
                FirstName = "Demo",
                LastName = "Admin",
                EmailConfirmed = true,
                MustChangePassword = false
            };

            var createResult = await userManager.CreateAsync(demoUser, "Password123!");
            if (!createResult.Succeeded)
            {
                return;
            }
        }

        // Ensure user has global Admin Identity role
        if (!await userManager.IsInRoleAsync(demoUser, adminRoleName))
        {
            await userManager.AddToRoleAsync(demoUser, adminRoleName);
        }

        // 3. Ensure Demo Organization exists
        var demoOrg = await context.Organizations
            .FirstOrDefaultAsync(o => o.CreatedByUserId == demoUser.Id || o.Name == "Demo Corp");

        if (demoOrg == null)
        {
            demoOrg = new Organization
            {
                Id = Guid.NewGuid(),
                Name = "Demo Corp",
                CreatedByUserId = demoUser.Id,
                CreatedAt = DateTime.UtcNow
            };
            context.Organizations.Add(demoOrg);
            await context.SaveChangesAsync();
        }

        // 4. Ensure Employee record exists for demoUser in demoOrg
        var demoEmployee = await context.Employees
            .FirstOrDefaultAsync(e => e.UserId == demoUser.Id && e.OrganizationId == demoOrg.Id);

        if (demoEmployee == null)
        {
            demoEmployee = new Employee
            {
                Id = Guid.NewGuid().ToString(),
                UserId = demoUser.Id,
                OrganizationId = demoOrg.Id,
                EmployeeId = "EMP-001",
                DateHired = DateTime.UtcNow,
                IsActive = true,
                IsEmployee = true,
                IsEligibleForLeave = true
            };
            context.Employees.Add(demoEmployee);
            await context.SaveChangesAsync();
        }

        // 5. Ensure Organization Roles and Claims exist
        var adminOrgRole = await context.OrganizationRoles
            .Include(r => r.Claims)
            .FirstOrDefaultAsync(r => r.OrganizationId == demoOrg.Id && r.Name == "Admin");

        if (adminOrgRole == null)
        {
            adminOrgRole = new OrganizationRole
            {
                Id = Guid.NewGuid(),
                OrganizationId = demoOrg.Id,
                Name = "Admin",
                Description = "Administrator with full access",
                ShowAsTitle = true
            };
            context.OrganizationRoles.Add(adminOrgRole);
            await context.SaveChangesAsync();
        }

        var memberOrgRole = await context.OrganizationRoles
            .Include(r => r.Claims)
            .FirstOrDefaultAsync(r => r.OrganizationId == demoOrg.Id && r.Name == "Employee");

        if (memberOrgRole == null)
        {
            memberOrgRole = new OrganizationRole
            {
                Id = Guid.NewGuid(),
                OrganizationId = demoOrg.Id,
                Name = "Employee",
                Description = "Standard organization member",
                ShowAsTitle = false
            };
            context.OrganizationRoles.Add(memberOrgRole);
            await context.SaveChangesAsync();
        }

        var allModules = new[]
        {
            "Dashboard", "Clients", "Attendance", "Boards",
            "Payroll", "Invoices", "Ledger",
            "WorkSchedules", "TimeOff", "Locations",
            "Users", "Roles", "Organization", "Advances", "MyLeaves", "LeaveApprovals"
        };

        foreach (var module in allModules)
        {
            if (!adminOrgRole.Claims.Any(c => c.ClaimType == "ModuleAccess" && c.ClaimValue == module))
            {
                context.OrganizationRoleClaims.Add(new OrganizationRoleClaim
                {
                    Id = Guid.NewGuid(),
                    OrganizationRoleId = adminOrgRole.Id,
                    ClaimType = "ModuleAccess",
                    ClaimValue = module
                });
            }

            if (module is "Dashboard" or "Attendance" or "Boards" or "MyLeaves" or "Advances")
            {
                if (!memberOrgRole.Claims.Any(c => c.ClaimType == "ModuleAccess" && c.ClaimValue == module))
                {
                    context.OrganizationRoleClaims.Add(new OrganizationRoleClaim
                    {
                        Id = Guid.NewGuid(),
                        OrganizationRoleId = memberOrgRole.Id,
                        ClaimType = "ModuleAccess",
                        ClaimValue = module
                    });
                }
            }
        }
        await context.SaveChangesAsync();

        // 6. Ensure Employee is assigned to the Admin role
        var hasAdminRole = await context.EmployeeRoles
            .AnyAsync(er => er.EmployeeId == demoEmployee.Id && er.OrganizationRoleId == adminOrgRole.Id);

        if (!hasAdminRole)
        {
            context.EmployeeRoles.Add(new EmployeeRole
            {
                EmployeeId = demoEmployee.Id,
                OrganizationRoleId = adminOrgRole.Id
            });
            await context.SaveChangesAsync();
        }

        // 7. Ensure Default WorkSchedule exists
        var schedule = await context.WorkSchedules
            .Include(s => s.Days)
            .FirstOrDefaultAsync(s => s.OrganizationId == demoOrg.Id);

        if (schedule == null)
        {
            schedule = new WorkSchedule
            {
                Id = Guid.NewGuid(),
                OrganizationId = demoOrg.Id,
                Name = "Standard Working Hours",
                WorkArrangement = WorkArrangement.Fixed,
                TotalWeeklyHours = 40,
                IsFixedWeeklyRate = false
            };

            for (int day = 0; day < 7; day++)
            {
                bool isRestDay = (day == 0 || day == 6); // Sun, Sat
                schedule.Days.Add(new WorkScheduleDay
                {
                    WorkScheduleId = schedule.Id,
                    OrganizationId = demoOrg.Id,
                    DayOfWeek = (DayOfWeek)day,
                    IsRestDay = isRestDay,
                    StartTime = isRestDay ? TimeSpan.Zero : new TimeSpan(9, 0, 0),
                    EndTime = isRestDay ? TimeSpan.Zero : new TimeSpan(18, 0, 0),
                    WorkingHours = isRestDay ? 0 : 8
                });
            }

            context.WorkSchedules.Add(schedule);
            await context.SaveChangesAsync();
        }

        if (demoEmployee.WorkScheduleId == null)
        {
            demoEmployee.WorkScheduleId = schedule.Id;
            await context.SaveChangesAsync();
        }

        // 8. Ensure a Default Kanban Board exists
        var board = await context.Boards
            .FirstOrDefaultAsync(b => b.OrganizationId == demoOrg.Id);

        if (board == null)
        {
            board = new Board
            {
                Id = Guid.NewGuid(),
                OrganizationId = demoOrg.Id,
                Name = "OfficeCore Sprint",
                Description = "Welcome to your first project board! Drag and drop cards to organize work.",
                CreatedAt = DateTime.UtcNow,
                CreatedById = demoEmployee.Id,
                Members = new List<BoardMember>
                {
                    new BoardMember
                    {
                        Id = Guid.NewGuid(),
                        OrganizationId = demoOrg.Id,
                        UserId = demoEmployee.Id,
                        Role = BoardRole.Owner,
                        IsStarred = true
                    }
                },
                Lists = new List<BoardList>
                {
                    new BoardList
                    {
                        Id = Guid.NewGuid(),
                        OrganizationId = demoOrg.Id,
                        Name = "To Do",
                        OrderIndex = 0,
                        Cards = new List<BoardCard>
                        {
                            new BoardCard
                            {
                                Id = Guid.NewGuid(),
                                OrganizationId = demoOrg.Id,
                                Title = "Review employee attendance logs",
                                Description = "Check the daily biometric and Wi-Fi attendance logs.",
                                OrderIndex = 0,
                                AssignedToUserId = demoEmployee.Id
                            },
                            new BoardCard
                            {
                                Id = Guid.NewGuid(),
                                OrganizationId = demoOrg.Id,
                                Title = "Configure company payroll settings",
                                Description = "Set up SSS, PhilHealth, Pag-IBIG and tax calculation rates.",
                                OrderIndex = 1,
                                AssignedToUserId = demoEmployee.Id
                            }
                        }
                    },
                    new BoardList
                    {
                        Id = Guid.NewGuid(),
                        OrganizationId = demoOrg.Id,
                        Name = "In Progress",
                        OrderIndex = 1,
                        Cards = new List<BoardCard>
                        {
                            new BoardCard
                            {
                                Id = Guid.NewGuid(),
                                OrganizationId = demoOrg.Id,
                                Title = "Explore OfficeCore features",
                                Description = "Test out Chat, Invoicing, Leave Management, and Boards.",
                                OrderIndex = 0,
                                AssignedToUserId = demoEmployee.Id
                            }
                        }
                    },
                    new BoardList
                    {
                        Id = Guid.NewGuid(),
                        OrganizationId = demoOrg.Id,
                        Name = "Done",
                        OrderIndex = 2,
                        Cards = new List<BoardCard>
                        {
                            new BoardCard
                            {
                                Id = Guid.NewGuid(),
                                OrganizationId = demoOrg.Id,
                                Title = "Migrate database to SQLite",
                                Description = "Successfully transitioned the local environment to SQLite.",
                                OrderIndex = 0,
                                AssignedToUserId = demoEmployee.Id
                            }
                        }
                    }
                }
            };

            context.Boards.Add(board);
            await context.SaveChangesAsync();
        }
    }
}

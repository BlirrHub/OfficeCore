using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Models;
using OfficeCore.Shared.Authorization;

namespace OfficeCore.Api.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Ensure database is created and migrations applied
        await context.Database.MigrateAsync();

        // Only seed development demo data if database is empty
        if (await context.Organizations.AnyAsync())
        {
            return;
        }

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

        // 2. Ensure Primary Admin User exists (demo@officecore.com)
        const string demoEmail = "demo@officecore.com";
        var demoUser = new ApplicationUser
        {
            UserName = demoEmail,
            Email = demoEmail,
            FirstName = "Demo",
            LastName = "Admin",
            EmailConfirmed = true,
            MustChangePassword = false
        };
        await userManager.CreateAsync(demoUser, "Password123!");
        await userManager.AddToRoleAsync(demoUser, adminRoleName);

        // 3. Ensure Demo Organization exists
        var demoOrg = new Organization
        {
            Id = Guid.NewGuid(),
            Name = "Demo Corp",
            CreatedByUserId = demoUser.Id,
            CreatedAt = DateTime.UtcNow
        };
        context.Organizations.Add(demoOrg);
        await context.SaveChangesAsync();

        // 4. Ensure Organization Roles and Claims exist
        var adminOrgRole = new OrganizationRole
        {
            Id = Guid.NewGuid(),
            OrganizationId = demoOrg.Id,
            Name = "Admin",
            Description = "Administrator with full access",
            ShowAsTitle = true
        };
        context.OrganizationRoles.Add(adminOrgRole);

        var memberOrgRole = new OrganizationRole
        {
            Id = Guid.NewGuid(),
            OrganizationId = demoOrg.Id,
            Name = "Employee",
            Description = "Standard organization member",
            ShowAsTitle = false
        };
        context.OrganizationRoles.Add(memberOrgRole);
        await context.SaveChangesAsync();

        foreach (var module in ModuleCatalog.AllKeys)
        {
            context.OrganizationRoleClaims.Add(new OrganizationRoleClaim
            {
                Id = Guid.NewGuid(),
                OrganizationRoleId = adminOrgRole.Id,
                ClaimType = "ModuleAccess",
                ClaimValue = module
            });

            if (ModuleCatalog.DefaultKeys.Contains(module))
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
        await context.SaveChangesAsync();

        // 5. Ensure Regular WorkSchedule exists (8:00 AM to 5:00 PM with 1-hr unpaid lunch break)
        var regularSchedule = new WorkSchedule
        {
            Id = Guid.NewGuid(),
            OrganizationId = demoOrg.Id,
            Name = "Regular Schedule",
            WorkArrangement = WorkArrangement.Fixed,
            TotalWeeklyHours = 40,
            IsFixedWeeklyRate = false,
            IncludeTimeTrackedBeforeScheduledStartTime = false
        };

        for (int day = 0; day < 7; day++)
        {
            bool isRestDay = (day == 0 || day == 6); // Sun (0), Sat (6)
            regularSchedule.Days.Add(new WorkScheduleDay
            {
                WorkScheduleId = regularSchedule.Id,
                OrganizationId = demoOrg.Id,
                DayOfWeek = (DayOfWeek)day,
                IsRestDay = isRestDay,
                StartTime = isRestDay ? TimeSpan.Zero : new TimeSpan(8, 0, 0),
                EndTime = isRestDay ? TimeSpan.Zero : new TimeSpan(17, 0, 0),
                WorkingHours = isRestDay ? 0 : 8
            });
        }

        regularSchedule.Breaks.Add(new WorkScheduleBreak
        {
            WorkScheduleId = regularSchedule.Id,
            OrganizationId = demoOrg.Id,
            Name = "Lunch Break",
            IsPaid = false,
            StartTime = new TimeSpan(12, 0, 0),
            EndTime = new TimeSpan(13, 0, 0)
        });

        context.WorkSchedules.Add(regularSchedule);
        await context.SaveChangesAsync();

        // 6. Ensure Government Deduction Settings
        context.GovernmentDeductionSettings.AddRange(
            new GovernmentDeductionSetting { AgencyName = "SSS", EmployeeShare = 250m, EmployerShare = 510m, OrganizationId = demoOrg.Id },
            new GovernmentDeductionSetting { AgencyName = "PhilHealth", EmployeeShare = 250m, EmployerShare = 200m, OrganizationId = demoOrg.Id },
            new GovernmentDeductionSetting { AgencyName = "Pag-IBIG", EmployeeShare = 100m, EmployerShare = 100m, OrganizationId = demoOrg.Id }
        );
        await context.SaveChangesAsync();

        // 7. Seed Specific Employees Defined by User
        async Task<Employee> RegisterEmployeeAsync(
            string firstName, 
            string lastName, 
            string email, 
            string empCode, 
            decimal? rate, 
            bool hasSchedule, 
            bool hasSss, 
            bool hasPhilHealth, 
            bool hasPagIbig,
            bool isAdmin = false)
        {
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                EmailConfirmed = true,
                MustChangePassword = false
            };
            await userManager.CreateAsync(user, "Password123!");
            if (isAdmin)
            {
                await userManager.AddToRoleAsync(user, adminRoleName);
            }

            var employee = new Employee
            {
                Id = Guid.NewGuid().ToString(),
                UserId = user.Id,
                OrganizationId = demoOrg.Id,
                EmployeeId = empCode,
                DateHired = DateTime.UtcNow.AddMonths(-6),
                IsActive = true,
                IsEmployee = true,
                IsEligibleForLeave = true,
                DailyRate = rate,
                WorkScheduleId = hasSchedule ? regularSchedule.Id : null
            };
            context.Employees.Add(employee);
            await context.SaveChangesAsync();

            context.EmployeeRoles.Add(new EmployeeRole
            {
                EmployeeId = employee.Id,
                OrganizationRoleId = isAdmin ? adminOrgRole.Id : memberOrgRole.Id
            });

            bool eligible = hasSss || hasPhilHealth || hasPagIbig;
            if (eligible)
            {
                context.EmployeeGovernmentRecords.Add(new EmployeeGovernmentRecord
                {
                    Id = Guid.NewGuid(),
                    EmployeeId = employee.Id,
                    OrganizationId = demoOrg.Id,
                    IsEligibleForDeductions = true,
                    SssNumber = hasSss ? $"04-{Random.Shared.Next(1000000, 9999999)}-{Random.Shared.Next(0, 9)}" : null,
                    PhilHealthNumber = hasPhilHealth ? $"12-{Random.Shared.Next(100000000, 999999999)}-{Random.Shared.Next(0, 9)}" : null,
                    PagIbigNumber = hasPagIbig ? $"1234-{Random.Shared.Next(1000, 9999)}-{Random.Shared.Next(1000, 9999)}" : null,
                    TinNumber = $"123-{Random.Shared.Next(100, 999)}-{Random.Shared.Next(100, 999)}-000"
                });
            }
            await context.SaveChangesAsync();
            return employee;
        }

        // Demo Employee record for demo@officecore.com
        var demoEmployee = new Employee
        {
            Id = Guid.NewGuid().ToString(),
            UserId = demoUser.Id,
            OrganizationId = demoOrg.Id,
            EmployeeId = "ADM-001",
            DateHired = DateTime.UtcNow,
            IsActive = true,
            IsEmployee = true,
            IsEligibleForLeave = true,
            WorkScheduleId = regularSchedule.Id
        };
        context.Employees.Add(demoEmployee);
        await context.SaveChangesAsync();
        context.EmployeeRoles.Add(new EmployeeRole { EmployeeId = demoEmployee.Id, OrganizationRoleId = adminOrgRole.Id });
        await context.SaveChangesAsync();

        // 1. Adrian: no schedules, fixed rate of 3775 (no gov deductions)
        await RegisterEmployeeAsync("Adrian", "Employee", "adrian@officecore.com", "EMP-001", 3775m, hasSchedule: false, hasSss: false, hasPhilHealth: false, hasPagIbig: false);

        // 2. Blaire: no schedule, fixed rate of 1000 (no gov deductions, also Admin role)
        await RegisterEmployeeAsync("Blaire", "Dela Cruz", "blaire@officecore.com", "EMP-002", 1000m, hasSchedule: false, hasSss: false, hasPhilHealth: false, hasPagIbig: false, isAdmin: true);

        // 3. Lina: no schedule, fixed rate of 2500 (no gov deductions)
        await RegisterEmployeeAsync("Lina", "Employee", "lina@officecore.com", "EMP-003", 2500m, hasSchedule: false, hasSss: false, hasPhilHealth: false, hasPagIbig: false);

        // 4. Honey: Daily Rate 800 (Gov deductions: SSS, PhilHealth, Pag-IBIG)
        await RegisterEmployeeAsync("Honey", "Employee", "honey@officecore.com", "EMP-004", 800m, hasSchedule: true, hasSss: true, hasPhilHealth: true, hasPagIbig: true);

        // 5. Jenny: Daily Rate 900 (Gov deductions: SSS, PhilHealth, Pag-IBIG)
        await RegisterEmployeeAsync("Jenny", "Employee", "jenny@officecore.com", "EMP-005", 900m, hasSchedule: true, hasSss: true, hasPhilHealth: true, hasPagIbig: true);

        // 6. Gian: Daily Rate 755 (no gov deductions)
        await RegisterEmployeeAsync("Gian", "Employee", "gian@officecore.com", "EMP-006", 755m, hasSchedule: true, hasSss: false, hasPhilHealth: false, hasPagIbig: false);

        // 7. Luan: Daily Rate 755 (Gov deductions: SSS, PhilHealth, Pag-IBIG)
        await RegisterEmployeeAsync("Luan", "Employee", "luan@officecore.com", "EMP-007", 755m, hasSchedule: true, hasSss: true, hasPhilHealth: true, hasPagIbig: true);

        // 8. Romnick: Daily Rate 755 (Gov deductions: SSS, PhilHealth, Pag-IBIG)
        await RegisterEmployeeAsync("Romnick", "Employee", "romnick@officecore.com", "EMP-008", 755m, hasSchedule: true, hasSss: true, hasPhilHealth: true, hasPagIbig: true);

        // 9. Dholmer: Daily Rate 755 (Gov deductions: SSS, PhilHealth, Pag-IBIG)
        await RegisterEmployeeAsync("Dholmer", "Employee", "dholmer@officecore.com", "EMP-009", 755m, hasSchedule: true, hasSss: true, hasPhilHealth: true, hasPagIbig: true);

        // 10. Jhonathan: Daily Rate 755 (Gov deductions: SSS, PhilHealth, Pag-IBIG)
        await RegisterEmployeeAsync("Jhonathan", "Employee", "jhonathan@officecore.com", "EMP-010", 755m, hasSchedule: true, hasSss: true, hasPhilHealth: true, hasPagIbig: true);

        // 11. Toni: Daily Rate 755 (no gov deductions)
        await RegisterEmployeeAsync("Toni", "Employee", "toni@officecore.com", "EMP-011", 755m, hasSchedule: true, hasSss: false, hasPhilHealth: false, hasPagIbig: false);

        // 12. Eishrine: Daily Rate 755 (Gov deductions: SSS, PhilHealth, but WITHOUT Pag-IBIG)
        await RegisterEmployeeAsync("Eishrine", "Employee", "eishrine@officecore.com", "EMP-012", 755m, hasSchedule: true, hasSss: true, hasPhilHealth: true, hasPagIbig: false);

        // 13. Ram: Daily Rate 600 (no gov deductions)
        await RegisterEmployeeAsync("Ram", "Employee", "ram@officecore.com", "EMP-013", 600m, hasSchedule: true, hasSss: false, hasPhilHealth: false, hasPagIbig: false);

        // 8. Ensure a Default Kanban Board exists
        var board = new Board
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
                            Title = "Import biometric attendance logs",
                            Description = "Upload weekly biometric report file to generate payroll.",
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
                    OrderIndex = 1
                }
            }
        };

        context.Boards.Add(board);
        await context.SaveChangesAsync();
    }
}

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

        var hrOrgRole = await context.OrganizationRoles
            .Include(r => r.Claims)
            .FirstOrDefaultAsync(r => r.OrganizationId == demoOrg.Id && r.Name == "HR Manager");

        if (hrOrgRole == null)
        {
            hrOrgRole = new OrganizationRole
            {
                Id = Guid.NewGuid(),
                OrganizationId = demoOrg.Id,
                Name = "HR Manager",
                Description = "HR, attendance, leave approvals, and employee records",
                ShowAsTitle = true
            };
            context.OrganizationRoles.Add(hrOrgRole);
            await context.SaveChangesAsync();
        }

        var financeOrgRole = await context.OrganizationRoles
            .Include(r => r.Claims)
            .FirstOrDefaultAsync(r => r.OrganizationId == demoOrg.Id && r.Name == "Finance Specialist");

        if (financeOrgRole == null)
        {
            financeOrgRole = new OrganizationRole
            {
                Id = Guid.NewGuid(),
                OrganizationId = demoOrg.Id,
                Name = "Finance Specialist",
                Description = "Payroll computation, ledger, billing, and advances",
                ShowAsTitle = true
            };
            context.OrganizationRoles.Add(financeOrgRole);
            await context.SaveChangesAsync();
        }

        var allModules = new[]
        {
            "Dashboard", "Clients", "Attendance", "Boards",
            "Payroll", "Invoices", "Ledger",
            "WorkSchedules", "TimeOff", "Locations",
            "Users", "Roles", "Organization", "Advances", "MyLeaves", "LeaveApprovals", "OvertimeApprovals",
            "AuditLogs"
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

            // Standard member self-service claims
            if (module is "Dashboard" or "Boards" or "MyLeaves" or "Advances")
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
            else if (module == "Attendance")
            {
                var existingMemberAttendanceClaim = memberOrgRole.Claims
                    .FirstOrDefault(c => c.ClaimType == "ModuleAccess" && c.ClaimValue == "Attendance");
                if (existingMemberAttendanceClaim != null)
                {
                    context.OrganizationRoleClaims.Remove(existingMemberAttendanceClaim);
                }
            }

            // HR Manager claims: People, Attendance, TimeOff, Leaves, Overtime, Advances
            if (module is "Dashboard" or "Boards" or "Attendance" or "WorkSchedules" or "TimeOff" or "Users" or "MyLeaves" or "LeaveApprovals" or "OvertimeApprovals" or "Advances")
            {
                if (!hrOrgRole.Claims.Any(c => c.ClaimType == "ModuleAccess" && c.ClaimValue == module))
                {
                    context.OrganizationRoleClaims.Add(new OrganizationRoleClaim
                    {
                        Id = Guid.NewGuid(),
                        OrganizationRoleId = hrOrgRole.Id,
                        ClaimType = "ModuleAccess",
                        ClaimValue = module
                    });
                }
            }

            // Finance Specialist claims: Payroll, Ledger, Invoices, Advances, Leaves
            if (module is "Dashboard" or "Boards" or "Payroll" or "Ledger" or "Invoices" or "Advances" or "MyLeaves")
            {
                if (!financeOrgRole.Claims.Any(c => c.ClaimType == "ModuleAccess" && c.ClaimValue == module))
                {
                    context.OrganizationRoleClaims.Add(new OrganizationRoleClaim
                    {
                        Id = Guid.NewGuid(),
                        OrganizationRoleId = financeOrgRole.Id,
                        ClaimType = "ModuleAccess",
                        ClaimValue = module
                    });
                }
            }
        }

        // Ensure all Admin/Owner roles across all organizations have AuditLogs and OvertimeApprovals
        var allAdminRoles = await context.OrganizationRoles
            .Include(r => r.Claims)
            .Where(r => r.Name == "Admin" || r.Name == "Owner")
            .ToListAsync();

        foreach (var adminRole in allAdminRoles)
        {
            foreach (var mod in new[] { "AuditLogs", "OvertimeApprovals" })
            {
                if (!adminRole.Claims.Any(c => c.ClaimType == "ModuleAccess" && c.ClaimValue == mod))
                {
                    context.OrganizationRoleClaims.Add(new OrganizationRoleClaim
                    {
                        Id = Guid.NewGuid(),
                        OrganizationRoleId = adminRole.Id,
                        ClaimType = "ModuleAccess",
                        ClaimValue = mod
                    });
                }
            }
        }

        // Ensure standard Employee roles do not have admin Attendance module access
        var allEmployeeRoles = await context.OrganizationRoles
            .Include(r => r.Claims)
            .Where(r => r.Name == "Employee")
            .ToListAsync();

        foreach (var empRole in allEmployeeRoles)
        {
            var adminAttendanceClaim = empRole.Claims
                .FirstOrDefault(c => c.ClaimType == "ModuleAccess" && c.ClaimValue == "Attendance");
            if (adminAttendanceClaim != null)
            {
                context.OrganizationRoleClaims.Remove(adminAttendanceClaim);
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

        // 7. Ensure Default WorkSchedule exists (8:00 AM to 5:00 PM with 1-hr unpaid lunch break)
        var schedule = await context.WorkSchedules
            .Include(s => s.Days)
            .Include(s => s.Breaks)
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
                IsFixedWeeklyRate = false,
                IncludeTimeTrackedBeforeScheduledStartTime = false
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
                    StartTime = isRestDay ? TimeSpan.Zero : new TimeSpan(8, 0, 0),
                    EndTime = isRestDay ? TimeSpan.Zero : new TimeSpan(17, 0, 0),
                    WorkingHours = isRestDay ? 0 : 8
                });
            }

            schedule.Breaks.Add(new WorkScheduleBreak
            {
                WorkScheduleId = schedule.Id,
                OrganizationId = demoOrg.Id,
                Name = "Lunch Break",
                IsPaid = false,
                StartTime = new TimeSpan(12, 0, 0),
                EndTime = new TimeSpan(13, 0, 0)
            });

            context.WorkSchedules.Add(schedule);
            await context.SaveChangesAsync();
        }
        else
        {
            // Sync shift hours to 8:00 AM - 5:00 PM for all working days
            foreach (var d in schedule.Days)
            {
                if (!d.IsRestDay)
                {
                    d.StartTime = new TimeSpan(8, 0, 0);
                    d.EndTime = new TimeSpan(17, 0, 0);
                    d.WorkingHours = 8;
                }
            }

            // Ensure unpaid lunch break (12:00 PM - 1:00 PM) exists
            if (!schedule.Breaks.Any(b => !b.IsPaid && b.StartTime == new TimeSpan(12, 0, 0) && b.EndTime == new TimeSpan(13, 0, 0)))
            {
                schedule.Breaks.Add(new WorkScheduleBreak
                {
                    WorkScheduleId = schedule.Id,
                    OrganizationId = demoOrg.Id,
                    Name = "Lunch Break",
                    IsPaid = false,
                    StartTime = new TimeSpan(12, 0, 0),
                    EndTime = new TimeSpan(13, 0, 0)
                });
            }
            await context.SaveChangesAsync();
        }

        if (demoEmployee.WorkScheduleId == null)
        {
            demoEmployee.WorkScheduleId = schedule.Id;
            await context.SaveChangesAsync();
        }

        // Helper method to ensure Government Record exists for deductions
        async Task EnsureGovernmentRecordAsync(string employeeId, string sss, string philHealth, string pagIbig, string tin)
        {
            var gov = await context.EmployeeGovernmentRecords.FirstOrDefaultAsync(g => g.EmployeeId == employeeId);
            if (gov == null)
            {
                context.EmployeeGovernmentRecords.Add(new EmployeeGovernmentRecord
                {
                    Id = Guid.NewGuid(),
                    EmployeeId = employeeId,
                    OrganizationId = demoOrg.Id,
                    IsEligibleForDeductions = true,
                    SssNumber = sss,
                    PhilHealthNumber = philHealth,
                    PagIbigNumber = pagIbig,
                    TinNumber = tin
                });
                await context.SaveChangesAsync();
            }
        }

        // 7b. Employee 1: Alex Staff (Standard Employee role)
        const string standardEmployeeEmail = "employee@officecore.com";
        var standardUser = await userManager.FindByEmailAsync(standardEmployeeEmail);
        if (standardUser == null)
        {
            standardUser = new ApplicationUser
            {
                UserName = standardEmployeeEmail,
                Email = standardEmployeeEmail,
                FirstName = "Alex",
                LastName = "Staff",
                EmailConfirmed = true,
                MustChangePassword = false
            };

            await userManager.CreateAsync(standardUser, "Password123!");
        }

        var standardEmployee = await context.Employees
            .FirstOrDefaultAsync(e => e.UserId == standardUser.Id && e.OrganizationId == demoOrg.Id);

        if (standardEmployee == null)
        {
            standardEmployee = new Employee
            {
                Id = Guid.NewGuid().ToString(),
                UserId = standardUser.Id,
                OrganizationId = demoOrg.Id,
                EmployeeId = "EMP-002",
                DateHired = DateTime.UtcNow.AddMonths(-3),
                IsActive = true,
                IsEmployee = true,
                IsEligibleForLeave = true,
                DailyRate = 800m,
                WorkScheduleId = schedule.Id
            };
            context.Employees.Add(standardEmployee);
            await context.SaveChangesAsync();
        }
        else
        {
            standardEmployee.DailyRate = 800m;
            standardEmployee.WorkScheduleId = schedule.Id;
            await context.SaveChangesAsync();
        }

        var hasMemberRole = await context.EmployeeRoles
            .AnyAsync(er => er.EmployeeId == standardEmployee.Id && er.OrganizationRoleId == memberOrgRole.Id);

        if (!hasMemberRole)
        {
            context.EmployeeRoles.Add(new EmployeeRole
            {
                EmployeeId = standardEmployee.Id,
                OrganizationRoleId = memberOrgRole.Id
            });
            await context.SaveChangesAsync();
        }

        await EnsureGovernmentRecordAsync(standardEmployee.Id, "04-1234567-8", "12-345678901-2", "1234-5678-9012", "123-456-789-000");

        // 7c. Employee 2: Beatriz Ramos (HR Manager role)
        const string hrEmail = "beatriz.ramos@officecore.com";
        var hrUser = await userManager.FindByEmailAsync(hrEmail);
        if (hrUser == null)
        {
            hrUser = new ApplicationUser
            {
                UserName = hrEmail,
                Email = hrEmail,
                FirstName = "Beatriz",
                LastName = "Ramos",
                EmailConfirmed = true,
                MustChangePassword = false
            };
            await userManager.CreateAsync(hrUser, "Password123!");
        }

        var hrEmployee = await context.Employees
            .FirstOrDefaultAsync(e => e.UserId == hrUser.Id && e.OrganizationId == demoOrg.Id);

        if (hrEmployee == null)
        {
            hrEmployee = new Employee
            {
                Id = Guid.NewGuid().ToString(),
                UserId = hrUser.Id,
                OrganizationId = demoOrg.Id,
                EmployeeId = "EMP-003",
                DateHired = DateTime.UtcNow.AddMonths(-6),
                IsActive = true,
                IsEmployee = true,
                IsEligibleForLeave = true,
                DailyRate = 1200m,
                WorkScheduleId = schedule.Id
            };
            context.Employees.Add(hrEmployee);
            await context.SaveChangesAsync();
        }
        else
        {
            hrEmployee.DailyRate = 1200m;
            hrEmployee.WorkScheduleId = schedule.Id;
            await context.SaveChangesAsync();
        }

        var hasHrRole = await context.EmployeeRoles
            .AnyAsync(er => er.EmployeeId == hrEmployee.Id && er.OrganizationRoleId == hrOrgRole.Id);

        if (!hasHrRole)
        {
            context.EmployeeRoles.Add(new EmployeeRole
            {
                EmployeeId = hrEmployee.Id,
                OrganizationRoleId = hrOrgRole.Id
            });
            await context.SaveChangesAsync();
        }

        await EnsureGovernmentRecordAsync(hrEmployee.Id, "04-8765432-1", "12-987654321-0", "9876-5432-1098", "987-654-321-000");

        // 7d. Employee 3: Carlos Mendoza (Finance Specialist role)
        const string financeEmail = "carlos.mendoza@officecore.com";
        var financeUser = await userManager.FindByEmailAsync(financeEmail);
        if (financeUser == null)
        {
            financeUser = new ApplicationUser
            {
                UserName = financeEmail,
                Email = financeEmail,
                FirstName = "Carlos",
                LastName = "Mendoza",
                EmailConfirmed = true,
                MustChangePassword = false
            };
            await userManager.CreateAsync(financeUser, "Password123!");
        }

        var financeEmployee = await context.Employees
            .FirstOrDefaultAsync(e => e.UserId == financeUser.Id && e.OrganizationId == demoOrg.Id);

        if (financeEmployee == null)
        {
            financeEmployee = new Employee
            {
                Id = Guid.NewGuid().ToString(),
                UserId = financeUser.Id,
                OrganizationId = demoOrg.Id,
                EmployeeId = "EMP-004",
                DateHired = DateTime.UtcNow.AddMonths(-4),
                IsActive = true,
                IsEmployee = true,
                IsEligibleForLeave = true,
                DailyRate = 1000m,
                WorkScheduleId = schedule.Id
            };
            context.Employees.Add(financeEmployee);
            await context.SaveChangesAsync();
        }
        else
        {
            financeEmployee.DailyRate = 1000m;
            financeEmployee.WorkScheduleId = schedule.Id;
            await context.SaveChangesAsync();
        }

        var hasFinanceRole = await context.EmployeeRoles
            .AnyAsync(er => er.EmployeeId == financeEmployee.Id && er.OrganizationRoleId == financeOrgRole.Id);

        if (!hasFinanceRole)
        {
            context.EmployeeRoles.Add(new EmployeeRole
            {
                EmployeeId = financeEmployee.Id,
                OrganizationRoleId = financeOrgRole.Id
            });
            await context.SaveChangesAsync();
        }

        await EnsureGovernmentRecordAsync(financeEmployee.Id, "04-5551234-9", "12-555678901-3", "5555-1234-9012", "555-123-456-000");

        // 7e. Ensure Default Government Deduction Settings
        if (!await context.GovernmentDeductionSettings.AnyAsync(s => s.OrganizationId == demoOrg.Id))
        {
            context.GovernmentDeductionSettings.AddRange(
                new GovernmentDeductionSetting { AgencyName = "SSS", EmployeeShare = 250m, EmployerShare = 510m, OrganizationId = demoOrg.Id },
                new GovernmentDeductionSetting { AgencyName = "PhilHealth", EmployeeShare = 250m, EmployerShare = 200m, OrganizationId = demoOrg.Id },
                new GovernmentDeductionSetting { AgencyName = "Pag-IBIG", EmployeeShare = 100m, EmployerShare = 100m, OrganizationId = demoOrg.Id }
            );
            await context.SaveChangesAsync();
        }

        // 7f. Seed 1-Week Attendance Records (Sept 21 – Sept 25, 2026) for Testing Payroll
        var localTz = TimeZoneInfo.Local;
        var offset = localTz.GetUtcOffset(new DateTime(2026, 9, 21));

        void AddPunch(string empId, int year, int month, int day, int hour, int minute, AttendanceType type)
        {
            var localTime = new DateTime(year, month, day, hour, minute, 0);
            var timestamp = new DateTimeOffset(localTime, offset).ToUniversalTime();
            var startRange = timestamp.AddMinutes(-2);
            var endRange = timestamp.AddMinutes(2);

            if (!context.AttendanceRecords.Any(a => a.EmployeeId == empId && a.Type == type && a.Timestamp >= startRange && a.Timestamp <= endRange))
            {
                context.AttendanceRecords.Add(new AttendanceRecord
                {
                    Id = Guid.NewGuid(),
                    EmployeeId = empId,
                    OrganizationId = demoOrg.Id,
                    Timestamp = timestamp,
                    Type = type,
                    Source = AttendanceSource.Device
                });
            }
        }

        // Scenario 1: Alex Staff — Early-bird / Perfect 8AM-5PM attendance (Punches between 7AM and 8AM every day)
        AddPunch(standardEmployee.Id, 2026, 9, 21, 7, 42, AttendanceType.ClockIn);
        AddPunch(standardEmployee.Id, 2026, 9, 21, 17, 5, AttendanceType.ClockOut);

        AddPunch(standardEmployee.Id, 2026, 9, 22, 7, 35, AttendanceType.ClockIn);
        AddPunch(standardEmployee.Id, 2026, 9, 22, 17, 0, AttendanceType.ClockOut);

        AddPunch(standardEmployee.Id, 2026, 9, 23, 7, 50, AttendanceType.ClockIn);
        AddPunch(standardEmployee.Id, 2026, 9, 23, 17, 10, AttendanceType.ClockOut);

        AddPunch(standardEmployee.Id, 2026, 9, 24, 7, 30, AttendanceType.ClockIn);
        AddPunch(standardEmployee.Id, 2026, 9, 24, 17, 2, AttendanceType.ClockOut);

        AddPunch(standardEmployee.Id, 2026, 9, 25, 7, 45, AttendanceType.ClockIn);
        AddPunch(standardEmployee.Id, 2026, 9, 25, 17, 15, AttendanceType.ClockOut);

        // Scenario 2: Beatriz Ramos — Irregular attendance (Late on some days, punch during 12PM-1PM lunch, 1 day absent)
        AddPunch(hrEmployee.Id, 2026, 9, 21, 9, 30, AttendanceType.ClockIn); // Late arrival (9:30 AM)
        AddPunch(hrEmployee.Id, 2026, 9, 21, 17, 0, AttendanceType.ClockOut);

        AddPunch(hrEmployee.Id, 2026, 9, 22, 12, 45, AttendanceType.ClockIn); // Arriving at work between 12PM and 1PM!
        AddPunch(hrEmployee.Id, 2026, 9, 22, 17, 0, AttendanceType.ClockOut);

        // Wednesday 2026-09-23: ABSENT (No punches recorded)

        AddPunch(hrEmployee.Id, 2026, 9, 24, 8, 45, AttendanceType.ClockIn); // Late arrival (8:45 AM)
        AddPunch(hrEmployee.Id, 2026, 9, 24, 17, 0, AttendanceType.ClockOut);

        AddPunch(hrEmployee.Id, 2026, 9, 25, 8, 0, AttendanceType.ClockIn);  // On-time (8:00 AM)
        AddPunch(hrEmployee.Id, 2026, 9, 25, 17, 0, AttendanceType.ClockOut);

        // Scenario 3: Carlos Mendoza — Overtime and Undertime variations
        AddPunch(financeEmployee.Id, 2026, 9, 21, 8, 0, AttendanceType.ClockIn);  // Extended evening overtime (19:30)
        AddPunch(financeEmployee.Id, 2026, 9, 21, 19, 30, AttendanceType.ClockOut);

        AddPunch(financeEmployee.Id, 2026, 9, 22, 8, 0, AttendanceType.ClockIn);  // Undertime / Early departure (15:00)
        AddPunch(financeEmployee.Id, 2026, 9, 22, 15, 0, AttendanceType.ClockOut);

        AddPunch(financeEmployee.Id, 2026, 9, 23, 8, 0, AttendanceType.ClockIn);  // Standard 8-hr shift
        AddPunch(financeEmployee.Id, 2026, 9, 23, 17, 0, AttendanceType.ClockOut);

        AddPunch(financeEmployee.Id, 2026, 9, 24, 8, 30, AttendanceType.ClockIn); // Late arrival + early departure (16:30)
        AddPunch(financeEmployee.Id, 2026, 9, 24, 16, 30, AttendanceType.ClockOut);

        AddPunch(financeEmployee.Id, 2026, 9, 25, 8, 0, AttendanceType.ClockIn);  // Extended shift (18:00)
        AddPunch(financeEmployee.Id, 2026, 9, 25, 18, 0, AttendanceType.ClockOut);

        await context.SaveChangesAsync();

        // 7g. Ensure a Draft Payroll Period exists for testing the Sept 21 – Sept 27, 2026 cutoff
        var testPeriod = await context.PayrollPeriods
            .FirstOrDefaultAsync(p => p.OrganizationId == demoOrg.Id && p.StartDate == new DateOnly(2026, 9, 21) && p.EndDate == new DateOnly(2026, 9, 27));

        if (testPeriod == null)
        {
            testPeriod = new PayrollPeriod
            {
                Id = Guid.NewGuid(),
                OrganizationId = demoOrg.Id,
                StartDate = new DateOnly(2026, 9, 21),
                EndDate = new DateOnly(2026, 9, 27),
                Status = PayrollStatus.Draft,
                DeductionMultiplier = 0.25m, // Weekly proportion for gov deductions
                Remarks = "Cutoff Sept 21 - Sept 27, 2026 (Test Week)",
                CreatedAt = DateTime.UtcNow
            };
            context.PayrollPeriods.Add(testPeriod);
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

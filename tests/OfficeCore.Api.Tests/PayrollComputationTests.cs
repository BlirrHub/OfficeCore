using System.IO.Compression;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OfficeCore.Api.Controllers;
using OfficeCore.Api.Models;
using OfficeCore.Api.Services;
using Xunit;

namespace OfficeCore.Api.Tests;

public class PayrollComputationTests
{
    // Exercise a positive UTC offset independently of the developer/CI machine timezone.
    private static readonly TimeZoneInfo OfficeTime = TimeZoneInfo.CreateCustomTimeZone(
        "TestOffice", TimeSpan.FromHours(8), "Test office", "Test office");

    [Fact]
    public async Task Full_shift_deducts_unpaid_break_and_ignores_early_arrival()
    {
        await using var fixture = new PayrollFixture();
        fixture.Punch(7, AttendanceType.ClockIn);
        fixture.Punch(17, AttendanceType.ClockOut);
        var payslip = await fixture.Compute();
        Assert.Equal(8, payslip.TotalWorkedHours);
        Assert.Equal(800m, payslip.BasicPay);
        Assert.Equal(800m, payslip.NetPay);
    }

    [Theory]
    [InlineData(17, 10)]
    [InlineData(18, 0)]
    public async Task Unapproved_overtime_does_not_increase_pay(int hour, int minute)
    {
        await using var fixture = new PayrollFixture();
        fixture.Punch(8, AttendanceType.ClockIn);
        fixture.Punch(hour, AttendanceType.ClockOut, minute);
        var payslip = await fixture.Compute();
        Assert.Equal(800m, payslip.GrossPay);
        Assert.Equal(0m, payslip.TotalOvertimePay);
    }

    [Fact]
    public async Task Approved_overtime_is_paid_once()
    {
        await using var fixture = new PayrollFixture();
        fixture.Punch(8, AttendanceType.ClockIn);
        fixture.Punch(18, AttendanceType.ClockOut);
        fixture.Db.OvertimeRequests.Add(new OvertimeRequest
        {
            EmployeeId = fixture.Employee.Id, OrganizationId = fixture.Organization.Id,
            Date = fixture.CalendarDate, Status = OvertimeStatus.Approved,
            RequestedHours = 1, ApprovedHours = 1
        });
        var payslip = await fixture.Compute();
        Assert.Equal(800m, payslip.BasicPay);
        Assert.Equal(125m, payslip.TotalOvertimePay);
        Assert.Equal(925m, payslip.NetPay);
    }

    [Fact]
    public async Task Incomplete_punch_pair_does_not_create_worked_hours()
    {
        await using var fixture = new PayrollFixture();
        fixture.Punch(8, AttendanceType.ClockIn);
        var payslip = await fixture.Compute();
        Assert.Equal(0, payslip.TotalWorkedHours);
        Assert.Equal(0m, payslip.BasicPay);
    }

    [Fact]
    public async Task Midnight_shift_includes_punch_at_local_cutoff_start()
    {
        await using var fixture = new PayrollFixture();
        var day = fixture.Employee.WorkSchedule!.Days.Single(d => d.DayOfWeek == DayOfWeek.Monday);
        day.StartTime = TimeSpan.Zero;
        day.EndTime = TimeSpan.FromHours(8);
        fixture.Employee.WorkSchedule.Breaks.Clear();
        fixture.Punch(0, AttendanceType.ClockIn);
        fixture.Punch(8, AttendanceType.ClockOut);
        Assert.Equal(800m, (await fixture.Compute()).BasicPay);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(-8)]
    public async Task Overtime_calendar_date_is_independent_of_office_utc_offset(int offsetHours)
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("Offset test", TimeSpan.FromHours(offsetHours), "Offset test", "Offset test");
        await using var fixture = new PayrollFixture(timeZone: zone);
        fixture.Punch(8, AttendanceType.ClockIn);
        fixture.Punch(18, AttendanceType.ClockOut);
        fixture.Db.OvertimeRequests.Add(new OvertimeRequest
        {
            EmployeeId = fixture.Employee.Id, OrganizationId = fixture.Organization.Id,
            Date = fixture.CalendarDate, Status = OvertimeStatus.Approved,
            RequestedHours = 1, ApprovedHours = 1
        });
        Assert.Equal(125m, (await fixture.Compute()).TotalOvertimePay);
    }

    [Theory]
    [InlineData(2026, 10, 26, 800)] // Monday: local workday, UTC cutoff starts Sunday.
    [InlineData(2026, 10, 31, 0)]   // Saturday: local rest day, UTC cutoff starts Friday.
    public async Task Fixed_weekly_rate_uses_office_calendar_day(int year, int month, int day, int expected)
    {
        await using var fixture = new PayrollFixture(new DateOnly(year, month, day));
        fixture.Employee.WorkSchedule!.IsFixedWeeklyRate = true;
        var payslip = await fixture.Compute();
        Assert.Equal((decimal)expected, payslip.BasicPay);
    }

    [Fact]
    public async Task Approved_monday_leave_is_not_mistaken_for_sunday()
    {
        await using var fixture = new PayrollFixture();
        fixture.Db.GeneralPayrollSettings.Add(new GeneralPayrollSetting
        {
            Key = "MinimumDailyRate", Value = 610m, OrganizationId = fixture.Organization.Id
        });
        fixture.Db.LeaveRequests.Add(new LeaveRequest
        {
            Id = Guid.NewGuid(), EmployeeId = fixture.Employee.Id,
            OrganizationId = fixture.Organization.Id, Status = LeaveRequestStatus.Approved,
            StartDate = fixture.CalendarDate, EndDate = fixture.CalendarDate
        });
        var payslip = await fixture.Compute();
        Assert.Equal(610m, payslip.TotalLeavePay);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-3)]
    public async Task Leave_spanning_cutoff_credits_the_workday_inside_the_period(int startDayOffset)
    {
        await using var fixture = new PayrollFixture();
        fixture.Db.GeneralPayrollSettings.Add(new GeneralPayrollSetting
        {
            Key = "MinimumDailyRate", Value = 610m, OrganizationId = fixture.Organization.Id
        });
        fixture.Db.LeaveRequests.Add(new LeaveRequest
        {
            Id = Guid.NewGuid(), EmployeeId = fixture.Employee.Id,
            OrganizationId = fixture.Organization.Id, Status = LeaveRequestStatus.Approved,
            StartDate = fixture.CalendarDate.AddDays(startDayOffset), EndDate = fixture.CalendarDate
        });
        var payslip = await fixture.Compute();
        Assert.Equal(610m, payslip.TotalLeavePay);
    }

    [Fact]
    public async Task Government_deductions_are_prorated_and_cash_advance_is_capped_to_balance()
    {
        await using var fixture = new PayrollFixture();
        fixture.Employee.WorkSchedule = null;
        fixture.Employee.WorkScheduleId = null;
        fixture.Period.DeductionMultiplier = 0.5m;
        fixture.Employee.GovernmentRecord = new EmployeeGovernmentRecord
        {
            Id = Guid.NewGuid(), EmployeeId = fixture.Employee.Id,
            OrganizationId = fixture.Organization.Id, SssNumber = "test-only",
            PhilHealthNumber = "test-only", PagIbigNumber = "test-only"
        };
        foreach (var (agency, amount) in new[] { ("SSS", 200m), ("PhilHealth", 100m), ("Pag-IBIG", 80m) })
            fixture.Db.GovernmentDeductionSettings.Add(new GovernmentDeductionSetting
            {
                Id = Guid.NewGuid(), AgencyName = agency, EmployeeShare = amount,
                OrganizationId = fixture.Organization.Id
            });
        fixture.Db.AdvanceRequests.Add(new AdvanceRequest
        {
            Id = Guid.NewGuid(), EmployeeId = fixture.Employee.Id,
            OrganizationId = fixture.Organization.Id, Type = AdvanceType.CashAdvance,
            Status = AdvanceStatus.Approved, RequestedAmount = 500m, RemainingBalance = 100m
        });
        var payslip = await fixture.Compute();
        Assert.Equal(100m, payslip.SssDeduction);
        Assert.Equal(50m, payslip.PhilHealthDeduction);
        Assert.Equal(40m, payslip.PagIbigDeduction);
        Assert.Equal(100m, payslip.AdvancesDeduction);
        Assert.Equal(510m, payslip.NetPay);
    }

    [Fact]
    public async Task Other_workspace_records_do_not_change_payroll()
    {
        await using var fixture = new PayrollFixture();
        var otherOrg = Guid.NewGuid();
        fixture.Db.Employees.Add(new Employee { UserId = "other-user", OrganizationId = otherOrg, DailyRate = 9999m });
        // Deliberately mismatched records verify query-level tenant filtering as well as employee filtering.
        fixture.Punch(8, AttendanceType.ClockIn, organizationId: otherOrg);
        fixture.Punch(17, AttendanceType.ClockOut, organizationId: otherOrg);
        fixture.Db.LeaveRequests.Add(new LeaveRequest
        {
            Id = Guid.NewGuid(), EmployeeId = fixture.Employee.Id, OrganizationId = otherOrg,
            Status = LeaveRequestStatus.Approved,
            StartDate = fixture.CalendarDate, EndDate = fixture.CalendarDate
        });
        fixture.Db.AdvanceRequests.Add(new AdvanceRequest
        {
            Id = Guid.NewGuid(), EmployeeId = fixture.Employee.Id, OrganizationId = otherOrg,
            Status = AdvanceStatus.Approved, RequestedAmount = 10000m, RemainingBalance = 10000m
        });
        fixture.Db.OvertimeRequests.Add(new OvertimeRequest
        {
            EmployeeId = fixture.Employee.Id, OrganizationId = otherOrg,
            Date = fixture.CalendarDate, Status = OvertimeStatus.Approved, ApprovedHours = 10
        });
        var payslip = await fixture.Compute();
        Assert.Equal(fixture.Employee.Id, payslip.EmployeeId);
        Assert.Equal(0m, payslip.GrossPay);
        Assert.Equal(0m, payslip.AdvancesDeduction);
    }

    [Fact]
    public async Task Recompute_replaces_payslips_without_mutating_loan_balance()
    {
        await using var fixture = new PayrollFixture();
        fixture.Db.AdvanceRequests.Add(new AdvanceRequest
        {
            Id = Guid.NewGuid(), EmployeeId = fixture.Employee.Id, OrganizationId = fixture.Organization.Id,
            Status = AdvanceStatus.Approved, RequestedAmount = 100m, RemainingBalance = 100m
        });
        await fixture.Compute();
        await fixture.Compute();
        Assert.Single(await fixture.Db.Payslips.ToListAsync());
        Assert.Equal(100m, (await fixture.Db.AdvanceRequests.SingleAsync()).RemainingBalance);
        Assert.Equal(PayrollStatus.Computed, fixture.Period.Status);
    }

    [Theory]
    [InlineData(PayrollStatus.Finalized)]
    [InlineData(PayrollStatus.Paid)]
    public async Task Locked_payroll_rejects_recomputation(PayrollStatus status)
    {
        await using var fixture = new PayrollFixture();
        fixture.Period.Status = status;
        await fixture.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<Exception>(() => fixture.Service.ComputePayrollPeriodAsync(fixture.Period.Id));
        Assert.Empty(await fixture.Db.Payslips.ToListAsync());
        Assert.Equal(status, fixture.Period.Status);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Inactive_or_non_payroll_members_are_excluded(bool active, bool employee)
    {
        await using var fixture = new PayrollFixture();
        fixture.Employee.IsActive = active;
        fixture.Employee.IsEmployee = employee;
        await fixture.Db.SaveChangesAsync();
        await fixture.Service.ComputePayrollPeriodAsync(fixture.Period.Id);
        Assert.Empty(await fixture.Db.Payslips.ToListAsync());
    }

    [Fact]
    public async Task Split_shift_pairs_only_worked_intervals_and_excludes_midday_absence()
    {
        await using var fixture = new PayrollFixture();
        fixture.Punch(8, AttendanceType.ClockIn);
        fixture.Punch(10, AttendanceType.ClockOut);
        fixture.Punch(15, AttendanceType.ClockIn);
        fixture.Punch(17, AttendanceType.ClockOut);
        var payslip = await fixture.Compute();
        Assert.Equal(4, payslip.TotalWorkedHours);
        Assert.Equal(400m, payslip.BasicPay);
    }

    [Fact]
    public async Task Custom_daily_rate_and_attended_day_deduplication_apply_to_leaves()
    {
        await using var fixture = new PayrollFixture();
        fixture.Employee.UseCustomDailyRate = true;
        fixture.Employee.DailyRate = 900m;
        fixture.Db.LeaveRequests.Add(new LeaveRequest
        {
            Id = Guid.NewGuid(), EmployeeId = fixture.Employee.Id,
            OrganizationId = fixture.Organization.Id, Status = LeaveRequestStatus.Approved,
            StartDate = fixture.CalendarDate, EndDate = fixture.CalendarDate
        });
        var leaveOnlyPayslip = await fixture.Compute();
        Assert.Equal(900m, leaveOnlyPayslip.TotalLeavePay);

        // If employee also attended that same day, do not double-credit full leave pay on top of attendance
        fixture.Punch(8, AttendanceType.ClockIn);
        fixture.Punch(17, AttendanceType.ClockOut);
        var attendedPayslip = await fixture.Compute();
        Assert.Equal(900m, attendedPayslip.BasicPay);
        Assert.Equal(0m, attendedPayslip.TotalLeavePay);
    }

    [Fact]
    public void Biometric_name_matching_uses_word_tokens_and_rejects_substring_collisions()
    {
        var ann = new ApplicationUser { FirstName = "Ann", LastName = "Smith" };
        var joanna = new ApplicationUser { FirstName = "Joanna", LastName = "Cruz" };

        Assert.False(BiometricReportService.MatchesEmployeeNameTokens("Joanna Cruz", ann));
        Assert.True(BiometricReportService.MatchesEmployeeNameTokens("Cruz, Joanna", joanna));
    }

    [Fact]
    public async Task Minimum_daily_rate_zero_falls_back_to_610_and_settings_controller_enforces_positive_default()
    {
        await using var fixture = new PayrollFixture();
        fixture.Employee.DailyRate = null;
        fixture.Db.GeneralPayrollSettings.Add(new GeneralPayrollSetting
        {
            Key = "MinimumDailyRate",
            Value = 0m,
            OrganizationId = fixture.Organization.Id
        });
        fixture.Punch(8, AttendanceType.ClockIn);
        fixture.Punch(17, AttendanceType.ClockOut);

        var payslip = await fixture.Compute();
        Assert.Equal(610m, payslip.BasicPay);

        // Controller should return 610 without persisting 0 and reject <= 0 updates.
        var http = new DefaultHttpContext();
        http.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, fixture.Employee.UserId) }, "Test"));
        http.Request.Headers.Cookie = $"ActiveOrganizationId={fixture.Organization.Id}";
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped(_ => fixture.Db);
        services.AddIdentityCore<ApplicationUser>().AddEntityFrameworkStores<AppDbContext>();
        var accessor = new HttpContextAccessor { HttpContext = http };
        services.AddSingleton<IHttpContextAccessor>(accessor);
        services.AddScoped<TenantService>();
        await using var sp = services.BuildServiceProvider();
        var tenant = sp.GetRequiredService<TenantService>();
        var controller = new PayrollSettingsController(fixture.Db, tenant)
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };

        var getRes = Assert.IsType<OkObjectResult>(await controller.GetGeneralSettings());
        var minimumDailyRateProp = getRes.Value!.GetType().GetProperty("minimumDailyRate")
            ?? getRes.Value!.GetType().GetProperty("MinimumDailyRate");
        Assert.NotNull(minimumDailyRateProp);
        Assert.Equal(610m, (decimal)minimumDailyRateProp!.GetValue(getRes.Value)!);

        var badUpdate = await controller.UpdateGeneralSettings(new GeneralSettingsDto { MinimumDailyRate = 0m });
        Assert.IsType<BadRequestObjectResult>(badUpdate);
    }

    [Fact]
    public async Task Equal_timestamps_order_clock_out_before_clock_in()
    {
        await using var fixture = new PayrollFixture();
        // Add ClockIn at 10:00 before ClockOut at 10:00 to ensure EF/insertion order does not break pairing.
        fixture.Punch(8, AttendanceType.ClockIn);
        fixture.Punch(10, AttendanceType.ClockIn);
        fixture.Punch(10, AttendanceType.ClockOut);
        fixture.Punch(17, AttendanceType.ClockOut);

        var payslip = await fixture.Compute();
        Assert.Equal(8, payslip.TotalWorkedHours);
        Assert.Equal(800m, payslip.BasicPay);
    }

    [Fact]
    public async Task Scheduled_workday_pre_overtime_is_zero_when_unworked_and_capped_by_actual_excess_hours()
    {
        // Case 1: Regular workday with 8:00-17:00 punches (no excess) and PreOvertime request -> 0 OT pay
        await using (var noExcess = new PayrollFixture())
        {
            noExcess.Punch(8, AttendanceType.ClockIn);
            noExcess.Punch(17, AttendanceType.ClockOut);
            noExcess.Db.OvertimeRequests.Add(new OvertimeRequest
            {
                Id = Guid.NewGuid(),
                EmployeeId = noExcess.Employee.Id,
                OrganizationId = noExcess.Organization.Id,
                Type = OvertimeType.PreOvertime,
                Date = noExcess.CalendarDate,
                Status = OvertimeStatus.Approved,
                ApprovedHours = 2.0
            });

            var payslip = await noExcess.Compute();
            Assert.Equal(800m, payslip.BasicPay);
            Assert.Equal(0m, payslip.TotalOvertimePay);
        }

        // Case 2: Regular workday with 1h actual OT (17:00->18:00) and 3h ApprovedHours -> capped to 1h OT
        await using (var capped = new PayrollFixture())
        {
            capped.Punch(8, AttendanceType.ClockIn);
            capped.Punch(18, AttendanceType.ClockOut);
            capped.Db.OvertimeRequests.Add(new OvertimeRequest
            {
                Id = Guid.NewGuid(),
                EmployeeId = capped.Employee.Id,
                OrganizationId = capped.Organization.Id,
                Type = OvertimeType.PostOvertime,
                Date = capped.CalendarDate,
                Status = OvertimeStatus.Approved,
                ApprovedHours = 3.0
            });

            var payslip = await capped.Compute();
            Assert.Equal(800m, payslip.BasicPay);
            Assert.Equal(125m, payslip.TotalOvertimePay); // 1h * (800 / 8) * 1.25
        }
    }

    [Fact]
    public async Task Biometric_lunch_window_punches_normalize_to_1200_out_and_1300_in_and_yield_8_hours()
    {
        await using var fixture = new PayrollFixture();
        fixture.Employee.EmployeeId = "001";
        await fixture.Db.SaveChangesAsync();

        using var xlsx = BuildSingleDayBiometricXlsx("10/26/2026 ~ 10/26/2026", "001", "Test Employee", "08:00 12:05 12:55 17:00");
        var bioService = new BiometricReportService(fixture.Db);
        var preview = await bioService.PreviewBiometricReportAsync(xlsx, fixture.Organization.Id);

        Assert.True(preview.Success);
        var empLog = Assert.Single(preview.Employees);
        var day = Assert.Single(empLog.Days);
        Assert.Equal(4, day.Punches.Count);

        Assert.Equal(AttendanceType.ClockIn, day.Punches[0].Type);
        Assert.Equal("08:00", day.Punches[0].Time);
        Assert.Equal(AttendanceType.ClockOut, day.Punches[1].Type);
        Assert.Equal("12:00", day.Punches[1].Time);
        Assert.Equal(AttendanceType.ClockIn, day.Punches[2].Type);
        Assert.Equal("13:00", day.Punches[2].Time);
        Assert.Equal(AttendanceType.ClockOut, day.Punches[3].Type);
        Assert.Equal("17:00", day.Punches[3].Time);

        foreach (var punch in day.Punches)
        {
            fixture.Db.AttendanceRecords.Add(new AttendanceRecord
            {
                Id = Guid.NewGuid(),
                EmployeeId = fixture.Employee.Id,
                OrganizationId = fixture.Organization.Id,
                Timestamp = punch.Timestamp,
                Type = punch.Type
            });
        }

        var payslip = await fixture.Compute();
        Assert.Equal(8, payslip.TotalWorkedHours);
        Assert.Equal(800m, payslip.BasicPay);
    }

    private static MemoryStream BuildSingleDayBiometricXlsx(string dateRange, string bioId, string name, string punches)
    {
        var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            static void WriteEntry(ZipArchive zip, string path, string content)
            {
                var entry = zip.CreateEntry(path);
                using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
                writer.Write(content);
            }

            WriteEntry(archive, "[Content_Types].xml", """
                <?xml version="1.0" encoding="UTF-8"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                  <Default Extension="xml" ContentType="application/xml"/>
                  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
                  <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
                </Types>
                """.Trim());
            WriteEntry(archive, "_rels/.rels", """
                <?xml version="1.0" encoding="UTF-8"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
                </Relationships>
                """.Trim());
            WriteEntry(archive, "xl/workbook.xml", """
                <?xml version="1.0" encoding="UTF-8"?>
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <sheets><sheet name="Attend. Logs" sheetId="1" r:id="rId1"/></sheets>
                </workbook>
                """.Trim());
            WriteEntry(archive, "xl/_rels/workbook.xml.rels", """
                <?xml version="1.0" encoding="UTF-8"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
                </Relationships>
                """.Trim());
            WriteEntry(archive, "xl/worksheets/sheet1.xml", $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
                  <sheetData>
                    <row r="1"><c r="A1" t="inlineStr"><is><t>{dateRange}</t></is></c></row>
                    <row r="2"><c r="A2" t="inlineStr"><is><t>ID : {bioId}</t></is></c><c r="B2" t="inlineStr"><is><t>Name : {name}</t></is></c></row>
                    <row r="3"><c r="A3" t="inlineStr"><is><t>{punches}</t></is></c></row>
                  </sheetData>
                </worksheet>
                """.Trim());
        }
        ms.Position = 0;
        return ms;
    }

    private sealed class PayrollFixture : IAsyncDisposable
    {
        private readonly TimeZoneInfo _officeTime;
        public AppDbContext Db { get; }
        public Organization Organization { get; }
        public Employee Employee { get; }
        public PayrollPeriod Period { get; }
        public PayrollComputationService Service { get; }
        // Leave/overtime controllers store date-only labels as UTC midnight.
        public DateTime CalendarDate => DateTime.SpecifyKind(Period.StartDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);

        public PayrollFixture(DateOnly? date = null, TimeZoneInfo? timeZone = null)
        {
            _officeTime = timeZone ?? OfficeTime;
            Db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            Organization = new Organization { Id = Guid.NewGuid(), Name = "Test workspace" };
            var schedule = new WorkSchedule
            {
                Id = Guid.NewGuid(), Name = "Test shift", OrganizationId = Organization.Id,
                Days = Enum.GetValues<DayOfWeek>().Select(day => new WorkScheduleDay
                {
                    DayOfWeek = day, IsRestDay = day is DayOfWeek.Saturday or DayOfWeek.Sunday,
                    StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(17), OrganizationId = Organization.Id
                }).ToList(),
                Breaks = new List<WorkScheduleBreak>
                {
                    new() { Name = "Lunch", StartTime = TimeSpan.FromHours(12), EndTime = TimeSpan.FromHours(13), OrganizationId = Organization.Id }
                }
            };
            var user = new ApplicationUser { Id = Guid.NewGuid().ToString(), FirstName = "Test", LastName = "Employee" };
            Employee = new Employee { UserId = user.Id, User = user, OrganizationId = Organization.Id, DailyRate = 800m, WorkSchedule = schedule };
            Period = new PayrollPeriod { OrganizationId = Organization.Id, StartDate = date ?? new DateOnly(2026, 10, 26), EndDate = date ?? new DateOnly(2026, 10, 26) };
            Db.AddRange(Organization, Employee, Period);
            Service = new PayrollComputationService(Db, _officeTime);
        }

        public DateTimeOffset Local(int hour, int minute = 0)
            => new(Period.StartDate.ToDateTime(new TimeOnly(hour, minute)), _officeTime.BaseUtcOffset);

        public void Punch(int hour, AttendanceType type, int minute = 0, Guid? organizationId = null)
            => Db.AttendanceRecords.Add(new AttendanceRecord
            {
                Id = Guid.NewGuid(), EmployeeId = Employee.Id, OrganizationId = organizationId ?? Organization.Id,
                Timestamp = Local(hour, minute), Type = type
            });

        public async Task<Payslip> Compute()
        {
            await Db.SaveChangesAsync();
            await Service.ComputePayrollPeriodAsync(Period.Id);
            return await Db.Payslips.SingleAsync(p => p.PayrollPeriodId == Period.Id);
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}

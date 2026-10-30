using Microsoft.EntityFrameworkCore;
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

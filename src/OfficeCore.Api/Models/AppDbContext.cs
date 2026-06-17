using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace OfficeCore.Api.Models;

public class AppDbContext : IdentityDbContext<Employee, AppRole, string>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<WorkSchedule> WorkSchedules { get; set; }
    public DbSet<WorkScheduleDay> WorkScheduleDays { get; set; }
    public DbSet<WorkScheduleBreak> WorkScheduleBreaks { get; set; }
    public DbSet<WorkScheduleAutoDeduction> WorkScheduleAutoDeductions { get; set; }
    public DbSet<WorkScheduleOvertimeRule> WorkScheduleOvertimeRules { get; set; }
    public DbSet<EmployeeGovernmentRecord> EmployeeGovernmentRecords { get; set; }
    public DbSet<GovernmentDeductionSetting> GovernmentDeductionSettings { get; set; }
    public DbSet<LeaveRequest> LeaveRequests { get; set; }
    
    public DbSet<EmployeeFingerprint> EmployeeFingerprints { get; set; }
    public DbSet<AttendanceRecord> AttendanceRecords { get; set; }
    
    public DbSet<HolidayCalendar> HolidayCalendars { get; set; }
    public DbSet<Holiday> Holidays { get; set; }
    public DbSet<AdvanceRequest> AdvanceRequests { get; set; }
    public DbSet<CompanyExpense> CompanyExpenses { get; set; }
    public DbSet<Client> Clients { get; set; }
    public DbSet<Business> Businesses { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        
        // Customize the ASP.NET Identity model and override the defaults if needed.
        // For example, you can rename the ASP.NET Identity table names and more.
        // Add your customizations after calling base.OnModelCreating(builder);



        // Configure Client-Business relationship
        builder.Entity<Client>()
            .HasMany(c => c.Businesses)
            .WithOne(b => b.Client)
            .HasForeignKey(b => b.ClientId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace OfficeCore.Api.Models;

public class AppDbContext : IdentityDbContext<ApplicationUser, AppRole, string>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Organization> Organizations { get; set; }
    public DbSet<OrganizationPhone> OrganizationPhones { get; set; }
    public DbSet<OrganizationInvitation> OrganizationInvitations { get; set; }
    public DbSet<Employee> Employees { get; set; }
    public DbSet<WorkSchedule> WorkSchedules { get; set; }
    public DbSet<WorkScheduleDay> WorkScheduleDays { get; set; }
    public DbSet<WorkScheduleBreak> WorkScheduleBreaks { get; set; }
    public DbSet<WorkScheduleAutoDeduction> WorkScheduleAutoDeductions { get; set; }
    public DbSet<WorkScheduleOvertimeRule> WorkScheduleOvertimeRules { get; set; }
    public DbSet<EmployeeGovernmentRecord> EmployeeGovernmentRecords { get; set; }
    public DbSet<GovernmentDeductionSetting> GovernmentDeductionSettings { get; set; }
    public DbSet<LeaveRequest> LeaveRequests { get; set; }
    public DbSet<GeneralPayrollSetting> GeneralPayrollSettings { get; set; }
    
    public DbSet<EmployeeFingerprint> EmployeeFingerprints { get; set; }
    public DbSet<AttendanceRecord> AttendanceRecords { get; set; }
    public DbSet<FingerprintDevice> FingerprintDevices { get; set; }
    
    public DbSet<HolidayCalendar> HolidayCalendars { get; set; }
    public DbSet<Holiday> Holidays { get; set; }
    public DbSet<AdvanceRequest> AdvanceRequests { get; set; }
    public DbSet<CompanyExpense> CompanyExpenses { get; set; }
    public DbSet<Client> Clients { get; set; }
    public DbSet<Business> Businesses { get; set; }

    public DbSet<PayrollPeriod> PayrollPeriods { get; set; }
    public DbSet<Payslip> Payslips { get; set; }

    public DbSet<Board> Boards { get; set; }
    public DbSet<BoardMember> BoardMembers { get; set; }
    public DbSet<BoardList> BoardLists { get; set; }
    public DbSet<BoardCard> BoardCards { get; set; }
    public DbSet<BoardLabel> BoardLabels { get; set; }
    public DbSet<BoardCardLabel> BoardCardLabels { get; set; }
    public DbSet<BoardCardComment> BoardCardComments { get; set; }
    public DbSet<BoardCardChecklist> BoardCardChecklists { get; set; }
    public DbSet<BoardCardChecklistItem> BoardCardChecklistItems { get; set; }
    public DbSet<BoardCardAttachment> BoardCardAttachments { get; set; }

    // Chat System
    public DbSet<ChatRoom> ChatRooms { get; set; }
    public DbSet<ChatRoomMember> ChatRoomMembers { get; set; }
    public DbSet<ChatMessage> ChatMessages { get; set; }

    // Multi-Tenant RBAC
    public DbSet<OrganizationRole> OrganizationRoles { get; set; }
    public DbSet<OrganizationRoleClaim> OrganizationRoleClaims { get; set; }
    public DbSet<EmployeeRole> EmployeeRoles { get; set; }

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

        // Configure RBAC
        builder.Entity<OrganizationRole>()
            .HasMany(r => r.Claims)
            .WithOne(c => c.Role)
            .HasForeignKey(c => c.OrganizationRoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<EmployeeRole>()
            .HasKey(er => new { er.EmployeeId, er.OrganizationRoleId });

        builder.Entity<EmployeeRole>()
            .HasOne(er => er.Employee)
            .WithMany(e => e.EmployeeRoles)
            .HasForeignKey(er => er.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<EmployeeRole>()
            .HasOne(er => er.Role)
            .WithMany(r => r.EmployeeRoles)
            .HasForeignKey(er => er.OrganizationRoleId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure Boards
        builder.Entity<Board>()
            .HasMany(b => b.Lists)
            .WithOne(l => l.Board)
            .HasForeignKey(l => l.BoardId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Board>()
            .HasMany(b => b.Members)
            .WithOne(m => m.Board)
            .HasForeignKey(m => m.BoardId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<BoardCardChecklistItem>()
            .HasOne(i => i.Checklist)
            .WithMany(cl => cl.Items)
            .HasForeignKey(i => i.ChecklistId)
            .OnDelete(DeleteBehavior.Cascade);

        // Chat System Configurations
        builder.Entity<ChatRoomMember>()
            .HasOne(m => m.ChatRoom)
            .WithMany(r => r.Members)
            .HasForeignKey(m => m.ChatRoomId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ChatRoomMember>()
            .HasOne(m => m.User)
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ChatMessage>()
            .HasOne(m => m.ChatRoom)
            .WithMany(r => r.Messages)
            .HasForeignKey(m => m.ChatRoomId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ChatMessage>()
            .HasOne(m => m.Sender)
            .WithMany()
            .HasForeignKey(m => m.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<BoardList>()
            .HasMany(l => l.Cards)
            .WithOne(c => c.List)
            .HasForeignKey(c => c.ListId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure Board Labels
        builder.Entity<BoardCardLabel>()
            .HasKey(bcl => new { bcl.CardId, bcl.LabelId });

        builder.Entity<BoardCardLabel>()
            .HasOne(bcl => bcl.Card)
            .WithMany()
            .HasForeignKey(bcl => bcl.CardId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<BoardCardLabel>()
            .HasOne(bcl => bcl.Label)
            .WithMany()
            .HasForeignKey(bcl => bcl.LabelId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

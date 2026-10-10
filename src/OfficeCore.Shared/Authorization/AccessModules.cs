namespace OfficeCore.Shared.Authorization;

/// <summary>
/// Canonical catalog for the legacy module-level permissions used by OfficeCore.
/// Keep authorization identifiers stable; display names belong in <see cref="ModuleCatalog"/>.
/// </summary>
public static class AccessModules
{
    public const string Dashboard = "Dashboard";
    public const string MyAttendance = "MyAttendance";
    public const string MyOvertime = "MyOvertime";
    public const string MyLeaves = "MyLeaves";
    public const string Advances = "Advances";
    public const string Boards = "Boards";
    public const string Clients = "Clients";
    public const string Attendance = "Attendance";
    public const string Ledger = "Ledger";
    public const string Payroll = "Payroll";
    public const string Invoices = "Invoices";
    public const string Users = "Users";
    public const string Roles = "Roles";
    public const string LeaveApprovals = "LeaveApprovals";
    public const string OvertimeApprovals = "OvertimeApprovals";
    public const string WorkSchedules = "WorkSchedules";
    public const string TimeOff = "TimeOff";
    public const string Locations = "Locations";
    public const string Organization = "Organization";
    public const string AuditLogs = "AuditLogs";
}

public sealed record ModuleDefinition(string Key, string DisplayName, string Category, bool IsDefault = false);

public static class ModuleCatalog
{
    public static readonly IReadOnlyList<ModuleDefinition> All =
    [
        new(AccessModules.Dashboard, "Dashboard", "Main", true),
        new(AccessModules.MyAttendance, "My Attendance", "Main", true),
        new(AccessModules.MyOvertime, "My Overtime", "Main", true),
        new(AccessModules.MyLeaves, "My Leaves", "Main", true),
        new(AccessModules.Advances, "Advances & Loans", "Main", true),
        new(AccessModules.Boards, "Boards", "Main", true),
        new(AccessModules.Ledger, "Ledger", "Finance"),
        new(AccessModules.Payroll, "Payroll", "Finance"),
        new(AccessModules.Invoices, "Invoices", "Finance"),
        new(AccessModules.Clients, "Clients", "Administration"),
        new(AccessModules.Attendance, "Attendance", "Administration"),
        new(AccessModules.Users, "Users", "Administration"),
        new(AccessModules.Roles, "Roles", "Administration"),
        new(AccessModules.LeaveApprovals, "Leave Approvals", "Administration"),
        new(AccessModules.OvertimeApprovals, "Overtime Approvals", "Administration"),
        new(AccessModules.WorkSchedules, "Work Schedules", "Administration"),
        new(AccessModules.TimeOff, "Time Off", "Administration"),
        new(AccessModules.Locations, "Locations", "Administration"),
        new(AccessModules.Organization, "Organization", "Administration"),
        new(AccessModules.AuditLogs, "Audit Trail", "Administration")
    ];

    public static readonly IReadOnlyList<string> DefaultKeys = All.Where(m => m.IsDefault).Select(m => m.Key).ToArray();
    public static readonly IReadOnlyList<string> AllKeys = All.Select(m => m.Key).ToArray();

    private static readonly Dictionary<string, string> CanonicalKeys =
        All.ToDictionary(m => m.Key, m => m.Key, StringComparer.OrdinalIgnoreCase);

    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = string.Empty;
        return !string.IsNullOrWhiteSpace(value)
            && CanonicalKeys.TryGetValue(value.Trim(), out normalized!);
    }

    public static bool TryNormalizeMany(IEnumerable<string>? values, out IReadOnlyList<string> normalized, out IReadOnlyList<string> invalid)
    {
        var valid = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rejected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var value in values ?? [])
        {
            if (TryNormalize(value, out var canonical)) valid.Add(canonical);
            else rejected.Add(value?.Trim() ?? string.Empty);
        }

        normalized = valid.OrderBy(v => v, StringComparer.Ordinal).ToArray();
        invalid = rejected.OrderBy(v => v, StringComparer.Ordinal).ToArray();
        return invalid.Count == 0;
    }
}

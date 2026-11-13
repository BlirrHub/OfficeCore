namespace OfficeCore.Shared.Authorization;

public static class AccessPermissions
{
    public const string ClientsView = "clients.view";
    public const string ClientsManage = "clients.manage";
    public const string AttendanceView = "attendance.view";
    public const string AttendanceAdjust = "attendance.adjust";
    public const string LedgerView = "ledger.view";
    public const string LedgerManage = "ledger.manage";
    public const string PayrollView = "payroll.view";
    public const string PayrollCalculate = "payroll.calculate";
    public const string PayrollFinalize = "payroll.finalize";
    public const string PayrollExport = "payroll.export";
    public const string PayrollSettingsManage = "payroll.settings.manage";
    public const string InvoicesView = "invoices.view";
    public const string InvoicesManage = "invoices.manage";
    public const string UsersView = "users.view";
    public const string UsersManage = "users.manage";
    public const string EmployeeRolesAssign = "employees.roles.assign";
    public const string RolesView = "roles.view";
    public const string RolesCreate = "roles.create";
    public const string RolesEdit = "roles.edit";
    public const string RolesDelete = "roles.delete";
    public const string LeaveApprovalsView = "leave-approvals.view";
    public const string LeaveApprovalsManage = "leave-approvals.manage";
    public const string OvertimeApprovalsView = "overtime-approvals.view";
    public const string OvertimeApprovalsManage = "overtime-approvals.manage";
    public const string WorkSchedulesView = "work-schedules.view";
    public const string WorkSchedulesManage = "work-schedules.manage";
    public const string TimeOffView = "time-off.view";
    public const string TimeOffManage = "time-off.manage";
    public const string LocationsView = "locations.view";
    public const string LocationsManage = "locations.manage";
    public const string OrganizationView = "organization.view";
    public const string OrganizationManage = "organization.manage";
    public const string AuditView = "audit.view";
    public const string AuditExport = "audit.export";
    public const string BoardsView = "boards.view";
    public const string BoardsManageMembers = "boards.members.manage";
}

public sealed record PermissionDefinition(string Key, string DisplayName, string Module);

public static class PermissionCatalog
{
    public const string ClaimType = "Permission";

    public static readonly IReadOnlyList<PermissionDefinition> All =
    [
        new(AccessPermissions.ClientsView, "View clients", AccessModules.Clients),
        new(AccessPermissions.ClientsManage, "Create and edit clients", AccessModules.Clients),
        new(AccessPermissions.AttendanceView, "View attendance", AccessModules.Attendance),
        new(AccessPermissions.AttendanceAdjust, "Adjust attendance", AccessModules.Attendance),
        new(AccessPermissions.LedgerView, "View ledger", AccessModules.Ledger),
        new(AccessPermissions.LedgerManage, "Manage ledger", AccessModules.Ledger),
        new(AccessPermissions.PayrollView, "View payroll", AccessModules.Payroll),
        new(AccessPermissions.PayrollCalculate, "Calculate payroll", AccessModules.Payroll),
        new(AccessPermissions.PayrollFinalize, "Finalize payroll", AccessModules.Payroll),
        new(AccessPermissions.PayrollExport, "Export payroll", AccessModules.Payroll),
        new(AccessPermissions.PayrollSettingsManage, "Manage payroll settings", AccessModules.Payroll),
        new(AccessPermissions.InvoicesView, "View invoices", AccessModules.Invoices),
        new(AccessPermissions.InvoicesManage, "Manage invoices", AccessModules.Invoices),
        new(AccessPermissions.UsersView, "View employees", AccessModules.Users),
        new(AccessPermissions.UsersManage, "Manage employees", AccessModules.Users),
        new(AccessPermissions.EmployeeRolesAssign, "Assign employee roles", AccessModules.Users),
        new(AccessPermissions.RolesView, "View roles", AccessModules.Roles),
        new(AccessPermissions.RolesCreate, "Create roles", AccessModules.Roles),
        new(AccessPermissions.RolesEdit, "Edit role permissions", AccessModules.Roles),
        new(AccessPermissions.RolesDelete, "Delete roles", AccessModules.Roles),
        new(AccessPermissions.LeaveApprovalsView, "View leave requests", AccessModules.LeaveApprovals),
        new(AccessPermissions.LeaveApprovalsManage, "Approve or reject leave", AccessModules.LeaveApprovals),
        new(AccessPermissions.OvertimeApprovalsView, "View overtime requests", AccessModules.OvertimeApprovals),
        new(AccessPermissions.OvertimeApprovalsManage, "Approve or reject overtime", AccessModules.OvertimeApprovals),
        new(AccessPermissions.WorkSchedulesView, "View work schedules", AccessModules.WorkSchedules),
        new(AccessPermissions.WorkSchedulesManage, "Manage work schedules", AccessModules.WorkSchedules),
        new(AccessPermissions.TimeOffView, "View time-off settings", AccessModules.TimeOff),
        new(AccessPermissions.TimeOffManage, "Manage time-off settings", AccessModules.TimeOff),
        new(AccessPermissions.LocationsView, "View locations", AccessModules.Locations),
        new(AccessPermissions.LocationsManage, "Manage locations", AccessModules.Locations),
        new(AccessPermissions.OrganizationView, "View organization", AccessModules.Organization),
        new(AccessPermissions.OrganizationManage, "Manage organization", AccessModules.Organization),
        new(AccessPermissions.AuditView, "View audit trail", AccessModules.AuditLogs),
        new(AccessPermissions.AuditExport, "Export audit trail", AccessModules.AuditLogs),
        new(AccessPermissions.BoardsView, "View boards", AccessModules.Boards),
        new(AccessPermissions.BoardsManageMembers, "Manage board members", AccessModules.Boards)
    ];

    public static readonly IReadOnlyList<string> AllKeys = All.Select(p => p.Key).ToArray();
    private static readonly Dictionary<string, string> CanonicalKeys =
        All.ToDictionary(p => p.Key, p => p.Key, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<PermissionDefinition> ForModule(string module) =>
        All.Where(p => string.Equals(p.Module, module, StringComparison.OrdinalIgnoreCase)).ToArray();

    public static IReadOnlyList<string> KeysForModule(string module) =>
        All.Where(p => string.Equals(p.Module, module, StringComparison.OrdinalIgnoreCase))
           .Select(p => p.Key)
           .ToArray();

    public static IReadOnlyList<string> DeriveModules(IEnumerable<string>? permissions)
    {
        var permSet = (permissions ?? [])
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (permSet.Count == 0)
            return [];

        return All
            .Where(p => permSet.Contains(p.Key))
            .Select(p => p.Module)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(m => m, StringComparer.Ordinal)
            .ToArray();
    }

    public static IReadOnlyList<string> ExpandModules(IEnumerable<string> modules)
    {
        var moduleSet = modules.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var permissions = All
            .Where(p => moduleSet.Contains(p.Module) && p.Key != AccessPermissions.BoardsManageMembers)
            .Select(p => p.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Preserve the previous board administration behavior for legacy roles:
        // Users or Roles module access allowed managing board membership.
        if (moduleSet.Contains(AccessModules.Users) || moduleSet.Contains(AccessModules.Roles))
            permissions.Add(AccessPermissions.BoardsManageMembers);

        return permissions.OrderBy(p => p, StringComparer.Ordinal).ToArray();
    }

    public static bool TryNormalizeMany(IEnumerable<string>? values, out IReadOnlyList<string> normalized, out IReadOnlyList<string> invalid)
    {
        var valid = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rejected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values ?? [])
        {
            if (!string.IsNullOrWhiteSpace(value) && CanonicalKeys.TryGetValue(value.Trim(), out var canonical)) valid.Add(canonical);
            else rejected.Add(value?.Trim() ?? string.Empty);
        }
        normalized = valid.OrderBy(v => v, StringComparer.Ordinal).ToArray();
        invalid = rejected.OrderBy(v => v, StringComparer.Ordinal).ToArray();
        return invalid.Count == 0;
    }
}

public static class PermissionPolicies
{
    public static string For(string permission) => $"Permission:{permission}";
}

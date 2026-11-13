namespace OfficeCore.Shared.DTOs;

public sealed record LeaveTypeInfo(
    int Value,
    string Key,
    string Label,
    string DotClass,
    string BadgeClass,
    bool SupportsAttachment,
    bool RequiresAttachment);

public static class LeaveTypeCatalog
{
    public const int Sick = 0;
    public const int Vacation = 1;
    public const int Emergency = 2;
    public const int Maternity = 3;
    public const int Other = 4;

    public static IReadOnlyList<LeaveTypeInfo> All { get; } =
    [
        new(
            Sick,
            "Sick",
            "Sick Leave",
            "bg-red-400",
            "bg-red-500/10 text-red-400 border border-red-500/20",
            SupportsAttachment: true,
            RequiresAttachment: true),
        new(
            Vacation,
            "Vacation",
            "Vacation Leave",
            "bg-sky-400",
            "bg-sky-500/10 text-sky-400 border border-sky-500/20",
            SupportsAttachment: false,
            RequiresAttachment: false),
        new(
            Emergency,
            "Emergency",
            "Emergency Leave",
            "bg-orange-400",
            "bg-orange-500/10 text-orange-400 border border-orange-500/20",
            SupportsAttachment: true,
            RequiresAttachment: false),
        new(
            Maternity,
            "Maternity",
            "Maternity Leave",
            "bg-fuchsia-400",
            "bg-fuchsia-500/10 text-fuchsia-400 border border-fuchsia-500/20",
            SupportsAttachment: true,
            RequiresAttachment: false),
        new(
            Other,
            "Other",
            "Other",
            "bg-zinc-400",
            "bg-zinc-500/10 text-zinc-300 border border-zinc-500/20",
            SupportsAttachment: true,
            RequiresAttachment: false)
    ];

    public static LeaveTypeInfo Get(int value)
    {
        for (var i = 0; i < All.Count; i++)
        {
            if (All[i].Value == value)
            {
                return All[i];
            }
        }

        return All[^1];
    }

    public static bool IsDefined(int value)
    {
        for (var i = 0; i < All.Count; i++)
        {
            if (All[i].Value == value)
            {
                return true;
            }
        }

        return false;
    }
}

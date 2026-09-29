using System;
using System.Collections.Generic;

namespace OfficeCore.Web.Models;

public class MyAttendanceResponse
{
    public bool IsClockedIn { get; set; }
    public DateTimeOffset? LastPunchTime { get; set; }
    public double TodayTotalHours { get; set; }
    public string ScheduledShiftText { get; set; } = "Flexible Schedule";
    public TimeSpan? ScheduledStartTime { get; set; }
    public TimeSpan? ScheduledEndTime { get; set; }
    public int DaysPresent { get; set; }
    public double MonthTotalHours { get; set; }
    public List<MyAttendanceRecordDto> Records { get; set; } = new();
}

public class MyAttendanceRecordDto
{
    public Guid Id { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
}

public class PunchResult
{
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
}

public class AttendanceRecordDto
{
    public Guid Id { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public bool IncludeTimeTrackedBeforeScheduledStartTime { get; set; }
    public TimeSpan? ScheduledStartTime { get; set; }
}

public class AdminCreateAttendanceDto
{
    public string EmployeeId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public AttendanceType Type { get; set; }
}

public class EmployeeDto
{
    public string Id { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public enum AttendanceType
{
    ClockIn,
    ClockOut
}

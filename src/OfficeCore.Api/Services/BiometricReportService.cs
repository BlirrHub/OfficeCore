using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ExcelDataReader;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Models;

namespace OfficeCore.Api.Services;

public class BiometricPunchDto
{
    public string Time { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
    public AttendanceType Type { get; set; }
    public bool IsDuplicate { get; set; }
    public bool IsSelected { get; set; } = true;
}

public class BiometricDayRecordDto
{
    public DateTime Date { get; set; }
    public int DayNumber { get; set; }
    public string DayOfWeek { get; set; } = string.Empty;
    public string RawPunchText { get; set; } = string.Empty;
    public List<BiometricPunchDto> Punches { get; set; } = new();
    public string Status { get; set; } = "Normal";
    public bool HasMissingPair { get; set; }
}

public class BiometricEmployeeLogDto
{
    public string BiometricId { get; set; } = string.Empty;
    public string ReportedName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string? MatchedEmployeeId { get; set; }
    public string? MatchedEmployeeName { get; set; }
    public bool IsMatched => !string.IsNullOrEmpty(MatchedEmployeeId);
    public List<BiometricDayRecordDto> Days { get; set; } = new();
    public int TotalPunches => Days.Sum(d => d.Punches.Count);
    public int MissingPunchCount => Days.Count(d => d.HasMissingPair);
}

public class BiometricPreviewResultDto
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string ReportTitle { get; set; } = "Biometric Attendance Report";
    public string DateRangeText { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int TotalEmployeesFound { get; set; }
    public int TotalMatchedEmployees { get; set; }
    public int TotalPunchesFound { get; set; }
    public int TotalMissingPunchDays { get; set; }
    public List<BiometricEmployeeLogDto> Employees { get; set; } = new();
    public List<EmployeeOptionDto> AvailableEmployees { get; set; } = new();
}

public class EmployeeOptionDto
{
    public string Id { get; set; } = string.Empty;
    public string? EmployeeId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Department { get; set; }
}

public class BiometricCommitPunchItemDto
{
    public string EmployeeId { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
    public AttendanceType Type { get; set; }
    public string? BiometricId { get; set; }
}

public class BiometricCommitRequestDto
{
    public List<BiometricCommitPunchItemDto> Punches { get; set; } = new();
    public Dictionary<string, string> BiometricIdToEmployeeMappings { get; set; } = new();
    public bool AutoUpdateEmployeeIds { get; set; } = true;
}

public class BiometricReportService
{
    private readonly AppDbContext _context;

    public BiometricReportService(AppDbContext context)
    {
        _context = context;
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
    }

    public async Task<BiometricPreviewResultDto> PreviewBiometricReportAsync(Stream fileStream, Guid organizationId)
    {
        var result = new BiometricPreviewResultDto();

        try
        {
            using var reader = ExcelReaderFactory.CreateReader(fileStream);
            var dataSet = reader.AsDataSet();

            if (dataSet.Tables.Count == 0)
            {
                result.Success = false;
                result.ErrorMessage = "The uploaded file does not contain any readable sheets or tables.";
                return result;
            }

            // 1. Locate the Attend. Logs table
            DataTable? table = null;
            foreach (DataTable t in dataSet.Tables)
            {
                if (string.Equals(t.TableName.Trim(), "Attend. Logs", StringComparison.OrdinalIgnoreCase))
                {
                    table = t;
                    break;
                }
            }

            // Fallback: look for table with Attend. Logs in the first cell
            if (table == null)
            {
                foreach (DataTable t in dataSet.Tables)
                {
                    if (t.Rows.Count > 0 && t.Rows[0][0]?.ToString()?.Contains("Attend. Logs", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        table = t;
                        break;
                    }
                }
            }

            if (table == null)
            {
                // Fallback to table at index 1 or 0
                table = dataSet.Tables.Count > 1 ? dataSet.Tables[1] : dataSet.Tables[0];
            }

            // 2. Extract Date Range (e.g. "09/21/2026 ~ 09/25/2026")
            DateTime? startDate = null;
            DateTime? endDate = null;
            string dateRangeStr = string.Empty;

            for (int r = 0; r < Math.Min(6, table.Rows.Count); r++)
            {
                for (int c = 0; c < table.Columns.Count; c++)
                {
                    var val = table.Rows[r][c]?.ToString() ?? string.Empty;
                    var match = Regex.Match(val, @"(\d{1,2}/\d{1,2}/\d{4})\s*~\s*(\d{1,2}/\d{1,2}/\d{4})");
                    if (match.Success)
                    {
                        if (DateTime.TryParseExact(match.Groups[1].Value, "MM/dd/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var s) &&
                            DateTime.TryParseExact(match.Groups[2].Value, "MM/dd/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var e))
                        {
                            startDate = s;
                            endDate = e;
                            dateRangeStr = $"{match.Groups[1].Value} ~ {match.Groups[2].Value}";
                            break;
                        }
                    }
                }
                if (startDate.HasValue) break;
            }

            if (!startDate.HasValue || !endDate.HasValue)
            {
                startDate = DateTime.UtcNow.Date.AddDays(-6);
                endDate = DateTime.UtcNow.Date;
                dateRangeStr = $"{startDate.Value:MM/dd/yyyy} ~ {endDate.Value:MM/dd/yyyy} (Estimated)";
            }

            result.StartDate = startDate;
            result.EndDate = endDate;
            result.DateRangeText = dateRangeStr;

            // Compute the day count and day mapping
            var totalDays = Math.Max(1, (int)(endDate.Value - startDate.Value).TotalDays + 1);
            var dateColumns = new List<DateTime>();
            for (int d = 0; d < totalDays; d++)
            {
                dateColumns.Add(startDate.Value.AddDays(d));
            }

            // 3. Fetch Organization Employees for matching
            var orgEmployees = await _context.Employees
                .Include(e => e.User)
                .Where(e => e.OrganizationId == organizationId && e.IsActive)
                .ToListAsync();

            result.AvailableEmployees = orgEmployees.Select(e => new EmployeeOptionDto
            {
                Id = e.Id,
                EmployeeId = e.EmployeeId,
                FullName = $"{e.User?.FirstName} {e.User?.LastName}".Trim(),
                Department = "Staff"
            }).OrderBy(e => e.FullName).ToList();

            // Fetch existing attendance records in this date window to flag duplicates
            var startUtc = new DateTimeOffset(startDate.Value, TimeSpan.Zero);
            var endUtc = new DateTimeOffset(endDate.Value.AddDays(1), TimeSpan.Zero);
            var existingPunches = await _context.AttendanceRecords
                .Where(a => a.OrganizationId == organizationId && a.Timestamp >= startUtc && a.Timestamp < endUtc)
                .Select(a => new { a.EmployeeId, a.Timestamp, a.Type })
                .ToListAsync();

            // 4. Scan rows for Employee blocks (ID :, Name :, Punches)
            var employeesList = new List<BiometricEmployeeLogDto>();

            for (int r = 0; r < table.Rows.Count; r++)
            {
                var row = table.Rows[r];
                var rowFirstCell = row[0]?.ToString()?.Trim() ?? string.Empty;

                int idColIndex = -1;
                // Look for "ID :" pattern
                if (rowFirstCell.StartsWith("ID", StringComparison.OrdinalIgnoreCase) || 
                    ContainsIdLabel(row, out idColIndex))
                {
                    var bioId = ExtractCellString(row, idColIndex >= 0 ? idColIndex : 0, "ID :");
                    var bioName = ExtractNamedField(row, "Name :");
                    var bioDept = ExtractNamedField(row, "Dept. :");

                    if (string.IsNullOrWhiteSpace(bioId)) continue;

                    var empLog = new BiometricEmployeeLogDto
                    {
                        BiometricId = bioId,
                        ReportedName = bioName,
                        Department = bioDept
                    };

                    // Attempt Employee Matching:
                    // 1st: Exact Employee.EmployeeId match
                    var matchedEmp = orgEmployees.FirstOrDefault(e => 
                        !string.IsNullOrEmpty(e.EmployeeId) && 
                        string.Equals(e.EmployeeId.Trim(), bioId.Trim(), StringComparison.OrdinalIgnoreCase));

                    // 2nd: Name match if not matched by ID
                    if (matchedEmp == null && !string.IsNullOrWhiteSpace(bioName))
                    {
                        matchedEmp = orgEmployees.FirstOrDefault(e =>
                            e.User != null && (
                                string.Equals(e.User.FirstName?.Trim(), bioName.Trim(), StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(e.User.LastName?.Trim(), bioName.Trim(), StringComparison.OrdinalIgnoreCase) ||
                                bioName.Contains(e.User.FirstName?.Trim() ?? "---", StringComparison.OrdinalIgnoreCase)
                            ));
                    }

                    if (matchedEmp != null)
                    {
                        empLog.MatchedEmployeeId = matchedEmp.Id;
                        empLog.MatchedEmployeeName = $"{matchedEmp.User?.FirstName} {matchedEmp.User?.LastName}".Trim();
                    }

                    // Punch row is the immediately following row: r + 1
                    if (r + 1 < table.Rows.Count)
                    {
                        var punchRow = table.Rows[r + 1];
                        for (int col = 0; col < dateColumns.Count && col < table.Columns.Count; col++)
                        {
                            var targetDate = dateColumns[col];
                            var cellVal = punchRow[col]?.ToString() ?? string.Empty;

                            var dayRecord = new BiometricDayRecordDto
                            {
                                Date = targetDate,
                                DayNumber = targetDate.Day,
                                DayOfWeek = targetDate.ToString("ddd"),
                                RawPunchText = cellVal.Trim()
                            };

                            if (!string.IsNullOrWhiteSpace(cellVal))
                            {
                                // Extract all HH:mm punch times
                                var timeMatches = Regex.Matches(cellVal, @"\b([01]?\d|2[0-3]):[0-5]\d\b");
                                var sortedTimes = timeMatches
                                    .Select(m => m.Value)
                                    .Distinct()
                                    .OrderBy(t => TimeSpan.Parse(t))
                                    .ToList();

                                for (int i = 0; i < sortedTimes.Count; i++)
                                {
                                    var timeStr = sortedTimes[i];
                                    var timeSpan = TimeSpan.Parse(timeStr);
                                    var punchDateTime = targetDate.Add(timeSpan);
                                    var punchTimestamp = new DateTimeOffset(punchDateTime, TimeSpan.FromHours(8)); // Philippine UTC+8 standard

                                    // Pair determination: 1st is ClockIn, 2nd is ClockOut, 3rd is ClockIn, 4th is ClockOut
                                    var punchType = (i % 2 == 0) ? AttendanceType.ClockIn : AttendanceType.ClockOut;

                                    // Duplicate detection
                                    var isDup = matchedEmp != null && existingPunches.Any(ep =>
                                        ep.EmployeeId == matchedEmp.Id &&
                                        ep.Type == punchType &&
                                        Math.Abs((ep.Timestamp - punchTimestamp).TotalMinutes) < 2);

                                    dayRecord.Punches.Add(new BiometricPunchDto
                                    {
                                        Time = timeStr,
                                        Timestamp = punchTimestamp,
                                        Type = punchType,
                                        IsDuplicate = isDup,
                                        IsSelected = !isDup
                                    });
                                }

                                if (dayRecord.Punches.Count > 0)
                                {
                                    if (dayRecord.Punches.Count % 2 != 0)
                                    {
                                        dayRecord.HasMissingPair = true;
                                        dayRecord.Status = "Missing Time Out";
                                    }
                                    else
                                    {
                                        dayRecord.Status = "Complete Pair";
                                    }
                                }
                                else
                                {
                                    dayRecord.Status = "No Punches";
                                }
                            }
                            else
                            {
                                dayRecord.Status = "No Punches";
                            }

                            empLog.Days.Add(dayRecord);
                        }
                    }

                    employeesList.Add(empLog);
                }
            }

            result.Employees = employeesList;
            result.TotalEmployeesFound = employeesList.Count;
            result.TotalMatchedEmployees = employeesList.Count(e => e.IsMatched);
            result.TotalPunchesFound = employeesList.Sum(e => e.TotalPunches);
            result.TotalMissingPunchDays = employeesList.Sum(e => e.MissingPunchCount);
            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Failed to parse biometric report: {ex.Message}";
        }

        return result;
    }

    public async Task<(int insertedCount, int skippedDuplicates, int updatedEmployeeMappings)> CommitBiometricAttendanceAsync(
        BiometricCommitRequestDto request, 
        Guid organizationId)
    {
        int inserted = 0;
        int duplicates = 0;
        int updatedMappings = 0;

        // 1. Optionally update Employee.EmployeeId mapping
        if (request.AutoUpdateEmployeeIds && request.BiometricIdToEmployeeMappings?.Count > 0)
        {
            foreach (var kvp in request.BiometricIdToEmployeeMappings)
            {
                var bioId = kvp.Key;
                var empId = kvp.Value;
                if (!string.IsNullOrWhiteSpace(bioId) && !string.IsNullOrWhiteSpace(empId))
                {
                    var emp = await _context.Employees
                        .FirstOrDefaultAsync(e => e.Id == empId && e.OrganizationId == organizationId);
                    if (emp != null && emp.EmployeeId != bioId)
                    {
                        emp.EmployeeId = bioId;
                        updatedMappings++;
                    }
                }
            }
            if (updatedMappings > 0)
            {
                await _context.SaveChangesAsync();
            }
        }

        // 2. Insert Punches
        if (request.Punches?.Count > 0)
        {
            // Fetch existing to avoid race-condition duplicate inserts
            var minDate = request.Punches.Min(p => p.Timestamp).AddMinutes(-5);
            var maxDate = request.Punches.Max(p => p.Timestamp).AddMinutes(5);

            var existingPunches = await _context.AttendanceRecords
                .Where(a => a.OrganizationId == organizationId && a.Timestamp >= minDate && a.Timestamp <= maxDate)
                .Select(a => new { a.EmployeeId, a.Timestamp, a.Type })
                .ToListAsync();

            var recordsToAdd = new List<AttendanceRecord>();

            foreach (var item in request.Punches)
            {
                var isDup = existingPunches.Any(ep =>
                    ep.EmployeeId == item.EmployeeId &&
                    ep.Type == item.Type &&
                    Math.Abs((ep.Timestamp - item.Timestamp).TotalMinutes) < 2);

                if (isDup)
                {
                    duplicates++;
                    continue;
                }

                recordsToAdd.Add(new AttendanceRecord
                {
                    Id = Guid.NewGuid(),
                    EmployeeId = item.EmployeeId,
                    Timestamp = item.Timestamp.ToUniversalTime(),
                    Type = item.Type,
                    Source = AttendanceSource.Device,
                    OrganizationId = organizationId
                });
                inserted++;
            }

            if (recordsToAdd.Count > 0)
            {
                await _context.AttendanceRecords.AddRangeAsync(recordsToAdd);
                await _context.SaveChangesAsync();
            }
        }

        return (inserted, duplicates, updatedMappings);
    }

    private static bool ContainsIdLabel(DataRow row, out int colIndex)
    {
        colIndex = -1;
        for (int c = 0; c < row.Table.Columns.Count; c++)
        {
            var text = row[c]?.ToString()?.Trim() ?? string.Empty;
            if (text.Equals("ID :", StringComparison.OrdinalIgnoreCase) || text.Equals("ID:", StringComparison.OrdinalIgnoreCase))
            {
                colIndex = c;
                return true;
            }
        }
        return false;
    }

    private static string ExtractCellString(DataRow row, int startCol, string label)
    {
        for (int c = startCol; c < row.Table.Columns.Count; c++)
        {
            var text = row[c]?.ToString()?.Trim() ?? string.Empty;
            if (text.StartsWith(label, StringComparison.OrdinalIgnoreCase))
            {
                var remainder = text.Substring(label.Length).Trim();
                if (!string.IsNullOrEmpty(remainder)) return remainder;
                if (c + 1 < row.Table.Columns.Count)
                {
                    for (int next = c + 1; next < row.Table.Columns.Count; next++)
                    {
                        var nVal = row[next]?.ToString()?.Trim() ?? string.Empty;
                        if (!string.IsNullOrEmpty(nVal)) return nVal;
                    }
                }
            }
            else if (text.Equals("ID :", StringComparison.OrdinalIgnoreCase) || text.Equals("ID:", StringComparison.OrdinalIgnoreCase))
            {
                for (int next = c + 1; next < row.Table.Columns.Count; next++)
                {
                    var nVal = row[next]?.ToString()?.Trim() ?? string.Empty;
                    if (!string.IsNullOrEmpty(nVal)) return nVal;
                }
            }
        }
        return string.Empty;
    }

    private static string ExtractNamedField(DataRow row, string fieldLabel)
    {
        for (int c = 0; c < row.Table.Columns.Count; c++)
        {
            var text = row[c]?.ToString()?.Trim() ?? string.Empty;
            if (text.StartsWith(fieldLabel, StringComparison.OrdinalIgnoreCase))
            {
                var remainder = text.Substring(fieldLabel.Length).Trim();
                if (!string.IsNullOrEmpty(remainder)) return remainder;
                for (int next = c + 1; next < row.Table.Columns.Count; next++)
                {
                    var nVal = row[next]?.ToString()?.Trim() ?? string.Empty;
                    if (!string.IsNullOrEmpty(nVal)) return nVal;
                }
            }
        }
        return string.Empty;
    }
}

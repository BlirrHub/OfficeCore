using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Models;

namespace OfficeCore.Api.Services;

public class PayrollComputationService : IPayrollComputationService
{
    private readonly AppDbContext _context;

    private readonly TimeZoneInfo _timeZone;

    public PayrollComputationService(AppDbContext context, TimeZoneInfo? timeZone = null)
    {
        _context = context;
        _timeZone = timeZone ?? TimeZoneInfo.Local;
    }

    public async Task ComputePayrollPeriodAsync(Guid payrollPeriodId)
    {
        var period = await _context.PayrollPeriods.FindAsync(payrollPeriodId);
        if (period == null) throw new Exception("Payroll period not found");

        if (period.Status == PayrollStatus.Finalized || period.Status == PayrollStatus.Paid)
        {
            throw new Exception("Cannot recompute a finalized or paid payroll period");
        }

        var org = await _context.Organizations.FindAsync(period.OrganizationId);
        var createdByUserId = org?.CreatedByUserId;

        var employees = await _context.Employees
            .Include(e => e.User)
            .Include(e => e.GovernmentRecord)
            .Include(e => e.EmployeeRoles)
                .ThenInclude(er => er.Role)
            .Where(e => e.IsActive && e.IsEmployee && e.OrganizationId == period.OrganizationId)
            .Include(e => e.WorkSchedule)
                .ThenInclude(ws => ws!.Days)
            .Include(e => e.WorkSchedule)
                .ThenInclude(ws => ws!.Breaks)
            .ToListAsync();

        var generalSetting = await _context.GeneralPayrollSettings.FirstOrDefaultAsync(s => s.Key == "MinimumDailyRate" && s.OrganizationId == period.OrganizationId);
        var defaultRate = generalSetting?.Value ?? 610m;

        var govDeductions = await _context.GovernmentDeductionSettings
            .Where(s => s.OrganizationId == period.OrganizationId)
            .ToListAsync();

        var timeOffPolicies = await _context.TimeOffPolicies
            .Where(p => p.OrganizationId == period.OrganizationId && p.IsActive)
            .ToListAsync();

        decimal monthlySss = govDeductions.FirstOrDefault(d => d.AgencyName == "SSS")?.EmployeeShare ?? 0m;
        decimal monthlyPhilHealth = govDeductions.FirstOrDefault(d => d.AgencyName == "PhilHealth")?.EmployeeShare ?? 0m;
        decimal monthlyPagIbig = govDeductions.FirstOrDefault(d => d.AgencyName == "Pag-IBIG")?.EmployeeShare ?? 0m;

        var existingPayslips = await _context.Payslips.Where(p => p.PayrollPeriodId == payrollPeriodId).ToListAsync();
        _context.Payslips.RemoveRange(existingPayslips);

        var payslips = new List<Payslip>();

        // Convert local start of day (00:00:00) and local end of day (23:59:59.999) to UTC so that early morning punches in positive UTC offsets (e.g., UTC+8) are not clipped
        var localStart = period.StartDate.ToDateTime(TimeOnly.MinValue);
        var localEnd = period.EndDate.ToDateTime(TimeOnly.MaxValue);
        var periodStartDt = new DateTimeOffset(localStart, _timeZone.GetUtcOffset(localStart)).UtcDateTime;
        var periodEndDt = new DateTimeOffset(localEnd, _timeZone.GetUtcOffset(localEnd)).UtcDateTime;
        // Leave and overtime dates are calendar labels stored at UTC midnight by their controllers.
        // They must not be clipped against the UTC instants used for attendance timestamps.
        var calendarStart = DateTime.SpecifyKind(localStart, DateTimeKind.Utc);
        var calendarEnd = DateTime.SpecifyKind(localEnd, DateTimeKind.Utc);
        
        // Use the explicitly defined DeductionMultiplier for government deductions
        decimal proportionMultiplier = period.DeductionMultiplier;

        var allAttendance = await _context.AttendanceRecords
            .Where(a => a.OrganizationId == period.OrganizationId && a.Timestamp >= periodStartDt && a.Timestamp <= periodEndDt)
            .OrderBy(a => a.Timestamp)
            .ToListAsync();

        var approvedLeaves = await _context.LeaveRequests
            .Where(lr => lr.OrganizationId == period.OrganizationId && lr.Status == LeaveRequestStatus.Approved && lr.StartDate <= calendarEnd && lr.EndDate >= calendarStart)
            .ToListAsync();

        var approvedOvertime = await _context.OvertimeRequests
            .Where(o => o.OrganizationId == period.OrganizationId 
                     && o.Status == OvertimeStatus.Approved 
                     && o.Date >= calendarStart && o.Date <= calendarEnd)
            .ToListAsync();

        var allActiveAdvances = await _context.AdvanceRequests
            .Where(ar => ar.OrganizationId == period.OrganizationId && ar.Status == AdvanceStatus.Approved && (ar.RemainingBalance == null || ar.RemainingBalance > 0))
            .ToListAsync();

        foreach (var emp in employees)
        {
            var trackedDailyRate = emp.DailyRate ?? defaultRate;
            var hourlyRate = trackedDailyRate / 8m; // Standard assumption of 8 working hours per day for hourly computation

            // 1. Calculate Tracked Hours & Overtime
            var empAttendance = allAttendance.Where(a => a.EmployeeId == emp.Id).ToList();
            var empOvertime = approvedOvertime.Where(o => o.EmployeeId == emp.Id).ToList();
            var creditedOtIds = new HashSet<Guid>();
            var attendedDates = new HashSet<DateTime>();
            double totalNetHours = 0;
            double totalOvertimeHours = 0;
            int daysWithAttendance = 0;
            decimal basicPay = 0;

            if (emp.WorkSchedule == null)
            {
                // No schedule: Fixed rate employee for the cutoff period (e.g. Adrian 3775, Blaire 1000, Lina 2500)
                basicPay = trackedDailyRate;
                foreach (var dt in empAttendance.Select(a => TimeZoneInfo.ConvertTime(a.Timestamp, _timeZone).Date).Distinct())
                {
                    attendedDates.Add(dt);
                }
                daysWithAttendance = attendedDates.Count;
                totalNetHours = daysWithAttendance > 0 ? daysWithAttendance * 8 : 40;
            }
            else if (emp.WorkSchedule.WorkArrangement == WorkArrangement.Fixed && emp.WorkSchedule.IsFixedWeeklyRate)
            {
                for (var date = localStart.Date; date <= localEnd.Date; date = date.AddDays(1))
                {
                    bool isRestDay = false;
                    var schedDay = emp.WorkSchedule.Days.FirstOrDefault(d => d.DayOfWeek == date.DayOfWeek);
                    if (schedDay != null && schedDay.IsRestDay) isRestDay = true;
                    else if (schedDay == null && (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)) isRestDay = true;
                    
                    if (!isRestDay)
                    {
                        daysWithAttendance++;
                        attendedDates.Add(date);
                        totalNetHours += 8; // Calculate standard 8 hours to yield full daily rate
                    }
                }
                basicPay = (decimal)totalNetHours * hourlyRate;
            }
            else
            {
                // Group by day and pair consecutive (ClockIn, ClockOut) intervals
                var attendanceByDay = empAttendance.GroupBy(a => TimeZoneInfo.ConvertTime(a.Timestamp, _timeZone).Date);

                foreach (var dayGroup in attendanceByDay)
                {
                    var records = dayGroup.OrderBy(a => a.Timestamp).ToList();
                    var pairs = new List<(AttendanceRecord In, AttendanceRecord Out)>();
                    AttendanceRecord? openIn = null;

                    foreach (var rec in records)
                    {
                        if (rec.Type == AttendanceType.ClockIn)
                        {
                            // Keep earliest ClockIn if duplicate ClockIns occur before a ClockOut
                            openIn ??= rec;
                        }
                        else if (rec.Type == AttendanceType.ClockOut && openIn != null && rec.Timestamp > openIn.Timestamp)
                        {
                            pairs.Add((openIn, rec));
                            openIn = null;
                        }
                    }

                    if (pairs.Count > 0)
                    {
                        var shiftDate = dayGroup.Key;
                        bool isRestDay = false;
                        WorkScheduleDay? schedDay = null;
                        if (emp.WorkSchedule?.Days != null)
                        {
                            schedDay = emp.WorkSchedule.Days.FirstOrDefault(d => d.DayOfWeek == shiftDate.DayOfWeek);
                            if (schedDay != null && schedDay.IsRestDay) isRestDay = true;
                        }

                        if (!isRestDay)
                        {
                            double dailyHours = 0;

                            for (int i = 0; i < pairs.Count; i++)
                            {
                                var (clockIn, clockOut) = pairs[i];
                                var timeIn = TimeZoneInfo.ConvertTime(clockIn.Timestamp, _timeZone).TimeOfDay;
                                var timeOut = TimeZoneInfo.ConvertTime(clockOut.Timestamp, _timeZone).TimeOfDay;

                                var effectiveTimeIn = timeIn;
                                if (i == 0)
                                {
                                    // Early in: rounded to scheduled start time (early time is not paid), unless schedule allows it
                                    if (schedDay?.StartTime.HasValue == true && timeIn < schedDay.StartTime.Value)
                                    {
                                        if (emp.WorkSchedule == null || !emp.WorkSchedule.IncludeTimeTrackedBeforeScheduledStartTime)
                                        {
                                            effectiveTimeIn = schedDay.StartTime.Value;
                                        }
                                    }
                                    // Afternoon half-day arriving during lunch break (12:00 PM - 1:00 PM): afternoon shift starts at 1:00 PM
                                    else if (timeIn >= new TimeSpan(12, 0, 0) && timeIn <= new TimeSpan(13, 0, 0))
                                    {
                                        effectiveTimeIn = new TimeSpan(13, 0, 0);
                                    }
                                }

                                var effectiveTimeOut = timeOut;
                                if (i == pairs.Count - 1)
                                {
                                    // Morning half-day leaving during lunch break (12:00 PM - 1:00 PM): morning shift ends at 12:00 PM
                                    if (timeOut >= new TimeSpan(12, 0, 0) && timeOut <= new TimeSpan(13, 0, 0))
                                    {
                                        effectiveTimeOut = new TimeSpan(12, 0, 0);
                                    }
                                    else if (schedDay?.EndTime.HasValue == true)
                                    {
                                        if (timeOut > schedDay.EndTime.Value)
                                        {
                                            var excessMinutes = (timeOut - schedDay.EndTime.Value).TotalMinutes;
                                            var thresholdMinutes = emp.WorkSchedule?.OvertimeThresholdMinutes > 0 ? emp.WorkSchedule.OvertimeThresholdMinutes : 15;
                                            if (excessMinutes < thresholdMinutes)
                                            {
                                                effectiveTimeOut = schedDay.EndTime.Value;
                                            }
                                            else
                                            {
                                                var approvedOt = empOvertime.FirstOrDefault(o => o.Date.Date == shiftDate);
                                                if (approvedOt != null)
                                                {
                                                    effectiveTimeOut = schedDay.EndTime.Value;
                                                    var otHours = approvedOt.ApprovedHours ?? (timeOut - schedDay.EndTime.Value).TotalHours;
                                                    totalOvertimeHours += Math.Max(0, otHours);
                                                    creditedOtIds.Add(approvedOt.Id);
                                                }
                                                else
                                                {
                                                    effectiveTimeOut = schedDay.EndTime.Value;
                                                }
                                            }
                                        }
                                        else
                                        {
                                            effectiveTimeOut = timeOut;
                                        }
                                    }
                                }

                                double pairHours = (effectiveTimeOut - effectiveTimeIn).TotalHours;

                                // Deduct unpaid breaks overlapping this interval
                                if (pairHours > 0 && emp.WorkSchedule?.Breaks != null)
                                {
                                    foreach (var b in emp.WorkSchedule.Breaks.Where(br => !br.IsPaid))
                                    {
                                        var overlapStart = effectiveTimeIn > b.StartTime ? effectiveTimeIn : b.StartTime;
                                        var overlapEnd = effectiveTimeOut < b.EndTime ? effectiveTimeOut : b.EndTime;
                                        if (overlapEnd > overlapStart)
                                        {
                                            pairHours -= (overlapEnd - overlapStart).TotalHours;
                                        }
                                    }
                                }

                                if (pairHours > 0)
                                {
                                    dailyHours += pairHours;
                                }
                            }

                            if (dailyHours > 0)
                            {
                                daysWithAttendance++;
                                attendedDates.Add(shiftDate);
                                totalNetHours += dailyHours;
                            }
                        }
                    }
                }
            }

            // Include any additional approved standalone OT (e.g. approved OT on non-work days or separate requests)
            foreach (var standaloneOt in empOvertime.Where(o => !creditedOtIds.Contains(o.Id)))
            {
                var otHours = standaloneOt.ApprovedHours ?? standaloneOt.RequestedHours;
                totalOvertimeHours += Math.Max(0, otHours);
            }

            decimal otMultiplier = (decimal)(emp.WorkSchedule?.OvertimeMultiplier ?? 1.25);
            decimal totalOvertimePay = Math.Round((decimal)totalOvertimeHours * hourlyRate * otMultiplier, 2);

            if (emp.WorkSchedule != null && !(emp.WorkSchedule.WorkArrangement == WorkArrangement.Fixed && emp.WorkSchedule.IsFixedWeeklyRate))
            {
                basicPay = (decimal)totalNetHours * hourlyRate;
            }

            // 2. Calculate Leave Pay
            var leaveDailyRate = (emp.UseCustomDailyRate && emp.DailyRate.HasValue) ? emp.DailyRate.Value : defaultRate;
            var empLeaves = approvedLeaves.Where(l => l.EmployeeId == emp.Id).ToList();
            decimal totalLeavePay = 0;
            int totalLeaveDays = 0;
            int totalLeaveHours = 0;
            var creditedLeaveDates = new HashSet<DateTime>();

            foreach (var leave in empLeaves)
            {
                var matchingPolicy = timeOffPolicies.FirstOrDefault(p => p.LeaveType == leave.Type);
                if (matchingPolicy != null && !matchingPolicy.IsPaid)
                    continue;

                var leaveStart = leave.StartDate.Date < localStart.Date ? localStart.Date : leave.StartDate.Date;
                var leaveEnd = leave.EndDate.Date > localEnd.Date ? localEnd.Date : leave.EndDate.Date;
                
                int leaveDays = 0;
                for (var date = leaveStart; date <= leaveEnd; date = date.AddDays(1))
                {
                    // Skip dates already credited via attendance or another leave request
                    if (attendedDates.Contains(date) || !creditedLeaveDates.Add(date))
                        continue;

                    // Skip rest days
                    bool isRestDay = false;
                    if (emp.WorkSchedule != null)
                    {
                        var schedDay = emp.WorkSchedule.Days.FirstOrDefault(d => d.DayOfWeek == date.DayOfWeek);
                        if (schedDay != null && schedDay.IsRestDay) isRestDay = true;
                    }
                    else if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
                    {
                        isRestDay = true;
                    }

                    if (!isRestDay) leaveDays++;
                }
                
                totalLeaveDays += leaveDays;
                totalLeaveHours += leaveDays * 8;
                totalLeavePay += leaveDays * leaveDailyRate;
            }

            // 3. Deductions & Advances
            decimal sss = 0;
            decimal philhealth = 0;
            decimal pagibig = 0;
            decimal tax = 0;

            if (emp.GovernmentRecord != null && emp.GovernmentRecord.IsEligibleForDeductions)
            {
                if (!string.IsNullOrWhiteSpace(emp.GovernmentRecord.SssNumber))
                    sss = Math.Round(monthlySss * proportionMultiplier, 2);
                if (!string.IsNullOrWhiteSpace(emp.GovernmentRecord.PhilHealthNumber))
                    philhealth = Math.Round(monthlyPhilHealth * proportionMultiplier, 2);
                if (!string.IsNullOrWhiteSpace(emp.GovernmentRecord.PagIbigNumber))
                    pagibig = Math.Round(monthlyPagIbig * proportionMultiplier, 2);
            }
            
            var activeAdvances = allActiveAdvances.Where(ar => ar.EmployeeId == emp.Id).ToList();
            decimal advancesDeduction = 0;
            foreach (var advance in activeAdvances)
            {
                decimal deduction = 0m;
                if (advance.Type == AdvanceType.ELoan)
                {
                    // Delay E-Loan deduction to payroll periods that start after the approval date
                    var approvalDate = DateOnly.FromDateTime((advance.ResolutionDate ?? advance.RequestDate).Date);
                    if (period.StartDate > approvalDate)
                    {
                        deduction = (advance.InstallmentAmount ?? 0m) / 2m;
                    }
                }
                else
                {
                    // Cash Advance: deduct the entire remaining balance immediately
                    deduction = advance.RemainingBalance ?? advance.RequestedAmount;
                }
                
                if (deduction > advance.RemainingBalance) deduction = advance.RemainingBalance ?? 0m;
                advancesDeduction += deduction;
            }

            var grossPay = basicPay + totalLeavePay + totalOvertimePay;
            var netPay = grossPay - sss - philhealth - pagibig - tax - advancesDeduction;

            var calculationDetails = new
            {
                DailyRate = trackedDailyRate,
                HourlyRate = hourlyRate,
                TotalTrackedHours = totalNetHours,
                TotalOvertimeHours = totalOvertimeHours,
                OvertimeMultiplier = otMultiplier,
                TotalOvertimePay = totalOvertimePay,
                DaysWithAttendance = daysWithAttendance,
                BasicPay = basicPay,
                LeaveDays = totalLeaveDays,
                LeaveHours = totalLeaveHours,
                TotalLeavePay = totalLeavePay
            };

            var payslip = new Payslip
            {
                PayrollPeriodId = payrollPeriodId,
                EmployeeId = emp.Id,
                BasicPay = basicPay,
                TotalLeavePay = totalLeavePay,
                TotalOvertimePay = totalOvertimePay,
                TotalHolidayPay = 0,
                GrossPay = grossPay,
                SssDeduction = sss,
                PhilHealthDeduction = philhealth,
                PagIbigDeduction = pagibig,
                TaxDeduction = tax,
                LateUndertimeDeduction = 0,
                AdvancesDeduction = advancesDeduction,
                OtherAllowances = 0,
                OtherDeductions = 0,
                NetPay = netPay,
                TotalWorkedHours = totalNetHours,
                AssignedRole = string.Join(", ", emp.EmployeeRoles.Select(er => er.Role?.Name).Where(n => !string.IsNullOrEmpty(n))),
                CalculationDetailsJson = JsonSerializer.Serialize(calculationDetails),
                OrganizationId = period.OrganizationId
            };

            payslips.Add(payslip);
        }

        _context.Payslips.AddRange(payslips);
        
        period.Status = PayrollStatus.Computed;
        await _context.SaveChangesAsync();
    }
}

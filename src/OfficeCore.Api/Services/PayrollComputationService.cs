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
            .Include(e => e.WorkSchedule)

            .ToListAsync();

        var generalSetting = await _context.GeneralPayrollSettings.FirstOrDefaultAsync(s => s.Key == "MinimumDailyRate" && s.OrganizationId == period.OrganizationId);
        var defaultRate = generalSetting?.Value ?? 610m;

        var govDeductions = await _context.GovernmentDeductionSettings
            .Where(s => s.OrganizationId == period.OrganizationId)
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
            double totalNetHours = 0;
            double totalOvertimeHours = 0;
            int daysWithAttendance = 0;
            decimal basicPay = 0;

            if (emp.WorkSchedule == null)
            {
                // No schedule: Fixed rate employee for the cutoff period (e.g. Adrian 3775, Blaire 1000, Lina 2500)
                basicPay = trackedDailyRate;
                daysWithAttendance = empAttendance.Select(a => TimeZoneInfo.ConvertTime(a.Timestamp, _timeZone).Date).Distinct().Count();
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
                        totalNetHours += 8; // Calculate standard 8 hours to yield full daily rate
                    }
                }
                basicPay = (decimal)totalNetHours * hourlyRate;
            }
            else
            {
                // Group by day to find pairs
                var attendanceByDay = empAttendance.GroupBy(a => TimeZoneInfo.ConvertTime(a.Timestamp, _timeZone).Date);

                foreach (var dayGroup in attendanceByDay)
                {
                    var records = dayGroup.OrderBy(a => a.Timestamp).ToList();
                    var clockIn = records.FirstOrDefault(r => r.Type == AttendanceType.ClockIn);
                    var clockOut = records.LastOrDefault(r => r.Type == AttendanceType.ClockOut);

                    if (clockIn != null && clockOut != null && clockOut.Timestamp > clockIn.Timestamp)
                    {
                        var shiftDate = dayGroup.Key;
                        bool isRestDay = false;
                        WorkScheduleDay? schedDay = null;
                        if (emp.WorkSchedule?.Days != null)
                        {
                            schedDay = emp.WorkSchedule.Days.FirstOrDefault(d => d.DayOfWeek == TimeZoneInfo.ConvertTime(clockIn.Timestamp, _timeZone).DayOfWeek);
                            if (schedDay != null && schedDay.IsRestDay) isRestDay = true;
                        }

                        if (!isRestDay)
                        {
                            daysWithAttendance++;
                            var timeIn = TimeZoneInfo.ConvertTime(clockIn.Timestamp, _timeZone).TimeOfDay;
                            var timeOut = TimeZoneInfo.ConvertTime(clockOut.Timestamp, _timeZone).TimeOfDay;

                            // Early in: rounded to scheduled start time (early time is not paid), unless schedule allows it
                            var effectiveTimeIn = timeIn;
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

                            // Clock-out handling:
                            var effectiveTimeOut = timeOut;

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
                                    // 15-minute threshold rule
                                    if (excessMinutes < 15)
                                    {
                                        // Under 15 mins: automatically clamps down to shift end
                                        effectiveTimeOut = schedDay.EndTime.Value;
                                    }
                                    else
                                    {
                                        // 15+ mins: Check for approved Pre-OT or Post-OT
                                        var approvedOt = empOvertime.FirstOrDefault(o => o.Date.Date == shiftDate);
                                        if (approvedOt != null)
                                        {
                                            // Approved OT: Regular shift ends at scheduled end
                                            effectiveTimeOut = schedDay.EndTime.Value;
                                            var otHours = approvedOt.ApprovedHours ?? (timeOut - schedDay.EndTime.Value).TotalHours;
                                            totalOvertimeHours += Math.Max(0, otHours);
                                            creditedOtIds.Add(approvedOt.Id);
                                        }
                                        else
                                        {
                                            // Rejected or unapproved OT: automatically clamps down to scheduled shift out
                                            effectiveTimeOut = schedDay.EndTime.Value;
                                        }
                                    }
                                }
                                else
                                {
                                    // Undertime: used as-is
                                    effectiveTimeOut = timeOut;
                                }
                            }

                            double dailyHours = (effectiveTimeOut - effectiveTimeIn).TotalHours;

                            // Deduct unpaid breaks
                            if (emp.WorkSchedule?.Breaks != null)
                            {
                                foreach (var b in emp.WorkSchedule.Breaks.Where(br => !br.IsPaid))
                                {
                                    // Overlap logic: max(0, min(timeOut, breakEnd) - max(timeIn, breakStart))
                                    var overlapStart = effectiveTimeIn > b.StartTime ? effectiveTimeIn : b.StartTime;
                                    var overlapEnd = effectiveTimeOut < b.EndTime ? effectiveTimeOut : b.EndTime;
                                    if (overlapEnd > overlapStart)
                                    {
                                        dailyHours -= (overlapEnd - overlapStart).TotalHours;
                                    }
                                }
                            }

                            if (dailyHours > 0)
                            {
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

            // 2. Calculate Leave Pay using default/minimum rate
            var leaveDailyRate = defaultRate;
            var empLeaves = approvedLeaves.Where(l => l.EmployeeId == emp.Id).ToList();
            decimal totalLeavePay = 0;
            int totalLeaveDays = 0;
            int totalLeaveHours = 0;

            foreach (var leave in empLeaves)
            {
                var leaveStart = leave.StartDate.Date < localStart.Date ? localStart.Date : leave.StartDate.Date;
                var leaveEnd = leave.EndDate.Date > localEnd.Date ? localEnd.Date : leave.EndDate.Date;
                
                int leaveDays = 0;
                for (var date = leaveStart; date <= leaveEnd; date = date.AddDays(1))
                {
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
            decimal tax = 0; // Tax calculation left as 0 for now unless there's a specific rule

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
                    // Delay E-Loan deduction to the next payroll period
                    // (The period must start after the E-Loan was approved)
                    if (periodStartDt > (advance.ResolutionDate ?? advance.RequestDate))
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
                LateUndertimeDeduction = 0, // Obsolete, replaced by raw time tracking
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

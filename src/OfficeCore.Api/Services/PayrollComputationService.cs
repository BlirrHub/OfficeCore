using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OfficeCore.Api.Models;

namespace OfficeCore.Api.Services;

public class PayrollComputationService : IPayrollComputationService
{
    private readonly AppDbContext _context;

    public PayrollComputationService(AppDbContext context)
    {
        _context = context;
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
            .Where(e => e.IsActive && e.IsEmployee && e.OrganizationId == period.OrganizationId)
            .Include(e => e.WorkSchedule)
                .ThenInclude(ws => ws!.Days)
            .Include(e => e.WorkSchedule)
                .ThenInclude(ws => ws!.Breaks)
            .Include(e => e.WorkSchedule)
                .ThenInclude(ws => ws!.OvertimeRules)
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
        var periodStartDt = new DateTimeOffset(localStart, TimeZoneInfo.Local.GetUtcOffset(localStart)).UtcDateTime;
        var periodEndDt = new DateTimeOffset(localEnd, TimeZoneInfo.Local.GetUtcOffset(localEnd)).UtcDateTime;
        
        // Use the explicitly defined DeductionMultiplier for government deductions
        decimal proportionMultiplier = period.DeductionMultiplier;

        var allAttendance = await _context.AttendanceRecords
            .Where(a => a.Timestamp >= periodStartDt && a.Timestamp <= periodEndDt)
            .OrderBy(a => a.Timestamp)
            .ToListAsync();

        var approvedLeaves = await _context.LeaveRequests
            .Where(lr => lr.Status == LeaveRequestStatus.Approved && lr.StartDate <= periodEndDt && lr.EndDate >= periodStartDt)
            .ToListAsync();

        var approvedOvertime = await _context.OvertimeRequests
            .Where(o => o.OrganizationId == period.OrganizationId 
                     && o.Status == OvertimeStatus.Approved 
                     && o.Date >= periodStartDt && o.Date <= periodEndDt)
            .ToListAsync();

        var allActiveAdvances = await _context.AdvanceRequests
            .Where(ar => ar.Status == AdvanceStatus.Approved && (ar.RemainingBalance == null || ar.RemainingBalance > 0))
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
                daysWithAttendance = empAttendance.Select(a => a.Timestamp.ToLocalTime().Date).Distinct().Count();
                totalNetHours = daysWithAttendance > 0 ? daysWithAttendance * 8 : 40;
            }
            else if (emp.WorkSchedule.WorkArrangement == WorkArrangement.Fixed && emp.WorkSchedule.IsFixedWeeklyRate)
            {
                for (var date = periodStartDt; date <= periodEndDt; date = date.AddDays(1))
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
                var attendanceByDay = empAttendance.GroupBy(a => a.Timestamp.ToLocalTime().Date);

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
                            schedDay = emp.WorkSchedule.Days.FirstOrDefault(d => d.DayOfWeek == clockIn.Timestamp.ToLocalTime().DayOfWeek);
                            if (schedDay != null && schedDay.IsRestDay) isRestDay = true;
                        }

                        if (!isRestDay)
                        {
                            daysWithAttendance++;
                            var timeIn = clockIn.Timestamp.ToLocalTime().TimeOfDay;
                            var timeOut = clockOut.Timestamp.ToLocalTime().TimeOfDay;

                            // Early in: rounded to scheduled start time (early time is not paid), unless schedule allows it
                            var effectiveTimeIn = timeIn;
                            if (schedDay?.StartTime.HasValue == true && timeIn < schedDay.StartTime.Value)
                            {
                                if (emp.WorkSchedule == null || !emp.WorkSchedule.IncludeTimeTrackedBeforeScheduledStartTime)
                                {
                                    effectiveTimeIn = schedDay.StartTime.Value;
                                }
                            }
                            // Late in: used as-is

                            // Clock-out handling:
                            var effectiveTimeOut = timeOut;
                            if (schedDay?.EndTime.HasValue == true)
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

            decimal otMultiplier = (decimal)(emp.WorkSchedule?.OvertimeRules?.FirstOrDefault()?.Multiplier ?? 1.25);
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
                var leaveStart = leave.StartDate < periodStartDt ? periodStartDt : leave.StartDate;
                var leaveEnd = leave.EndDate > periodEndDt ? periodEndDt : leave.EndDate;
                
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

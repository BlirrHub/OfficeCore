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

        var employees = await _context.Users
            .Where(e => e.IsActive && e.IsEmployee)
            .Include(e => e.WorkSchedule)
                .ThenInclude(ws => ws!.Days)
            .Include(e => e.WorkSchedule)
                .ThenInclude(ws => ws!.Breaks)
            .ToListAsync();

        var generalSetting = await _context.GeneralPayrollSettings.FirstOrDefaultAsync(s => s.Key == "MinimumDailyRate");
        var defaultRate = generalSetting?.Value ?? 610m;

        var existingPayslips = await _context.Payslips.Where(p => p.PayrollPeriodId == payrollPeriodId).ToListAsync();
        _context.Payslips.RemoveRange(existingPayslips);

        var payslips = new List<Payslip>();

        var periodStartDt = DateTime.SpecifyKind(period.StartDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var periodEndDt = DateTime.SpecifyKind(period.EndDate.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc);

        var allAttendance = await _context.AttendanceRecords
            .Where(a => a.Timestamp >= periodStartDt && a.Timestamp <= periodEndDt)
            .OrderBy(a => a.Timestamp)
            .ToListAsync();

        var approvedLeaves = await _context.LeaveRequests
            .Where(lr => lr.Status == LeaveRequestStatus.Approved && lr.StartDate <= periodEndDt && lr.EndDate >= periodStartDt)
            .ToListAsync();

        var allActiveAdvances = await _context.AdvanceRequests
            .Where(ar => ar.Status == AdvanceStatus.Approved && (ar.RemainingBalance == null || ar.RemainingBalance > 0))
            .ToListAsync();

        foreach (var emp in employees)
        {
            var trackedDailyRate = emp.DailyRate ?? defaultRate;
            var hourlyRate = trackedDailyRate / 8m; // Standard assumption of 8 working hours per day for hourly computation

            // 1. Calculate Tracked Hours
            var empAttendance = allAttendance.Where(a => a.EmployeeId == emp.Id).ToList();
            double totalNetHours = 0;
            int daysWithAttendance = 0;

            if (emp.WorkSchedule?.WorkArrangement == WorkArrangement.Fixed && emp.WorkSchedule.IsFixedWeeklyRate)
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
                        // Check if it's a rest day and skip unless we support OT on rest days (currently ignored per requirements)
                        bool isRestDay = false;
                        if (emp.WorkSchedule != null)
                        {
                            var schedDay = emp.WorkSchedule.Days.FirstOrDefault(d => d.DayOfWeek == clockIn.Timestamp.ToLocalTime().DayOfWeek);
                            if (schedDay != null && schedDay.IsRestDay) isRestDay = true;
                        }

                        if (!isRestDay)
                        {
                            daysWithAttendance++;
                            var timeIn = clockIn.Timestamp.ToLocalTime().TimeOfDay;
                            var timeOut = clockOut.Timestamp.ToLocalTime().TimeOfDay;

                            double dailyHours = (timeOut - timeIn).TotalHours;

                            // Deduct unpaid breaks
                            if (emp.WorkSchedule != null && emp.WorkSchedule.Breaks != null)
                            {
                                foreach (var b in emp.WorkSchedule.Breaks.Where(br => !br.IsPaid))
                                {
                                    // Overlap logic: max(0, min(timeOut, breakEnd) - max(timeIn, breakStart))
                                    var overlapStart = timeIn > b.StartTime ? timeIn : b.StartTime;
                                    var overlapEnd = timeOut < b.EndTime ? timeOut : b.EndTime;
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

            var basicPay = (decimal)totalNetHours * hourlyRate;

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
            decimal tax = 0;
            
            var activeAdvances = allActiveAdvances.Where(ar => ar.EmployeeId == emp.Id).ToList();
            decimal advancesDeduction = 0;
            foreach (var advance in activeAdvances)
            {
                decimal deduction = 0m;
                if (advance.Type == AdvanceType.ELoan)
                {
                    deduction = (advance.InstallmentAmount ?? 0m) / 2m;
                }
                else
                {
                    // Cash Advance: deduct the entire remaining balance
                    deduction = advance.RemainingBalance ?? advance.RequestedAmount;
                }
                
                if (deduction > advance.RemainingBalance) deduction = advance.RemainingBalance ?? 0m;
                advancesDeduction += deduction;
            }

            var grossPay = basicPay + totalLeavePay;
            var netPay = grossPay - sss - philhealth - pagibig - tax - advancesDeduction;

            var calculationDetails = new
            {
                DailyRate = trackedDailyRate,
                HourlyRate = hourlyRate,
                TotalTrackedHours = totalNetHours,
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
                TotalOvertimePay = 0,
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
                CalculationDetailsJson = JsonSerializer.Serialize(calculationDetails)
            };

            payslips.Add(payslip);
        }

        _context.Payslips.AddRange(payslips);
        
        period.Status = PayrollStatus.Computed;
        await _context.SaveChangesAsync();
    }
}

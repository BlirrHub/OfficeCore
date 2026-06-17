using OfficeCore.Api.Models;

namespace OfficeCore.Api.Services;

public interface IPayrollComputationService
{
    Task ComputePayrollPeriodAsync(Guid payrollPeriodId);
}

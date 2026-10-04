using System;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OfficeCore.Api.Models;

namespace OfficeCore.Api.Services;

public interface IAuditService
{
    Task LogAsync(
        string action,
        string entityName,
        string? entityId,
        string details,
        object? oldValues = null,
        object? newValues = null,
        Guid? customOrgId = null);

    Task LogPayrollAsync(string action, string details, string? periodId = null, object? metadata = null);
    Task LogPayrollAsync(string action, string details, Guid? periodId, object? metadata = null);

    Task LogAttendanceAsync(string action, string details, string? recordId = null, object? oldValues = null, object? newValues = null);
    Task LogAttendanceAsync(string action, string details, Guid? recordId, object? oldValues = null, object? newValues = null);

    Task LogLeaveAsync(string action, string details, string? leaveId = null, object? metadata = null);
    Task LogLeaveAsync(string action, string details, Guid? leaveId, object? metadata = null);

    Task LogAdvanceAsync(string action, string details, string? advanceId = null, object? metadata = null);
    Task LogAdvanceAsync(string action, string details, Guid? advanceId, object? metadata = null);

    Task LogEmployeeAsync(string action, string details, string? employeeId = null, object? oldValues = null, object? newValues = null);

    Task LogSecurityAsync(string action, string details, string? targetUserId = null, object? metadata = null);
}

public class AuditService : IAuditService
{
    private readonly AppDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly TenantService _tenant;
    private readonly ILogger<AuditService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public AuditService(
        AppDbContext context,
        IHttpContextAccessor httpContextAccessor,
        TenantService tenant,
        ILogger<AuditService> logger)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _tenant = tenant;
        _logger = logger;
    }

    public async Task LogAsync(
        string action,
        string entityName,
        string? entityId,
        string details,
        object? oldValues = null,
        object? newValues = null,
        Guid? customOrgId = null)
    {
        try
        {
            var orgId = customOrgId ?? _tenant.GetActiveOrganizationId();
            if (orgId == null || orgId == Guid.Empty)
            {
                // Fallback: Check if there is an organization in context or default to first
                return;
            }

            var httpContext = _httpContextAccessor.HttpContext;
            string? userId = null;
            string userName = "System";
            string? userEmail = null;
            string? ipAddress = null;
            string? clientInfo = null;

            if (httpContext != null)
            {
                var user = httpContext.User;
                if (user?.Identity?.IsAuthenticated == true)
                {
                    userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    userEmail = user.FindFirst(ClaimTypes.Email)?.Value;

                    var firstName = user.FindFirst("FirstName")?.Value;
                    var lastName = user.FindFirst("LastName")?.Value;
                    if (!string.IsNullOrWhiteSpace(firstName) || !string.IsNullOrWhiteSpace(lastName))
                    {
                        userName = $"{firstName} {lastName}".Trim();
                    }
                    else
                    {
                        userName = user.Identity.Name ?? userEmail ?? "User";
                    }
                }
                else if (httpContext.Request.Headers.TryGetValue("X-OfficeCore-Station", out var stationHeader) &&
                         stationHeader == "ServerStationOrchestrator")
                {
                    userName = "Server Station (Host Console)";
                }

                ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
                if (ipAddress == "::1") ipAddress = "127.0.0.1 (Localhost)";

                if (httpContext.Request.Headers.TryGetValue("X-OfficeCore-Client", out var clientHeader))
                {
                    clientInfo = clientHeader.ToString();
                }
                else if (httpContext.Request.Headers.TryGetValue("User-Agent", out var uaHeader))
                {
                    clientInfo = uaHeader.ToString();
                    if (clientInfo.Length > 200) clientInfo = clientInfo[..200];
                }
            }

            string? oldJson = null;
            if (oldValues != null)
            {
                oldJson = oldValues is string s ? s : JsonSerializer.Serialize(oldValues, JsonOptions);
            }

            string? newJson = null;
            if (newValues != null)
            {
                newJson = newValues is string s ? s : JsonSerializer.Serialize(newValues, JsonOptions);
            }

            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                OrganizationId = orgId.Value,
                UserId = userId,
                UserName = userName,
                UserEmail = userEmail,
                Action = action.ToUpperInvariant(),
                EntityName = entityName,
                EntityId = entityId,
                Details = details,
                OldValues = oldJson,
                NewValues = newJson,
                IpAddress = ipAddress,
                ClientInfo = clientInfo,
                CreatedAt = DateTime.UtcNow
            };

            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record audit log for action {Action} on {EntityName}", action, entityName);
        }
    }

    public Task LogPayrollAsync(string action, string details, string? periodId = null, object? metadata = null)
    {
        return LogAsync(action, AuditModules.Payroll, periodId, details, null, metadata);
    }

    public Task LogPayrollAsync(string action, string details, Guid? periodId, object? metadata = null)
    {
        return LogPayrollAsync(action, details, periodId?.ToString(), metadata);
    }

    public Task LogAttendanceAsync(string action, string details, string? recordId = null, object? oldValues = null, object? newValues = null)
    {
        return LogAsync(action, AuditModules.Attendance, recordId, details, oldValues, newValues);
    }

    public Task LogAttendanceAsync(string action, string details, Guid? recordId, object? oldValues = null, object? newValues = null)
    {
        return LogAttendanceAsync(action, details, recordId?.ToString(), oldValues, newValues);
    }

    public Task LogLeaveAsync(string action, string details, string? leaveId = null, object? metadata = null)
    {
        return LogAsync(action, AuditModules.Leave, leaveId, details, null, metadata);
    }

    public Task LogLeaveAsync(string action, string details, Guid? leaveId, object? metadata = null)
    {
        return LogLeaveAsync(action, details, leaveId?.ToString(), metadata);
    }

    public Task LogAdvanceAsync(string action, string details, string? advanceId = null, object? metadata = null)
    {
        return LogAsync(action, AuditModules.Advance, advanceId, details, null, metadata);
    }

    public Task LogAdvanceAsync(string action, string details, Guid? advanceId, object? metadata = null)
    {
        return LogAdvanceAsync(action, details, advanceId?.ToString(), metadata);
    }

    public Task LogEmployeeAsync(string action, string details, string? employeeId = null, object? oldValues = null, object? newValues = null)
    {
        return LogAsync(action, AuditModules.Employee, employeeId, details, oldValues, newValues);
    }

    public Task LogSecurityAsync(string action, string details, string? targetUserId = null, object? metadata = null)
    {
        return LogAsync(action, AuditModules.Roles, targetUserId, details, null, metadata);
    }
}

using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using OfficeCore.Api.Models;
using OfficeCore.Api.Hubs;

namespace OfficeCore.Api.Services;

public class FingerprintDeviceService : BackgroundService
{
    private readonly ILogger<FingerprintDeviceService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    
    private TcpListener? _listener;
    private TcpClient? _activeClient;
    private NetworkStream? _activeStream;

    // State for enrollment mode and active connected device tenant
    private string? _pendingEnrollEmployeeId;
    private Guid? _pendingEnrollOrganizationId;
    private Guid? _connectedDeviceId;
    private Guid? _connectedDeviceOrgId;
    public bool IsEnrollModeActive => _pendingEnrollEmployeeId != null;

    public FingerprintDeviceService(ILogger<FingerprintDeviceService> logger, IServiceProvider serviceProvider, IConfiguration configuration)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var port = _configuration.GetValue<int>("FingerprintDevice:TcpPort", 9000);
        
        try
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var devices = await context.FingerprintDevices.Where(d => d.IpAddress != null).ToListAsync(stoppingToken);
                foreach (var device in devices)
                {
                    device.IpAddress = null;
                }
                if (devices.Any())
                {
                    await context.SaveChangesAsync(stoppingToken);
                }
            }

            try
            {
                _listener = new TcpListener(IPAddress.Any, port);
                _listener.Start();
                _logger.LogInformation("TCP Listener started for Wi-Fi Fingerprint Devices on port {Port}.", port);
            }
            catch (SocketException ex)
            {
                _logger.LogWarning(ex, "Could not start TCP Listener for Fingerprint Devices on port {Port}. The port may already be in use.", port);
                _listener = null;
            }

            while (!stoppingToken.IsCancellationRequested && _listener != null)
            {
                if (_listener.Pending())
                {
                    if (_activeClient != null)
                    {
                        // Disconnect old client if a new one connects (single device setup per server for now)
                        _activeClient.Close();
                    }

                    _activeClient = await _listener.AcceptTcpClientAsync(stoppingToken);
                    _activeStream = _activeClient.GetStream();
                    
                    var endpoint = _activeClient.Client.RemoteEndPoint?.ToString();
                    _logger.LogInformation("Fingerprint device connected via TCP: {Endpoint}", endpoint);
                    
                    var remoteIp = endpoint?.Split(':')[0];
                    _connectedDeviceId = null;
                    _connectedDeviceOrgId = null;

                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        var allDevices = await context.FingerprintDevices.ToListAsync(stoppingToken);
                        var device = (!string.IsNullOrWhiteSpace(remoteIp)
                                ? allDevices.FirstOrDefault(d => string.Equals(d.IpAddress, remoteIp, StringComparison.OrdinalIgnoreCase))
                                : null)
                            ?? (allDevices.Count(d => d.IsActive) == 1 ? allDevices.First(d => d.IsActive) : null)
                            ?? (allDevices.Count == 1 ? allDevices[0] : null);

                        if (device != null)
                        {
                            _connectedDeviceId = device.Id;
                            _connectedDeviceOrgId = device.OrganizationId;
                            device.LastSeen = DateTimeOffset.UtcNow;
                            if (!string.IsNullOrWhiteSpace(remoteIp))
                            {
                                device.IpAddress = remoteIp; // Just the IP, drop the port
                            }
                            await context.SaveChangesAsync(stoppingToken);
                            
                            var hubContext = scope.ServiceProvider.GetService<IHubContext<AttendanceHub>>();
                            if (hubContext != null)
                            {
                                await hubContext.Clients.Group($"org_{device.OrganizationId}")
                                    .SendAsync("ReceiveDeviceConnectionStatus", device.IpAddress, device.LastSeen, cancellationToken: stoppingToken);
                            }
                        }
                    }
                    
                    _ = HandleClientAsync(_activeClient, _activeStream, stoppingToken);
                }
                else
                {
                    await Task.Delay(100, stoppingToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting TCP Listener for Fingerprint Devices");
        }
        finally
        {
            _listener?.Stop();
        }
    }

    private async Task HandleClientAsync(TcpClient client, NetworkStream stream, CancellationToken stoppingToken)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);
        try
        {
            while (!stoppingToken.IsCancellationRequested && client.Connected)
            {
                if (stream.DataAvailable)
                {
                    var line = await reader.ReadLineAsync();
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        await ProcessMessageAsync(line.Trim());
                    }
                }
                else
                {
                    await Task.Delay(100, stoppingToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("TCP Client disconnected or error: {Message}", ex.Message);
        }
        finally
        {
            if (_activeClient == client)
            {
                _activeClient = null;
                _activeStream = null;
                
                if (_connectedDeviceId.HasValue)
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        var device = await context.FingerprintDevices.FirstOrDefaultAsync(d => d.Id == _connectedDeviceId.Value);
                        if (device != null)
                        {
                            device.IpAddress = null;
                            await context.SaveChangesAsync();
                            
                            var hubContext = scope.ServiceProvider.GetService<IHubContext<AttendanceHub>>();
                            if (hubContext != null)
                            {
                                await hubContext.Clients.Group($"org_{device.OrganizationId}")
                                    .SendAsync("ReceiveDeviceConnectionStatus", null, device.LastSeen);
                            }
                        }
                    }
                }
                _connectedDeviceId = null;
                _connectedDeviceOrgId = null;
                _pendingEnrollEmployeeId = null;
                _pendingEnrollOrganizationId = null;
            }
            client.Close();
        }
    }

    private async Task ProcessMessageAsync(string message)
    {
        _logger.LogInformation("Received from TCP device: {Message}", message);

        if (message.StartsWith("ATTENDANCE:"))
        {
            if (int.TryParse(message.Substring(11), out int fingerprintId))
            {
                await LogAttendanceAsync(fingerprintId);
            }
        }
        else if (message.StartsWith("ENROLL_SUCCESS:"))
        {
            if (int.TryParse(message.Substring(15), out int fingerprintId))
            {
                await LinkFingerprintAsync(fingerprintId);
            }
        }
        else if (message.StartsWith("ERROR:"))
        {
            if (_pendingEnrollEmployeeId != null)
            {
                _logger.LogWarning("Device reported error during enrollment. Clearing pending state.");
                await SendEnrollmentStatusAsync(_pendingEnrollEmployeeId, _pendingEnrollOrganizationId, message);
                _pendingEnrollEmployeeId = null;
                _pendingEnrollOrganizationId = null;
            }
        }
        else if (message.StartsWith("INFO:") || message.StartsWith("MODE:"))
        {
             if (_pendingEnrollEmployeeId != null)
             {
                 await SendEnrollmentStatusAsync(_pendingEnrollEmployeeId, _pendingEnrollOrganizationId, message);
             }
             else
             {
                 await SendDeviceMessageAsync(message, _connectedDeviceOrgId);
             }
        }
    }

    private async Task SendDeviceMessageAsync(string message, Guid? organizationId)
    {
        if (!organizationId.HasValue) return;
        using var scope = _serviceProvider.CreateScope();
        var hubContext = scope.ServiceProvider.GetService<IHubContext<AttendanceHub>>();
        if (hubContext != null)
        {
            await hubContext.Clients.Group($"org_{organizationId.Value}").SendAsync("ReceiveDeviceMessage", message);
        }
    }

    private async Task SendEnrollmentStatusAsync(string employeeId, Guid? organizationId, string status)
    {
        if (!organizationId.HasValue) return;
        using var scope = _serviceProvider.CreateScope();
        var hubContext = scope.ServiceProvider.GetService<Microsoft.AspNetCore.SignalR.IHubContext<OfficeCore.Api.Hubs.AttendanceHub>>();
        if (hubContext != null)
        {
            await hubContext.Clients.Group($"org_{organizationId.Value}").SendAsync("ReceiveEnrollmentStatus", employeeId, status);
        }
    }

    private async Task LogAttendanceAsync(int fingerprintId)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var mapping = await context.EmployeeFingerprints
            .Include(f => f.Employee)
            .FirstOrDefaultAsync(f => f.FingerprintId == fingerprintId
                                   && (_connectedDeviceOrgId == null || f.OrganizationId == _connectedDeviceOrgId.Value)
                                   && f.Employee != null
                                   && f.Employee.IsActive);

        if (mapping == null)
        {
            _logger.LogWarning("Unknown fingerprint scanned via TCP: {FingerprintId}", fingerprintId);
            return;
        }

        var phtNow = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8));
        var todayUtc = new DateTimeOffset(phtNow.Date, TimeSpan.FromHours(8)).ToUniversalTime();
        var tomorrowUtc = todayUtc.AddDays(1);

        var latestRecord = await context.AttendanceRecords
            .Where(a => a.OrganizationId == mapping.OrganizationId && a.EmployeeId == mapping.EmployeeId
                     && a.Timestamp >= todayUtc 
                     && a.Timestamp < tomorrowUtc)
            .OrderByDescending(a => a.Timestamp)
            .FirstOrDefaultAsync();

        var type = AttendanceType.ClockIn;
        if (latestRecord != null && latestRecord.Type == AttendanceType.ClockIn)
        {
            type = AttendanceType.ClockOut;
        }

        var record = new AttendanceRecord
        {
            EmployeeId = mapping.EmployeeId,
            Timestamp = DateTimeOffset.UtcNow,
            Type = type,
            Source = AttendanceSource.Device,
            OrganizationId = mapping.OrganizationId
        };

        context.AttendanceRecords.Add(record);
        await context.SaveChangesAsync();

        _logger.LogInformation("Logged {Type} for Employee {EmployeeId} via TCP", type, mapping.EmployeeId);

        var hubContext = scope.ServiceProvider.GetService<IHubContext<AttendanceHub>>();
        if (hubContext != null)
        {
            await hubContext.Clients.Group($"org_{mapping.OrganizationId}").SendAsync("ReceiveAttendanceUpdate");
        }
    }

    private async Task LinkFingerprintAsync(int fingerprintId)
    {
        if (_pendingEnrollEmployeeId == null)
        {
            _logger.LogWarning("TCP Device enrolled a fingerprint ({FingerprintId}) but no employee was pending enrollment.", fingerprintId);
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        try
        {
            var employee = await context.Employees.FirstOrDefaultAsync(e =>
                (_pendingEnrollOrganizationId == null || e.OrganizationId == _pendingEnrollOrganizationId.Value)
                && (e.Id == _pendingEnrollEmployeeId || e.UserId == _pendingEnrollEmployeeId)
                && e.IsActive);
            if (employee != null)
            {
                context.EmployeeFingerprints.Add(new EmployeeFingerprint
                {
                    EmployeeId = employee.Id,
                    OrganizationId = employee.OrganizationId,
                    FingerprintId = fingerprintId
                });
                await context.SaveChangesAsync();
                _logger.LogInformation("Successfully linked Fingerprint {FingerprintId} to Employee {EmployeeId} via TCP", fingerprintId, _pendingEnrollEmployeeId);
                
                var hubContext = scope.ServiceProvider.GetService<Microsoft.AspNetCore.SignalR.IHubContext<OfficeCore.Api.Hubs.AttendanceHub>>();
                if (hubContext != null)
                {
                    await hubContext.Clients.Group($"org_{employee.OrganizationId}").SendAsync("ReceiveEnrollmentSuccess", employee.Id);
                }
            }
        }
        finally
        {
            _pendingEnrollEmployeeId = null;
            _pendingEnrollOrganizationId = null;
        }
    }

    public bool StartEnrollment(string employeeId, Guid organizationId)
    {
        if (_activeStream == null || _activeClient == null || !_activeClient.Connected)
        {
            _logger.LogError("Cannot start TCP enrollment, no active Wi-Fi scanner connected.");
            return false;
        }

        if (_connectedDeviceOrgId.HasValue && _connectedDeviceOrgId.Value != organizationId)
        {
            _logger.LogWarning("Cannot start TCP enrollment: connected device belongs to organization {DeviceOrgId}, not {OrgId}.", _connectedDeviceOrgId.Value, organizationId);
            return false;
        }

        if (IsEnrollModeActive)
        {
            _logger.LogWarning("Enrollment is already active for another employee.");
            return false;
        }

        _pendingEnrollEmployeeId = employeeId;
        _pendingEnrollOrganizationId = organizationId;
        
        try
        {
            var command = Encoding.UTF8.GetBytes("ENROLL\n");
            _activeStream.Write(command, 0, command.Length);
            _activeStream.Flush();
            _logger.LogInformation("Sent ENROLL command to TCP device for Employee {EmployeeId}", employeeId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError("Error sending ENROLL command: {Message}", ex.Message);
            _pendingEnrollEmployeeId = null;
            _pendingEnrollOrganizationId = null;
            return false;
        }
    }

    public void CancelEnrollment(Guid organizationId)
    {
        if (_pendingEnrollEmployeeId == null) return;
        if (_pendingEnrollOrganizationId.HasValue && _pendingEnrollOrganizationId.Value != organizationId) return;
        
        _pendingEnrollEmployeeId = null;
        _pendingEnrollOrganizationId = null;
        
        if (_activeStream != null && _activeClient != null && _activeClient.Connected)
        {
            try
            {
                var command = Encoding.UTF8.GetBytes("CANCEL\n");
                _activeStream.Write(command, 0, command.Length);
                _activeStream.Flush();
                _logger.LogInformation("Sent CANCEL command to TCP device.");
            }
            catch (Exception ex)
            {
                _logger.LogError("Error sending CANCEL command: {Message}", ex.Message);
            }
        }
    }
}

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

    // State for enrollment mode
    private string? _pendingEnrollEmployeeId;
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

            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();
            _logger.LogInformation("TCP Listener started for Wi-Fi Fingerprint Devices on port {Port}.", port);

            while (!stoppingToken.IsCancellationRequested)
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
                    
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        var device = await context.FingerprintDevices.FirstOrDefaultAsync();
                        if (device != null)
                        {
                            device.LastSeen = DateTimeOffset.UtcNow;
                            if (endpoint != null)
                            {
                                device.IpAddress = endpoint.Split(':')[0]; // Just the IP, drop the port
                            }
                            await context.SaveChangesAsync(stoppingToken);
                            
                            var hubContext = scope.ServiceProvider.GetService<IHubContext<AttendanceHub>>();
                            if (hubContext != null)
                            {
                                await hubContext.Clients.All.SendAsync("ReceiveDeviceConnectionStatus", device.IpAddress, device.LastSeen);
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
                
                using (var scope = _serviceProvider.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var device = await context.FingerprintDevices.FirstOrDefaultAsync();
                    if (device != null)
                    {
                        device.IpAddress = null;
                        await context.SaveChangesAsync();
                        
                        var hubContext = scope.ServiceProvider.GetService<IHubContext<AttendanceHub>>();
                        if (hubContext != null)
                        {
                            await hubContext.Clients.All.SendAsync("ReceiveDeviceConnectionStatus", null, device.LastSeen);
                        }
                    }
                }
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
                await SendEnrollmentStatusAsync(_pendingEnrollEmployeeId, message);
                _pendingEnrollEmployeeId = null;
            }
        }
        else if (message.StartsWith("INFO:") || message.StartsWith("MODE:"))
        {
             if (_pendingEnrollEmployeeId != null)
             {
                 await SendEnrollmentStatusAsync(_pendingEnrollEmployeeId, message);
             }
             else
             {
                 await SendDeviceMessageAsync(message);
             }
        }
    }

    private async Task SendDeviceMessageAsync(string message)
    {
        using var scope = _serviceProvider.CreateScope();
        var hubContext = scope.ServiceProvider.GetService<IHubContext<AttendanceHub>>();
        if (hubContext != null)
        {
            await hubContext.Clients.All.SendAsync("ReceiveDeviceMessage", message);
        }
    }

    private async Task SendEnrollmentStatusAsync(string employeeId, string status)
    {
        using var scope = _serviceProvider.CreateScope();
        var hubContext = scope.ServiceProvider.GetService<Microsoft.AspNetCore.SignalR.IHubContext<OfficeCore.Api.Hubs.AttendanceHub>>();
        if (hubContext != null)
        {
            await hubContext.Clients.All.SendAsync("ReceiveEnrollmentStatus", employeeId, status);
        }
    }

    private async Task LogAttendanceAsync(int fingerprintId)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var mapping = await context.EmployeeFingerprints
            .FirstOrDefaultAsync(f => f.FingerprintId == fingerprintId);

        if (mapping == null)
        {
            _logger.LogWarning("Unknown fingerprint scanned via TCP: {FingerprintId}", fingerprintId);
            return;
        }

        var todayUtc = new DateTimeOffset(DateTimeOffset.UtcNow.Date, TimeSpan.Zero);
        var tomorrowUtc = todayUtc.AddDays(1);

        var latestRecord = await context.AttendanceRecords
            .Where(a => a.EmployeeId == mapping.EmployeeId 
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
            await hubContext.Clients.All.SendAsync("ReceiveAttendanceUpdate");
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

        var employee = await context.Users.FindAsync(_pendingEnrollEmployeeId);
        if (employee != null)
        {
            context.EmployeeFingerprints.Add(new EmployeeFingerprint
            {
                EmployeeId = _pendingEnrollEmployeeId,
                FingerprintId = fingerprintId
            });
            await context.SaveChangesAsync();
            _logger.LogInformation("Successfully linked Fingerprint {FingerprintId} to Employee {EmployeeId} via TCP", fingerprintId, _pendingEnrollEmployeeId);
            
            var hubContext = scope.ServiceProvider.GetService<Microsoft.AspNetCore.SignalR.IHubContext<OfficeCore.Api.Hubs.AttendanceHub>>();
            if (hubContext != null)
            {
                await hubContext.Clients.All.SendAsync("ReceiveEnrollmentSuccess", _pendingEnrollEmployeeId);
            }
        }

        _pendingEnrollEmployeeId = null;
    }

    public bool StartEnrollment(string employeeId)
    {
        if (_activeStream == null || _activeClient == null || !_activeClient.Connected)
        {
            _logger.LogError("Cannot start TCP enrollment, no active Wi-Fi scanner connected.");
            return false;
        }

        if (IsEnrollModeActive)
        {
            _logger.LogWarning("Enrollment is already active for another employee.");
            return false;
        }

        _pendingEnrollEmployeeId = employeeId;
        
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
            return false;
        }
    }

    public void CancelEnrollment()
    {
        if (_pendingEnrollEmployeeId == null) return;
        
        _pendingEnrollEmployeeId = null;
        
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

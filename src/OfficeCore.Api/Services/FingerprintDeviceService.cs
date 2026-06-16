using System.IO.Ports;
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
    private SerialPort? _serialPort;

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
        while (!stoppingToken.IsCancellationRequested)
        {
            var portName = _configuration["FingerprintDevice:PortName"] ?? "COM3";
            var baudRate = _configuration.GetValue<int>("FingerprintDevice:BaudRate", 9600);

            try
            {
                _serialPort = new SerialPort(portName, baudRate);
                _serialPort.Open();
                _logger.LogInformation("Opened Serial Port {PortName} at {BaudRate} baud.", portName, baudRate);

                using var reader = new StreamReader(_serialPort.BaseStream);
                
                while (!stoppingToken.IsCancellationRequested && _serialPort.IsOpen)
                {
                    if (_serialPort.BytesToRead > 0)
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
                _logger.LogError("Error with Fingerprint Device on port {PortName}: {Message}. Retrying in 5 seconds...", portName, ex.Message);
                await Task.Delay(5000, stoppingToken);
            }
            finally
            {
                if (_serialPort != null && _serialPort.IsOpen)
                {
                    _serialPort.Close();
                }
            }
        }
    }

    private async Task ProcessMessageAsync(string message)
    {
        _logger.LogInformation("Received from device: {Message}", message);

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
            _logger.LogWarning("Unknown fingerprint scanned: {FingerprintId}", fingerprintId);
            return;
        }

        // Logic for ClockIn vs ClockOut (simple toggle based on today's records)
        var today = DateTimeOffset.UtcNow.Date;
        var latestRecord = await context.AttendanceRecords
            .Where(a => a.EmployeeId == mapping.EmployeeId && a.Timestamp.Date == today)
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
            Source = AttendanceSource.Device
        };

        context.AttendanceRecords.Add(record);
        await context.SaveChangesAsync();

        _logger.LogInformation("Logged {Type} for Employee {EmployeeId}", type, mapping.EmployeeId);

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
            _logger.LogWarning("Device enrolled a fingerprint ({FingerprintId}) but no employee was pending enrollment.", fingerprintId);
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Check if employee exists
        var employee = await context.Users.FindAsync(_pendingEnrollEmployeeId);
        if (employee != null)
        {
            context.EmployeeFingerprints.Add(new EmployeeFingerprint
            {
                EmployeeId = _pendingEnrollEmployeeId,
                FingerprintId = fingerprintId
            });
            await context.SaveChangesAsync();
            _logger.LogInformation("Successfully linked Fingerprint {FingerprintId} to Employee {EmployeeId}", fingerprintId, _pendingEnrollEmployeeId);
            
            var hubContext = scope.ServiceProvider.GetService<Microsoft.AspNetCore.SignalR.IHubContext<OfficeCore.Api.Hubs.AttendanceHub>>();
            if (hubContext != null)
            {
                await hubContext.Clients.All.SendAsync("ReceiveEnrollmentSuccess", _pendingEnrollEmployeeId);
            }
        }

        // Clear enrollment state
        _pendingEnrollEmployeeId = null;
    }

    public bool StartEnrollment(string employeeId)
    {
        if (_serialPort == null || !_serialPort.IsOpen)
        {
            _logger.LogError("Cannot start enrollment, serial port is not open.");
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
            _serialPort.WriteLine("ENROLL");
            _logger.LogInformation("Sent ENROLL command to device for Employee {EmployeeId}", employeeId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send ENROLL command");
            _pendingEnrollEmployeeId = null;
            return false;
        }
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OfficeCore.Api.Services;

public class BackupHostedService : BackgroundService
{
    private readonly BackupService _backupService;
    private readonly ILogger<BackupHostedService> _logger;
    private string? _lastRunDateKey;

    public BackupHostedService(BackupService backupService, ILogger<BackupHostedService> logger)
    {
        _backupService = backupService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OfficeCore Automated Backup Service initialized.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var settings = _backupService.GetSettings();
                if (settings.AutoBackupEnabled && !string.IsNullOrWhiteSpace(settings.ScheduledTime))
                {
                    var now = DateTime.Now;
                    var currentDateKey = now.ToString("yyyy-MM-dd");

                    if (TimeSpan.TryParse(settings.ScheduledTime, out var targetTime))
                    {
                        // Check if current hour and minute match target time and we haven't executed yet today
                        if (now.Hour == targetTime.Hours && now.Minute == targetTime.Minutes && _lastRunDateKey != currentDateKey)
                        {
                            _logger.LogInformation("Executing automated scheduled database backup for {Date}...", currentDateKey);
                            _lastRunDateKey = currentDateKey;

                            var result = await _backupService.CreateBackupAsync("Scheduled Automated Backup", isAutomatic: true);
                            if (result.Success)
                            {
                                _logger.LogInformation("Automated database backup completed successfully: {FileName}", result.BackupInfo?.FileName);
                            }
                            else
                            {
                                _logger.LogError("Automated database backup failed: {Message}", result.Message);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Automated Backup background worker loop.");
            }

            // Check every 30 seconds
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}

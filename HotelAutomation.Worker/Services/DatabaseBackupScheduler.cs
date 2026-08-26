namespace HotelAutomation.Worker.Services;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// BackgroundService that ensures the database is automatically backed up every day
/// between 02:00 PM and 04:00 PM IST (UTC+05:30) and stored into the 'backup_date' folder.
/// </summary>
public sealed class DatabaseBackupScheduler : BackgroundService
{
    private readonly DatabaseBackupService _backupService;
    private readonly ILogger<DatabaseBackupScheduler> _logger;
    private static readonly TimeSpan IstOffset = TimeSpan.FromHours(5.5);
    private static readonly TimeSpan WindowStartTime = new(14, 0, 0); // 02:00 PM IST
    private static readonly TimeSpan WindowEndTime   = new(16, 0, 0); // 04:00 PM IST

    private string? _lastBackedUpDateStr = null;

    public DatabaseBackupScheduler(
        DatabaseBackupService backupService,
        ILogger<DatabaseBackupScheduler> logger)
    {
        _backupService = backupService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DatabaseBackupScheduler started. Daily backup window: 02:00 PM to 04:00 PM IST -> 'backup_date' folder.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var nowUtc = DateTimeOffset.UtcNow;
                var nowIst = nowUtc.ToOffset(IstOffset);
                var todayDateStr = nowIst.ToString("yyyy-MM-dd");

                bool isInWindow = nowIst.TimeOfDay >= WindowStartTime && nowIst.TimeOfDay <= WindowEndTime;

                if (isInWindow)
                {
                    // Check if today's backup has already been performed (in-memory or on disk)
                    bool alreadyBackedUp = _lastBackedUpDateStr == todayDateStr || _backupService.HasBackupForDate(nowIst);

                    if (!alreadyBackedUp)
                    {
                        _logger.LogInformation("Current IST time {Time} is within the 02:00 PM - 04:00 PM IST window. Triggering daily database backup...", nowIst.ToString("HH:mm:ss"));

                        var result = await _backupService.CreateBackupAsync("Scheduled_02PM_to_04PM_IST", stoppingToken);
                        if (result.Success)
                        {
                            _lastBackedUpDateStr = todayDateStr;
                            _logger.LogInformation("Daily scheduled database backup succeeded: {FileName}", result.FileName);

                            #if WINDOWS
                            TrayIconService.ShowNotification(
                                "💾 Database Backup Completed",
                                $"Daily backup created in backup_date folder\n{result.FileName}");
                            #endif
                        }
                        else
                        {
                            _logger.LogError("Daily scheduled database backup failed: {Message}", result.Message);
                        }
                    }
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error in DatabaseBackupScheduler loop.");
            }

            // Check every 60 seconds for precision and resilience against system sleep / clock changes
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("DatabaseBackupScheduler stopped.");
    }
}

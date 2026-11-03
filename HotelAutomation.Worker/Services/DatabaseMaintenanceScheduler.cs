namespace HotelAutomation.Worker.Services;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HotelAutomation.Core.Interfaces;
using HotelAutomation.Data;
using HotelAutomation.Data.Repositories;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// BackgroundService that performs automatic daily database and file maintenance:
/// - Prunes SystemLogs older than 15 days
/// - Prunes ActivityLogs older than 15 days
/// - Prunes old processed OutboxMessages older than 15 days
/// - Autodeletes database backups older than 15 days from 'backup_date' folder
/// - Autodeletes rolling disk log files (.log) and crash dump files older than 15 days from 'logs' folder
/// - Runs incremental vacuum / optimizes SQLite pages to reclaim disk space
/// </summary>
public sealed class DatabaseMaintenanceScheduler : BackgroundService
{
    private readonly SqliteConnectionFactory _factory;
    private readonly SystemLogRepository _systemLogRepo;
    private readonly ActivityLogRepository _activityLogRepo;
    private readonly IOutboxRepository _outboxRepo;
    private readonly DatabaseBackupService _backupService;
    private readonly ILogger<DatabaseMaintenanceScheduler> _logger;
    private readonly string _logsDirectory;

    public DatabaseMaintenanceScheduler(
        SqliteConnectionFactory factory,
        SystemLogRepository systemLogRepo,
        ActivityLogRepository activityLogRepo,
        IOutboxRepository outboxRepo,
        DatabaseBackupService backupService,
        ILogger<DatabaseMaintenanceScheduler> logger)
    {
        _factory = factory;
        _systemLogRepo = systemLogRepo;
        _activityLogRepo = activityLogRepo;
        _outboxRepo = outboxRepo;
        _backupService = backupService;
        _logger = logger;

        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HotelSankalpRetreat");
        _logsDirectory = Path.Combine(appDataDir, "logs");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DatabaseMaintenanceScheduler started. Automatic DB & backup cleanup scheduled daily.");

        // Run an initial quick cleanup 30 seconds after startup
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            await RunCleanupCycleAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Initial database maintenance cycle failed.");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Wait 24 hours between routine cleanups
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
                await RunCleanupCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during automated database maintenance cycle.");
            }
        }
    }

    private async Task RunCleanupCycleAsync(CancellationToken ct)
    {
        _logger.LogInformation("Starting daily auto-pruning cycle (15-day retention policy)...");

        try
        {
            // 1. Prune SystemLogs older than 15 days
            var deletedSystemLogs = await _systemLogRepo.ClearLogsAsync(olderThanDays: 15, ct);
            if (deletedSystemLogs > 0)
                _logger.LogInformation("Auto-pruned {Count} old system log entries (older than 15 days).", deletedSystemLogs);

            // 2. Prune ActivityLog older than 15 days
            var deletedActivityLogs = await _activityLogRepo.ClearLogsAsync(olderThanDays: 15, ct);
            if (deletedActivityLogs > 0)
                _logger.LogInformation("Auto-pruned {Count} old activity log entries (older than 15 days).", deletedActivityLogs);

            // 3. Prune sent/delivered/failed OutboxMessages older than 15 days
            var deletedOutbox = await _outboxRepo.ClearOldMessagesAsync(olderThanDays: 15, ct);
            if (deletedOutbox > 0)
                _logger.LogInformation("Auto-pruned {Count} old outbox messages (older than 15 days).", deletedOutbox);

            // 4. Autodelete database backups in 'backup_date' folder older than 15 days
            CleanupOldBackups(olderThanDays: 15);

            // 5. Autodelete rolling disk log files (.log) and crash dumps older than 15 days
            CleanupOldLogFiles(olderThanDays: 15);

            // 6. Reclaim free pages incrementally in SQLite
            await using var conn = await _factory.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA incremental_vacuum(500); PRAGMA optimize;";
            await cmd.ExecuteNonQueryAsync(ct);

            _logger.LogInformation("Database & backup maintenance cycle completed successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database maintenance cycle encountered an error.");
        }
    }

    private void CleanupOldBackups(int olderThanDays)
    {
        try
        {
            var cutoff = DateTimeOffset.UtcNow.AddDays(-olderThanDays);
            var backups = _backupService.GetAllBackups();
            int deleted = 0;

            foreach (var backup in backups)
            {
                if (backup.CreatedAtUtc < cutoff)
                {
                    if (_backupService.DeleteBackup(backup.FileName))
                    {
                        deleted++;
                    }
                }
            }

            if (deleted > 0)
            {
                _logger.LogInformation("Auto-deleted {Count} database backup file(s) older than {Days} days.", deleted, olderThanDays);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup old backups.");
        }
    }

    private void CleanupOldLogFiles(int olderThanDays)
    {
        try
        {
            if (!Directory.Exists(_logsDirectory)) return;

            var cutoff = DateTime.UtcNow.AddDays(-olderThanDays);
            var files = Directory.GetFiles(_logsDirectory);
            int deleted = 0;

            foreach (var file in files)
            {
                var fi = new FileInfo(file);
                if (fi.LastWriteTimeUtc < cutoff)
                {
                    try
                    {
                        fi.Delete();
                        deleted++;
                    }
                    catch { /* File might be locked by current logger */ }
                }
            }

            if (deleted > 0)
            {
                _logger.LogInformation("Auto-deleted {Count} disk log file(s) older than {Days} days.", deleted, olderThanDays);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup old disk log files.");
        }
    }
}

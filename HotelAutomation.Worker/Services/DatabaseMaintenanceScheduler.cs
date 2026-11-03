namespace HotelAutomation.Worker.Services;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using HotelAutomation.Core.Interfaces;
using HotelAutomation.Data;
using HotelAutomation.Data.Repositories;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// BackgroundService that performs automatic daily database and file maintenance:
/// - Prunes SystemLogs older than 15 days
/// - Prunes ActivityLogs older than 15 days
/// - Prunes sent/delivered/read/failed OutboxMessages older than 15 days
/// - Prunes PMS/PDF EventLog records older than 15 days
/// - Prunes Processed/Cancelled ScheduledJobs older than 15 days
/// - Prunes Sent ContactCampaignProgress rows older than 15 days
/// - Prunes ConversationReplies older than 15 days
/// - Autodeletes database backups older than 15 days from 'backup_date' folder
/// - Autodeletes rolling disk log files (.log) and crash dumps older than 15 days
/// - Autodeletes guest media in uploads/inbox older than 15 days
/// - Autodeletes temporary printer bridge PDFs (bill_temp_*, recipient_temp_*, supplier_temp_*)
/// - Loops PRAGMA incremental_vacuum to reclaim all unused SQLite database pages
/// </summary>
public sealed class DatabaseMaintenanceScheduler : BackgroundService
{
    private readonly SqliteConnectionFactory _factory;
    private readonly SystemLogRepository _systemLogRepo;
    private readonly ActivityLogRepository _activityLogRepo;
    private readonly IOutboxRepository _outboxRepo;
    private readonly EventLogRepository _eventLogRepo;
    private readonly JobRepository _jobRepo;
    private readonly ICampaignRepository _campaignRepo;
    private readonly DatabaseBackupService _backupService;
    private readonly ILogger<DatabaseMaintenanceScheduler> _logger;
    private readonly string _logsDirectory;
    private readonly string _appDataUploadsDir;

    public DatabaseMaintenanceScheduler(
        SqliteConnectionFactory factory,
        SystemLogRepository systemLogRepo,
        ActivityLogRepository activityLogRepo,
        IOutboxRepository outboxRepo,
        EventLogRepository eventLogRepo,
        JobRepository jobRepo,
        ICampaignRepository campaignRepo,
        DatabaseBackupService backupService,
        ILogger<DatabaseMaintenanceScheduler> logger)
    {
        _factory = factory;
        _systemLogRepo = systemLogRepo;
        _activityLogRepo = activityLogRepo;
        _outboxRepo = outboxRepo;
        _eventLogRepo = eventLogRepo;
        _jobRepo = jobRepo;
        _campaignRepo = campaignRepo;
        _backupService = backupService;
        _logger = logger;

        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HotelSankalpRetreat");
        _logsDirectory = Path.Combine(appDataDir, "logs");
        _appDataUploadsDir = Path.Combine(appDataDir, "uploads");
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

            // 4. Prune PMS/PDF EventLog older than 15 days
            var deletedEvents = await _eventLogRepo.ClearOldLogsAsync(olderThanDays: 15, ct);
            if (deletedEvents > 0)
                _logger.LogInformation("Auto-pruned {Count} old PMS/PDF event log entries (older than 15 days).", deletedEvents);

            // 5. Prune Processed/Cancelled ScheduledJobs older than 15 days
            var deletedJobs = await _jobRepo.ClearOldJobsAsync(olderThanDays: 15, ct);
            if (deletedJobs > 0)
                _logger.LogInformation("Auto-pruned {Count} old processed/cancelled scheduled jobs (older than 15 days).", deletedJobs);

            // 6. Prune Sent ContactCampaignProgress older than 15 days
            var deletedProgress = await _campaignRepo.ClearOldProgressAsync(olderThanDays: 15, ct);
            if (deletedProgress > 0)
                _logger.LogInformation("Auto-pruned {Count} old campaign progress records (older than 15 days).", deletedProgress);

            // 7. Prune ConversationReplies older than 15 days
            await PruneConversationRepliesAsync(olderThanDays: 15, ct);

            // 8. Autodelete database backups in 'backup_date' folder older than 15 days
            CleanupOldBackups(olderThanDays: 15);

            // 9. Autodelete rolling disk log files (.log) and crash dumps older than 15 days
            CleanupOldLogFiles(olderThanDays: 15);

            // 10. Autodelete inbox media uploads older than 15 days
            CleanupInboxMedia(olderThanDays: 15);

            // 11. Autodelete temporary printer bridge PDFs
            CleanupTempPdfFiles(olderThanDays: 2);

            // 12. Reclaim free pages incrementally in SQLite until freelist is exhausted
            await CompactDatabasePagesAsync(ct);

            _logger.LogInformation("Database & disk maintenance cycle completed successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database maintenance cycle encountered an error.");
        }
    }

    private async Task PruneConversationRepliesAsync(int olderThanDays, CancellationToken ct)
    {
        try
        {
            await using var conn = await _factory.OpenAsync(ct);
            var cutoff = DateTimeOffset.UtcNow.AddDays(-olderThanDays).ToString("O");
            var deleted = await conn.ExecuteAsync(
                "DELETE FROM ConversationReplies WHERE SentAt < @Cutoff;",
                new { Cutoff = cutoff });
            if (deleted > 0)
                _logger.LogInformation("Auto-pruned {Count} old conversation replies (older than 15 days).", deleted);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "ConversationReplies cleanup skipped or table does not exist.");
        }
    }

    private async Task CompactDatabasePagesAsync(CancellationToken ct)
    {
        try
        {
            await using var conn = await _factory.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();

            cmd.CommandText = "PRAGMA freelist_count;";
            var freelistObj = await cmd.ExecuteScalarAsync(ct);
            var freelistCount = Convert.ToInt32(freelistObj);

            if (freelistCount > 0)
            {
                _logger.LogInformation("Reclaiming {Count} unused database pages via incremental vacuum...", freelistCount);
                int safety = 50;
                while (freelistCount > 0 && safety-- > 0)
                {
                    cmd.CommandText = "PRAGMA incremental_vacuum(1000);";
                    await cmd.ExecuteNonQueryAsync(ct);

                    cmd.CommandText = "PRAGMA freelist_count;";
                    var nextCount = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
                    if (nextCount >= freelistCount) break; // Not incremental vacuum or pages can't be released
                    freelistCount = nextCount;
                }
            }

            cmd.CommandText = "PRAGMA optimize;";
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to compact database pages during maintenance.");
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

    private void CleanupInboxMedia(int olderThanDays)
    {
        var cutoff = DateTime.UtcNow.AddDays(-olderThanDays);
        var searchDirs = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "wwwroot", "uploads", "inbox"),
            Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "inbox"),
            Path.Combine(_appDataUploadsDir, "inbox"),
            _appDataUploadsDir
        };

        int deleted = 0;
        foreach (var dir in searchDirs)
        {
            try
            {
                if (!Directory.Exists(dir)) continue;

                foreach (var file in Directory.GetFiles(dir))
                {
                    var fi = new FileInfo(file);
                    if (fi.LastWriteTimeUtc < cutoff)
                    {
                        try
                        {
                            fi.Delete();
                            deleted++;
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        if (deleted > 0)
        {
            _logger.LogInformation("Auto-deleted {Count} inbox media file(s) older than {Days} days.", deleted, olderThanDays);
        }
    }

    private void CleanupTempPdfFiles(int olderThanDays)
    {
        var cutoff = DateTime.UtcNow.AddDays(-olderThanDays);
        var baseDir = AppContext.BaseDirectory;
        var searchDirs = new[]
        {
            Path.Combine(baseDir, "PDFs"),
            Path.Combine(baseDir, "PDFs", "Processed"),
            @"C:\HotelPrints\Output",
            @"C:\HotelPrints\Processed"
        };

        int deleted = 0;
        foreach (var dir in searchDirs)
        {
            try
            {
                if (!Directory.Exists(dir)) continue;

                foreach (var file in Directory.GetFiles(dir))
                {
                    var name = Path.GetFileName(file);
                    if (name.StartsWith("bill_temp_", StringComparison.OrdinalIgnoreCase) ||
                        name.StartsWith("recipient_temp_", StringComparison.OrdinalIgnoreCase) ||
                        name.StartsWith("supplier_temp_", StringComparison.OrdinalIgnoreCase) ||
                        name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
                    {
                        var fi = new FileInfo(file);
                        if (fi.LastWriteTimeUtc < cutoff)
                        {
                            try
                            {
                                fi.Delete();
                                deleted++;
                            }
                            catch { }
                        }
                    }
                }
            }
            catch { }
        }

        if (deleted > 0)
        {
            _logger.LogInformation("Auto-deleted {Count} temporary printer bridge PDF file(s).", deleted);
        }
    }
}

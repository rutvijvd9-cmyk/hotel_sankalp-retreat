namespace HotelAutomation.Worker.Services;

using System;
using System.Threading;
using System.Threading.Tasks;
using HotelAutomation.Core.Interfaces;
using HotelAutomation.Data;
using HotelAutomation.Data.Repositories;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// BackgroundService that performs automatic daily database maintenance:
/// - Prunes SystemLogs older than 14 days
/// - Prunes ActivityLogs older than 90 days
/// - Prunes old processed OutboxMessages older than 90 days
/// - Runs incremental vacuum / optimizes SQLite pages to reclaim disk space
/// </summary>
public sealed class DatabaseMaintenanceScheduler : BackgroundService
{
    private readonly SqliteConnectionFactory _factory;
    private readonly SystemLogRepository _systemLogRepo;
    private readonly ActivityLogRepository _activityLogRepo;
    private readonly IOutboxRepository _outboxRepo;
    private readonly ILogger<DatabaseMaintenanceScheduler> _logger;

    public DatabaseMaintenanceScheduler(
        SqliteConnectionFactory factory,
        SystemLogRepository systemLogRepo,
        ActivityLogRepository activityLogRepo,
        IOutboxRepository outboxRepo,
        ILogger<DatabaseMaintenanceScheduler> logger)
    {
        _factory = factory;
        _systemLogRepo = systemLogRepo;
        _activityLogRepo = activityLogRepo;
        _outboxRepo = outboxRepo;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DatabaseMaintenanceScheduler started. Automatic DB cleanup scheduled daily.");

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
        _logger.LogInformation("Starting daily database auto-pruning cycle...");

        try
        {
            // 1. Prune SystemLogs older than 14 days
            var deletedSystemLogs = await _systemLogRepo.ClearLogsAsync(olderThanDays: 14, ct);
            if (deletedSystemLogs > 0)
                _logger.LogInformation("Auto-pruned {Count} old system log entries (older than 14 days).", deletedSystemLogs);

            // 2. Prune ActivityLog older than 90 days
            var deletedActivityLogs = await _activityLogRepo.ClearLogsAsync(olderThanDays: 90, ct);
            if (deletedActivityLogs > 0)
                _logger.LogInformation("Auto-pruned {Count} old activity log entries (older than 90 days).", deletedActivityLogs);

            // 3. Prune sent/delivered/failed OutboxMessages older than 90 days
            var deletedOutbox = await _outboxRepo.ClearOldMessagesAsync(olderThanDays: 90, ct);
            if (deletedOutbox > 0)
                _logger.LogInformation("Auto-pruned {Count} old outbox messages (older than 90 days).", deletedOutbox);

            // 4. Reclaim free pages incrementally in SQLite
            await using var conn = await _factory.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA incremental_vacuum(500); PRAGMA optimize;";
            await cmd.ExecuteNonQueryAsync(ct);

            _logger.LogInformation("Database maintenance cycle completed successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database maintenance cycle encountered an error.");
        }
    }
}

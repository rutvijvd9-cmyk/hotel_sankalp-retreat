namespace HotelAutomation.Worker.Services;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Background worker that automatically synchronizes WhatsApp Message Templates from Meta WABA.
/// Runs once immediately at startup (to repair any stale seed-DB data) and then again
/// every day at 09:00 AM IST (Indian Standard Time, UTC+05:30).
/// </summary>
public sealed class TemplateSyncScheduler : BackgroundService
{
    private readonly TemplateSyncService _syncService;
    private readonly ILogger<TemplateSyncScheduler> _logger;
    private static readonly TimeSpan IstOffset = TimeSpan.FromHours(5.5);

    // Small delay before the startup sync so all services are fully initialized
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(10);

    public TemplateSyncScheduler(
        TemplateSyncService syncService,
        ILogger<TemplateSyncScheduler> logger)
    {
        _syncService = syncService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TemplateSyncScheduler started. Startup sync in {Delay}s, then daily at 09:00 AM IST.", StartupDelay.TotalSeconds);

        // ── Startup sync ──────────────────────────────────────────────────────────
        // Runs shortly after boot so that any stale template names coming from the
        // bundled seed database are repaired before the first real checkout fires.
        try
        {
            await Task.Delay(StartupDelay, stoppingToken);
        }
        catch (OperationCanceledException) { return; }

        await RunSyncAsync("Startup", stoppingToken);

        // ── Daily sync at 09:00 AM IST ────────────────────────────────────────────
        while (!stoppingToken.IsCancellationRequested)
        {
            var nowUtc  = DateTimeOffset.UtcNow;
            var nowIst  = nowUtc.ToOffset(IstOffset);

            // Calculate next 09:00:00 AM IST
            var targetIstToday = new DateTimeOffset(nowIst.Year, nowIst.Month, nowIst.Day, 9, 0, 0, IstOffset);
            var nextRunIst     = nowIst < targetIstToday ? targetIstToday : targetIstToday.AddDays(1);

            var delay = nextRunIst - nowIst;
            _logger.LogInformation(
                "Next automatic WhatsApp template sync scheduled at {NextRun} IST (in {Hours:F1} hours / {Minutes:F0} minutes)",
                nextRunIst.ToString("yyyy-MM-dd HH:mm:ss"),
                delay.TotalHours,
                delay.TotalMinutes);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) { break; }

            if (stoppingToken.IsCancellationRequested) break;

            _logger.LogInformation("Triggering scheduled daily WhatsApp template sync at 09:00 AM IST...");
            await RunSyncAsync("AutoScheduler", stoppingToken);

            // Sleep 30 seconds to prevent double firing if execution is near-instant
            try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task RunSyncAsync(string trigger, CancellationToken ct)
    {
        try
        {
            var (success, count, message) = await _syncService.SyncTemplatesAsync(trigger, ct);
            _logger.LogInformation(
                "Template sync [{Trigger}] completed. Success={Success}, Added={Added}, Message={Msg}",
                trigger, success, count, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during template sync [{Trigger}].", trigger);
        }
    }
}

namespace HotelAutomation.Worker.Services;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Background worker that automatically synchronizes WhatsApp Message Templates from Meta WABA
/// everyday at 09:00 AM IST (Indian Standard Time, UTC+05:30).
/// </summary>
public sealed class TemplateSyncScheduler : BackgroundService
{
    private readonly TemplateSyncService _syncService;
    private readonly ILogger<TemplateSyncScheduler> _logger;
    private static readonly TimeSpan IstOffset = TimeSpan.FromHours(5.5);

    public TemplateSyncScheduler(
        TemplateSyncService syncService,
        ILogger<TemplateSyncScheduler> logger)
    {
        _syncService = syncService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TemplateSyncScheduler started. Daily sync scheduled for 09:00 AM IST.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var nowUtc = DateTimeOffset.UtcNow;
            var nowIst = nowUtc.ToOffset(IstOffset);

            // Calculate target 09:00:00 AM IST today
            var targetIstToday = new DateTimeOffset(nowIst.Year, nowIst.Month, nowIst.Day, 9, 0, 0, IstOffset);
            var nextRunIst = nowIst < targetIstToday 
                ? targetIstToday 
                : targetIstToday.AddDays(1);

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
            catch (OperationCanceledException)
            {
                break;
            }

            if (stoppingToken.IsCancellationRequested) break;

            _logger.LogInformation("Triggering scheduled daily WhatsApp template sync at 09:00 AM IST...");
            try
            {
                var (success, count, message) = await _syncService.SyncTemplatesAsync("AutoScheduler", stoppingToken);
                _logger.LogInformation("Daily template sync completed. Success={Success}, Added={Added}, Message={Msg}", success, count, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during scheduled daily WhatsApp template sync.");
            }

            // Sleep 30 seconds to prevent double firing if execution is instantaneous
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}

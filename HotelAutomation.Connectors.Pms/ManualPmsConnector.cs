namespace HotelAutomation.Connectors.Pms;

using HotelAutomation.Core.Interfaces;
using HotelAutomation.Core.Models;
using Microsoft.Extensions.Logging;

/// <summary>
/// Stand-in IPmsEventSource. Exposes RaiseTestEvent() so tests and a future UI
/// can inject events without a real PMS connection.
/// Adding a real PMS connector later = new class, zero edits here (Open/Closed Principle).
/// </summary>
public sealed class ManualPmsConnector : IPmsEventSource
{
    private readonly ILogger<ManualPmsConnector> _logger;

    public ManualPmsConnector(ILogger<ManualPmsConnector> logger) => _logger = logger;

    public event EventHandler<PmsEvent>? EventReceived;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("ManualPmsConnector started. Use RaiseTestEvent() to inject events.");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("ManualPmsConnector stopped.");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Call this to fire a test hotel event into the pipeline.
    /// </summary>
    public void RaiseTestEvent(PmsEvent pmsEvent)
    {
        _logger.LogInformation("Test event raised: {EventType} for {GuestName}",
            pmsEvent.EventType, pmsEvent.GuestName);
        EventReceived?.Invoke(this, pmsEvent);
    }
}

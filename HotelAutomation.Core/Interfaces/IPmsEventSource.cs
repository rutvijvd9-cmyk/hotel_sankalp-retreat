namespace HotelAutomation.Core.Interfaces;

using HotelAutomation.Core.Models;

/// <summary>
/// Contract every PMS connector must implement.
/// The Worker subscribes to EventReceived to be notified of hotel events.
/// </summary>
public interface IPmsEventSource
{
    event EventHandler<PmsEvent> EventReceived;
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}

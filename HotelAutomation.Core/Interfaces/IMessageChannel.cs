namespace HotelAutomation.Core.Interfaces;

using HotelAutomation.Core.Models;

/// <summary>
/// Contract every message-sending connector must implement (WhatsApp, SMS, Email…).
/// Returns true on success, false on transient failure (OutboxProcessor will retry).
/// </summary>
public interface IMessageChannel
{
    Task<bool> SendAsync(OutboxMessage message, CancellationToken cancellationToken);
}

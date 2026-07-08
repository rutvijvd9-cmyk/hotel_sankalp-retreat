namespace HotelAutomation.Core.Interfaces;

using HotelAutomation.Core.Models;

public interface IOutboxRepository
{
    Task<int> AddAsync(OutboxMessage message, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(int limit, CancellationToken cancellationToken = default);
    Task UpdateStatusAsync(int id, string status, int retryCount, DateTimeOffset? sentAt, string? externalMessageId,
        string? failureReason = null, CancellationToken cancellationToken = default);
}

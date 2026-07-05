namespace HotelAutomation.Worker;

using HotelAutomation.Core.Interfaces;
using Microsoft.Extensions.Options;

/// <summary>
/// BackgroundService: polls the OutboxMessages table every N seconds,
/// calls IMessageChannel.SendAsync, and updates status.
/// This loop ensures no message is ever permanently lost.
/// </summary>
public sealed class OutboxProcessor : BackgroundService
{
    private readonly IOutboxRepository _outboxRepository;
    private readonly IMessageChannel _messageChannel;
    private readonly ILogger<OutboxProcessor> _logger;
    private readonly int _intervalSeconds;
    private readonly int _batchSize;
    private readonly int _maxRetries;

    public OutboxProcessor(
        IOutboxRepository outboxRepository,
        IMessageChannel messageChannel,
        IConfiguration configuration,
        ILogger<OutboxProcessor> logger)
    {
        _outboxRepository = outboxRepository;
        _messageChannel = messageChannel;
        _logger = logger;
        _intervalSeconds = configuration.GetValue("OutboxProcessor:IntervalSeconds", 5);
        _batchSize = configuration.GetValue("OutboxProcessor:BatchSize", 10);
        _maxRetries = configuration.GetValue("OutboxProcessor:MaxRetries", 5);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxProcessor started. Polling every {Interval}s.", _intervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessBatchAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(_intervalSeconds), stoppingToken)
                .ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        var pendingMessages = await _outboxRepository.GetPendingAsync(_batchSize, ct);

        foreach (var message in pendingMessages)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                var success = await _messageChannel.SendAsync(message, ct);

                if (success)
                {
                    await _outboxRepository.UpdateStatusAsync(
                        message.Id, "Sent", message.RetryCount, DateTimeOffset.UtcNow, ct);
                    _logger.LogInformation("OutboxMessage {Id} → Sent", message.Id);
                }
                else
                {
                    var newRetryCount = message.RetryCount + 1;
                    var newStatus = newRetryCount >= _maxRetries ? "Failed" : "Pending";

                    await _outboxRepository.UpdateStatusAsync(
                        message.Id, newStatus, newRetryCount, null, ct);

                    _logger.LogWarning("OutboxMessage {Id} send failed. RetryCount={Retry}, Status={Status}",
                        message.Id, newRetryCount, newStatus);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing OutboxMessage {Id}", message.Id);
            }
        }
    }
}

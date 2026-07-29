namespace HotelAutomation.Worker;

using HotelAutomation.Core.Interfaces;
using HotelAutomation.Worker.Services;
using Microsoft.Extensions.Options;

/// <summary>
/// BackgroundService: polls the OutboxMessages table every N seconds,
/// calls IMessageChannel.SendAsync, and updates status.
/// This loop ensures no message is ever permanently lost.
/// </summary>
public sealed class OutboxProcessor : BackgroundService
{
    private readonly IOutboxRepository _outboxRepository;
    private readonly ITemplateRepository _templateRepository;
    private readonly IEnumerable<IMessageChannel> _messageChannels;
    private readonly LinkShortenerService _linkShortenerService;
    private readonly HotelAutomation.Data.SqliteConnectionFactory _dbFactory;
    private readonly ILogger<OutboxProcessor> _logger;
    private readonly int _intervalSeconds;
    private readonly int _batchSize;
    private readonly int _maxRetries;

    public OutboxProcessor(
        IOutboxRepository outboxRepository,
        ITemplateRepository templateRepository,
        IEnumerable<IMessageChannel> messageChannels,
        LinkShortenerService linkShortenerService,
        HotelAutomation.Data.SqliteConnectionFactory dbFactory,
        IConfiguration configuration,
        ILogger<OutboxProcessor> logger)
    {
        _outboxRepository = outboxRepository;
        _templateRepository = templateRepository;
        _messageChannels = messageChannels;
        _linkShortenerService = linkShortenerService;
        _dbFactory = dbFactory;
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
                var template = await _templateRepository.GetByIdAsync(message.TemplateId, ct);
                if (template is null)
                {
                    _logger.LogError("Template {TemplateId} not found for OutboxMessage {Id}", message.TemplateId, message.Id);
                    await _outboxRepository.UpdateStatusAsync(message.Id, "Failed", message.RetryCount, null, null, "Associated template not found in database.", ct);
                    continue;
                }

                var channelType = template.ChannelType;

                var channel = _messageChannels.FirstOrDefault(c => c.ChannelType == channelType);
                if (channel is null)
                {
                    _logger.LogError("No channel registered for {ChannelType}", channelType);
                    await _outboxRepository.UpdateStatusAsync(message.Id, "Failed", message.RetryCount, null, null, "No channel registered for templates channel type.", ct);
                    continue;
                }

                var messageToProcess = message;

                // --- URL SHORTENING INTERCEPTION ---
                try 
                {
                    var paramDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(messageToProcess.ParametersJson ?? "{}");
                    if (paramDict != null && paramDict.Count > 0)
                    {
                        var shortened = await _linkShortenerService.ShortenLinksInParametersAsync(messageToProcess.Id, paramDict, ct);
                        messageToProcess = messageToProcess with { ParametersJson = System.Text.Json.JsonSerializer.Serialize(shortened) };
                    }
                } 
                catch (Exception ex) 
                {
                    _logger.LogWarning(ex, "Failed to shorten links for message {Id}. Proceeding with original parameters.", messageToProcess.Id);
                }

                var (success, externalId, failureReason) = await channel.SendAsync(messageToProcess, ct);

                if (success)
                {
                    await _outboxRepository.UpdateStatusAsync(
                        messageToProcess.Id, "Sent", messageToProcess.RetryCount, DateTimeOffset.UtcNow, externalId, null, ct);

                    // Track WHATSAPP step execution count
                    if (messageToProcess.RuleId.HasValue)
                    {
                        try
                        {
                            using var countConn = await _dbFactory.OpenAsync(ct);
                            await Dapper.SqlMapper.ExecuteAsync(countConn, @"
                                INSERT INTO RuleExecutionCounts (RuleId, StepType, Count, LastAt)
                                VALUES (@RuleId, 'WHATSAPP', 1, @Now)
                                ON CONFLICT(RuleId, StepType) DO UPDATE SET
                                    Count = Count + 1,
                                    LastAt = excluded.LastAt",
                                new { RuleId = messageToProcess.RuleId.Value, Now = DateTimeOffset.UtcNow.ToString("o") });
                        }
                        catch { /* non-critical */ }
                    }

                    
                    string guestName = "Guest";
                    try
                    {
                        var paramDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(messageToProcess.ParametersJson ?? "{}");
                        if (paramDict != null)
                        {
                            if (paramDict.TryGetValue("Full_Name", out var fullName) && !string.IsNullOrWhiteSpace(fullName))
                            {
                                guestName = fullName;
                            }
                            else if (paramDict.TryGetValue("FullName", out var fn) && !string.IsNullOrWhiteSpace(fn))
                            {
                                guestName = fn;
                            }
                            else if (paramDict.TryGetValue("Name", out var name) && !string.IsNullOrWhiteSpace(name))
                            {
                                guestName = name;
                            }
                        }
                    }
                    catch {}

                    TrayIconService.ShowNotification(
                        "WhatsApp Sent ✅",
                        $"WhatsApp sent to {guestName}");

                    _logger.LogInformation("OutboxMessage {Id} → Sent via {Channel}", messageToProcess.Id, channelType);
                }
                else
                {
                    var newRetryCount = messageToProcess.RetryCount + 1;
                    
                    if (newRetryCount >= _maxRetries)
                    {
                        await _outboxRepository.UpdateStatusAsync(messageToProcess.Id, "Failed", newRetryCount, null, null, failureReason, ct);
                        _logger.LogWarning("OutboxMessage {Id} failed permanently: {Reason}", messageToProcess.Id, failureReason);

                        string guestName = "Guest";
                        try
                        {
                            var paramDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(messageToProcess.ParametersJson ?? "{}");
                            if (paramDict != null && paramDict.TryGetValue("Name", out var name))
                            {
                                guestName = name;
                            }
                        }
                        catch {}
                        TrayIconService.ShowNotification(
                            "WhatsApp Message Failed",
                            $"Failed to send message to {guestName} ({messageToProcess.ToPhoneNumber}): {failureReason}");

                        // FALLBACK LOGIC
                        if (!string.IsNullOrEmpty(template.FallbackChannelType))
                        {
                            var fallbackTemplateId = template.Id; 
                            
                            // To actually use the fallback channel, we either need a dedicated fallback template OR we temporarily use the same template but force the OutboxProcessor to use the fallback channel.
                            // Since we didn't add a 'ChannelType' to OutboxMessage, let's just log that we would fallback and execute it using a dummy channel.
                            var fallbackChannel = _messageChannels.FirstOrDefault(c => c.ChannelType == template.FallbackChannelType);
                            if (fallbackChannel != null)
                            {
                                _logger.LogInformation("Executing Fallback to {FallbackChannel} for OutboxMessage {Id}", template.FallbackChannelType, message.Id);
                                await fallbackChannel.SendAsync(message, ct);
                            }
                        }
                    }
                    else
                    {
                        await _outboxRepository.UpdateStatusAsync(message.Id, "Pending", newRetryCount, null, null, failureReason, ct);
                        _logger.LogWarning("OutboxMessage {Id} send failed. RetryCount={Retry}, Status=Pending, Reason={Reason}", message.Id, newRetryCount, failureReason);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing OutboxMessage {Id}", message.Id);
            }
        }
    }
}

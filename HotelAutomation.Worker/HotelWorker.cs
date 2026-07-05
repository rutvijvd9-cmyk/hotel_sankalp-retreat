namespace HotelAutomation.Worker;

using HotelAutomation.Core.Engine;
using HotelAutomation.Core.Interfaces;
using HotelAutomation.Core.Models;
using HotelAutomation.Data.Repositories;

/// <summary>
/// BackgroundService: subscribes to IPmsEventSource, matches rules, writes OutboxMessages.
/// </summary>
public sealed class HotelWorker : BackgroundService
{
    private readonly IPmsEventSource _pmsSource;
    private readonly IRuleRepository _ruleRepository;
    private readonly ITemplateRepository _templateRepository;
    private readonly IOutboxRepository _outboxRepository;
    private readonly EventLogRepository _eventLog;
    private readonly ILogger<HotelWorker> _logger;

    public HotelWorker(
        IPmsEventSource pmsSource,
        IRuleRepository ruleRepository,
        ITemplateRepository templateRepository,
        IOutboxRepository outboxRepository,
        EventLogRepository eventLog,
        ILogger<HotelWorker> logger)
    {
        _pmsSource = pmsSource;
        _ruleRepository = ruleRepository;
        _templateRepository = templateRepository;
        _outboxRepository = outboxRepository;
        _eventLog = eventLog;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _pmsSource.EventReceived += OnPmsEventReceived;
        await _pmsSource.StartAsync(stoppingToken);

        _logger.LogInformation("HotelWorker running. Waiting for PMS events...");
        await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

        _pmsSource.EventReceived -= OnPmsEventReceived;
        await _pmsSource.StopAsync(CancellationToken.None);
    }

    private async void OnPmsEventReceived(object? sender, PmsEvent pmsEvent)
    {
        try
        {
            _logger.LogInformation("Event received: {EventType} for {Guest}",
                pmsEvent.EventType, pmsEvent.GuestName);

            var rules = await _ruleRepository.GetEnabledRulesForEventTypeAsync(pmsEvent.EventType);
            var templates = await _templateRepository.GetAllAsync();
            var matchedTemplate = AutomationRuleMatcher.Match(pmsEvent, rules, templates);

            if (matchedTemplate is null)
            {
                _logger.LogWarning("No matching rule for event type: {EventType}", pmsEvent.EventType);
                await _eventLog.LogAsync(pmsEvent);
                return;
            }

            // Build parameters for the template (guest name, room, amount)
            var parameters = new List<string> { pmsEvent.GuestName, pmsEvent.RoomNumber };
            if (pmsEvent.Amount.HasValue)
                parameters.Add(pmsEvent.Amount.Value.ToString("F2"));

            var outboxMessage = new OutboxMessage
            {
                ToPhoneNumber = pmsEvent.PhoneNumber,
                TemplateId = matchedTemplate.Id,
                ParametersJson = System.Text.Json.JsonSerializer.Serialize(parameters),
                Status = "Pending",
                CreatedAt = DateTimeOffset.UtcNow
            };

            var outboxId = await _outboxRepository.AddAsync(outboxMessage);
            await _eventLog.LogAsync(pmsEvent, outboxId);

            _logger.LogInformation("OutboxMessage {Id} queued for {Phone}", outboxId, pmsEvent.PhoneNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling PmsEvent {EventType}", pmsEvent.EventType);
        }
    }
}

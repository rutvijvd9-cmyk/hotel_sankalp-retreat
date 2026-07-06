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
    private readonly JobRepository _jobRepository;
    private readonly ILogger<HotelWorker> _logger;

    public HotelWorker(
        IPmsEventSource pmsSource,
        IRuleRepository ruleRepository,
        ITemplateRepository templateRepository,
        IOutboxRepository outboxRepository,
        EventLogRepository eventLog,
        JobRepository jobRepository,
        ILogger<HotelWorker> logger)
    {
        _pmsSource = pmsSource;
        _ruleRepository = ruleRepository;
        _templateRepository = templateRepository;
        _outboxRepository = outboxRepository;
        _eventLog = eventLog;
        _jobRepository = jobRepository;
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
            var match = AutomationRuleMatcher.Match(pmsEvent, rules, templates);

            if (match is null)
            {
                _logger.LogWarning("No matching rule for event type: {EventType}", pmsEvent.EventType);
                await _eventLog.LogAsync(pmsEvent);
                return;
            }

            var (matchedTemplate, matchedRule) = match.Value;

            // Enqueue into ScheduledJobs to support Drip Campaigns (Phase 1)
            var job = new ScheduledJob
            {
                GuestName = pmsEvent.GuestName,
                PhoneNumber = pmsEvent.PhoneNumber,
                TemplateId = matchedTemplate.Id,
                ParametersJson = System.Text.Json.JsonSerializer.Serialize(new 
                { 
                    Name = pmsEvent.GuestName, 
                    Room = pmsEvent.RoomNumber, 
                    Bill = pmsEvent.Amount 
                }),
                TriggerTime = DateTimeOffset.UtcNow.AddMinutes(matchedRule.DelayMinutes),
                Status = "Pending"
            };

            await _jobRepository.AddAsync(job);
            
            await _eventLog.LogAsync(pmsEvent, null); // We don't have OutboxMessageId yet

            _logger.LogInformation("ScheduledJob {Id} queued for {Phone} to execute in {Delay} minutes", job.Id, pmsEvent.PhoneNumber, matchedRule.DelayMinutes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling PmsEvent {EventType}", pmsEvent.EventType);
        }
    }
}

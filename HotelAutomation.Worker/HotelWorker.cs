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
    private readonly UnsubscribedContactRepository _unsubRepo;
    private readonly ILogger<HotelWorker> _logger;

    public HotelWorker(
        IPmsEventSource pmsSource,
        IRuleRepository ruleRepository,
        ITemplateRepository templateRepository,
        IOutboxRepository outboxRepository,
        EventLogRepository eventLog,
        JobRepository jobRepository,
        UnsubscribedContactRepository unsubRepo,
        ILogger<HotelWorker> logger)
    {
        _pmsSource = pmsSource;
        _ruleRepository = ruleRepository;
        _templateRepository = templateRepository;
        _outboxRepository = outboxRepository;
        _eventLog = eventLog;
        _jobRepository = jobRepository;
        _unsubRepo = unsubRepo;
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

            // Stop Marketing messages if the user is unsubscribed
            if (matchedTemplate.Category == "Marketing")
            {
                var isUnsub = await _unsubRepo.IsUnsubscribedAsync(pmsEvent.PhoneNumber);
                if (isUnsub)
                {
                    _logger.LogInformation("Skipping marketing automation {RuleId} for {Phone} because user is unsubscribed.", matchedRule.Id, pmsEvent.PhoneNumber);
                    return;
                }
            }

            // Enqueue into ScheduledJobs to support Drip Campaigns (Phase 1)
            var job = new ScheduledJob
            {
                GuestName = pmsEvent.GuestName,
                PhoneNumber = pmsEvent.PhoneNumber,
                TemplateId = matchedTemplate.Id,
                ParametersJson = System.Text.Json.JsonSerializer.Serialize(new 
                { 
                    // Core guest info
                    Name = pmsEvent.GuestName,
                    Room = pmsEvent.RoomNumber, 
                    Amount = pmsEvent.Amount?.ToString("N2") ?? "0.00",
                    HotelName = pmsEvent.HotelName ?? "Sankalp Retreat",
                    // Rich check-in/out fields
                    CheckInDate = pmsEvent.CheckInDate ?? DateTimeOffset.UtcNow.ToString("d MMM yyyy"),
                    CheckOutDate = pmsEvent.CheckOutDate ?? DateTimeOffset.UtcNow.AddDays(1).ToString("d MMM yyyy"),
                    WifiPassword = pmsEvent.WifiPassword ?? "SankalpGuest@2026",
                    BookingReference = pmsEvent.BookingReference ?? $"BK-{DateTimeOffset.UtcNow:yyyyMMdd}-{pmsEvent.RoomNumber}",
                    FeedbackLink = pmsEvent.GuestFeedbackLink ?? "https://g.page/sankalp-retreat/review",
                    ExtraNote = pmsEvent.ExtraNote ?? string.Empty,
                    // Greeting with spintax for variety
                    Greeting = SpintaxParser.Parse("{Hi|Hello|Namaste} ")
                }),
                TriggerTime = DateTimeOffset.UtcNow.AddMinutes(matchedRule.DelayMinutes),
                Status = "Pending",
                RuleId = matchedRule.Id
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

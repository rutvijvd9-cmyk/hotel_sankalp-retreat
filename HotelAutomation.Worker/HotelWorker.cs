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
    private readonly IServiceScopeFactory _scopeFactory;

    public HotelWorker(
        IPmsEventSource pmsSource,
        IRuleRepository ruleRepository,
        ITemplateRepository templateRepository,
        IOutboxRepository outboxRepository,
        EventLogRepository eventLog,
        JobRepository jobRepository,
        UnsubscribedContactRepository unsubRepo,
        ILogger<HotelWorker> logger,
        IServiceScopeFactory scopeFactory)
    {
        _pmsSource = pmsSource;
        _ruleRepository = ruleRepository;
        _templateRepository = templateRepository;
        _outboxRepository = outboxRepository;
        _eventLog = eventLog;
        _jobRepository = jobRepository;
        _unsubRepo = unsubRepo;
        _logger = logger;
        _scopeFactory = scopeFactory;
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

            // Stop Marketing messages if the user is unsubscribed from the category
            // We need to look up the CategoryId based on matchedTemplate.Category
            // And then look up the ContactId based on pmsEvent.PhoneNumber
            // To keep things decentralized, we use the DI injected repositories
            if (matchedTemplate.Category != "Utility")
            {
                using var scope = _logger.BeginScope("Category Check");
                var contactRepo = _scopeFactory.CreateScope().ServiceProvider.GetRequiredService<ContactRepository>();
                var categoryRepo = _scopeFactory.CreateScope().ServiceProvider.GetRequiredService<CategoryRepository>();
                var consentRepo = _scopeFactory.CreateScope().ServiceProvider.GetRequiredService<ContactConsentRepository>();
                
                var contact = await contactRepo.GetByPhoneNumberAsync(pmsEvent.PhoneNumber);
                if (contact != null)
                {
                    var categories = await categoryRepo.GetAllAsync();
                    var category = categories.FirstOrDefault(c => c.Name == matchedTemplate.Category);
                    if (category != null)
                    {
                        var isSubscribed = await consentRepo.IsSubscribedAsync(contact.Id, category.Id);
                        if (!isSubscribed)
                        {
                            _logger.LogInformation("Skipping {Category} automation {RuleId} for {Phone} because user opted out.", matchedTemplate.Category, matchedRule.Id, pmsEvent.PhoneNumber);
                            return;
                        }
                    }
                }
            }

            // Build the ParametersJson dictionary
            var paramDict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            
            // Core guest info
            var guestName = pmsEvent.GuestName?.Trim() ?? "";
            paramDict["Name"] = guestName;
            
            var nameParts = guestName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var firstName = nameParts.Length > 0 ? nameParts[0] : "";
            var lastName = nameParts.Length > 1 ? string.Join(" ", nameParts.Skip(1)) : "";
            paramDict["First Name"] = firstName;
            paramDict["FirstName"] = firstName;
            paramDict["Last Name"] = lastName;
            paramDict["LastName"] = lastName;
            
            paramDict["Room"] = pmsEvent.RoomNumber ?? "";
            paramDict["Amount"] = pmsEvent.Amount?.ToString("N2") ?? "0.00";
            paramDict["HotelName"] = !string.IsNullOrWhiteSpace(pmsEvent.HotelName) ? pmsEvent.HotelName : "Sankalp Retreat";
            paramDict["CheckInDate"] = !string.IsNullOrWhiteSpace(pmsEvent.CheckInDate) ? pmsEvent.CheckInDate : DateTimeOffset.UtcNow.ToString("d MMM yyyy");
            paramDict["CheckOutDate"] = !string.IsNullOrWhiteSpace(pmsEvent.CheckOutDate) ? pmsEvent.CheckOutDate : DateTimeOffset.UtcNow.AddDays(1).ToString("d MMM yyyy");
            paramDict["WifiPassword"] = !string.IsNullOrWhiteSpace(pmsEvent.WifiPassword) ? pmsEvent.WifiPassword : "SankalpGuest@2026";
            paramDict["BookingReference"] = !string.IsNullOrWhiteSpace(pmsEvent.BookingReference) ? pmsEvent.BookingReference : $"BK-{DateTimeOffset.UtcNow:yyyyMMdd}-{pmsEvent.RoomNumber}";
            paramDict["FeedbackLink"] = !string.IsNullOrWhiteSpace(pmsEvent.GuestFeedbackLink) ? pmsEvent.GuestFeedbackLink : "https://g.page/sankalp-retreat/review";
            paramDict["ExtraNote"] = pmsEvent.ExtraNote ?? string.Empty;
            paramDict["Greeting"] = SpintaxParser.Parse("{Hi|Hello|Namaste} ");

            // Merge dynamic keys from RawPayload
            if (!string.IsNullOrWhiteSpace(pmsEvent.RawPayload))
            {
                try
                {
                    var rawDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(pmsEvent.RawPayload);
                    if (rawDict != null)
                    {
                        foreach (var kv in rawDict)
                        {
                            var valStr = kv.Value.ValueKind == System.Text.Json.JsonValueKind.Null ? "" : (kv.Value.ToString() ?? "");
                            if (!string.IsNullOrWhiteSpace(valStr))
                            {
                                paramDict[kv.Key] = valStr;
                            }
                        }
                    }
                }
                catch { }
            }

            var cleanPhone = new string((pmsEvent.PhoneNumber ?? "").Where(char.IsDigit).ToArray());

            // Enqueue into ScheduledJobs to support Drip Campaigns (Phase 1)
            var job = new ScheduledJob
            {
                GuestName = pmsEvent.GuestName ?? "",
                PhoneNumber = cleanPhone,
                TemplateId = matchedTemplate.Id,
                ParametersJson = System.Text.Json.JsonSerializer.Serialize(paramDict),
                TriggerTime = DateTimeOffset.UtcNow.AddMinutes(matchedRule.DelayMinutes),
                Status = "Pending",
                RuleId = matchedRule.Id
            };


            await _jobRepository.AddAsync(job);
            
            await _eventLog.LogAsync(pmsEvent, null); // We don't have OutboxMessageId yet

            // Save or Update Contact
            if (!string.IsNullOrWhiteSpace(cleanPhone) && !string.IsNullOrWhiteSpace(pmsEvent.GuestName))
            {
                using var scope = _scopeFactory.CreateScope();
                var contactRepo = scope.ServiceProvider.GetRequiredService<ContactRepository>();
                
                var existingColumns = new HashSet<string>(await contactRepo.GetDynamicColumnsAsync(), StringComparer.OrdinalIgnoreCase);
                var customFields = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

                if (!string.IsNullOrWhiteSpace(pmsEvent.RawPayload))
                {
                    try
                    {
                        var rawDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(pmsEvent.RawPayload);
                        if (rawDict != null)
                        {
                            foreach (var kv in rawDict)
                            {
                                if (existingColumns.Contains(kv.Key))
                                {
                                    var valStr = kv.Value.ValueKind == System.Text.Json.JsonValueKind.Null ? "" : (kv.Value.ToString() ?? "");
                                    customFields[kv.Key] = valStr;
                                }
                            }
                        }
                    }
                    catch { }
                }

                if (!customFields.ContainsKey("Room No") && !string.IsNullOrEmpty(pmsEvent.RoomNumber))
                {
                    customFields["Room No"] = pmsEvent.RoomNumber;
                }

                var existingContact = await contactRepo.GetByPhoneNumberAsync(cleanPhone);
                if (existingContact == null)
                {
                    await contactRepo.InsertAsync(new Contact
                    {
                        PhoneNumber = cleanPhone,
                        FirstName = firstName,
                        LastName = lastName,
                        CustomFieldsJson = System.Text.Json.JsonSerializer.Serialize(customFields),
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow
                    });
                    _logger.LogInformation("Added new contact: {Name} ({Phone}) with dynamic fields", pmsEvent.GuestName, cleanPhone);
                }
                else
                {
                    var mergedCustomFields = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(existingContact.CustomFieldsJson ?? "{}") ?? new Dictionary<string, object>();
                    foreach (var kv in customFields)
                    {
                        mergedCustomFields[kv.Key] = kv.Value;
                    }

                    var updatedContact = new Contact
                    {
                        Id = existingContact.Id,
                        PhoneNumber = existingContact.PhoneNumber,
                        FirstName = firstName,
                        LastName = lastName,
                        CategoryId = existingContact.CategoryId,
                        CustomFieldsJson = System.Text.Json.JsonSerializer.Serialize(mergedCustomFields),
                        CreatedAt = existingContact.CreatedAt,
                        UpdatedAt = DateTimeOffset.UtcNow,
                        UpdateCount = existingContact.UpdateCount,
                        CheckInCount = existingContact.CheckInCount,
                        AverageStayDays = existingContact.AverageStayDays,
                        IsVip = existingContact.IsVip
                    };
                    await contactRepo.UpdateAsync(existingContact.Id, updatedContact);
                }
            }

            _logger.LogInformation("ScheduledJob {Id} queued for {Phone} to execute in {Delay} minutes", job.Id, cleanPhone, matchedRule.DelayMinutes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling PmsEvent {EventType}", pmsEvent.EventType);
        }
    }
}

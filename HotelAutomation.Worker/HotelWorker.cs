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
    private static readonly SemaphoreSlim _dedupLock = new(1, 1);

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

            // ── Compute name parts (needed for contact save below) ────────────
            var guestNameEarly = pmsEvent.GuestName?.Trim() ?? "";
            var namePartsEarly = guestNameEarly.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var firstNameEarly = namePartsEarly.Length > 0 ? namePartsEarly[0] : "";
            var lastNameEarly  = namePartsEarly.Length > 1 ? string.Join(" ", namePartsEarly.Skip(1)) : "";
            var cleanPhoneEarly = new string((pmsEvent.PhoneNumber ?? "").Where(char.IsDigit).ToArray());

            // ── ALWAYS save/update contact on every PMS event ─────────────────
            // This runs unconditionally — even when no automation rule matches,
            // so the Contacts tab is always populated from scanned PDFs.
            await UpsertContactAsync(pmsEvent, cleanPhoneEarly, firstNameEarly, lastNameEarly);

            var rules = await _ruleRepository.GetEnabledRulesForEventTypeAsync(pmsEvent.EventType);
            var templates = await _templateRepository.GetAllAsync();
            var match = AutomationRuleMatcher.Match(pmsEvent, rules, templates);

            if (match is null)
            {
                _logger.LogWarning("No matching rule for event type: {EventType} — contact saved but no WhatsApp message will be sent.", pmsEvent.EventType);
                await _eventLog.LogAsync(pmsEvent);
                return;
            }

            var (matchedTemplate, matchedRule) = match.Value;

            // ── If/Else Branching: evaluate FieldCondition and override template if needed ──
            if (!string.IsNullOrWhiteSpace(matchedRule.ConditionsJson)
                && matchedRule.YesTemplateId.HasValue
                && matchedRule.NoTemplateId.HasValue)
            {
                try
                {
                    var cond = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, System.Text.Json.JsonElement>>(matchedRule.ConditionsJson);
                    if (cond != null && cond.TryGetValue("Type", out var typeEl) && typeEl.GetString() == "FieldCondition")
                    {
                        var field    = cond.TryGetValue("Field",           out var fEl)   ? fEl.GetString()   ?? "" : "";
                        var op       = cond.TryGetValue("Operator",        out var oEl)   ? oEl.GetString()   ?? "" : "";
                        var value    = cond.TryGetValue("Value",           out var vEl)   ? vEl.GetString()   ?? "" : "";
                        var cfName   = cond.TryGetValue("CustomFieldName", out var cfEl)  ? cfEl.GetString()  ?? "" : "";

                        // Load contact to check field values
                        var cleanPhoneForCond = new string((pmsEvent.PhoneNumber ?? "").Where(char.IsDigit).ToArray());
                        using var condScope = _scopeFactory.CreateScope();
                        var condContactRepo = condScope.ServiceProvider.GetRequiredService<HotelAutomation.Data.Repositories.ContactRepository>();
                        var condContact = await condContactRepo.GetByPhoneNumberAsync(cleanPhoneForCond);

                        bool conditionMet = false;
                        if (condContact != null)
                        {
                            conditionMet = EvaluateFieldCondition(field, op, value, cfName, condContact);
                        }

                        var branchTemplateId = conditionMet ? matchedRule.YesTemplateId.Value : matchedRule.NoTemplateId.Value;
                        var allTemplatesForBranch = await _templateRepository.GetAllAsync();
                        var branchTemplate = allTemplatesForBranch.FirstOrDefault(t => t.Id == branchTemplateId);
                        if (branchTemplate != null)
                        {
                            matchedTemplate = branchTemplate;
                            _logger.LogInformation("If/Else branch: condition={Result}, using template {TemplateId}", conditionMet ? "YES" : "NO", branchTemplateId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "FieldCondition evaluation failed; falling back to default template.");
                }
            }

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
                
                var contact = await contactRepo.GetByPhoneNumberAsync(pmsEvent.PhoneNumber ?? string.Empty);
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
            
            // Core guest info (reuse the name parts already computed above)
            var guestName = guestNameEarly;
            var firstName = firstNameEarly;
            var lastName  = lastNameEarly;
            var cleanPhone = cleanPhoneEarly;
            paramDict["Name"] = guestName;
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

            // cleanPhone already declared above from cleanPhoneEarly

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


            // ── Deduplication: prevent duplicate WhatsApp messages from double-clicks / retries ──
            // If the same phone + rule already has a job created in the last 5 minutes, skip.
            await _dedupLock.WaitAsync();
            try
            {
                var isDuplicate = await _jobRepository.HasRecentJobAsync(cleanPhone, matchedRule.Id, windowMinutes: 5);
                if (isDuplicate)
                {
                    _logger.LogWarning(
                        "Duplicate job skipped for {Phone} on rule {RuleId} — already triggered within the 5-minute dedup window.",
                        cleanPhone, matchedRule.Id);
                    return;
                }

                await _jobRepository.AddAsync(job);
            }
            finally
            {
                _dedupLock.Release();
            }
            
            await _eventLog.LogAsync(pmsEvent, null); // We don't have OutboxMessageId yet

            // Contact already saved above (unconditionally before rule matching)

            _logger.LogInformation("ScheduledJob {Id} queued for {Phone} to execute in {Delay} minutes", job.Id, cleanPhone, matchedRule.DelayMinutes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling PmsEvent {EventType}", pmsEvent.EventType);
        }
    }

    /// <summary>
    /// Always creates or updates a contact from a PmsEvent.
    /// Called unconditionally on every PDF scan — before rule matching —
    /// so the Contacts tab is always populated regardless of WhatsApp setup.
    /// </summary>
    private async Task UpsertContactAsync(PmsEvent pmsEvent, string cleanPhone, string firstName, string lastName)
    {
        if (string.IsNullOrWhiteSpace(cleanPhone) || string.IsNullOrWhiteSpace(pmsEvent.GuestName))
        {
            _logger.LogWarning("UpsertContact: skipping — phone or guest name is empty.");
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var contactRepo = scope.ServiceProvider.GetRequiredService<ContactRepository>();

            // Build custom fields from all extracted PDF data (RawPayload)
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
                            var valStr = kv.Value.ValueKind == System.Text.Json.JsonValueKind.Null
                                ? "" : (kv.Value.ToString() ?? "");
                            if (!string.IsNullOrWhiteSpace(valStr))
                                customFields[kv.Key] = valStr;
                        }
                    }
                }
                catch { /* ignore malformed payload */ }
            }

            // Always include room number from the top-level event field
            if (!string.IsNullOrEmpty(pmsEvent.RoomNumber))
                customFields["Room No"] = pmsEvent.RoomNumber;

            var existingContact = await contactRepo.GetByPhoneNumberAsync(cleanPhone);
            if (existingContact == null)
            {
                await contactRepo.InsertAsync(new Contact
                {
                    PhoneNumber      = cleanPhone,
                    FirstName        = firstName,
                    LastName         = lastName,
                    CustomFieldsJson = System.Text.Json.JsonSerializer.Serialize(customFields),
                    CreatedAt        = DateTimeOffset.UtcNow,
                    UpdatedAt        = DateTimeOffset.UtcNow
                });
                _logger.LogInformation("Contact CREATED: {Name} ({Phone})", pmsEvent.GuestName, cleanPhone);
            }
            else
            {
                // Merge: existing fields are overwritten only when the new value is non-empty
                var merged = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(
                    existingContact.CustomFieldsJson ?? "{}") ?? new Dictionary<string, object>();
                foreach (var kv in customFields)
                    merged[kv.Key] = kv.Value;

                await contactRepo.UpdateAsync(existingContact.Id, new Contact
                {
                    Id               = existingContact.Id,
                    PhoneNumber      = existingContact.PhoneNumber,
                    FirstName        = firstName,
                    LastName         = lastName,
                    CategoryId       = existingContact.CategoryId,
                    CustomFieldsJson = System.Text.Json.JsonSerializer.Serialize(merged),
                    CreatedAt        = existingContact.CreatedAt,
                    UpdatedAt        = DateTimeOffset.UtcNow,
                    UpdateCount      = existingContact.UpdateCount,
                    CheckInCount     = existingContact.CheckInCount,
                    AverageStayDays  = existingContact.AverageStayDays,
                    IsVip            = existingContact.IsVip
                });
                _logger.LogInformation("Contact UPDATED: {Name} ({Phone})", pmsEvent.GuestName, cleanPhone);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpsertContact: failed for {Phone}", cleanPhone);
        }
    }

    /// <summary>
    /// Evaluates a single FieldCondition against a contact record.
    /// Returns true if the condition passes (→ YES branch), false otherwise (→ NO branch).
    /// </summary>
    private static bool EvaluateFieldCondition(
        string field, string op, string value, string customFieldName,
        HotelAutomation.Core.Models.Contact contact)
    {
        // Boolean shortcuts
        if (op == "is_true")
        {
            return field switch
            {
                "is_vip" => contact.IsVip,
                _        => false
            };
        }
        if (op == "is_false")
        {
            return field switch
            {
                "is_vip" => !contact.IsVip,
                _        => true
            };
        }

        // Numeric fields
        if (field == "checkin_count")
        {
            if (!double.TryParse(value, out var threshold)) return false;
            return op switch
            {
                "eq"  => contact.CheckInCount == (int)threshold,
                "gt"  => contact.CheckInCount >  (int)threshold,
                "lt"  => contact.CheckInCount <  (int)threshold,
                "gte" => contact.CheckInCount >= (int)threshold,
                "lte" => contact.CheckInCount <= (int)threshold,
                _     => false
            };
        }

        if (field == "avg_stay_days")
        {
            if (!double.TryParse(value, out var threshold)) return false;
            return op switch
            {
                "eq"  => Math.Abs(contact.AverageStayDays - threshold) < 0.01,
                "gt"  => contact.AverageStayDays >  threshold,
                "lt"  => contact.AverageStayDays <  threshold,
                "gte" => contact.AverageStayDays >= threshold,
                "lte" => contact.AverageStayDays <= threshold,
                _     => false
            };
        }

        // String fields from core contact properties
        string? contactValue = field switch
        {
            "country"        => GetCustomField(contact, "Country"),
            "city"           => GetCustomField(contact, "City"),
            "language"       => GetCustomField(contact, "Language"),
            "booking_status" => GetCustomField(contact, "BookingStatus"),
            "custom_field"   => GetCustomField(contact, customFieldName),
            _                => null
        };

        if (contactValue == null) return false;

        return op switch
        {
            "eq"       => string.Equals(contactValue, value, StringComparison.OrdinalIgnoreCase),
            "neq"      => !string.Equals(contactValue, value, StringComparison.OrdinalIgnoreCase),
            "contains" => contactValue.Contains(value, StringComparison.OrdinalIgnoreCase),
            "exists"   => !string.IsNullOrWhiteSpace(contactValue),
            _          => false
        };
    }

    private static string? GetCustomField(HotelAutomation.Core.Models.Contact contact, string key)
    {
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(contact.CustomFieldsJson))
            return null;
        try
        {
            var dict = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, System.Text.Json.JsonElement>>(contact.CustomFieldsJson);
            if (dict == null) return null;
            var match = dict.FirstOrDefault(kv => string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase));
            if (match.Key == null) return null;
            return match.Value.ValueKind == System.Text.Json.JsonValueKind.Null ? null : match.Value.ToString();
        }
        catch { return null; }
    }
}

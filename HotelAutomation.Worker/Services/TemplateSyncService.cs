namespace HotelAutomation.Worker.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HotelAutomation.Connectors.WhatsApp;
using HotelAutomation.Core.Interfaces;
using HotelAutomation.Core.Models;
using HotelAutomation.Data.Repositories;
using Microsoft.Extensions.Logging;

public sealed class TemplateSyncService
{
    private readonly ITemplateRepository _repo;
    private readonly WhatsAppApiClient _whatsappClient;
    private readonly ActivityLogRepository _activityLog;
    private readonly ILogger<TemplateSyncService> _logger;

    public TemplateSyncService(
        ITemplateRepository repo,
        WhatsAppApiClient whatsappClient,
        ActivityLogRepository activityLog,
        ILogger<TemplateSyncService> logger)
    {
        _repo = repo;
        _whatsappClient = whatsappClient;
        _activityLog = activityLog;
        _logger = logger;
    }

    public async Task<(bool Success, int AddedCount, string Message)> SyncTemplatesAsync(string triggeredBy = "Manual", CancellationToken ct = default)
    {
        try
        {
            var (success, content, error) = await _whatsappClient.GetTemplatesAsync(ct);
            if (!success || string.IsNullOrEmpty(content))
            {
                var errMsg = $"Meta Sync Error: {error ?? "Unknown error"}";
                _logger.LogWarning("Template sync failed: {Error}", errMsg);
                await _activityLog.LogAsync("SYSTEM", "TEMPLATE_SYNC_FAILED", errMsg, null, ct);
                return (false, 0, errMsg);
            }

            using var doc = JsonDocument.Parse(content);
            if (!doc.RootElement.TryGetProperty("data", out var dataProp) || dataProp.ValueKind != JsonValueKind.Array)
            {
                return (true, 0, "No templates found in Meta WABA");
            }

            var localTemplates = await _repo.GetAllAsync();
            var localNames = localTemplates.Select(t => t.ExternalTemplateName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            int added = 0;

            foreach (var item in dataProp.EnumerateArray())
            {
                if (item.TryGetProperty("name", out var nameProp))
                {
                    var name = nameProp.GetString();
                    if (string.IsNullOrEmpty(name) || localNames.Contains(name)) continue;

                    var category = item.TryGetProperty("category", out var catProp) ? catProp.GetString() ?? "Utility" : "Utility";
                    var lang = item.TryGetProperty("language", out var langProp) ? langProp.GetString() ?? "en" : "en";
                    
                    string bodyText = "";
                    string headerText = "";
                    string headerType = "None";
                    string footerText = "";
                    string buttonsJson = "[]";

                    if (item.TryGetProperty("components", out var compsProp) && compsProp.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var comp in compsProp.EnumerateArray())
                        {
                            var type = comp.TryGetProperty("type", out var typeP) ? typeP.GetString() : "";
                            if (type == "BODY")
                            {
                                bodyText = comp.TryGetProperty("text", out var textP) ? textP.GetString() ?? "" : "";
                            }
                            else if (type == "HEADER")
                            {
                                headerType = comp.TryGetProperty("format", out var fmtP) ? fmtP.GetString() ?? "Text" : "Text";
                                if (headerType == "TEXT")
                                {
                                    headerText = comp.TryGetProperty("text", out var textP) ? textP.GetString() ?? "" : "";
                                }
                            }
                            else if (type == "FOOTER")
                            {
                                footerText = comp.TryGetProperty("text", out var textP) ? textP.GetString() ?? "" : "";
                            }
                            else if (type == "BUTTONS")
                            {
                                var btns = new List<object>();
                                if (comp.TryGetProperty("buttons", out var btnsP) && btnsP.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var b in btnsP.EnumerateArray())
                                    {
                                        var bType = b.TryGetProperty("type", out var btP) ? btP.GetString() : "";
                                        var bText = b.TryGetProperty("text", out var btTxt) ? btTxt.GetString() ?? "" : "";
                                        if (bType == "QUICK_REPLY")
                                        {
                                            btns.Add(new { type = "QUICK_REPLY", text = bText });
                                        }
                                        else if (bType == "URL")
                                        {
                                            var bUrl = b.TryGetProperty("url", out var btUrl) ? btUrl.GetString() ?? "" : "";
                                            btns.Add(new { type = "URL", text = bText, url = bUrl });
                                        }
                                        else if (bType == "PHONE_NUMBER")
                                        {
                                            var bPhone = b.TryGetProperty("phone_number", out var btPhone) ? btPhone.GetString() ?? "" : "";
                                            btns.Add(new { type = "PHONE_NUMBER", text = bText, phone_number = bPhone });
                                        }
                                    }
                                }
                                buttonsJson = JsonSerializer.Serialize(btns);
                            }
                        }
                    }

                    // Parse parameter definitions from bodyText
                    var matches = Regex.Matches(bodyText, @"\{\{(\d+)\}\}");
                    var positions = matches.Cast<Match>().Select(m => int.Parse(m.Groups[1].Value)).Distinct().OrderBy(p => p).ToList();
                    var defs = positions.Select(pos => new {
                        Position = pos,
                        Label = pos == 1 ? "Guest Name" : (pos == 2 ? "Hotel Name" : $"Variable {pos}"),
                        SampleValue = pos == 1 ? "Dear Guest" : (pos == 2 ? "The Sankalp Retreat" : ""),
                        SourceColumn = pos == 1 ? "FirstName" : "",
                        DynamicKey = pos == 1 ? "Name" : (pos == 2 ? "HotelName" : "")
                    }).ToList();

                    var newT = new MessageTemplate
                    {
                        Name = name,
                        ExternalTemplateName = name,
                        LanguageCode = lang,
                        Category = category,
                        HeaderType = headerType,
                        BodyText = bodyText,
                        HeaderText = string.IsNullOrEmpty(headerText) ? null : headerText,
                        FooterText = string.IsNullOrEmpty(footerText) ? null : footerText,
                        ButtonsJson = buttonsJson,
                        ParameterDefinitionsJson = JsonSerializer.Serialize(defs),
                        BodyParamCount = defs.Count
                    };

                    await _repo.AddAsync(newT);
                    added++;
                }
            }

            var msg = $"Sync completed successfully ({added} new templates added). Triggered by: {triggeredBy}";
            _logger.LogInformation("{Message}", msg);
            await _activityLog.LogAsync(
                triggeredBy == "AutoScheduler" ? "SYSTEM" : "SRKadmin",
                "TEMPLATE_SYNC",
                $"WhatsApp templates synchronized with Meta WABA ({added} new added). Source: {triggeredBy}",
                null,
                ct);

            return (true, added, msg);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Template sync failed");
            await _activityLog.LogAsync("SYSTEM", "TEMPLATE_SYNC_ERROR", ex.Message, null, ct);
            return (false, 0, $"Sync failed: {ex.Message}");
        }
    }
}

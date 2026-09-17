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

            // Collect every name currently live on Meta for later stale-name detection
            var metaNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in dataProp.EnumerateArray())
            {
                if (item.TryGetProperty("name", out var np))
                {
                    var n = np.GetString();
                    if (!string.IsNullOrEmpty(n)) metaNames.Add(n);
                }
            }

            int added = 0;

            // ── Phase 1: Add brand-new templates from Meta ────────────────────────────
            foreach (var item in dataProp.EnumerateArray())
            {
                if (!item.TryGetProperty("name", out var nameProp)) continue;
                var name = nameProp.GetString();
                if (string.IsNullOrEmpty(name) || localNames.Contains(name)) continue;

                var category = item.TryGetProperty("category", out var catProp) ? catProp.GetString() ?? "Utility" : "Utility";
                var lang     = item.TryGetProperty("language", out var langProp) ? langProp.GetString() ?? "en" : "en";

                string bodyText    = "";
                string headerText  = "";
                string headerType  = "None";
                string footerText  = "";
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
                                headerText = comp.TryGetProperty("text", out var textP) ? textP.GetString() ?? "" : "";
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
                                    var bType  = b.TryGetProperty("type", out var btP)   ? btP.GetString()   : "";
                                    var bText  = b.TryGetProperty("text", out var btTxt) ? btTxt.GetString() ?? "" : "";
                                    if (bType == "QUICK_REPLY")
                                        btns.Add(new { type = "QUICK_REPLY", text = bText });
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
                var paramMatches = Regex.Matches(bodyText, @"\{\{(\d+)\}\}");
                var positions    = paramMatches.Cast<Match>().Select(m => int.Parse(m.Groups[1].Value)).Distinct().OrderBy(p => p).ToList();
                var defs = positions.Select(pos => new {
                    Position    = pos,
                    Label       = pos == 1 ? "Guest Name"  : (pos == 2 ? "Hotel Name" : $"Variable {pos}"),
                    SampleValue = pos == 1 ? "Dear Guest"  : (pos == 2 ? "The Sankalp Retreat" : ""),
                    SourceColumn = pos == 1 ? "FirstName"  : "",
                    DynamicKey  = pos == 1 ? "Name"        : (pos == 2 ? "HotelName" : "")
                }).ToList();

                var newT = new MessageTemplate
                {
                    Name                     = name,
                    ExternalTemplateName     = name,
                    LanguageCode             = lang,
                    Category                 = category,
                    HeaderType               = headerType,
                    BodyText                 = bodyText,
                    HeaderText               = string.IsNullOrEmpty(headerText) ? null : headerText,
                    FooterText               = string.IsNullOrEmpty(footerText) ? null : footerText,
                    ButtonsJson              = buttonsJson,
                    ParameterDefinitionsJson = JsonSerializer.Serialize(defs),
                    BodyParamCount           = defs.Count
                };

                await _repo.AddAsync(newT);
                added++;
            }

            // ── Phase 2: Repair stale template names ──────────────────────────────────
            // When a template is renamed on Meta (e.g. "check_out" → "checkout_1") the old
            // name stays in the local DB and automation rules continue pointing to it, causing
            // WhatsApp API error 132001 ("template name does not exist in translation").
            //
            // Fix: detect local templates whose ExternalTemplateName is no longer on Meta,
            // find the best matching current Meta name using normalised comparison, and update
            // the record IN-PLACE.  The AutomationRule.TemplateId FK is untouched — no manual
            // intervention is needed after this repair.
            int repaired = 0;
            var refreshedLocal = await _repo.GetAllAsync(); // re-fetch so newly-added rows are included
            foreach (var local in refreshedLocal)
            {
                if (metaNames.Contains(local.ExternalTemplateName))
                    continue; // still present on Meta — nothing to do

                var normalizedLocal = NormalizeTemplateName(local.ExternalTemplateName);
                var bestMatch = metaNames
                    .Where(metaName => NormalizeTemplateName(metaName) == normalizedLocal)
                    .OrderBy(metaName => metaName.Length) // prefer shorter / less versioned name
                    .FirstOrDefault();

                if (bestMatch is not null)
                {
                    _logger.LogWarning(
                        "Stale template detected: local '{OldName}' (Id={Id}) no longer exists on Meta. " +
                        "Auto-updating ExternalTemplateName → '{NewName}'. Automation rules are unaffected.",
                        local.ExternalTemplateName, local.Id, bestMatch);

                    await _repo.UpdateAsync(local.Id, local with { ExternalTemplateName = bestMatch, Name = bestMatch }, ct);
                    repaired++;

                    await _activityLog.LogAsync(
                        "SYSTEM",
                        "TEMPLATE_AUTO_REPAIRED",
                        $"Stale template '{local.ExternalTemplateName}' (Id={local.Id}) auto-updated to '{bestMatch}'. Triggered by: {triggeredBy}",
                        null, ct);
                }
                else
                {
                    _logger.LogWarning(
                        "Stale template '{Name}' (Id={Id}) has no matching template on Meta WABA. " +
                        "Automation rules using this template will fail with error 132001 until the template is recreated on Meta or the rule is updated manually.",
                        local.ExternalTemplateName, local.Id);
                }
            }

            var summary = added == 0 && repaired == 0
                ? $"Sync completed — all templates already up to date. Triggered by: {triggeredBy}"
                : $"Sync completed: {added} new template(s) added, {repaired} stale name(s) repaired. Triggered by: {triggeredBy}";

            _logger.LogInformation("{Message}", summary);
            await _activityLog.LogAsync(
                triggeredBy is "AutoScheduler" or "Startup" ? "SYSTEM" : "SRKadmin",
                "TEMPLATE_SYNC",
                $"WhatsApp templates synchronized with Meta WABA ({added} new added, {repaired} repaired). Source: {triggeredBy}",
                null,
                ct);

            return (true, added, summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Template sync failed");
            await _activityLog.LogAsync("SYSTEM", "TEMPLATE_SYNC_ERROR", ex.Message, null, ct);
            return (false, 0, $"Sync failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Normalises a template name for fuzzy matching so renamed templates can be identified.
    /// Strips trailing numeric version suffixes (_1, _2, …), removes underscores, hyphens,
    /// and spaces, then lowercases — so "check_out" and "checkout_1" both collapse to "checkout".
    /// </summary>
    private static string NormalizeTemplateName(string name)
    {
        // Remove trailing _<digits> suffix (e.g. checkout_1 → checkout)
        var stripped = Regex.Replace(name, @"_\d+$", "");
        // Remove remaining separators and lowercase
        return Regex.Replace(stripped, @"[_\- ]", "").ToLowerInvariant();
    }
}

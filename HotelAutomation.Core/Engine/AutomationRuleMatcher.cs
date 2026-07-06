namespace HotelAutomation.Core.Engine;

using HotelAutomation.Core.Models;

/// <summary>
/// Pure logic: given a PmsEvent and a list of enabled rules + templates,
/// returns the first matching template, or null if no rule matches.
/// No database, no HTTP — fully testable without any mocks.
/// </summary>
public static class AutomationRuleMatcher
{
    public static (MessageTemplate Template, AutomationRule Rule)? Match(
        PmsEvent pmsEvent,
        IReadOnlyList<AutomationRule> enabledRules,
        IReadOnlyList<MessageTemplate> allTemplates)
    {
        var matchingRule = enabledRules
            .FirstOrDefault(r => string.Equals(
                r.TriggerEventType, pmsEvent.EventType,
                StringComparison.OrdinalIgnoreCase));

        if (matchingRule is null)
            return null;

        int selectedTemplateId = matchingRule.TemplateId;

        if (!string.IsNullOrWhiteSpace(matchingRule.TemplateWeightsJson))
        {
            try
            {
                var weights = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<int, int>>(matchingRule.TemplateWeightsJson);
                if (weights is not null && weights.Count > 0)
                {
                    int totalWeight = 0;
                    foreach (var w in weights.Values) totalWeight += w;

                    int roll = new Random().Next(totalWeight);
                    int currentSum = 0;
                    foreach (var kvp in weights)
                    {
                        currentSum += kvp.Value;
                        if (roll < currentSum)
                        {
                            selectedTemplateId = kvp.Key;
                            break;
                        }
                    }
                }
            }
            catch
            {
                // Fallback to default TemplateId if JSON is malformed
            }
        }

        var template = allTemplates.FirstOrDefault(t => t.Id == selectedTemplateId);
        if (template is null)
            return null;

        return (template, matchingRule);
    }
}

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

        var template = allTemplates.FirstOrDefault(t => t.Id == matchingRule.TemplateId);
        if (template is null)
            return null;

        return (template, matchingRule);
    }
}

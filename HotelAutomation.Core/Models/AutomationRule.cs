namespace HotelAutomation.Core.Models;

public sealed record AutomationRule
{
    public int Id { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public required string TriggerEventType { get; init; }  // e.g. "CheckOut"
    public int TemplateId { get; init; }
    public bool IsEnabled { get; init; } = true;
    public string? ConditionsJson { get; init; }            // reserved for future extra-matching logic
    public string? AudienceFilter { get; init; }            // JSON for targeting specific segments
    public int DelayMinutes { get; init; } = 0;             // Support for Drip Campaigns
    public string? TemplateWeightsJson { get; init; }       // For A/B Testing
    public int? YesTemplateId { get; init; }                // If/Else: template when condition is TRUE
    public int? NoTemplateId { get; init; }                 // If/Else: template when condition is FALSE
}

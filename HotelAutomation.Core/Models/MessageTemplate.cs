namespace HotelAutomation.Core.Models;

public sealed record MessageTemplate
{
    public int Id { get; init; }
    public required string Name { get; init; }                    // internal label, e.g. "Checkout Bill"
    public required string ExternalTemplateName { get; init; }   // exact Meta-approved name
    public string LanguageCode { get; init; } = "en";
    public string ChannelType { get; init; } = "WhatsApp";
    public int BodyParamCount { get; init; } = -1; // -1=unknown, 0=no params, N=exact param count
    public string? FallbackChannelType { get; init; }
    public string Category { get; init; } = "Utility";
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

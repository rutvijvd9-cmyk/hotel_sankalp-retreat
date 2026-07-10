namespace HotelAutomation.Core.Models;

using System;
using System.Collections.Generic;

public sealed record Campaign
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public bool IsEnabled { get; init; } = true;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public List<CampaignStep> Steps { get; init; } = new();
}

public sealed record CampaignStep
{
    public int Id { get; init; }
    public int CampaignId { get; init; }
    public int StepNumber { get; init; }
    public int TemplateId { get; init; }
    public int DelayMinutes { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

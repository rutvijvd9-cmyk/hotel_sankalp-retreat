namespace HotelAutomation.Core.Models;

using System;

public sealed record UnsubscribedContact
{
    public required string PhoneNumber { get; init; }
    public DateTimeOffset UnsubscribedAt { get; init; } = DateTimeOffset.UtcNow;
    public string Source { get; init; } = "Unknown";
}

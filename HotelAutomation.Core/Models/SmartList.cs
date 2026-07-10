namespace HotelAutomation.Core.Models;

using System;

public sealed record SmartList
{
    public int Id { get; init; }
    public required string ListName { get; init; }
    public required string FilterConditionsJson { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

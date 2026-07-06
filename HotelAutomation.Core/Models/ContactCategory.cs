namespace HotelAutomation.Core.Models;

using System;

public sealed record ContactCategory
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

namespace HotelAutomation.Core.Models;

using System;

public sealed record Contact
{
    public int Id { get; init; }
    public required string PhoneNumber { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public int? CategoryId { get; init; }
    public string CustomFieldsJson { get; init; } = "{}";
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
    public int UpdateCount { get; init; } = 0;
    public int CheckInCount { get; init; } = 0;
    public double AverageStayDays { get; init; } = 0;
    public bool IsVip { get; init; } = false;
}

namespace HotelAutomation.Core.Models;

public sealed record ParameterDefinition
{
    public int Position { get; init; }           // 1-based index (e.g. 1 matches {{1}})
    public string Label { get; init; } = "";     // Friendly descriptive name (e.g. "GuestName")
    public string? SampleValue { get; init; }   // Dummy value for Meta submissions and local test UI
    public string? SourceColumn { get; init; }  // Dropdown mapping from contact list (e.g. "RoomNumber")
    public string? DynamicKey { get; init; }    // Key from the live PMS/API event payload (e.g. "CheckInDate")
}

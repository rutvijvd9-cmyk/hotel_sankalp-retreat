namespace HotelAutomation.Core.Models;

public sealed class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty; // 'Owner', 'Developer', 'Employer'
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public string? SignaturePath { get; set; }
    public string? LastActiveAt { get; set; }
}

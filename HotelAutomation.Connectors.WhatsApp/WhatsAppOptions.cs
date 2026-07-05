namespace HotelAutomation.Connectors.WhatsApp;

/// <summary>
/// Bound from the "WhatsApp" section in appsettings.json / user-secrets.
/// AccessToken must NEVER appear in appsettings.json — use dotnet user-secrets only.
/// </summary>
public sealed class WhatsAppOptions
{
    public const string SectionName = "WhatsApp";

    public string AccessToken { get; set; } = string.Empty;
    public string PhoneNumberId { get; set; } = string.Empty;
    public string ApiVersion { get; set; } = "v19.0";

    public string BaseUrl => $"https://graph.facebook.com/{ApiVersion}/{PhoneNumberId}/messages";
}

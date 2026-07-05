namespace HotelAutomation.Connectors.WhatsApp;

using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

/// <summary>
/// Thin HTTP wrapper around Meta's Cloud API.
/// Only concern: send the JSON payload, return success/failure.
/// </summary>
public sealed class WhatsAppApiClient
{
    private readonly HttpClient _httpClient;
    private readonly WhatsAppOptions _options;

    public WhatsAppApiClient(HttpClient httpClient, IOptions<WhatsAppOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _options.AccessToken);
    }

    /// <summary>
    /// Sends a pre-built JSON payload to the Meta API.
    /// Returns (true, null) on success, (false, errorBody) on failure.
    /// </summary>
    public async Task<(bool Success, string? ErrorBody)> PostAsync(
        object payload, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync(_options.BaseUrl, payload, ct);

        if (response.IsSuccessStatusCode)
            return (true, null);

        var errorBody = await response.Content.ReadAsStringAsync(ct);
        return (false, errorBody);
    }
}

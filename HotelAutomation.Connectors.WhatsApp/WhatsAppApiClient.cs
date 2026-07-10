namespace HotelAutomation.Connectors.WhatsApp;

using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.Extensions.Logging;

/// <summary>
/// Thin HTTP wrapper around Meta's Cloud API.
/// Only concern: send the JSON payload, return success/failure.
/// </summary>
public sealed class WhatsAppApiClient
{
    private readonly HttpClient _httpClient;
    private readonly WhatsAppOptions _options;
    private readonly ILogger<WhatsAppApiClient> _logger;

    public WhatsAppApiClient(HttpClient httpClient, IOptions<WhatsAppOptions> options, ILogger<WhatsAppApiClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
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
        try
        {
            var response = await _httpClient.PostAsJsonAsync(_options.BaseUrl, payload, ct);

            var responseBody = await response.Content.ReadAsStringAsync(ct);
            return (response.IsSuccessStatusCode, responseBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HTTP POST to WhatsApp API failed");
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Uploads media to WhatsApp and returns the media_id
    /// </summary>
    public async Task<(bool Success, string? MediaId, string? Error)> UploadMediaAsync(byte[] fileBytes, string filename, string mimeType, CancellationToken ct = default)
    {
        try
        {
            var url = $"{_options.ApiVersion}/{_options.PhoneNumberId}/media";
            
            using var content = new MultipartFormDataContent();
            content.Add(new StringContent("whatsapp"), "messaging_product");
            
            var fileContent = new ByteArrayContent(fileBytes);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mimeType);
            content.Add(fileContent, "file", filename);

            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = content
            };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.AccessToken);

            var response = await _httpClient.SendAsync(request, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                using var doc = System.Text.Json.JsonDocument.Parse(responseBody);
                if (doc.RootElement.TryGetProperty("id", out var idProp))
                {
                    return (true, idProp.GetString(), null);
                }
                return (false, null, "Media ID not found in response.");
            }
            
            _logger.LogError("Media upload failed: {Response}", responseBody);
            return (false, null, responseBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HTTP POST to WhatsApp Media API failed");
            return (false, null, ex.Message);
        }
    }
}

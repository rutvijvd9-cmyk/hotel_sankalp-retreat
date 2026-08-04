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
            var request = new HttpRequestMessage(HttpMethod.Post, _options.BaseUrl)
            {
                Content = JsonContent.Create(payload)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);

            var response = await _httpClient.SendAsync(request, ct);

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
            var url = $"https://graph.facebook.com/{_options.ApiVersion}/{_options.PhoneNumberId}/media";
            
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

    /// <summary>
    /// Fetches all message templates from the WhatsApp Business Account.
    /// </summary>
    public async Task<(bool Success, string? Content, string? Error)> GetTemplatesAsync(CancellationToken ct = default)
    {
        try
        {
            var url = $"https://graph.facebook.com/{_options.ApiVersion}/{_options.WhatsAppBusinessAccountId}/message_templates?limit=1000";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);

            var response = await _httpClient.SendAsync(request, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                return (true, responseBody, null);
            }
            return (false, null, responseBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get message templates from Meta");
            return (false, null, ex.Message);
        }
    }

    /// <summary>
    /// Creates a message template in the WhatsApp Business Account.
    /// </summary>
    public async Task<(bool Success, string? Content, string? Error)> CreateTemplateAsync(object payload, CancellationToken ct = default)
    {
        try
        {
            var url = $"https://graph.facebook.com/{_options.ApiVersion}/{_options.WhatsAppBusinessAccountId}/message_templates";
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(payload)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);

            var response = await _httpClient.SendAsync(request, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                return (true, responseBody, null);
            }
            return (false, null, responseBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create message template in Meta");
            return (false, null, ex.Message);
        }
    }

    /// <summary>
    /// Deletes a message template in the WhatsApp Business Account.
    /// </summary>
    public async Task<(bool Success, string? Content, string? Error)> DeleteTemplateAsync(string templateName, CancellationToken ct = default)
    {
        try
        {
            var url = $"https://graph.facebook.com/{_options.ApiVersion}/{_options.WhatsAppBusinessAccountId}/message_templates?name={templateName}";
            var request = new HttpRequestMessage(HttpMethod.Delete, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);

            var response = await _httpClient.SendAsync(request, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                return (true, responseBody, null);
            }
            return (false, null, responseBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete message template from Meta");
            return (false, null, ex.Message);
        }
    }

    /// <summary>
    /// Downloads media from Meta's CDN given a media_id.
    /// </summary>
    public async Task<(bool Success, byte[]? FileBytes, string? MimeType, string? Error)> DownloadMediaAsync(string mediaId, CancellationToken ct = default)
    {
        try
        {
            // 1. Get Media URL
            var urlRequestUrl = $"https://graph.facebook.com/{_options.ApiVersion}/{mediaId}";
            var urlRequest = new HttpRequestMessage(HttpMethod.Get, urlRequestUrl);
            urlRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);

            var urlResponse = await _httpClient.SendAsync(urlRequest, ct);
            var urlResponseBody = await urlResponse.Content.ReadAsStringAsync(ct);

            if (!urlResponse.IsSuccessStatusCode)
            {
                return (false, null, null, $"Failed to get media URL: {urlResponseBody}");
            }

            using var doc = System.Text.Json.JsonDocument.Parse(urlResponseBody);
            if (!doc.RootElement.TryGetProperty("url", out var urlProp) || !doc.RootElement.TryGetProperty("mime_type", out var mimeProp))
            {
                return (false, null, null, "Media URL or mime_type not found in response.");
            }

            var downloadUrl = urlProp.GetString();
            var mimeType = mimeProp.GetString();

            if (string.IsNullOrEmpty(downloadUrl)) return (false, null, null, "Download URL is empty.");

            // 2. Download actual bytes
            var downloadRequest = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
            downloadRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);
            
            var downloadResponse = await _httpClient.SendAsync(downloadRequest, ct);
            if (!downloadResponse.IsSuccessStatusCode)
            {
                var errBody = await downloadResponse.Content.ReadAsStringAsync(ct);
                return (false, null, null, $"Failed to download media bytes: {errBody}");
            }

            var bytes = await downloadResponse.Content.ReadAsByteArrayAsync(ct);
            return (true, bytes, mimeType, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download media {MediaId}", mediaId);
            return (false, null, null, ex.Message);
        }
    }
}

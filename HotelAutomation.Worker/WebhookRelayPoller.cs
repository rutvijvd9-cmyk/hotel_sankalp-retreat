namespace HotelAutomation.Worker;

using System.Text;
using System.Text.Json;

/// <summary>
/// Background service that polls the Render Node.js webhook relay every few seconds,
/// retrieves incoming Meta payloads, and forwards them to our own local webhook endpoint
/// so they are processed normally without requiring a public IP for the local machine.
/// </summary>
public class WebhookRelayPoller : BackgroundService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebhookRelayPoller> _logger;
    private readonly IConfiguration _config;
    private DateTime _lastPingTime = DateTime.MinValue;
    
    // Default to the Render app URL if provided via config
    private string RelayUrl => _config["WebhookRelay:Url"] ?? "https://hotel-sankalp-retreat.onrender.com";
    
    // We forward to our own local endpoint
    private const string LocalWebhookUrl = "http://localhost:59725/api/webhooks/whatsapp";

    public WebhookRelayPoller(IHttpClientFactory httpClientFactory, ILogger<WebhookRelayPoller> logger, IConfiguration config)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _config = config;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WebhookRelayPoller started. Polling {Url} every 5 seconds.", RelayUrl);
        
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(10);
                
                // 1. Fetch queued webhooks from Render
                var response = await client.GetAsync($"{RelayUrl.TrimEnd('/')}/api/webhooks/poll", stoppingToken);
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync(stoppingToken);
                    var payloads = JsonSerializer.Deserialize<JsonElement[]>(content);
                    
                    if (payloads != null && payloads.Length > 0)
                    {
                        _logger.LogInformation("Fetched {Count} webhook payloads from relay.", payloads.Length);
                        
                        // 2. Forward each payload to our local webhook endpoint
                        foreach (var payload in payloads)
                        {
                            var json = payload.GetRawText();
                            var localContent = new StringContent(json, Encoding.UTF8, "application/json");
                            
                            var postResponse = await client.PostAsync(LocalWebhookUrl, localContent, stoppingToken);
                            if (!postResponse.IsSuccessStatusCode)
                            {
                                _logger.LogError("Failed to forward payload to local endpoint. Status: {Status}", postResponse.StatusCode);
                            }
                            else
                            {
                                _logger.LogInformation("Successfully forwarded payload to local processing.");
                            }
                        }
                    }
                } // Added missing brace here

                // 3. Ping the server every 10 minutes to keep it awake and verify it's live
                if (DateTime.UtcNow - _lastPingTime > TimeSpan.FromMinutes(10))
                {
                    var pingResponse = await client.GetAsync($"{RelayUrl.TrimEnd('/')}/ping", stoppingToken);
                    if (pingResponse.IsSuccessStatusCode)
                    {
                        var pingContent = await pingResponse.Content.ReadAsStringAsync(stoppingToken);
                        _logger.LogInformation("✅ Render Webhook Server is {Status}! Ping successful.", pingContent.ToUpper());
                    }
                    _lastPingTime = DateTime.UtcNow;
                }
            }
            catch (HttpRequestException ex)
            {
                // Silently swallow connection errors so we don't spam logs if Render is sleeping
                _logger.LogDebug(ex, "Could not reach the webhook relay.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while polling webhook relay.");
            }
        }
    }
}

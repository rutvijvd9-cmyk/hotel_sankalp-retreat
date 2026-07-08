namespace HotelAutomation.Connectors.WhatsApp;

using HotelAutomation.Core.Interfaces;
using HotelAutomation.Core.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

/// <summary>
/// Implements IMessageChannel for WhatsApp.
/// Converts an OutboxMessage into Meta's template-message JSON format and sends it.
/// </summary>
public sealed class WhatsAppMessageSender : IMessageChannel
{
    private readonly WhatsAppApiClient _apiClient;
    private readonly ITemplateRepository _templateRepository;
    private readonly ILogger<WhatsAppMessageSender> _logger;

    public string ChannelType => "WhatsApp";

    public WhatsAppMessageSender(
        WhatsAppApiClient apiClient,
        ITemplateRepository templateRepository,
        ILogger<WhatsAppMessageSender> logger)
    {
        _apiClient = apiClient;
        _templateRepository = templateRepository;
        _logger = logger;
    }

    public async Task<(bool Success, string? ExternalId, string? FailureReason)> SendAsync(OutboxMessage message, CancellationToken ct = default)
    {
        var template = await _templateRepository.GetByIdAsync(message.TemplateId, ct);
        if (template is null)
        {
            _logger.LogError("Template {TemplateId} not found for OutboxMessage {Id}",
                message.TemplateId, message.Id);
            return (false, null, "Associated template not found in database.");
        }

        List<string> parameterValues = new();
        try
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(message.ParametersJson);
            if (dict is null) throw new JsonException("Deserialized to null");
            parameterValues = dict.Values.Select(v => v.ToString()).ToList();
        }
        catch (JsonException)
        {
            try
            {
                parameterValues = JsonSerializer.Deserialize<List<string>>(message.ParametersJson)
                                  ?? new List<string>();
            }
            catch
            {
                _logger.LogWarning("Failed to parse ParametersJson for OutboxMessage {Id}", message.Id);
            }
        }

        // Use BodyParamCount to decide whether to include body components.
        // BodyParamCount = 0  → template has no {{N}} placeholders, omit components (avoids Meta #132000)
        // BodyParamCount = -1 → unknown, send with contact params as before
        object templatePayload;
        if (template.BodyParamCount == 0)
        {
            // Zero-param template (e.g. hello_world) — send clean payload with no components
            templatePayload = new
            {
                messaging_product = "whatsapp",
                to = message.ToPhoneNumber,
                type = "template",
                template = new
                {
                    name = template.ExternalTemplateName,
                    language = new { code = template.LanguageCode }
                }
            };
        }
        else if (parameterValues.Count > 0)
        {
            templatePayload = new
            {
                messaging_product = "whatsapp",
                to = message.ToPhoneNumber,
                type = "template",
                template = new
                {
                    name = template.ExternalTemplateName,
                    language = new { code = template.LanguageCode },
                    components = new[]
                    {
                        new
                        {
                            type = "body",
                            parameters = parameterValues
                                .Select(v => new { type = "text", text = v })
                                .ToArray()
                        }
                    }
                }
            };
        }
        else
        {
            templatePayload = new
            {
                messaging_product = "whatsapp",
                to = message.ToPhoneNumber,
                type = "template",
                template = new
                {
                    name = template.ExternalTemplateName,
                    language = new { code = template.LanguageCode }
                }
            };
        }

        var (success, responseBody) = await _apiClient.PostAsync(templatePayload, ct);

        string? externalId = null;
        string? failureReason = null;

        if (!success)
        {
            _logger.LogWarning("WhatsApp send failed for OutboxMessage {Id}: {Error}",
                message.Id, responseBody);
            
            if (!string.IsNullOrEmpty(responseBody))
            {
                try
                {
                    using var doc = JsonDocument.Parse(responseBody);
                    if (doc.RootElement.TryGetProperty("error", out var errorEl))
                    {
                        var errorMsg = errorEl.TryGetProperty("message", out var mProp) ? mProp.GetString() : null;
                        var errorCode = errorEl.TryGetProperty("code", out var cProp) ? cProp.GetInt32() : 0;
                        var details = errorEl.TryGetProperty("error_data", out var edProp) && edProp.TryGetProperty("details", out var dProp) ? dProp.GetString() : null;

                        if (errorCode == 131030 || details == "Recipient phone number not in allowed list" || errorMsg?.Contains("allowed list") == true)
                        {
                            failureReason = "Recipient phone number is not verified or added to the developer allowed list in Meta Portal.";
                        }
                        else if (errorCode == 190 || errorMsg?.Contains("token") == true)
                        {
                            failureReason = "The access token has expired or is invalid. Please update the Meta Token in Settings.";
                        }
                        else
                        {
                            failureReason = details ?? errorMsg ?? "API request rejected by WhatsApp.";
                        }
                    }
                }
                catch
                {
                    failureReason = "Invalid response from WhatsApp API server.";
                }
            }
            
            if (string.IsNullOrEmpty(failureReason))
            {
                failureReason = "Failed to connect to Meta WhatsApp API server.";
            }
        }
        else if (!string.IsNullOrEmpty(responseBody))
        {
            try
            {
                using var doc = JsonDocument.Parse(responseBody);
                if (doc.RootElement.TryGetProperty("messages", out var messagesArray) &&
                    messagesArray.GetArrayLength() > 0 &&
                    messagesArray[0].TryGetProperty("id", out var idProp))
                {
                    externalId = idProp.GetString();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse WhatsApp response for wamid");
            }
        }

        return (success, externalId, failureReason);
    }
}

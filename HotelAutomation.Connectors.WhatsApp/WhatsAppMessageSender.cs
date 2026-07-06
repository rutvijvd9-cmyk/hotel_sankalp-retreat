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

    public WhatsAppMessageSender(
        WhatsAppApiClient apiClient,
        ITemplateRepository templateRepository,
        ILogger<WhatsAppMessageSender> logger)
    {
        _apiClient = apiClient;
        _templateRepository = templateRepository;
        _logger = logger;
    }

    public async Task<bool> SendAsync(OutboxMessage message, CancellationToken ct = default)
    {
        var template = await _templateRepository.GetByIdAsync(message.TemplateId, ct);
        if (template is null)
        {
            _logger.LogError("Template {TemplateId} not found for OutboxMessage {Id}",
                message.TemplateId, message.Id);
            return false;
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

        var payload = new
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

        var (success, errorBody) = await _apiClient.PostAsync(payload, ct);

        if (!success)
            _logger.LogWarning("WhatsApp send failed for OutboxMessage {Id}: {Error}",
                message.Id, errorBody);

        return success;
    }
}

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
    private readonly IPdfGenerator _pdfGenerator;
    private readonly ILogger<WhatsAppMessageSender> _logger;

    public string ChannelType => "WhatsApp";

    public WhatsAppMessageSender(
        WhatsAppApiClient apiClient,
        ITemplateRepository templateRepository,
        IPdfGenerator pdfGenerator,
        ILogger<WhatsAppMessageSender> logger)
    {
        _apiClient = apiClient;
        _templateRepository = templateRepository;
        _pdfGenerator = pdfGenerator;
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

        // Deserialize the raw payload dict from the job (contains Name, Room, CheckInDate, etc.)
        Dictionary<string, string> eventDict = new(StringComparer.OrdinalIgnoreCase);
        try
        {
            var rawDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(message.ParametersJson);
            if (rawDict != null)
            {
                foreach (var kv in rawDict)
                    eventDict[kv.Key] = kv.Value.ToString();
            }
        }
        catch { }

        // Try to resolve parameters in order using ParameterDefinitionsJson
        bool resolvedByDefs = false;
        if (!string.IsNullOrEmpty(template.ParameterDefinitionsJson))
        {
            try
            {
                var defs = JsonSerializer.Deserialize<List<ParameterDefinition>>(
                    template.ParameterDefinitionsJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (defs != null && defs.Count > 0)
                {
                    resolvedByDefs = true;
                    parameterValues = defs
                        .OrderBy(d => d.Position)
                        .Select(d =>
                        {
                            // Priority 1: DynamicKey — live event/API payload (e.g. CheckInDate)
                            if (!string.IsNullOrEmpty(d.DynamicKey) && eventDict.TryGetValue(d.DynamicKey, out var dynVal) && !string.IsNullOrWhiteSpace(dynVal))
                                return dynVal;

                            // Priority 2: SourceColumn — Contact DB field (e.g. FirstName)
                            if (!string.IsNullOrEmpty(d.SourceColumn) && eventDict.TryGetValue(d.SourceColumn, out var colVal) && !string.IsNullOrWhiteSpace(colVal))
                                return colVal;

                            // Priority 3: SampleValue — fallback for Meta-approved demo value
                            if (!string.IsNullOrEmpty(d.SampleValue))
                                return d.SampleValue;

                            // Priority 4: Any value from the event dict by label match
                            if (!string.IsNullOrEmpty(d.Label) && eventDict.TryGetValue(d.Label, out var labelVal) && !string.IsNullOrWhiteSpace(labelVal))
                                return labelVal;

                            return " "; // Meta requires non-empty strings
                        })
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse ParameterDefinitionsJson for Template {TemplateId}", message.TemplateId);
            }
        }

        // Fallback: if no defs, use values from the event dict in insertion order
        if (!resolvedByDefs)
        {
            try
            {
                // Try as ordered list first
                var list = JsonSerializer.Deserialize<List<string>>(message.ParametersJson);
                if (list != null) parameterValues = list;
            }
            catch
            {
                parameterValues = eventDict.Values.ToList();
            }
        }

        // Trim to BodyParamCount to avoid Meta error #132000
        if (template.BodyParamCount > 0 && parameterValues.Count >= template.BodyParamCount)
        {
            parameterValues = parameterValues.Take(template.BodyParamCount).ToList();
        }

        var components = new List<object>();

        // Handle Document Header if required
        if (template.HeaderType == "Document")
        {
            try
            {
                // Generate PDF using dummy ScheduledJob constructed from Outbox message
                var job = new ScheduledJob 
                { 
                    ParametersJson = message.ParametersJson,
                    GuestName = "Guest" // Can extract from parameters if needed
                };
                
                var pdfBytes = await _pdfGenerator.GenerateInvoicePdfAsync(job, ct);
                
                // Upload to Meta
                var (uploadSuccess, mediaId, uploadError) = await _apiClient.UploadMediaAsync(pdfBytes, $"Invoice_{DateTime.Now:yyyyMMdd}.pdf", "application/pdf", ct);
                
                if (!uploadSuccess || string.IsNullOrEmpty(mediaId))
                {
                    _logger.LogError("Failed to upload PDF invoice for OutboxMessage {Id}: {Error}", message.Id, uploadError);
                    return (false, null, $"Failed to upload PDF to Meta: {uploadError}");
                }

                components.Add(new
                {
                    type = "header",
                    parameters = new[]
                    {
                        new
                        {
                            type = "document",
                            document = new
                            {
                                id = mediaId,
                                filename = $"Invoice_{DateTime.Now:yyyyMMdd}.pdf"
                            }
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate or upload PDF invoice for OutboxMessage {Id}", message.Id);
                return (false, null, $"PDF Generation failed: {ex.Message}");
            }
        }
        // Static text headers do not require parameters and are sent automatically by Meta.
        // Dynamic text headers are not supported yet, so we omit the header component for Text type.
        else if (template.HeaderType == "Image" && !string.IsNullOrWhiteSpace(template.HeaderText))
        {
            components.Add(new
            {
                type = "header",
                parameters = new[]
                {
                    new
                    {
                        type = "image",
                        image = new
                        {
                            link = template.HeaderText
                        }
                    }
                }
            });
        }
        else if (template.HeaderType == "Video" && !string.IsNullOrWhiteSpace(template.HeaderText))
        {
            components.Add(new
            {
                type = "header",
                parameters = new[]
                {
                    new
                    {
                        type = "video",
                        video = new
                        {
                            link = template.HeaderText
                        }
                    }
                }
            });
        }

        // Handle Body
        if (template.BodyParamCount != 0 && parameterValues.Count > 0)
        {
            components.Add(new
            {
                type = "body",
                parameters = parameterValues
                    .Select(v => new { type = "text", text = string.IsNullOrWhiteSpace(v) ? " " : v })
                    .ToArray()
            });
        }

        object templatePayload;
        if (components.Count > 0)
        {
            templatePayload = new
            {
                messaging_product = "whatsapp",
                to = message.ToPhoneNumber,
                type = "template",
                template = new
                {
                    name = template.ExternalTemplateName?.Trim(),
                    language = new { code = template.LanguageCode?.Trim() },
                    components = components.ToArray()
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
                    name = template.ExternalTemplateName?.Trim(),
                    language = new { code = template.LanguageCode?.Trim() }
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

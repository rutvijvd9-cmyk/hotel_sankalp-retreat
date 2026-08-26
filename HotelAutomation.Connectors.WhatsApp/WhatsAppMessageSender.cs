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
                            // Priority 1: DynamicKey — live event/API payload (e.g. CheckInDate, Name)
                            if (!string.IsNullOrEmpty(d.DynamicKey))
                            {
                                var val = GetValueWithFallback(eventDict, d.DynamicKey);
                                if (!string.IsNullOrWhiteSpace(val)) return val;
                            }

                            // Priority 2: SourceColumn — Contact DB field (e.g. FirstName)
                            if (!string.IsNullOrEmpty(d.SourceColumn))
                            {
                                var val = GetValueWithFallback(eventDict, d.SourceColumn);
                                if (!string.IsNullOrWhiteSpace(val)) return val;
                            }

                            // Priority 3: SampleValue — fallback for Meta-approved demo value
                            if (!string.IsNullOrEmpty(d.SampleValue))
                                return d.SampleValue;

                            // Priority 4: Any value from the event dict by label match
                            if (!string.IsNullOrEmpty(d.Label))
                            {
                                var val = GetValueWithFallback(eventDict, d.Label);
                                if (!string.IsNullOrWhiteSpace(val)) return val;
                            }

                            // Priority 5: Smart positional deduction when definitions are unmapped (fresh install / new sync)
                            if (d.Position == 1)
                            {
                                var val = GetValueWithFallback(eventDict, "Name");
                                if (string.IsNullOrWhiteSpace(val)) val = GetValueWithFallback(eventDict, "FirstName");
                                if (!string.IsNullOrWhiteSpace(val)) return val;
                            }
                            else if (d.Position == 2)
                            {
                                var val = GetValueWithFallback(eventDict, "HotelName");
                                if (!string.IsNullOrWhiteSpace(val)) return val;
                            }

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

        // Fallback: if no defs or empty list, build from event dict
        if (!resolvedByDefs || parameterValues.Count == 0)
        {
            try
            {
                // Try as ordered list first
                var list = JsonSerializer.Deserialize<List<string>>(message.ParametersJson);
                if (list != null && list.Count > 0) parameterValues = list;
            }
            catch { }

            if (parameterValues.Count == 0)
            {
                var nameVal = GetValueWithFallback(eventDict, "Name");
                if (string.IsNullOrWhiteSpace(nameVal)) nameVal = GetValueWithFallback(eventDict, "FirstName");
                if (string.IsNullOrWhiteSpace(nameVal)) nameVal = "Guest";

                var hotelVal = GetValueWithFallback(eventDict, "HotelName");
                if (string.IsNullOrWhiteSpace(hotelVal)) hotelVal = "The Sankalp Retreat";

                parameterValues = new List<string> { nameVal, hotelVal };
            }
        }

        // Post-processing sanity check: ensure any blank/space-only parameter gets resolved to a real value
        if (parameterValues.Count > 0 && string.IsNullOrWhiteSpace(parameterValues[0]))
        {
            var nameVal = GetValueWithFallback(eventDict, "Name");
            if (string.IsNullOrWhiteSpace(nameVal)) nameVal = GetValueWithFallback(eventDict, "FirstName");
            if (string.IsNullOrWhiteSpace(nameVal)) nameVal = "Guest";
            parameterValues[0] = nameVal;
        }
        if (parameterValues.Count > 1 && string.IsNullOrWhiteSpace(parameterValues[1]))
        {
            var hotelVal = GetValueWithFallback(eventDict, "HotelName");
            if (string.IsNullOrWhiteSpace(hotelVal)) hotelVal = "The Sankalp Retreat";
            parameterValues[1] = hotelVal;
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
                // Check if a specific PDF Base64 string is provided in the event parameters
                string? base64Str = null;
                if (eventDict.TryGetValue("pdfBase64", out var b1) && !string.IsNullOrWhiteSpace(b1)) base64Str = b1;
                else if (eventDict.TryGetValue("billPdfBase64", out var b2) && !string.IsNullOrWhiteSpace(b2)) base64Str = b2;

                // Check if a specific PDF URL is provided in the event parameters
                string? pdfUrl = null;
                if (eventDict.TryGetValue("billPdfUrl", out var url1) && !string.IsNullOrWhiteSpace(url1)) pdfUrl = url1;
                else if (eventDict.TryGetValue("pdfUrl", out var url2) && !string.IsNullOrWhiteSpace(url2)) pdfUrl = url2;
                else if (eventDict.TryGetValue("InvoicePdfUrl", out var url3) && !string.IsNullOrWhiteSpace(url3)) pdfUrl = url3;
                else if (eventDict.TryGetValue("document", out var url4) && !string.IsNullOrWhiteSpace(url4)) pdfUrl = url4;

                if (!string.IsNullOrWhiteSpace(base64Str))
                {
                    // Decode Base64 string and upload directly to Meta
                    var pdfBytes = Convert.FromBase64String(base64Str);
                    var (uploadSuccess, mediaId, uploadError) = await _apiClient.UploadMediaAsync(pdfBytes, $"Invoice_{DateTime.Now:yyyyMMdd}.pdf", "application/pdf", ct);
                    
                    if (!uploadSuccess || string.IsNullOrEmpty(mediaId))
                    {
                        _logger.LogError("Failed to upload Base64 PDF invoice for OutboxMessage {Id}: {Error}", message.Id, uploadError);
                        return (false, null, $"Failed to upload Base64 PDF to Meta: {uploadError}");
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
                else if (pdfUrl != null && (pdfUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || pdfUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                {
                    // Use the provided PDF URL directly!
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
                                    link = pdfUrl,
                                    filename = $"Invoice_{DateTime.Now:yyyyMMdd}.pdf"
                                }
                            }
                        }
                    });
                }
                else
                {
                    byte[]? pdfBytes = null;
                    string pdfFileName = $"Invoice_{DateTime.Now:yyyyMMdd}.pdf";

                    // ── Priority: Use the real stamped PDF saved by PdfPmsConnector ──
                    // Keys checked: "Recipient Signature & Stamp", then "Recipient No Signature & Stamp"
                    string? relPdfPath = null;
                    if (eventDict.TryGetValue("Recipient Signature & Stamp", out var rss) && !string.IsNullOrWhiteSpace(rss))
                        relPdfPath = rss;
                    else if (eventDict.TryGetValue("Recipient No Signature & Stamp", out var rnss) && !string.IsNullOrWhiteSpace(rnss))
                        relPdfPath = rnss;

                    if (!string.IsNullOrWhiteSpace(relPdfPath))
                    {
                        // Resolve relative path against the application's base directory
                        // e.g. "PDFs/Processed/18-08-2026/Signature and stamp/RUTVIJ_1313_20260818.pdf"
                        var normalised = relPdfPath.Replace('/', Path.DirectorySeparatorChar);
                        var absPath = Path.IsPathRooted(normalised)
                            ? normalised
                            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, normalised));

                        _logger.LogInformation("WhatsApp sender: real PDF path resolved to '{AbsPath}'", absPath);

                        if (File.Exists(absPath))
                        {
                            pdfBytes = await File.ReadAllBytesAsync(absPath, ct);
                            pdfFileName = Path.GetFileName(absPath);
                            _logger.LogInformation("WhatsApp sender: loaded real PDF '{File}' ({Bytes} bytes)", pdfFileName, pdfBytes.Length);
                        }
                        else
                        {
                            _logger.LogWarning("WhatsApp sender: real PDF not found at '{AbsPath}', falling back to generated invoice.", absPath);
                        }
                    }

                    // ── Fallback: generate a QuestPDF invoice if the real file wasn't found ──
                    if (pdfBytes == null)
                    {
                        _logger.LogWarning("WhatsApp sender: no real PDF path in event — generating fallback invoice for OutboxMessage {Id}.", message.Id);
                        var guestName = eventDict.TryGetValue("Name", out var name) && !string.IsNullOrWhiteSpace(name) ? name :
                                        (eventDict.TryGetValue("FirstName", out var fn) ? fn : "Guest");
                        var job = new ScheduledJob
                        {
                            ParametersJson = message.ParametersJson,
                            GuestName = guestName
                        };
                        pdfBytes = await _pdfGenerator.GenerateInvoicePdfAsync(job, ct);
                    }

                    // Upload to Meta
                    var (uploadSuccess, mediaId, uploadError) = await _apiClient.UploadMediaAsync(pdfBytes, pdfFileName, "application/pdf", ct);

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
                                    filename = pdfFileName
                                }
                            }
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate or upload PDF invoice for OutboxMessage {Id}", message.Id);
                return (false, null, $"PDF Generation failed: {ex.Message}");
            }
        }
        // Static text headers with no parameters are sent automatically by Meta.
        // For dynamic text headers (containing {{1}}, etc.), we extract the max index and supply parameters.
        else if (template.HeaderType == "Text" && !string.IsNullOrWhiteSpace(template.HeaderText))
        {
            int headerParamCount = 0;
            var matches = System.Text.RegularExpressions.Regex.Matches(template.HeaderText, @"\{\{(\d+)\}\}");
            foreach (System.Text.RegularExpressions.Match m in matches)
            {
                if (int.TryParse(m.Groups[1].Value, out int val) && val > headerParamCount) headerParamCount = val;
            }

            if (headerParamCount > 0)
            {
                var headerParams = parameterValues.Take(headerParamCount).ToList();
                while (headerParams.Count < headerParamCount) headerParams.Add(" ");

                components.Add(new
                {
                    type = "header",
                    parameters = headerParams
                        .Select(v => new { type = "text", text = string.IsNullOrWhiteSpace(v) ? " " : v })
                        .ToArray()
                });
            }
        }
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

        _logger.LogInformation("Sending WhatsApp message to {Phone} with template '{Template}' (Params: {Params})",
            message.ToPhoneNumber, template.ExternalTemplateName, JsonSerializer.Serialize(parameterValues));

        var (success, responseBody) = await _apiClient.PostAsync(templatePayload, ct);

        string? externalId = null;
        string? failureReason = null;

        if (!success)
        {
            if (!string.IsNullOrEmpty(responseBody))
            {
                try
                {
                    using var doc = JsonDocument.Parse(responseBody);
                    if (doc.RootElement.TryGetProperty("error", out var errorEl))
                    {
                        var errorMsg = errorEl.TryGetProperty("message", out var mProp) ? mProp.GetString() : null;
                        var errorCode = errorEl.TryGetProperty("code", out var cProp) ? cProp.GetInt32() : 0;
                        var errorSubcode = errorEl.TryGetProperty("error_subcode", out var scProp) ? scProp.GetInt32() : 0;
                        var details = errorEl.TryGetProperty("error_data", out var edProp) && edProp.TryGetProperty("details", out var dProp) ? dProp.GetString() : null;

                        if (errorCode == 131030 || details == "Recipient phone number not in allowed list" || errorMsg?.Contains("allowed list") == true)
                        {
                            failureReason = "Recipient phone number is not verified or added to the developer allowed list in Meta Portal.";
                        }
                        else if (errorCode == 190 || errorMsg?.Contains("token") == true)
                        {
                            failureReason = "The access token has expired or is invalid. Please update the Meta Token in Settings.";
                        }
                        else if (errorCode == 132000 || errorMsg?.Contains("number of parameters") == true)
                        {
                            failureReason = $"Template parameter count mismatch: Meta expects a different number of parameters. {errorMsg}";
                        }
                        else if (errorCode == 131026 || errorMsg?.Contains("Message Undeliverable") == true)
                        {
                            failureReason = "Message Undeliverable: The recipient number cannot receive WhatsApp messages or has blocked the business.";
                        }
                        else
                        {
                            failureReason = details ?? errorMsg ?? $"API request rejected by Meta (Code: {errorCode}, Subcode: {errorSubcode}).";
                        }

                        _logger.LogError("WhatsApp API error for {Phone}. Code: {Code}, Subcode: {Subcode}, Reason: {Reason}, Raw: {Response}",
                            message.ToPhoneNumber, errorCode, errorSubcode, failureReason, responseBody);
                    }
                }
                catch
                {
                    failureReason = "Invalid response from WhatsApp API server.";
                    _logger.LogError("Failed to parse WhatsApp API response for {Phone}: {Response}", message.ToPhoneNumber, responseBody);
                }
            }
            
            if (string.IsNullOrEmpty(failureReason))
            {
                failureReason = "Failed to connect to Meta WhatsApp API server.";
                _logger.LogError("WhatsApp connection error for {Phone} (Outbox ID: {Id})", message.ToPhoneNumber, message.Id);
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
                    _logger.LogInformation("WhatsApp message sent successfully to {Phone}. Meta Message ID: {Wamid}",
                        message.ToPhoneNumber, externalId);
                }
                else
                {
                    _logger.LogInformation("WhatsApp message accepted by Meta for {Phone}. Response: {Response}",
                        message.ToPhoneNumber, responseBody);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse WhatsApp response for wamid");
            }
        }

        return (success, externalId, failureReason);
    }

    private static string GetValueWithFallback(Dictionary<string, string> dict, string key)
    {
        if (string.IsNullOrEmpty(key)) return "";
        
        // Try exact case-insensitive match first
        if (dict.TryGetValue(key, out var val) && !string.IsNullOrWhiteSpace(val))
            return val;
            
        // Generate list of fallback candidates based on the requested key
        var candidates = new List<string>();
        var normalizedKey = key.Replace("_", "").Replace("-", "").ToLowerInvariant();
        
        if (normalizedKey == "name" || normalizedKey == "guestname" || normalizedKey == "fullname" || normalizedKey == "firstname" || normalizedKey == "guest")
        {
            candidates.AddRange(new[] { "Name", "FirstName", "First Name", "GuestName", "FullName", "LastName", "name", "guest_name" });
        }
        else if (normalizedKey == "hotelname" || normalizedKey == "hotel")
        {
            candidates.AddRange(new[] { "HotelName", "Hotel", "hotel_name", "hotel" });
        }
        else if (normalizedKey == "roomnumber" || normalizedKey == "room" || normalizedKey == "room_number")
        {
            candidates.AddRange(new[] { "RoomNumber", "Room", "room_number", "room" });
        }
        else if (normalizedKey == "billamount" || normalizedKey == "totalbill" || normalizedKey == "amount" || normalizedKey == "total_bill" || normalizedKey == "bill_amount")
        {
            candidates.AddRange(new[] { "TotalBill", "Amount", "bill_amount", "total_bill", "amount" });
        }
        else if (normalizedKey == "checkin" || normalizedKey == "checkindate" || normalizedKey == "check_in")
        {
            candidates.AddRange(new[] { "CheckInDate", "CheckIn", "check_in", "check_in_date" });
        }
        else if (normalizedKey == "checkout" || normalizedKey == "checkoutdate" || normalizedKey == "check_out")
        {
            candidates.AddRange(new[] { "CheckOutDate", "CheckOut", "check_out", "check_out_date" });
        }
        else if (normalizedKey == "bookingref" || normalizedKey == "bookingreference" || normalizedKey == "booking_ref")
        {
            candidates.AddRange(new[] { "BookingReference", "BookingRef", "booking_ref", "booking_reference" });
        }
        else if (normalizedKey == "feedbacklink" || normalizedKey == "reviewlink" || normalizedKey == "review_link")
        {
            candidates.AddRange(new[] { "FeedbackLink", "ReviewLink", "review_link", "feedback_link" });
        }
        
        // Also add a general case-insensitive check for the key (e.g. "HotelName" -> "hotelname")
        foreach (var kv in dict)
        {
            if (kv.Key.Replace("_", "").Replace("-", "").ToLowerInvariant() == normalizedKey)
            {
                if (!string.IsNullOrWhiteSpace(kv.Value))
                    return kv.Value;
            }
        }
        
        foreach (var candidate in candidates)
        {
            if (dict.TryGetValue(candidate, out var candidateVal) && !string.IsNullOrWhiteSpace(candidateVal))
                return candidateVal;
        }
        
        return "";
    }
}

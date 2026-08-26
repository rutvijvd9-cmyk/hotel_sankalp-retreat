namespace HotelAutomation.Worker.Services;

using System.Collections.Concurrent;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public sealed class EmailAlertOptions
{
    public const string SectionName = "EmailAlerts";

    public bool Enabled { get; set; } = true;
    public string SmtpHost { get; set; } = "smtp.gmail.com";
    public int SmtpPort { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string Username { get; set; } = "rutvijpatel0709@gmail.com";
    public string Password { get; set; } = "lcej wbpj loux thxm";
    public string SenderEmail { get; set; } = "rutvijpatel0709@gmail.com";
    public string SenderName { get; set; } = "Hotel Sankalp Retreat - System Alert";
    public List<string> Recipients { get; set; } = new()
    {
        "rutvijpatel0709@gmail.com",
        "rutvijsk@outlook.com",
        "info@scalifts.com",
        "support@scalifts.com"
    };
    public int DeduplicationWindowMinutes { get; set; } = 10;
}

public sealed record EmailAlertPayload(
    string Level,
    string Category,
    string Source,
    string Message,
    string? Details,
    string? Exception,
    DateTimeOffset Timestamp
);

public sealed class EmailAlertService : BackgroundService
{
    private readonly EmailAlertOptions _options;
    private readonly ILogger<EmailAlertService> _logger;
    private readonly Channel<EmailAlertPayload> _channel;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _recentAlerts = new(StringComparer.Ordinal);
    private static EmailAlertService? _instance;

    public EmailAlertService(
        IOptions<EmailAlertOptions> options,
        ILogger<EmailAlertService> logger)
    {
        _options = options.Value ?? new EmailAlertOptions();
        _logger = logger;
        _channel = Channel.CreateBounded<EmailAlertPayload>(new BoundedChannelOptions(500)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });
        _instance = this;
    }

    public static EmailAlertService? Instance => _instance;

    public EmailAlertOptions Options => _options;

    public bool EnqueueAlert(
        string level,
        string category,
        string source,
        string message,
        string? details = null,
        string? exception = null)
    {
        if (!_options.Enabled || _options.Recipients.Count == 0) return false;

        // Only alert on ERROR, FATAL, CRITICAL, or explicit tests
        var normalizedLevel = (level ?? "INFO").ToUpperInvariant();
        if (normalizedLevel != "ERROR" && normalizedLevel != "FATAL" && normalizedLevel != "CRITICAL" && normalizedLevel != "TEST")
        {
            return false;
        }

        // Deduplication to prevent inbox flooding on repeated loops
        if (normalizedLevel != "TEST")
        {
            var deduplicationKey = GenerateDeduplicationKey(normalizedLevel, category, source, message, exception);
            var now = DateTimeOffset.UtcNow;
            if (_recentAlerts.TryGetValue(deduplicationKey, out var lastSent))
            {
                if (now - lastSent < TimeSpan.FromMinutes(_options.DeduplicationWindowMinutes))
                {
                    // Rate-limited: already sent recently
                    return false;
                }
            }
            _recentAlerts[deduplicationKey] = now;
        }

        var payload = new EmailAlertPayload(
            normalizedLevel,
            category ?? "System",
            source ?? "HotelAutomation",
            message ?? "Unspecified error",
            details,
            exception,
            DateTimeOffset.Now
        );

        return _channel.Writer.TryWrite(payload);
    }

    public async Task<(bool Success, string? ErrorMessage)> SendDirectAlertAsync(
        string level,
        string category,
        string source,
        string message,
        string? details = null,
        string? exception = null,
        CancellationToken ct = default)
    {
        var payload = new EmailAlertPayload(
            (level ?? "INFO").ToUpperInvariant(),
            category ?? "System",
            source ?? "HotelAutomation",
            message ?? "Unspecified error",
            details,
            exception,
            DateTimeOffset.Now
        );

        return await SendEmailInternalAsync(payload, ct);
    }

    public static void SendCrashAlertDirect(string crashType, Exception? ex, string? customMessage = null)
    {
        try
        {
            var inst = _instance;
            var opts = inst?._options ?? new EmailAlertOptions();
            if (!opts.Enabled || opts.Recipients.Count == 0) return;

            var payload = new EmailAlertPayload(
                "FATAL CRASH",
                "System Crash",
                crashType,
                customMessage ?? ex?.Message ?? "Fatal process crash",
                null,
                ex?.ToString(),
                DateTimeOffset.Now
            );

            using var smtp = CreateSmtpClient(opts);
            using var mail = CreateMailMessage(opts, payload);
            smtp.Send(mail);
        }
        catch { /* Failsafe during process termination */ }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var reader = _channel.Reader;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (await reader.WaitToReadAsync(stoppingToken))
                {
                    while (reader.TryRead(out var item))
                    {
                        try
                        {
                            var result = await SendEmailInternalAsync(item, stoppingToken);
                            if (!result.Success)
                            {
                                _logger.LogWarning("Failed to send email alert: {Error}", result.ErrorMessage);
                            }
                            else
                            {
                                _logger.LogInformation("Email alert sent successfully for [{Level}] {Category}: {Message}",
                                    item.Level, item.Category, item.Message);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Unexpected error in email dispatch loop");
                        }

                        // Prune stale deduplication cache entries periodically
                        PruneStaleDeduplications();
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error waiting for email alert channel reader");
                await Task.Delay(1000, stoppingToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            }
        }
    }

    private async Task<(bool Success, string? ErrorMessage)> SendEmailInternalAsync(
        EmailAlertPayload payload,
        CancellationToken ct)
    {
        try
        {
            using var smtp = CreateSmtpClient(_options);
            using var mail = CreateMailMessage(_options, payload);

            await smtp.SendMailAsync(mail, ct);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private static SmtpClient CreateSmtpClient(EmailAlertOptions opts)
    {
        var cleanPassword = (opts.Password ?? string.Empty).Replace(" ", "").Trim();
        var client = new SmtpClient(opts.SmtpHost, opts.SmtpPort)
        {
            EnableSsl = opts.EnableSsl,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(opts.Username.Trim(), cleanPassword),
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = 15000 // 15 seconds
        };
        return client;
    }

    private static MailMessage CreateMailMessage(EmailAlertOptions opts, EmailAlertPayload payload)
    {
        var mail = new MailMessage();
        mail.From = new MailAddress(opts.SenderEmail.Trim(), opts.SenderName.Trim());

        foreach (var recipient in opts.Recipients)
        {
            var trimmed = recipient?.Trim();
            if (!string.IsNullOrEmpty(trimmed) && trimmed.Contains('@'))
            {
                mail.To.Add(trimmed);
            }
        }

        var isFatal = payload.Level.Contains("FATAL", StringComparison.OrdinalIgnoreCase) ||
                      payload.Level.Contains("CRASH", StringComparison.OrdinalIgnoreCase);
        var isTest = payload.Level.Equals("TEST", StringComparison.OrdinalIgnoreCase);

        var prefix = isTest ? "[TEST ALERT]" : (isFatal ? "[CRITICAL CRASH]" : "[SYSTEM ERROR]");
        mail.Subject = $"{prefix} Hotel Sankalp Retreat: {payload.Category} - {payload.Message.TakeTrimmed(60)}";
        mail.IsBodyHtml = true;

        var html = BuildHtmlBody(payload, isFatal, isTest);
        mail.Body = html;

        return mail;
    }

    private static string BuildHtmlBody(EmailAlertPayload payload, bool isFatal, bool isTest)
    {
        var bannerColor = isTest ? "#2563eb" : (isFatal ? "#dc2626" : "#ea580c");
        var badgeText = isTest ? "TEST ALERT" : (isFatal ? "CRITICAL FATAL CRASH" : "SYSTEM ERROR ALERT");

        var sb = new StringBuilder();
        sb.Append($@"<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8'/>
<style>
  body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background-color: #f4f5f7; margin: 0; padding: 20px; color: #1e293b; }}
  .card {{ max-width: 680px; margin: 0 auto; background: #ffffff; border-radius: 8px; border: 1px solid #e2e8f0; overflow: hidden; box-shadow: 0 4px 6px -1px rgba(0,0,0,0.05); }}
  .header {{ background-color: {bannerColor}; color: #ffffff; padding: 20px 24px; }}
  .header h1 {{ margin: 0; font-size: 20px; font-weight: 700; }}
  .header .badge {{ display: inline-block; background: rgba(255,255,255,0.25); padding: 4px 10px; border-radius: 4px; font-size: 12px; font-weight: 600; text-transform: uppercase; margin-top: 6px; }}
  .content {{ padding: 24px; }}
  .summary-table {{ width: 100%; border-collapse: collapse; margin-bottom: 20px; font-size: 14px; }}
  .summary-table td {{ padding: 8px 12px; border-bottom: 1px solid #f1f5f9; }}
  .summary-table td.label {{ width: 140px; font-weight: 600; color: #64748b; background-color: #f8fafc; }}
  .message-box {{ background: #fff1f2; border: 1px solid #fecdd3; border-radius: 6px; padding: 14px; margin-bottom: 20px; color: #9f1239; font-size: 15px; font-weight: 500; word-break: break-word; }}
  .details-box {{ background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 6px; padding: 14px; margin-bottom: 20px; font-size: 13px; word-break: break-word; }}
  .stacktrace {{ background: #0f172a; color: #38bdf8; border-radius: 6px; padding: 14px; font-family: Consolas, Monaco, monospace; font-size: 12px; overflow-x: auto; white-space: pre-wrap; word-break: break-all; margin-top: 10px; max-height: 400px; }}
  .footer {{ background-color: #f8fafc; border-top: 1px solid #e2e8f0; padding: 16px 24px; font-size: 12px; color: #64748b; text-align: center; }}
</style>
</head>
<body>
<div class='card'>
  <div class='header'>
    <h1>Hotel Sankalp Retreat - System Diagnostics Alert</h1>
    <div class='badge'>{badgeText}</div>
  </div>
  <div class='content'>
    <div class='message-box'>
      {WebUtility.HtmlEncode(payload.Message)}
    </div>

    <table class='summary-table'>
      <tr>
        <td class='label'>Severity Level</td>
        <td><strong>{WebUtility.HtmlEncode(payload.Level)}</strong></td>
      </tr>
      <tr>
        <td class='label'>Component / Category</td>
        <td>{WebUtility.HtmlEncode(payload.Category)}</td>
      </tr>
      <tr>
        <td class='label'>Source</td>
        <td><code>{WebUtility.HtmlEncode(payload.Source)}</code></td>
      </tr>
      <tr>
        <td class='label'>Timestamp</td>
        <td>{payload.Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}</td>
      </tr>
      <tr>
        <td class='label'>Machine & OS</td>
        <td>{WebUtility.HtmlEncode(Environment.MachineName)} ({WebUtility.HtmlEncode(Environment.OSVersion.ToString())})</td>
      </tr>
      <tr>
        <td class='label'>Process ID</td>
        <td>{Environment.ProcessId} (CLR {Environment.Version})</td>
      </tr>
    </table>");

        if (!string.IsNullOrWhiteSpace(payload.Details))
        {
            sb.Append($@"
    <div>
      <strong>Additional Context:</strong>
      <div class='details-box'>{WebUtility.HtmlEncode(payload.Details)}</div>
    </div>");
        }

        if (!string.IsNullOrWhiteSpace(payload.Exception))
        {
            sb.Append($@"
    <div>
      <strong>Exception & Stack Trace:</strong>
      <pre class='stacktrace'>{WebUtility.HtmlEncode(payload.Exception)}</pre>
    </div>");
        }

        sb.Append($@"
  </div>
  <div class='footer'>
    This automated notification was generated by the Hotel Sankalp Retreat Automation Worker.<br/>
    Host: {WebUtility.HtmlEncode(Environment.MachineName)} | Generated at: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}
  </div>
</div>
</body>
</html>");

        return sb.ToString();
    }

    private static string GenerateDeduplicationKey(string level, string category, string source, string message, string? exception)
    {
        var raw = $"{level}|{category}|{source}|{message}|{exception?.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()}";
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes);
    }

    private void PruneStaleDeduplications()
    {
        var cutoff = DateTimeOffset.UtcNow - TimeSpan.FromMinutes(_options.DeduplicationWindowMinutes * 2);
        foreach (var (key, time) in _recentAlerts)
        {
            if (time < cutoff)
            {
                _recentAlerts.TryRemove(key, out _);
            }
        }
    }
}

file static class StringExtensions
{
    public static string TakeTrimmed(this string? text, int max)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var clean = text.Replace("\r", " ").Replace("\n", " ").Trim();
        return clean.Length <= max ? clean : clean[..max] + "...";
    }
}

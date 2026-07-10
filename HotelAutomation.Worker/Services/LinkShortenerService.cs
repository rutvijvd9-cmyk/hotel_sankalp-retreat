namespace HotelAutomation.Worker.Services;

using HotelAutomation.Data;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using Dapper;

public sealed class LinkShortenerService
{
    private readonly SqliteConnectionFactory _dbFactory;
    private readonly ILogger<LinkShortenerService> _logger;
    private readonly string _baseUrl;

    // Pattern to match common http/https URLs
    private static readonly Regex UrlRegex = new Regex(@"https?:\/\/(www\.)?[-a-zA-Z0-9@:%._\+~#=]{1,256}\.[a-zA-Z0-9()]{1,6}\b([-a-zA-Z0-9()@:%_\+.~#?&//=]*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public LinkShortenerService(SqliteConnectionFactory dbFactory, ILogger<LinkShortenerService> logger, IConfiguration config)
    {
        _dbFactory = dbFactory;
        _logger = logger;
        // The base URL to point shortlinks to (e.g. "https://my-hotel-app.com")
        // Default to localhost for development
        _baseUrl = config.GetValue<string>("BaseUrl", "https://localhost:59724");
    }

    /// <summary>
    /// Scans a parameters dictionary for URLs, generates short codes, 
    /// stores them in ShortenedLinks associated with the given outboxMessageId,
    /// and returns a modified dictionary with shortened links.
    /// </summary>
    public async Task<Dictionary<string, string>> ShortenLinksInParametersAsync(int outboxMessageId, Dictionary<string, string> parameters, CancellationToken ct = default)
    {
        var updatedParameters = new Dictionary<string, string>();
        await using var conn = await _dbFactory.OpenAsync(ct);

        foreach (var kv in parameters)
        {
            var value = kv.Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                // We check if the entire value is a URL or if it contains a URL.
                // For simplicity, we just replace all matches in the string.
                var matches = UrlRegex.Matches(value);
                if (matches.Count > 0)
                {
                    foreach (Match match in matches)
                    {
                        var originalUrl = match.Value;
                        // Avoid double shortening
                        if (originalUrl.StartsWith(_baseUrl + "/api/l/")) continue;

                        var shortCode = GenerateShortCode(6);
                        
                        // Insert to DB
                        var sql = "INSERT INTO ShortenedLinks (ShortCode, OriginalUrl, OutboxMessageId) VALUES (@ShortCode, @OriginalUrl, @OutboxMessageId)";
                        await conn.ExecuteAsync(sql, new { ShortCode = shortCode, OriginalUrl = originalUrl, OutboxMessageId = outboxMessageId });

                        var shortLink = $"{_baseUrl}/api/l/{shortCode}";
                        value = value.Replace(originalUrl, shortLink);
                        
                        _logger.LogInformation("Shortened link for message {MsgId}: {Original} -> {Short}", outboxMessageId, originalUrl, shortLink);
                    }
                }
            }
            updatedParameters[kv.Key] = value;
        }

        return updatedParameters;
    }

    private string GenerateShortCode(int length)
    {
        const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }
}

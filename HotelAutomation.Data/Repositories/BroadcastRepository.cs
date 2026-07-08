namespace HotelAutomation.Data.Repositories;

using Dapper;
using HotelAutomation.Core.Models;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>
/// Handles marketing broadcast campaigns.
/// Queues one OutboxMessage per contact in the selected category.
/// </summary>
public sealed class BroadcastRepository
{
    private readonly SqliteConnectionFactory _factory;

    public BroadcastRepository(SqliteConnectionFactory factory) => _factory = factory;

    /// <summary>
    /// Queues a broadcast message to all contacts in a given category (or all contacts if categoryId is null).
    /// Returns the count of messages queued.
    /// </summary>
    public async Task<int> QueueBroadcastAsync(
        int templateId,
        int? categoryId,
        string customParametersJson = "{}",
        CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);

        var templateCategory = await conn.QueryFirstOrDefaultAsync<string>(
            "SELECT Category FROM MessageTemplates WHERE Id = @TemplateId", new { TemplateId = templateId });

        // Get all target contacts
        string contactSql = categoryId.HasValue
            ? "SELECT PhoneNumber, FirstName, LastName, CustomFieldsJson FROM Contacts WHERE CategoryId = @CategoryId"
            : "SELECT PhoneNumber, FirstName, LastName, CustomFieldsJson FROM Contacts";

        if (templateCategory == "Marketing")
        {
            contactSql += " AND PhoneNumber NOT IN (SELECT PhoneNumber FROM UnsubscribedContacts)";
        }

        var contacts = await conn.QueryAsync(contactSql, new { CategoryId = categoryId });
        var contactList = contacts.AsList();

        if (contactList.Count == 0) return 0;

        int queued = 0;
        var batchTime = DateTimeOffset.UtcNow.ToString("O");
        foreach (var contact in contactList)
        {
            var phone = (string)(contact.PhoneNumber ?? "");
            if (string.IsNullOrWhiteSpace(phone)) continue;

            // Build parameters merging contact fields + custom parameters
            var baseParams = new Dictionary<string, object?>
            {
                ["Name"] = ((string?)contact.FirstName ?? "") + " " + ((string?)contact.LastName ?? ""),
                ["FirstName"] = contact.FirstName ?? "",
                ["LastName"] = contact.LastName ?? ""
            };

            // Merge in any custom fields from the contact's CustomFieldsJson
            try
            {
                var custom = JsonSerializer.Deserialize<Dictionary<string, object>>(
                    (string)(contact.CustomFieldsJson ?? "{}")) ?? new();
                foreach (var kv in custom) baseParams[kv.Key] = kv.Value;
            }
            catch { /* ignore malformed JSON */ }

            // Merge in any caller-supplied override parameters
            try
            {
                var overrides = JsonSerializer.Deserialize<Dictionary<string, object>>(customParametersJson) ?? new();
                foreach (var kv in overrides) baseParams[kv.Key] = kv.Value;
            }
            catch { /* ignore */ }

            var parametersJson = JsonSerializer.Serialize(baseParams);

            await conn.ExecuteAsync("""
                INSERT INTO OutboxMessages (ToPhoneNumber, TemplateId, ParametersJson, Status, RetryCount, CreatedAt)
                VALUES (@ToPhoneNumber, @TemplateId, @ParametersJson, 'Pending', 0, @CreatedAt)
                """, new
            {
                ToPhoneNumber = phone,
                TemplateId = templateId,
                ParametersJson = parametersJson,
                CreatedAt = batchTime
            });

            queued++;
        }

        return queued;
    }

    /// <summary>
    /// Returns all broadcast campaigns (grouped outbox batches) — for display in the Broadcast UI tab.
    /// </summary>
    public async Task<IEnumerable<dynamic>> GetRecentBroadcastsAsync(int limit = 50, CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        return await conn.QueryAsync("""
            SELECT 
                o.TemplateId,
                t.Name AS TemplateName,
                COUNT(*) AS TotalQueued,
                SUM(CASE WHEN o.Status = 'Sent' THEN 1 ELSE 0 END) AS Delivered,
                SUM(CASE WHEN o.Status = 'Pending' THEN 1 ELSE 0 END) AS Pending,
                SUM(CASE WHEN o.Status = 'Failed' THEN 1 ELSE 0 END) AS Failed,
                o.CreatedAt AS StartedAt
            FROM OutboxMessages o
            JOIN MessageTemplates t ON o.TemplateId = t.Id
            GROUP BY o.TemplateId, o.CreatedAt
            ORDER BY StartedAt DESC
            LIMIT @Limit
            """, new { Limit = limit });
    }

    /// <summary>
    /// Cancels all pending messages for a specific broadcast campaign.
    /// </summary>
    public async Task<int> DeleteBroadcastAsync(int templateId, string startedAt, CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        return await conn.ExecuteAsync("""
            DELETE FROM OutboxMessages 
            WHERE TemplateId = @TemplateId 
              AND CreatedAt = @StartedAt
            """, new { TemplateId = templateId, StartedAt = startedAt });
    }

    public async Task<int> StopBroadcastAsync(int templateId, string startedAt, CancellationToken ct = default)
    {
        await using var conn = await _factory.OpenAsync(ct);
        return await conn.ExecuteAsync("""
            UPDATE OutboxMessages 
            SET Status = 'Failed' 
            WHERE TemplateId = @TemplateId 
              AND CreatedAt = @StartedAt 
              AND Status = 'Pending'
            """, new { TemplateId = templateId, StartedAt = startedAt });
    }
}

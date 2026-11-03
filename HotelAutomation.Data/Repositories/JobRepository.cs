using Dapper;
using HotelAutomation.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HotelAutomation.Data.Repositories
{
    public class JobRepository
    {
        private readonly SqliteConnectionFactory _dbFactory;

        public JobRepository(SqliteConnectionFactory dbFactory)
        {
            _dbFactory = dbFactory;
        }

        public async Task AddAsync(ScheduledJob job)
        {
            using var connection = await _dbFactory.OpenAsync();
            const string sql = @"
                INSERT INTO ScheduledJobs (Id, GuestName, PhoneNumber, TemplateId, ParametersJson, TriggerTime, Status, CreatedAt, RuleId)
                VALUES (@Id, @GuestName, @PhoneNumber, @TemplateId, @ParametersJson, @TriggerTime, @Status, @CreatedAt, @RuleId);";
            
            await connection.ExecuteAsync(sql, new {
                Id = job.Id,
                job.GuestName,
                job.PhoneNumber,
                job.TemplateId,
                job.ParametersJson,
                TriggerTime = job.TriggerTime.ToString("o"),
                job.Status,
                CreatedAt = job.CreatedAt.ToString("o"),
                job.RuleId
            });
        }

        public async Task<IEnumerable<ScheduledJob>> GetRipeJobsAsync()
        {
            using var connection = await _dbFactory.OpenAsync();
            const string sql = @"
                SELECT * FROM ScheduledJobs 
                WHERE Status = 'Pending' AND TriggerTime <= @Now";
            
            return await connection.QueryAsync<ScheduledJob>(sql, new { Now = DateTimeOffset.UtcNow.ToString("o") });
        }

        public async Task MarkAsProcessedAsync(string id)
        {
            using var connection = await _dbFactory.OpenAsync();
            const string sql = "UPDATE ScheduledJobs SET Status = 'Processed' WHERE Id = @Id";
            await connection.ExecuteAsync(sql, new { Id = id });
        }

        public async Task CancelJobsForPhoneNumberAsync(string phoneNumber)
        {
            using var connection = await _dbFactory.OpenAsync();
            const string sql = "UPDATE ScheduledJobs SET Status = 'Cancelled' WHERE PhoneNumber = @PhoneNumber AND Status = 'Pending'";
            await connection.ExecuteAsync(sql, new { PhoneNumber = phoneNumber });
        }

        /// <summary>
        /// Returns true if a job for the same phone + rule already exists within the dedup window.
        /// Used to prevent duplicate WhatsApp messages from accidental double-triggers.
        /// </summary>
        public async Task<bool> HasRecentJobAsync(string phoneNumber, int ruleId, int windowMinutes)
        {
            using var connection = await _dbFactory.OpenAsync();
            const string sql = @"
                SELECT COUNT(*) FROM ScheduledJobs
                WHERE PhoneNumber = @PhoneNumber
                  AND RuleId      = @RuleId
                  AND Status     != 'Cancelled'
                  AND CreatedAt  >= @Cutoff";

            var cutoff = DateTimeOffset.UtcNow.AddMinutes(-windowMinutes).ToString("o");
            var count = await connection.ExecuteScalarAsync<int>(sql, new { PhoneNumber = phoneNumber, RuleId = ruleId, Cutoff = cutoff });
            return count > 0;
        }

        /// <summary>
        /// Deletes executed ('Processed') or 'Cancelled' jobs older than the specified retention days.
        /// Pending jobs are never deleted.
        /// </summary>
        public async Task<int> ClearOldJobsAsync(int olderThanDays = 15, CancellationToken ct = default)
        {
            using var connection = await _dbFactory.OpenAsync(ct);
            var cutoff = DateTimeOffset.UtcNow.AddDays(-olderThanDays).ToString("o");
            return await connection.ExecuteAsync(@"
                DELETE FROM ScheduledJobs 
                WHERE Status IN ('Processed', 'Cancelled') 
                  AND (TriggerTime < @Cutoff OR CreatedAt < @Cutoff);", 
                new { Cutoff = cutoff });
        }
    }
}

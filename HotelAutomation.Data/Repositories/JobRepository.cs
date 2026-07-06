using Dapper;
using HotelAutomation.Core.Models;
using System;
using System.Collections.Generic;
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
                INSERT INTO ScheduledJobs (Id, GuestName, PhoneNumber, TemplateId, TriggerTime, Status, CreatedAt)
                VALUES (@Id, @GuestName, @PhoneNumber, @TemplateId, @TriggerTime, @Status, @CreatedAt);";
            
            await connection.ExecuteAsync(sql, new {
                Id = job.Id,
                job.GuestName,
                job.PhoneNumber,
                job.TemplateId,
                job.TriggerTime,
                job.Status,
                job.CreatedAt
            });
        }

        public async Task<IEnumerable<ScheduledJob>> GetRipeJobsAsync()
        {
            using var connection = await _dbFactory.OpenAsync();
            const string sql = @"
                SELECT * FROM ScheduledJobs 
                WHERE Status = 'Pending' AND TriggerTime <= @Now";
            
            return await connection.QueryAsync<ScheduledJob>(sql, new { Now = DateTimeOffset.UtcNow });
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
    }
}

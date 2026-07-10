using Dapper;
using HotelAutomation.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HotelAutomation.Data.Repositories
{
    public class InboxRepository
    {
        private readonly SqliteConnectionFactory _dbFactory;

        public InboxRepository(SqliteConnectionFactory dbFactory)
        {
            _dbFactory = dbFactory;
        }

        public async Task AddAsync(InboxMessage message)
        {
            using var connection = await _dbFactory.OpenAsync();
            const string sql = @"
                INSERT INTO InboxMessages (Id, FromPhoneNumber, SenderName, Content, IsRead, ReceivedAt)
                VALUES (@Id, @FromPhoneNumber, @SenderName, @Content, @IsRead, @ReceivedAt);";
            
            await connection.ExecuteAsync(sql, new {
                Id = message.Id,
                message.FromPhoneNumber,
                message.SenderName,
                message.Content,
                IsRead = message.IsRead ? 1 : 0,
                message.ReceivedAt
            });
        }

        public async Task<IEnumerable<InboxMessage>> GetRecentMessagesAsync(int limit = 50)
        {
            using var connection = await _dbFactory.OpenAsync();
            const string sql = "SELECT * FROM InboxMessages ORDER BY ReceivedAt DESC LIMIT @Limit";
            return await connection.QueryAsync<InboxMessage>(sql, new { Limit = limit });
        }
    }
}

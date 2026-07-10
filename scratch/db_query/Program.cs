using System;
using Microsoft.Data.Sqlite;

class Program
{
    static void Main()
    {
        string dbPath = @"C:\Users\rutvi\Desktop\HotelAutomation_Sankalp_retreat\HotelAutomation.Worker\hotel_automation.db";
        using var conn = new SqliteConnection($"Data Source={dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
        SELECT * FROM (
            SELECT Id, FromPhoneNumber as PhoneNumber, 'Guest' as Sender, Content, ReceivedAt as SentAt
            FROM InboxMessages
            UNION ALL
            SELECT Id, PhoneNumber, Sender, Content, SentAt
            FROM ConversationReplies
        ) ORDER BY REPLACE(SentAt, 'T', ' ') ASC;";
        try {
            using var reader = cmd.ExecuteReader();
            Console.WriteLine("Query executed successfully.");
        } catch (Exception ex) {
            Console.WriteLine("Query failed: " + ex.Message);
        }
    }
}

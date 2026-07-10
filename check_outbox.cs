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
        cmd.CommandText = "SELECT Id, TemplateId, ToPhoneNumber, Status, ErrorMessage, CreatedAt FROM OutboxMessages ORDER BY CreatedAt DESC LIMIT 5";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            Console.WriteLine($"Id: {reader.GetInt32(0)}, TempId: {reader.GetInt32(1)}, Phone: {reader.GetString(2)}, Status: {reader.GetString(3)}, Error: {reader.GetValue(4)}, CreatedAt: {reader.GetValue(5)}");
        }
    }
}

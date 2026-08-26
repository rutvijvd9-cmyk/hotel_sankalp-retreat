using System;
using Microsoft.Data.Sqlite;

class Program
{
    static void Main()
    {
        string dbPath = @"e:\hotel_sankalp-retreat\HotelAutomation.Worker\hotel_automation.db";
        using var conn = new SqliteConnection($"Data Source={dbPath}");
        conn.Open();
        
        var settings = new (string Key, string Value)[]
        {
            ("WhatsApp_AccessToken", "EAAOZCdNFpqucBSB9Iaf1aLZBocJvsA8POrPOdEGJ4lDhefeCPB4j9xvRoDpnL8pZC6Oi4dRcfBJck9nwwp9ZAQjsYsZCPs0ig4wfFnuOciyoazus3tiPZCt4ZCaL4e5ZCnem5GdricfGSgUEr4cfmx97LCue3zq2YPhdmtpZBHtiMXjYlDO1pmNa6rsZAnhDcp15MDXAZDZD"),
            ("WhatsApp_PhoneNumberId", "787260811145630"),
            ("WhatsApp_ApiVersion", "v25.0"),
            ("WhatsApp_VerifyToken", "sankalp_retreat_webhook_secret"),
            ("WhatsApp_WhatsAppBusinessAccountId", "1964387864360271")
        };

        foreach (var (k, v) in settings)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO AppConfig (Key, Value) VALUES (@k, @v) ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value";
            cmd.Parameters.AddWithValue("@k", k);
            cmd.Parameters.AddWithValue("@v", v);
            cmd.ExecuteNonQuery();
            Console.WriteLine($"Updated seed DB key: {k}");
        }
    }
}

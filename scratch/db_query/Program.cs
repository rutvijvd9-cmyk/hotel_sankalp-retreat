using System;
using Microsoft.Data.Sqlite;

class Program
{
    static void Main()
    {
        string dbPath = @"e:\hotel_sankalp-retreat\HotelAutomation.Worker\hotel_automation.db";
        using var conn = new SqliteConnection($"Data Source={dbPath}");
        conn.Open();
        
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT 'TemplateVariants' as tbl, COUNT(*) as c FROM TemplateVariants WHERE TemplateId IN (SELECT Id FROM MessageTemplates WHERE Name IN ('Spa Up-sell Drip Variant A', 'Spa Up-sell Drip Variant B', 'Checkout Billing'))
            UNION ALL
            SELECT 'AutomationRules' as tbl, COUNT(*) as c FROM AutomationRules WHERE TemplateId IN (SELECT Id FROM MessageTemplates WHERE Name IN ('Spa Up-sell Drip Variant A', 'Spa Up-sell Drip Variant B', 'Checkout Billing'))
            UNION ALL
            SELECT 'Campaigns' as tbl, COUNT(*) as c FROM Campaigns WHERE TemplateId IN (SELECT Id FROM MessageTemplates WHERE Name IN ('Spa Up-sell Drip Variant A', 'Spa Up-sell Drip Variant B', 'Checkout Billing'))
            UNION ALL
            SELECT 'CampaignSteps' as tbl, COUNT(*) as c FROM CampaignSteps WHERE TemplateId IN (SELECT Id FROM MessageTemplates WHERE Name IN ('Spa Up-sell Drip Variant A', 'Spa Up-sell Drip Variant B', 'Checkout Billing'))
            UNION ALL
            SELECT 'ScheduledJobs' as tbl, COUNT(*) as c FROM ScheduledJobs WHERE TemplateId IN (SELECT Id FROM MessageTemplates WHERE Name IN ('Spa Up-sell Drip Variant A', 'Spa Up-sell Drip Variant B', 'Checkout Billing'))
            UNION ALL
            SELECT 'OutboxMessages' as tbl, COUNT(*) as c FROM OutboxMessages WHERE TemplateId IN (SELECT Id FROM MessageTemplates WHERE Name IN ('Spa Up-sell Drip Variant A', 'Spa Up-sell Drip Variant B', 'Checkout Billing'))
        ";
        try {
            using var reader = cmd.ExecuteReader();
            while(reader.Read()) {
                Console.WriteLine($"{reader.GetString(0)}: {reader.GetInt32(1)}");
            }
        } catch (Exception ex) {
            Console.WriteLine("Query failed: " + ex.Message);
        }
    }
}

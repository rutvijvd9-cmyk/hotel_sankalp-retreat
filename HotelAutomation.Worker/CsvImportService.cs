namespace HotelAutomation.Worker;

using CsvHelper;
using CsvHelper.Configuration;
using HotelAutomation.Core.Models;
using HotelAutomation.Data.Repositories;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

public sealed class CsvImportService
{
    private readonly ContactRepository _contactRepository;

    public CsvImportService(ContactRepository contactRepository)
    {
        _contactRepository = contactRepository;
    }

    public async Task<int> ProcessCsvAsync(Stream csvStream, int? categoryId, CancellationToken ct = default)
    {
        using var reader = new StreamReader(csvStream);
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            TrimOptions = TrimOptions.Trim,
            IgnoreBlankLines = true,
            MissingFieldFound = null,
            HeaderValidated = null
        };

        using var csv = new CsvReader(reader, config);
        
        await csv.ReadAsync();
        csv.ReadHeader();
        
        if (csv.HeaderRecord == null) return 0;
        
        var headers = csv.HeaderRecord.Select(h => h.ToLowerInvariant()).ToArray();
        
        int importedCount = 0;

        while (await csv.ReadAsync())
        {
            string? phone = null;
            string? firstName = null;
            string? lastName = null;
            var customFields = new Dictionary<string, string>();

            for (int i = 0; i < headers.Length; i++)
            {
                var val = csv.GetField(i);
                if (string.IsNullOrWhiteSpace(val)) continue;

                var h = headers[i];
                if (h == "phone" || h == "phonenumber") phone = val;
                else if (h == "firstname" || h == "first_name" || h == "first name") firstName = val;
                else if (h == "lastname" || h == "last_name" || h == "last name") lastName = val;
                else
                {
                    customFields[csv.HeaderRecord[i]] = val;
                }
            }

            if (string.IsNullOrWhiteSpace(phone)) continue; // skip rows without phone

            var contact = new Contact
            {
                PhoneNumber = phone,
                FirstName = firstName,
                LastName = lastName,
                CategoryId = categoryId,
                CustomFieldsJson = JsonSerializer.Serialize(customFields)
            };

            await _contactRepository.UpsertAsync(contact, ct);
            importedCount++;
        }

        return importedCount;
    }
}

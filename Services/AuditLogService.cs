using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper;
using CsvHelper.Configuration;
using MenuProUI.Models;

namespace MenuProUI.Services;

public sealed class AuditLogService
{
    private static CsvConfiguration Cfg => new(CultureInfo.InvariantCulture)
    {
        HasHeaderRecord = true,
        MissingFieldFound = null,
        BadDataFound = null,
        HeaderValidated = null
    };

    public void Append(string action, string entityType, string entityName, string details = "")
    {
        try
        {
            var current = Load();
            current.Add(new AuditEntry
            {
                TimestampUtc = DateTime.UtcNow,
                Action = action,
                EntityType = entityType,
                EntityName = entityName,
                Details = details
            });

            Save(current.OrderByDescending(x => x.TimestampUtc).Take(5000).OrderBy(x => x.TimestampUtc).ToList());
        }
        catch
        {
        }
    }

    public List<AuditEntry> Load()
    {
        if (!File.Exists(AppPaths.AuditLogPath))
            return new List<AuditEntry>();

        try
        {
            using var reader = new StreamReader(AppPaths.AuditLogPath);
            using var csv = new CsvReader(reader, Cfg);
            return csv.GetRecords<AuditEntry>().ToList();
        }
        catch
        {
            return new List<AuditEntry>();
        }
    }

    private void Save(List<AuditEntry> records)
    {
        var tmp = AppPaths.AuditLogPath + ".tmp";
        using (var writer = new StreamWriter(tmp))
        using (var csv = new CsvWriter(writer, Cfg))
        {
            csv.WriteRecords(records);
        }

        File.Move(tmp, AppPaths.AuditLogPath, true);
    }
}

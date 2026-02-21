using System.Reflection;
using System.Text.Json;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using ZSlayerCommandCenter.Models;

namespace ZSlayerCommandCenter.Services;

[Injectable(InjectionType.Singleton)]
public class ActivityLogService(
    ModHelper modHelper,
    SaveServer saveServer,
    ConfigService configService,
    ISptLogger<ActivityLogService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public void LogAction(string actionType, string sessionId, string details)
    {
        var adminName = GetProfileName(sessionId);
        var entry = new ActivityEntry
        {
            Timestamp = DateTime.UtcNow,
            Type = actionType,
            AdminName = adminName,
            Details = details
        };

        try
        {
            var path = GetLogFilePath(DateTime.UtcNow);
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            List<ActivityEntry> entries = [];
            if (File.Exists(path))
            {
                var existing = File.ReadAllText(path);
                entries = JsonSerializer.Deserialize<List<ActivityEntry>>(existing, JsonOptions) ?? [];
            }

            entries.Add(entry);
            File.WriteAllText(path, JsonSerializer.Serialize(entries, JsonOptions));
        }
        catch (Exception ex)
        {
            logger.Warning($"ZSlayerCommandCenter: Failed to write activity log: {ex.Message}");
        }
    }

    public ActivityResponse GetRecentActivity(int limit = 50, int offset = 0, string? typeFilter = null)
    {
        var allEntries = LoadRecentEntries();

        if (!string.IsNullOrEmpty(typeFilter))
            allEntries = allEntries.Where(e => e.Type.Equals(typeFilter, StringComparison.OrdinalIgnoreCase)).ToList();

        var total = allEntries.Count;
        var entries = allEntries
            .OrderByDescending(e => e.Timestamp)
            .Skip(offset)
            .Take(limit)
            .ToList();

        return new ActivityResponse
        {
            Entries = entries,
            Total = total,
            Limit = limit,
            Offset = offset
        };
    }

    public void CleanupOldLogs()
    {
        var config = configService.GetConfig();
        var retentionDays = config.Dashboard.ActivityRetentionDays;
        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
        var logsDir = GetLogsDir();

        if (!Directory.Exists(logsDir)) return;

        foreach (var file in Directory.GetFiles(logsDir, "activity-*.json"))
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            var datePart = fileName.Replace("activity-", "");
            if (DateTime.TryParse(datePart, out var fileDate) && fileDate < cutoff)
            {
                try
                {
                    File.Delete(file);
                    logger.Info($"ZSlayerCommandCenter: Cleaned up old activity log: {fileName}");
                }
                catch { /* ignore cleanup failures */ }
            }
        }
    }

    private List<ActivityEntry> LoadRecentEntries()
    {
        var logsDir = GetLogsDir();
        if (!Directory.Exists(logsDir)) return [];

        var allEntries = new List<ActivityEntry>();
        var files = Directory.GetFiles(logsDir, "activity-*.json")
            .OrderByDescending(f => f)
            .Take(7); // Last 7 days max for performance

        foreach (var file in files)
        {
            try
            {
                var json = File.ReadAllText(file);
                var entries = JsonSerializer.Deserialize<List<ActivityEntry>>(json, JsonOptions);
                if (entries != null)
                    allEntries.AddRange(entries);
            }
            catch { /* skip corrupt files */ }
        }

        return allEntries;
    }

    private string GetProfileName(string sessionId)
    {
        try
        {
            var profiles = saveServer.GetProfiles();
            if (profiles.TryGetValue(sessionId, out var profile))
                return profile.CharacterData?.PmcData?.Info?.Nickname ?? "Unknown";
        }
        catch { /* ignore */ }
        return "Unknown";
    }

    private string GetLogFilePath(DateTime date)
    {
        return Path.Combine(GetLogsDir(), $"activity-{date:yyyy-MM-dd}.json");
    }

    private string GetLogsDir()
    {
        var modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        return Path.Combine(modPath, "config", "logs");
    }
}

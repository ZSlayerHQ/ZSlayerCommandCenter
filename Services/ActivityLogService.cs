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
    private readonly object _logLock = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
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

            var line = JsonSerializer.Serialize(entry, JsonOptions) + Environment.NewLine;
            lock (_logLock)
            {
                File.AppendAllText(path, line);
            }
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

        var files = Directory.GetFiles(logsDir, "activity-*.json")
            .Concat(Directory.GetFiles(logsDir, "activity-*.jsonl"));

        foreach (var file in files)
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
                catch (Exception ex)
                {
                    logger.Debug($"ZSlayerCommandCenter: Failed to delete activity log '{fileName}': {ex.Message}");
                }
            }
        }
    }

    private List<ActivityEntry> LoadRecentEntries()
    {
        var logsDir = GetLogsDir();
        if (!Directory.Exists(logsDir)) return [];

        var allEntries = new List<ActivityEntry>();
        var jsonlFiles = Directory.GetFiles(logsDir, "activity-*.jsonl")
            .OrderByDescending(f => f)
            .Take(7)
            .ToList();

        // Back-compat: legacy JSON array files
        var jsonFiles = Directory.GetFiles(logsDir, "activity-*.json")
            .OrderByDescending(f => f)
            .Take(7)
            .ToList();

        foreach (var file in jsonlFiles)
        {
            try
            {
                foreach (var line in File.ReadLines(file))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var entry = JsonSerializer.Deserialize<ActivityEntry>(line, JsonOptions);
                    if (entry != null)
                        allEntries.Add(entry);
                }
            }
            catch (Exception ex)
            {
                logger.Debug($"ZSlayerCommandCenter: Skipping invalid activity log file '{Path.GetFileName(file)}': {ex.Message}");
            }
        }

        foreach (var file in jsonFiles)
        {
            try
            {
                var json = File.ReadAllText(file);
                var entries = JsonSerializer.Deserialize<List<ActivityEntry>>(json, JsonOptions);
                if (entries != null)
                    allEntries.AddRange(entries);
            }
            catch (Exception ex)
            {
                logger.Debug($"ZSlayerCommandCenter: Skipping invalid legacy activity log file '{Path.GetFileName(file)}': {ex.Message}");
            }
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
        catch (Exception ex)
        {
            logger.Debug($"ZSlayerCommandCenter: Failed to resolve profile name for session '{sessionId}': {ex.Message}");
        }
        return "Unknown";
    }

    private string GetLogFilePath(DateTime date)
    {
        return Path.Combine(GetLogsDir(), $"activity-{date:yyyy-MM-dd}.jsonl");
    }

    private string GetLogsDir()
    {
        var modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        return Path.Combine(modPath, "config", "logs");
    }
}

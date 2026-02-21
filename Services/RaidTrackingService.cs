using System.Reflection;
using System.Text.Json;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Utils;
using ZSlayerCommandCenter.Models;

namespace ZSlayerCommandCenter.Services;

[Injectable(InjectionType.Singleton)]
public class RaidTrackingService(
    ModHelper modHelper,
    ISptLogger<RaidTrackingService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private const int MaxRaids = 500;
    private readonly List<RaidEndRecord> _raids = [];
    private readonly Lock _lock = new();
    private bool _loaded;

    public void RecordRaid(RaidEndRecord record)
    {
        EnsureLoaded();
        lock (_lock)
        {
            _raids.Add(record);
            if (_raids.Count > MaxRaids)
                _raids.RemoveAt(0);
        }
        FlushToDisk();
        logger.Info($"ZSlayerCommandCenter: Raid recorded — {record.Nickname} on {record.Map} ({record.Result})");
    }

    public List<RaidEndRecord> GetRecentRaids(int limit = 20)
    {
        EnsureLoaded();
        lock (_lock)
        {
            return _raids
                .OrderByDescending(r => r.Timestamp)
                .Take(limit)
                .ToList();
        }
    }

    public RaidStatsDto GetRaidStats()
    {
        EnsureLoaded();
        lock (_lock)
        {
            var total = _raids.Count;
            var survived = _raids.Count(r =>
                r.Result.Equals("Survived", StringComparison.OrdinalIgnoreCase) ||
                r.Result.Equals("Runner", StringComparison.OrdinalIgnoreCase));
            var survivalRate = total > 0 ? (double)survived / total * 100 : 0;
            var avgPlayTime = total > 0 ? (int)_raids.Average(r => r.PlayTimeSeconds) : 0;

            var raidsByMap = _raids
                .GroupBy(r => r.Map)
                .ToDictionary(g => g.Key, g => g.Count());

            return new RaidStatsDto
            {
                TotalRaids = total,
                RaidsByMap = raidsByMap,
                SurvivalRate = Math.Round(survivalRate, 1),
                AvgPlayTimeSeconds = avgPlayTime,
                RecentRaids = _raids
                    .OrderByDescending(r => r.Timestamp)
                    .Take(10)
                    .ToList()
            };
        }
    }

    private void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;

        var path = GetDataPath();
        if (!File.Exists(path)) return;

        try
        {
            var json = File.ReadAllText(path);
            var raids = JsonSerializer.Deserialize<List<RaidEndRecord>>(json, JsonOptions);
            if (raids != null)
            {
                lock (_lock)
                {
                    _raids.AddRange(raids);
                }
            }
            logger.Info($"ZSlayerCommandCenter: Loaded {_raids.Count} raid records from disk");
        }
        catch (Exception ex)
        {
            logger.Warning($"ZSlayerCommandCenter: Failed to load raid history: {ex.Message}");
        }
    }

    private void FlushToDisk()
    {
        try
        {
            var path = GetDataPath();
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            List<RaidEndRecord> snapshot;
            lock (_lock)
            {
                snapshot = [.. _raids];
            }

            var json = JsonSerializer.Serialize(snapshot, JsonOptions);
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            logger.Warning($"ZSlayerCommandCenter: Failed to save raid history: {ex.Message}");
        }
    }

    private string GetDataPath()
    {
        var modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        return Path.Combine(modPath, "config", "data", "raid-history.json");
    }
}

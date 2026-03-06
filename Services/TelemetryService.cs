using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using ZSlayerCommandCenter.Models;

namespace ZSlayerCommandCenter.Services;

[Injectable(InjectionType.Singleton)]
public class TelemetryService(
    ConfigService configService,
    LocaleService localeService,
    HandbookHelper handbookHelper,
    WatchdogManager watchdogManager,
    SaveServer saveServer,
    ISptLogger<TelemetryService> logger)
{
    private readonly object _lock = new();

    // Headless version info (from hello handshake) — global, last reported
    private string _headlessTelemetryVersion = "";
    private string _headlessFikaVersion = "";

    // Multi-source state
    private readonly Dictionary<string, TelemetrySource> _sources = new();
    private static readonly TimeSpan StaleTimeout = TimeSpan.FromMinutes(30);

    // Kill feed — ring buffer, max 100 (global, entries already contain context)
    private readonly List<KillFeedEntry> _killFeed = new();
    private const int MaxKillFeed = 100;

    // Current raid kills (archived to history on raid end) — global
    private readonly List<KillFeedEntry> _currentRaidKills = new();
    private readonly List<ExtractPayload> _currentRaidExtracts = new();
    private string _currentRaidId = "";
    private DateTime _currentRaidStart;

    // Raid history — persisted to disk
    private readonly List<RaidHistoryRecord> _raidHistory = new();

    private int _killCounter;

    // ── Unity rich text tag stripping ──
    private static readonly Regex UnityTagRegex = new(@"<\/?[a-zA-Z][^>]*>", RegexOptions.Compiled);
    private const int MaxWeaponNameLength = 40;

    // ── Locale cache (lazy loaded) ──
    private Dictionary<string, string>? _localeCache;

    private Dictionary<string, string> GetLocales()
    {
        return _localeCache ??= localeService.GetLocaleDb("en");
    }

    private string ResolveItemName(string templateId)
    {
        if (string.IsNullOrEmpty(templateId)) return "";
        var locales = GetLocales();
        return locales.TryGetValue($"{templateId} Name", out var name) && !string.IsNullOrEmpty(name)
            ? name
            : templateId;
    }

    private string ResolveItemShortName(string templateId)
    {
        if (string.IsNullOrEmpty(templateId)) return "";
        var locales = GetLocales();
        if (locales.TryGetValue($"{templateId} ShortName", out var shortName) && !string.IsNullOrEmpty(shortName))
            return shortName;
        if (locales.TryGetValue($"{templateId} Name", out var name) && !string.IsNullOrEmpty(name))
            return TruncateWeaponName(name);
        return templateId;
    }

    private static readonly Dictionary<string, string> MapNameOverrides = new(StringComparer.OrdinalIgnoreCase)
    {
        ["factory4_day"] = "Factory",
        ["factory4_night"] = "Factory",
        ["bigmap"] = "Customs",
        ["laboratory"] = "Labs",
        ["Labyrinth"] = "Labyrinth",
        ["RezervBase"] = "Reserve",
        ["TarkovStreets"] = "Streets",
        ["Sandbox"] = "Ground Zero",
        ["Sandbox_high"] = "Ground Zero",
        ["city"] = "Streets"
    };

    private string ResolveMapName(string locationId)
    {
        if (string.IsNullOrEmpty(locationId)) return "";
        if (MapNameOverrides.TryGetValue(locationId, out var overrideName))
            return overrideName;
        var locales = GetLocales();
        if (locales.TryGetValue($"{locationId} Name", out var name) && !string.IsNullOrEmpty(name))
            return name;
        return char.ToUpper(locationId[0]) + locationId[1..];
    }

    private static string StripUnityTags(string? input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        return UnityTagRegex.Replace(input, "").Trim();
    }

    private static string CleanActorName(string name, string type)
    {
        var clean = StripUnityTags(name);
        if (string.IsNullOrEmpty(clean) || clean == "Unknown")
            return type switch
            {
                "scav" => "Scav",
                "raider" => "Raider",
                "rogue" => "Rogue",
                "follower" => "Follower",
                "boss" => clean,
                _ => clean
            };

        return type switch
        {
            "scav" => "Scav",
            "raider" => "Raider",
            "rogue" => "Rogue",
            "follower" => "Follower",
            "boss" => clean,
            "pmc" => clean,
            _ => clean
        };
    }

    private static string TruncateWeaponName(string name)
    {
        if (string.IsNullOrEmpty(name) || name.Length <= MaxWeaponNameLength) return name;
        return name[..(MaxWeaponNameLength - 3)] + "...";
    }

    private string NormalizeSourceId(string? sourceId)
    {
        return string.IsNullOrWhiteSpace(sourceId) ? "default" : sourceId;
    }

    private TelemetrySource GetOrCreateSource(string sourceId)
    {
        if (_sources.TryGetValue(sourceId, out var existing))
            return existing;

        var displayName = ResolveSourceDisplayName(sourceId);
        var source = new TelemetrySource
        {
            SourceId = sourceId,
            DisplayName = displayName,
            FirstSeen = DateTime.UtcNow,
            LastSeen = DateTime.UtcNow
        };
        _sources[sourceId] = source;
        return source;
    }

    private string ResolveSourceDisplayName(string sourceId)
    {
        if (sourceId == "default") return "Headless";

        try
        {
            var profiles = saveServer.GetProfiles();
            foreach (var (sid, profile) in profiles)
            {
                if (sid.ToString() == sourceId)
                {
                    var info = profile?.CharacterData?.PmcData?.Info;
                    if (info != null && !string.IsNullOrEmpty(info.Nickname))
                        return info.Nickname;
                }
            }
        }
        catch { /* best effort */ }

        return sourceId.Length > 8 ? sourceId[..8] : sourceId;
    }

    // ══════════════════════════════════════════════════════════════
    //  UPDATE methods (called from POST endpoints)
    // ══════════════════════════════════════════════════════════════

    public void UpdateHello(TelemetryHelloPayload payload)
    {
        var sourceId = NormalizeSourceId(payload.SourceId);
        lock (_lock)
        {
            _headlessTelemetryVersion = payload.TelemetryVersion;
            _headlessFikaVersion = payload.FikaClientVersion;

            var source = GetOrCreateSource(sourceId);
            source.LastSeen = DateTime.UtcNow;
            if (!string.IsNullOrEmpty(payload.Hostname))
                source.Hostname = payload.Hostname;
            if (!string.IsNullOrEmpty(payload.Ip))
                source.Ip = payload.Ip;
            if (!string.IsNullOrEmpty(payload.Hostname))
                source.DisplayName = payload.Hostname;
        }
        logger.Info($"Headless connected — Telemetry: {payload.TelemetryVersion}, Fika.Core: {payload.FikaClientVersion}, Source: {sourceId}");
    }

    public (string TelemetryVersion, string FikaClientVersion) GetHeadlessVersions()
    {
        lock (_lock) { return (_headlessTelemetryVersion, _headlessFikaVersion); }
    }

    public void UpdateRaidState(RaidStatePayload payload)
    {
        var sourceId = NormalizeSourceId(payload.SourceId);
        bool wasActive;
        bool wasIdle;

        lock (_lock)
        {
            var source = GetOrCreateSource(sourceId);
            source.LastSeen = DateTime.UtcNow;

            wasActive = source.RaidState != null && source.RaidState.Status != "idle";
            wasIdle = source.RaidState == null || source.RaidState.Status == "idle";
            source.RaidState = payload;

            if (payload.Status is "loading" or "deploying" && string.IsNullOrEmpty(_currentRaidId))
            {
                _currentRaidId = Guid.NewGuid().ToString("N")[..12];
                _currentRaidStart = DateTime.UtcNow;
                _currentRaidKills.Clear();
                _currentRaidExtracts.Clear();
            }

            if (payload.Status == "in-raid" && wasIdle)
            {
                _killFeed.Clear();
                _currentRaidKills.Clear();
            }

            if (payload.Status == "idle" && wasActive)
            {
                source.Performance = null;
                source.Players = null;
                source.Bots = null;
                source.DamageStats = null;
                source.Positions = [];
                _currentRaidId = "";
            }
        }

        if (payload.Status is "loading" or "deploying" && wasIdle)
            AddAlert("raid-start", $"Raid starting on {payload.Map}", payload.Map, "rocket");
        if (payload.Status == "idle" && wasActive)
        {
            AddAlert("raid-end", "Raid ended", payload.Map, "flag");
            watchdogManager.BroadcastRaidEnd(payload.Map ?? "");
        }

        logger.Info($"[Telemetry] Raid state → {payload.Status}, map: {payload.Map}, timer: {payload.RaidTimer}s, source: {sourceId}");
    }

    public void UpdatePerformance(PerformancePayload payload)
    {
        var sourceId = NormalizeSourceId(payload.SourceId);
        lock (_lock)
        {
            var source = GetOrCreateSource(sourceId);
            source.LastSeen = DateTime.UtcNow;
            source.Performance = payload;
        }
        RecordPerformanceHistory(payload, sourceId);
    }

    public void AddKill(KillPayload payload)
    {
        var sourceId = NormalizeSourceId(payload.SourceId);
        var killerName = CleanActorName(payload.Killer.Name, payload.Killer.Type);
        var victimName = CleanActorName(payload.Victim.Name, payload.Victim.Type);

        var weaponName = StripUnityTags(ResolveItemShortName(payload.Weapon));
        var ammoName = StripUnityTags(ResolveItemName(payload.Ammo));

        var bodyPart = StripUnityTags(payload.BodyPart);
        if (bodyPart is "None" or "Unknown" or "Common" or "")
            bodyPart = "";

        var isEnvironmentalDeath = string.IsNullOrEmpty(killerName) || killerName == "Unknown";
        var isUnknownWeapon = string.IsNullOrEmpty(weaponName) || weaponName == "Unknown";

        if (isEnvironmentalDeath && isUnknownWeapon)
        {
            killerName = "";
            weaponName = "";
        }

        var entry = new KillFeedEntry
        {
            Id = $"k{Interlocked.Increment(ref _killCounter)}",
            SourceId = sourceId,
            Timestamp = payload.Timestamp,
            RaidTime = payload.RaidTime,
            Map = payload.Map,
            Killer = new KillActorPayload
            {
                Name = killerName,
                Type = payload.Killer.Type,
                Level = payload.Killer.Level
            },
            Victim = new KillActorPayload
            {
                Name = victimName,
                Type = payload.Victim.Type,
                Level = payload.Victim.Level
            },
            WeaponName = weaponName,
            AmmoName = ammoName,
            BodyPart = bodyPart,
            Distance = payload.Distance,
            IsHeadshot = payload.IsHeadshot
        };

        lock (_lock)
        {
            var source = GetOrCreateSource(sourceId);
            source.LastSeen = DateTime.UtcNow;

            _killFeed.Add(entry);
            if (_killFeed.Count > MaxKillFeed)
                _killFeed.RemoveRange(0, _killFeed.Count - MaxKillFeed);

            _currentRaidKills.Add(entry);
        }

        if (payload.Victim.Type == "pmc")
            AddAlert("pmc-kill", $"{killerName} killed {victimName}", payload.Map, "skull");
        if (payload.IsHeadshot)
            AddAlert("headshot", $"Headshot! {killerName} → {victimName} ({Math.Round(payload.Distance)}m)", payload.Map, "crosshair");

        logger.Info($"[Telemetry] Kill: {killerName} → {victimName} [{weaponName}] ({bodyPart}, {payload.Distance:F0}m)");
    }

    public void UpdatePlayers(PlayerStatusPayload payload)
    {
        var sourceId = NormalizeSourceId(payload.SourceId);
        lock (_lock)
        {
            var source = GetOrCreateSource(sourceId);
            source.LastSeen = DateTime.UtcNow;
            source.Players = payload;
        }
    }

    public void UpdateBots(BotCountPayload payload)
    {
        var sourceId = NormalizeSourceId(payload.SourceId);
        lock (_lock)
        {
            var source = GetOrCreateSource(sourceId);
            source.LastSeen = DateTime.UtcNow;
            source.Bots = payload;
        }
    }

    public void UpdateDamageStats(DamageStatsPayload payload)
    {
        var sourceId = NormalizeSourceId(payload.SourceId);
        lock (_lock)
        {
            var source = GetOrCreateSource(sourceId);
            source.LastSeen = DateTime.UtcNow;
            source.DamageStats = payload;
        }
    }

    public void AddBossSpawn(BossSpawnPayload payload)
    {
        var sourceId = NormalizeSourceId(payload.SourceId);
        lock (_lock)
        {
            var source = GetOrCreateSource(sourceId);
            source.LastSeen = DateTime.UtcNow;

            if (source.Bots != null)
            {
                var existing = source.Bots.Bosses.FirstOrDefault(b =>
                    b.Name.Equals(payload.Boss, StringComparison.OrdinalIgnoreCase));
                if (existing == null)
                {
                    source.Bots.Bosses.Add(new BossStateEntry
                    {
                        Name = payload.Boss,
                        Alive = true
                    });
                }
            }
        }

        AddAlert("boss-spawn", $"Boss spawned: {payload.Boss}", payload.Map, "crown");

        logger.Info($"TelemetryService: Boss spawned — {payload.Boss} on {payload.Map} at {payload.RaidTime}s");
    }

    public void AddExtract(ExtractPayload payload)
    {
        lock (_lock)
        {
            _currentRaidExtracts.Add(payload);
        }
        AddAlert("extract", $"{payload.Player.Name} extracted ({payload.Outcome})", payload.Map, "door-open");
    }

    public void FinishRaid(RaidSummaryPayload payload)
    {
        var sourceId = NormalizeSourceId(payload.SourceId);

        logger.Info($"[Telemetry] FinishRaid received — map: {payload.Map}, duration: {payload.RaidDuration}s, " +
                     $"players: {payload.Players.Count}, kills: {payload.TotalKills}, deaths: {payload.TotalDeaths}, " +
                     $"bosses: {payload.Bosses.Count}, currentRaidKills: {_currentRaidKills.Count}, source: {sourceId}");

        lock (_lock)
        {
            var actualKills = payload.TotalKills > 0 ? payload.TotalKills : _currentRaidKills.Count;

            EnrichPlayerKillCounts(payload.Players);
            CalculateRaidProfit(payload.Players);

            var record = new RaidHistoryRecord
            {
                Summary = new RaidHistorySummary
                {
                    Id = string.IsNullOrEmpty(_currentRaidId)
                        ? Guid.NewGuid().ToString("N")[..12]
                        : _currentRaidId,
                    SourceId = sourceId,
                    Map = payload.Map,
                    MapName = ResolveMapName(payload.Map),
                    Timestamp = _currentRaidStart != default ? _currentRaidStart : DateTime.UtcNow,
                    Duration = payload.RaidDuration,
                    PlayerCount = payload.Players.Count,
                    Survived = payload.Players.Count(p =>
                        p.Outcome.Equals("survived", StringComparison.OrdinalIgnoreCase)),
                    TotalKills = actualKills,
                    TotalDeaths = payload.TotalDeaths,
                    Bosses = payload.Bosses
                },
                Players = payload.Players,
                Kills = new List<KillFeedEntry>(_currentRaidKills),
                Extracts = new List<ExtractPayload>(_currentRaidExtracts),
                DamageStats = _sources.TryGetValue(sourceId, out var src) ? src.DamageStats : null
            };

            _raidHistory.Insert(0, record);

            _currentRaidKills.Clear();
            _currentRaidExtracts.Clear();
            if (src != null) src.DamageStats = null;
            _currentRaidId = "";
        }

        SaveRaidHistory();

        logger.Success($"[Telemetry] Raid archived — {payload.Map}, {payload.Players.Count} players, raidHistory now has {_raidHistory.Count} entries");
    }

    private void EnrichPlayerKillCounts(List<RaidSummaryPlayer> players)
    {
        foreach (var player in players)
        {
            var playerKills = _currentRaidKills
                .Where(k => k.Killer.Name.Equals(player.Name, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (playerKills.Count == 0) continue;

            if (player.Kills == 0)
                player.Kills = playerKills.Count;

            if (player.KilledPmc == 0 && player.KilledScav == 0 && player.KilledBoss == 0)
            {
                player.KilledPmc = playerKills.Count(k => k.Victim.Type == "pmc");
                player.KilledScav = playerKills.Count(k => k.Victim.Type is "scav" or "raider" or "rogue" or "follower");
                player.KilledBoss = playerKills.Count(k => k.Victim.Type == "boss");
            }

            if (player.Headshots == 0)
                player.Headshots = playerKills.Count(k => k.IsHeadshot);

            if (player.LongestKill == 0)
                player.LongestKill = Math.Round(playerKills.Max(k => k.Distance), 1);

            if (player.KillStreak == 0)
                player.KillStreak = playerKills.Count;
        }
    }

    private void CalculateRaidProfit(List<RaidSummaryPlayer> players)
    {
        foreach (var player in players)
        {
            try
            {
                player.InventoryValueBefore = PriceItemList(player.InventoryBefore);
                player.InventoryValueAfter = PriceItemList(player.InventoryAfter);
                player.Profit = player.InventoryValueAfter - player.InventoryValueBefore;

                if (player.InventoryValueBefore > 0 || player.InventoryValueAfter > 0)
                    logger.Info($"[Telemetry] Profit: {player.Name} — before={player.InventoryValueBefore:N0}, after={player.InventoryValueAfter:N0}, profit={player.Profit:N0}");
            }
            catch (Exception ex)
            {
                logger.Warning($"[Telemetry] Profit calc error for {player.Name}: {ex.Message}");
            }

            player.InventoryBefore = null;
            player.InventoryAfter = null;
        }
    }

    private long PriceItemList(List<InventoryItemEntry>? items)
    {
        if (items == null || items.Count == 0) return 0;

        long total = 0;
        foreach (var item in items)
        {
            try
            {
                var price = (long)handbookHelper.GetTemplatePrice(item.TemplateId);
                total += price * item.Count;
            }
            catch { /* unknown template — skip */ }
        }
        return total;
    }

    // ══════════════════════════════════════════════════════════════
    //  GET methods (called from dashboard GET endpoints)
    // ══════════════════════════════════════════════════════════════

    private SeasonalEventService? _seasonService;
    public void SetSeasonService(SeasonalEventService svc) => _seasonService = svc;

    public TelemetryCurrentDto GetCurrent(string? sourceId = null)
    {
        var season = "";
        try
        {
            if (_seasonService != null)
            {
                var s = _seasonService.GetActiveWeatherSeason();
                season = s switch
                {
                    Season.SUMMER => "Summer",
                    Season.AUTUMN => "Autumn",
                    Season.WINTER => "Winter",
                    Season.SPRING => "Spring",
                    Season.AUTUMN_LATE => "Late Autumn",
                    Season.SPRING_EARLY => "Early Spring",
                    Season.STORM => "Storm",
                    _ => s.ToString()
                };
            }
        }
        catch (Exception ex)
        {
            logger.Debug($"[Telemetry] Failed to resolve active season: {ex.Message}");
        }

        lock (_lock)
        {
            var src = ResolveSource(sourceId);
            if (src == null)
            {
                return new TelemetryCurrentDto
                {
                    RaidActive = false,
                    Season = season
                };
            }

            var isActive = src.RaidState != null && src.RaidState.Status != "idle";
            return new TelemetryCurrentDto
            {
                RaidActive = isActive,
                RaidState = src.RaidState,
                Performance = src.Performance,
                Players = src.Players?.Players ?? [],
                Bots = src.Bots,
                DamageStats = src.DamageStats,
                Positions = isActive ? src.Positions : [],
                Season = season
            };
        }
    }

    public KillFeedResponse GetKillFeed(int limit = 50, string? sourceId = null)
    {
        lock (_lock)
        {
            var src = ResolveSource(sourceId);
            var isLive = src?.RaidState != null && src.RaidState.Status != "idle";

            IEnumerable<KillFeedEntry> kills = _killFeed;
            if (!string.IsNullOrEmpty(sourceId))
                kills = kills.Where(k => k.SourceId == sourceId);

            var result = kills.TakeLast(Math.Min(limit, MaxKillFeed)).Reverse().ToList();
            return new KillFeedResponse
            {
                Kills = result,
                IsLive = isLive,
                RaidMap = isLive ? "" : (src?.RaidState?.Map ?? "")
            };
        }
    }

    public RaidHistoryResponse GetRaidHistory(string? sourceId = null)
    {
        lock (_lock)
        {
            IEnumerable<RaidHistoryRecord> raids = _raidHistory;
            if (!string.IsNullOrEmpty(sourceId))
                raids = raids.Where(r => r.Summary.SourceId == sourceId);

            return new RaidHistoryResponse
            {
                Raids = raids.Select(r => r.Summary).ToList()
            };
        }
    }

    public RaidHistoryDetail? GetRaidDetail(string raidId)
    {
        lock (_lock)
        {
            var record = _raidHistory.FirstOrDefault(r => r.Summary.Id == raidId);
            if (record == null) return null;

            return new RaidHistoryDetail
            {
                Summary = record.Summary,
                Players = record.Players,
                Kills = record.Kills,
                Extracts = record.Extracts,
                DamageStats = record.DamageStats
            };
        }
    }

    private TelemetrySource? ResolveSource(string? sourceId)
    {
        if (!string.IsNullOrEmpty(sourceId))
        {
            _sources.TryGetValue(sourceId, out var exact);
            return exact;
        }

        // Default: return the most recently active source
        return _sources.Values
            .OrderByDescending(s => s.LastSeen)
            .FirstOrDefault();
    }

    // ══════════════════════════════════════════════════════════════
    //  Positions (for live minimap)
    // ══════════════════════════════════════════════════════════════

    private float _mapRefreshRate = 1.0f;

    public float GetMapRefreshRate() => _mapRefreshRate;
    public void SetMapRefreshRate(float rate)
    {
        _mapRefreshRate = Math.Clamp(rate, 0.05f, 10f);
        logger.Info($"[Telemetry] Map refresh rate set to {_mapRefreshRate}s");
    }

    public void UpdatePositions(PositionPayload payload)
    {
        var sourceId = NormalizeSourceId(payload.SourceId);
        lock (_lock)
        {
            var source = GetOrCreateSource(sourceId);
            source.LastSeen = DateTime.UtcNow;
            source.Positions = payload.Positions ?? [];
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  Performance history buffer (rolling 5 minutes)
    // ══════════════════════════════════════════════════════════════

    private const int MaxPerfHistory = 60;

    // Server process CPU tracking (delta-based)
    private TimeSpan _lastServerCpuTime;
    private DateTime _lastServerCpuSample;
    private readonly int _processorCount = Environment.ProcessorCount;

    private (double cpuPercent, long ramUsedMb) GetServerProcessStats()
    {
        try
        {
            var proc = Process.GetCurrentProcess();
            var now = DateTime.UtcNow;
            var cpuTime = proc.TotalProcessorTime;
            var ramMb = proc.WorkingSet64 / (1024 * 1024);

            double cpuPercent = 0;
            if (_lastServerCpuSample != default)
            {
                var elapsed = (now - _lastServerCpuSample).TotalMilliseconds;
                if (elapsed > 0)
                {
                    var cpuDelta = (cpuTime - _lastServerCpuTime).TotalMilliseconds;
                    cpuPercent = Math.Round(cpuDelta / elapsed / _processorCount * 100, 1);
                    cpuPercent = Math.Clamp(cpuPercent, 0, 100);
                }
            }

            _lastServerCpuTime = cpuTime;
            _lastServerCpuSample = now;
            return (cpuPercent, ramMb);
        }
        catch
        {
            return (0, 0);
        }
    }

    public void RecordPerformanceHistory(PerformancePayload payload, string sourceId)
    {
        var (serverCpu, serverRam) = GetServerProcessStats();

        lock (_lock)
        {
            var source = GetOrCreateSource(sourceId);

            var ramTotalMb = payload.SystemInfo?.TotalRamMb ?? 0;
            var processRamPercent = ramTotalMb > 0 ? Math.Round((double)payload.MemoryMb / ramTotalMb * 100, 1) : 0;
            var systemRamPercent = ramTotalMb > 0 ? Math.Round((double)payload.SystemRamUsedMb / ramTotalMb * 100, 1) : 0;

            var useSysCpu = payload.SystemCpuPercent > 0;
            var useSysRam = payload.SystemRamUsedMb > 0;

            var entry = new PerformanceHistoryEntry
            {
                Timestamp = DateTime.UtcNow,
                CpuPercent = useSysCpu ? payload.SystemCpuPercent : payload.CpuUsage,
                RamPercent = useSysRam ? systemRamPercent : processRamPercent,
                RamUsedMb = useSysRam ? payload.SystemRamUsedMb : payload.MemoryMb,
                RamTotalMb = ramTotalMb,
                Fps = payload.Fps,
                ProcessCpuPercent = payload.CpuUsage,
                ProcessRamPercent = processRamPercent,
                ProcessRamUsedMb = payload.MemoryMb,
                ServerCpuPercent = serverCpu,
                ServerRamUsedMb = serverRam
            };

            source.PerformanceHistory.Add(entry);
            if (source.PerformanceHistory.Count > MaxPerfHistory)
                source.PerformanceHistory.RemoveRange(0, source.PerformanceHistory.Count - MaxPerfHistory);
        }
    }

    public List<PerformanceHistoryEntry> GetPerformanceHistory(string? sourceId = null)
    {
        lock (_lock)
        {
            if (!string.IsNullOrEmpty(sourceId))
            {
                if (_sources.TryGetValue(sourceId, out var src))
                    return [.. src.PerformanceHistory];
                return [];
            }

            // Default: return from most recently active source
            var active = _sources.Values
                .OrderByDescending(s => s.LastSeen)
                .FirstOrDefault();
            return active != null ? [.. active.PerformanceHistory] : [];
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  Lifetime stats (aggregated from raid history)
    // ══════════════════════════════════════════════════════════════

    public LifetimeStatsDto GetLifetimeStats()
    {
        lock (_lock)
        {
            if (_raidHistory.Count == 0)
                return new LifetimeStatsDto();

            var totalRaids = _raidHistory.Count;
            var survived = _raidHistory.Count(r => r.Summary.Survived > 0);
            var deaths = totalRaids - survived;
            var totalKills = 0;
            var pmcKills = 0;
            var scavKills = 0;
            var bossKills = 0;
            var totalDamage = 0;
            var totalXp = 0;
            var totalHeadshots = 0;
            double longestShot = 0;
            var totalDurationSec = 0;
            var raidsByMap = new Dictionary<string, int>();
            var killsByWeapon = new Dictionary<string, int>();

            foreach (var raid in _raidHistory)
            {
                totalDurationSec += raid.Summary.Duration;

                var mapName = !string.IsNullOrEmpty(raid.Summary.MapName) ? raid.Summary.MapName : raid.Summary.Map;
                if (!string.IsNullOrEmpty(mapName))
                    raidsByMap[mapName] = raidsByMap.GetValueOrDefault(mapName) + 1;

                foreach (var p in raid.Players)
                {
                    totalKills += p.Kills;
                    pmcKills += p.KilledPmc;
                    scavKills += p.KilledScav;
                    bossKills += p.KilledBoss;
                    totalDamage += p.DamageDealt;
                    totalXp += p.XpEarned;
                }

                foreach (var k in raid.Kills)
                {
                    if (k.IsHeadshot) totalHeadshots++;
                    if (k.Distance > longestShot) longestShot = k.Distance;

                    var weapon = !string.IsNullOrEmpty(k.WeaponName) ? k.WeaponName : "Unknown";
                    killsByWeapon[weapon] = killsByWeapon.GetValueOrDefault(weapon) + 1;
                }

                if (raid.DamageStats != null)
                {
                    if (raid.DamageStats.LongestShot > longestShot)
                        longestShot = raid.DamageStats.LongestShot;
                }
            }

            var topWeapons = killsByWeapon
                .OrderByDescending(kv => kv.Value)
                .Take(10)
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            var favoriteMap = raidsByMap.Count > 0
                ? raidsByMap.OrderByDescending(kv => kv.Value).First().Key
                : "";

            var trend = _raidHistory.Take(20).Select(r => new RaidTrendEntry
            {
                Timestamp = r.Summary.Timestamp,
                Map = !string.IsNullOrEmpty(r.Summary.MapName) ? r.Summary.MapName : r.Summary.Map,
                Survived = r.Summary.Survived > 0,
                Kills = r.Summary.TotalKills,
                Duration = r.Summary.Duration
            }).ToList();

            return new LifetimeStatsDto
            {
                TotalRaids = totalRaids,
                Survived = survived,
                Deaths = deaths,
                SurvivalRate = totalRaids > 0 ? Math.Round((double)survived / totalRaids * 100, 1) : 0,
                TotalKills = totalKills,
                PmcKills = pmcKills,
                ScavKills = scavKills,
                BossKills = bossKills,
                AvgKillsPerRaid = totalRaids > 0 ? Math.Round((double)totalKills / totalRaids, 1) : 0,
                TotalDamage = totalDamage,
                TotalXp = totalXp,
                TotalHeadshots = totalHeadshots,
                LongestShot = Math.Round(longestShot, 1),
                FavoriteMap = favoriteMap,
                AvgRaidDurationSec = totalRaids > 0 ? totalDurationSec / totalRaids : 0,
                TotalPlayTimeSec = totalDurationSec,
                RaidsByMap = raidsByMap,
                KillsByWeapon = topWeapons,
                RecentTrend = trend
            };
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  Alerts
    // ══════════════════════════════════════════════════════════════

    private readonly List<AlertEntry> _alerts = new();
    private const int MaxAlerts = 200;
    private int _alertCounter;

    public void AddAlert(string type, string message, string map = "", string icon = "")
    {
        lock (_lock)
        {
            _alerts.Add(new AlertEntry
            {
                Id = $"a{Interlocked.Increment(ref _alertCounter)}",
                Type = type,
                Message = message,
                Timestamp = DateTime.UtcNow,
                Map = map,
                Icon = icon
            });
            if (_alerts.Count > MaxAlerts)
                _alerts.RemoveRange(0, _alerts.Count - MaxAlerts);
        }
    }

    public AlertResponse GetAlerts(DateTime? since = null, int limit = 50)
    {
        lock (_lock)
        {
            var filtered = since.HasValue
                ? _alerts.Where(a => a.Timestamp > since.Value).ToList()
                : _alerts;
            return new AlertResponse
            {
                Alerts = filtered.TakeLast(Math.Min(limit, MaxAlerts)).Reverse().ToList(),
                Total = _alerts.Count
            };
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  Sources
    // ══════════════════════════════════════════════════════════════

    public List<TelemetrySourceDto> GetSources()
    {
        lock (_lock)
        {
            CleanupStaleSources();
            return _sources.Values.Select(s => new TelemetrySourceDto
            {
                SourceId = s.SourceId,
                DisplayName = s.DisplayName,
                Hostname = s.Hostname,
                Ip = s.Ip,
                LastSeen = s.LastSeen,
                RaidStatus = s.RaidState?.Status ?? "idle",
                Map = s.RaidState?.Map ?? ""
            }).ToList();
        }
    }

    private void CleanupStaleSources()
    {
        var now = DateTime.UtcNow;
        var stale = _sources
            .Where(kv => (now - kv.Value.LastSeen) > StaleTimeout)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var key in stale)
        {
            _sources.Remove(key);
            logger.Info($"[Telemetry] Removed stale source: {key}");
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  Raid history persistence
    // ══════════════════════════════════════════════════════════════

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private string GetRaidHistoryPath() =>
        Path.Combine(configService.ModPath, "data", "raid-history.json");

    public void Initialize()
    {
        var path = GetRaidHistoryPath();
        try
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var store = JsonSerializer.Deserialize<RaidHistoryStore>(json, _jsonOptions);
                if (store?.Raids != null)
                {
                    lock (_lock)
                    {
                        _raidHistory.Clear();
                        _raidHistory.AddRange(store.Raids);
                    }
                    logger.Success($"[Telemetry] Loaded {store.Raids.Count} raids from disk");
                }
            }
        }
        catch (Exception ex)
        {
            logger.Warning($"[Telemetry] Failed to load raid history: {ex.Message} — starting with empty history");
        }
    }

    private void SaveRaidHistory()
    {
        List<RaidHistoryRecord> snapshot;
        lock (_lock)
        {
            snapshot = [.. _raidHistory];
        }

        try
        {
            var dir = Path.GetDirectoryName(GetRaidHistoryPath())!;
            Directory.CreateDirectory(dir);

            var store = new RaidHistoryStore { Raids = snapshot };
            var json = JsonSerializer.Serialize(store, _jsonOptions);
            File.WriteAllText(GetRaidHistoryPath(), json);
        }
        catch (Exception ex)
        {
            logger.Error($"[Telemetry] Failed to save raid history: {ex.Message}");
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  Inner class: per-source telemetry state
    // ══════════════════════════════════════════════════════════════

    private class TelemetrySource
    {
        public string SourceId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Hostname { get; set; } = "";
        public string Ip { get; set; } = "";
        public DateTime FirstSeen { get; set; }
        public DateTime LastSeen { get; set; }

        public RaidStatePayload? RaidState { get; set; }
        public PerformancePayload? Performance { get; set; }
        public PlayerStatusPayload? Players { get; set; }
        public BotCountPayload? Bots { get; set; }
        public DamageStatsPayload? DamageStats { get; set; }
        public List<PlayerPositionEntry> Positions { get; set; } = [];
        public List<PerformanceHistoryEntry> PerformanceHistory { get; } = new();
    }
}

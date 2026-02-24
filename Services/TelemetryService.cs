using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using ZSlayerCommandCenter.Models;

namespace ZSlayerCommandCenter.Services;

[Injectable(InjectionType.Singleton)]
public class TelemetryService(
    ConfigService configService,
    LocaleService localeService,
    ISptLogger<TelemetryService> logger)
{
    private readonly object _lock = new();

    // Headless version info (from hello handshake)
    private string _headlessTelemetryVersion = "";
    private string _headlessFikaVersion = "";

    // Current state (overwritten each update)
    private RaidStatePayload? _currentRaidState;
    private PerformancePayload? _currentPerformance;
    private PlayerStatusPayload? _currentPlayers;
    private BotCountPayload? _currentBots;
    private DamageStatsPayload? _currentDamageStats;

    // Kill feed — ring buffer, max 100
    private readonly List<KillFeedEntry> _killFeed = new();
    private const int MaxKillFeed = 100;

    // Current raid kills (archived to history on raid end)
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
        // Prefer ShortName for weapons (e.g. "AK-74M" instead of full verbose name)
        if (locales.TryGetValue($"{templateId} ShortName", out var shortName) && !string.IsNullOrEmpty(shortName))
            return shortName;
        if (locales.TryGetValue($"{templateId} Name", out var name) && !string.IsNullOrEmpty(name))
            return TruncateWeaponName(name);
        return templateId;
    }

    private static readonly Dictionary<string, string> MapNameOverrides = new(StringComparer.OrdinalIgnoreCase)
    {
        ["factory4_day"] = "Factory (Day)",
        ["factory4_night"] = "Factory (Night)",
        ["bigmap"] = "Customs",
        ["laboratory"] = "The Lab",
        ["RezervBase"] = "Reserve",
        ["TarkovStreets"] = "Streets",
        ["Sandbox"] = "Ground Zero",
        ["Sandbox_high"] = "Ground Zero (High)",
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

    /// <summary>Strip Unity rich text tags like &lt;b&gt;, &lt;color=#fff&gt;, etc.</summary>
    private static string StripUnityTags(string? input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        return UnityTagRegex.Replace(input, "").Trim();
    }

    /// <summary>For AI actors, replace individual names with type labels.</summary>
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
                "boss" => clean, // keep boss names
                _ => clean
            };

        // For AI types (not pmc, not boss), use the type label instead of Russian names
        return type switch
        {
            "scav" => "Scav",
            "raider" => "Raider",
            "rogue" => "Rogue",
            "follower" => "Follower",
            "boss" => clean, // keep resolved boss name
            "pmc" => clean,  // keep PMC names
            _ => clean
        };
    }

    private static string TruncateWeaponName(string name)
    {
        if (string.IsNullOrEmpty(name) || name.Length <= MaxWeaponNameLength) return name;
        return name[..(MaxWeaponNameLength - 3)] + "...";
    }

    // ══════════════════════════════════════════════════════════════
    //  UPDATE methods (called from POST endpoints)
    // ══════════════════════════════════════════════════════════════

    public void UpdateHello(TelemetryHelloPayload payload)
    {
        lock (_lock)
        {
            _headlessTelemetryVersion = payload.TelemetryVersion;
            _headlessFikaVersion = payload.FikaClientVersion;
        }
        logger.Info($"Headless connected — Telemetry: {payload.TelemetryVersion}, Fika.Core: {payload.FikaClientVersion}");
    }

    public (string TelemetryVersion, string FikaClientVersion) GetHeadlessVersions()
    {
        lock (_lock) { return (_headlessTelemetryVersion, _headlessFikaVersion); }
    }

    public void UpdateRaidState(RaidStatePayload payload)
    {
        bool wasActive;
        bool wasIdle;

        lock (_lock)
        {
            wasActive = _currentRaidState != null && _currentRaidState.Status != "idle";
            wasIdle = _currentRaidState == null || _currentRaidState.Status == "idle";
            _currentRaidState = payload;

            // Start a new raid tracking session
            if (payload.Status is "loading" or "deploying" && string.IsNullOrEmpty(_currentRaidId))
            {
                _currentRaidId = Guid.NewGuid().ToString("N")[..12];
                _currentRaidStart = DateTime.UtcNow;
                _currentRaidKills.Clear();
                _currentRaidExtracts.Clear();
            }

            // Clear kill feed when entering a new raid so stale data doesn't show
            if (payload.Status == "in-raid" && wasIdle)
            {
                _killFeed.Clear();
                _currentRaidKills.Clear();
            }

            // Clear stale data when going idle
            if (payload.Status == "idle" && wasActive)
            {
                _currentPerformance = null;
                _currentPlayers = null;
                _currentBots = null;
                _currentDamageStats = null;
                _currentPositions = [];
                _currentRaidId = "";
            }
        }

        // Generate alerts on status transitions
        if (payload.Status is "loading" or "deploying" && wasIdle)
            AddAlert("raid-start", $"Raid starting on {payload.Map}", payload.Map, "rocket");
        if (payload.Status == "idle" && wasActive)
            AddAlert("raid-end", "Raid ended", payload.Map, "flag");

        logger.Info($"[Telemetry] Raid state → {payload.Status}, map: {payload.Map}, timer: {payload.RaidTimer}s");
    }

    public void UpdatePerformance(PerformancePayload payload)
    {
        lock (_lock)
        {
            _currentPerformance = payload;
        }
        RecordPerformanceHistory(payload);
    }

    public void AddKill(KillPayload payload)
    {
        // Clean actor names: strip Unity tags, replace AI names with type labels
        var killerName = CleanActorName(payload.Killer.Name, payload.Killer.Type);
        var victimName = CleanActorName(payload.Victim.Name, payload.Victim.Type);

        // Resolve weapon name (use ShortName from locale, truncate if needed)
        var weaponName = StripUnityTags(ResolveItemShortName(payload.Weapon));
        var ammoName = StripUnityTags(ResolveItemName(payload.Ammo));

        // Clean body part — filter out "None", "Unknown", "Common"
        var bodyPart = StripUnityTags(payload.BodyPart);
        if (bodyPart is "None" or "Unknown" or "Common" or "")
            bodyPart = "";

        // Handle unknown/environmental deaths
        var isEnvironmentalDeath = string.IsNullOrEmpty(killerName) || killerName == "Unknown";
        var isUnknownWeapon = string.IsNullOrEmpty(weaponName) || weaponName == "Unknown";

        if (isEnvironmentalDeath && isUnknownWeapon)
        {
            killerName = ""; // Will display as "KIA" in dashboard
            weaponName = "";
        }

        var entry = new KillFeedEntry
        {
            Id = $"k{Interlocked.Increment(ref _killCounter)}",
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
            _killFeed.Add(entry);
            if (_killFeed.Count > MaxKillFeed)
                _killFeed.RemoveRange(0, _killFeed.Count - MaxKillFeed);

            _currentRaidKills.Add(entry);
        }

        // Generate alerts for notable kills
        if (payload.Victim.Type == "pmc")
            AddAlert("pmc-kill", $"{killerName} killed {victimName}", payload.Map, "skull");
        if (payload.IsHeadshot)
            AddAlert("headshot", $"Headshot! {killerName} → {victimName} ({Math.Round(payload.Distance)}m)", payload.Map, "crosshair");

        logger.Info($"[Telemetry] Kill: {killerName} → {victimName} [{weaponName}] ({bodyPart}, {payload.Distance:F0}m)");
    }

    public void UpdatePlayers(PlayerStatusPayload payload)
    {
        lock (_lock)
        {
            _currentPlayers = payload;
        }
    }

    public void UpdateBots(BotCountPayload payload)
    {
        lock (_lock)
        {
            _currentBots = payload;
        }
    }

    public void UpdateDamageStats(DamageStatsPayload payload)
    {
        lock (_lock)
        {
            _currentDamageStats = payload;
        }
    }

    public void AddBossSpawn(BossSpawnPayload payload)
    {
        lock (_lock)
        {
            // Merge into current bots state if available
            if (_currentBots != null)
            {
                var existing = _currentBots.Bosses.FirstOrDefault(b =>
                    b.Name.Equals(payload.Boss, StringComparison.OrdinalIgnoreCase));
                if (existing == null)
                {
                    _currentBots.Bosses.Add(new BossStateEntry
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
        logger.Info($"[Telemetry] FinishRaid received — map: {payload.Map}, duration: {payload.RaidDuration}s, " +
                     $"players: {payload.Players.Count}, kills: {payload.TotalKills}, deaths: {payload.TotalDeaths}, " +
                     $"bosses: {payload.Bosses.Count}, currentRaidKills: {_currentRaidKills.Count}");

        lock (_lock)
        {
            // Use server-tracked kill count if plugin reported 0
            var actualKills = payload.TotalKills > 0 ? payload.TotalKills : _currentRaidKills.Count;

            // Derive per-player kill counts from kill feed as fallback
            EnrichPlayerKillCounts(payload.Players);

            var record = new RaidHistoryRecord
            {
                Summary = new RaidHistorySummary
                {
                    Id = string.IsNullOrEmpty(_currentRaidId)
                        ? Guid.NewGuid().ToString("N")[..12]
                        : _currentRaidId,
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
                DamageStats = _currentDamageStats
            };

            _raidHistory.Insert(0, record);

            // Clear current raid state
            _currentRaidKills.Clear();
            _currentRaidExtracts.Clear();
            _currentDamageStats = null;
            _currentRaidId = "";
        }

        SaveRaidHistory();

        logger.Success($"[Telemetry] Raid archived — {payload.Map}, {payload.Players.Count} players, raidHistory now has {_raidHistory.Count} entries");
    }

    /// <summary>
    /// If the plugin sent kills=0 for a player, derive kill counts from the server-tracked kill feed.
    /// Also derives kill types (PMC/Scav/Boss) if the plugin didn't send them.
    /// </summary>
    private void EnrichPlayerKillCounts(List<RaidSummaryPlayer> players)
    {
        foreach (var player in players)
        {
            // Find kills where this player is the killer (match by cleaned name)
            var playerKills = _currentRaidKills
                .Where(k => k.Killer.Name.Equals(player.Name, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Derive total kills if plugin sent 0
            if (player.Kills == 0 && playerKills.Count > 0)
                player.Kills = playerKills.Count;

            // Derive kill types if plugin sent all zeros
            if (player.KilledPmc == 0 && player.KilledScav == 0 && player.KilledBoss == 0 && playerKills.Count > 0)
            {
                player.KilledPmc = playerKills.Count(k => k.Victim.Type == "pmc");
                player.KilledScav = playerKills.Count(k => k.Victim.Type is "scav" or "raider" or "rogue" or "follower");
                player.KilledBoss = playerKills.Count(k => k.Victim.Type == "boss");
            }
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  GET methods (called from dashboard GET endpoints)
    // ══════════════════════════════════════════════════════════════

    // Late-bound season service (injected after construction to avoid DI ordering issues)
    private SeasonalEventService? _seasonService;
    public void SetSeasonService(SeasonalEventService svc) => _seasonService = svc;

    public TelemetryCurrentDto GetCurrent()
    {
        // Get season from server-side SeasonalEventService (not plugin)
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
            var isActive = _currentRaidState != null && _currentRaidState.Status != "idle";
            return new TelemetryCurrentDto
            {
                RaidActive = isActive,
                RaidState = _currentRaidState,
                Performance = _currentPerformance,
                Players = _currentPlayers?.Players ?? [],
                Bots = _currentBots,
                DamageStats = _currentDamageStats,
                Positions = isActive ? _currentPositions : [],
                Season = season
            };
        }
    }

    public KillFeedResponse GetKillFeed(int limit = 50)
    {
        lock (_lock)
        {
            var isLive = _currentRaidState != null && _currentRaidState.Status != "idle";
            var kills = _killFeed.TakeLast(Math.Min(limit, MaxKillFeed)).Reverse().ToList();
            return new KillFeedResponse
            {
                Kills = kills,
                IsLive = isLive,
                RaidMap = isLive ? "" : (_currentRaidState?.Map ?? "")
            };
        }
    }

    public RaidHistoryResponse GetRaidHistory()
    {
        lock (_lock)
        {
            return new RaidHistoryResponse
            {
                Raids = _raidHistory.Select(r => r.Summary).ToList()
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

    // ══════════════════════════════════════════════════════════════
    //  Positions (for live minimap)
    // ══════════════════════════════════════════════════════════════

    private List<PlayerPositionEntry> _currentPositions = [];

    // Map refresh rate (configurable via frontend slider)
    private float _mapRefreshRate = 1.0f; // seconds

    public float GetMapRefreshRate() => _mapRefreshRate;
    public void SetMapRefreshRate(float rate)
    {
        _mapRefreshRate = Math.Clamp(rate, 0.05f, 10f);
        logger.Info($"[Telemetry] Map refresh rate set to {_mapRefreshRate}s");
    }

    public void UpdatePositions(PositionPayload payload)
    {
        lock (_lock)
        {
            _currentPositions = payload.Positions ?? [];
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  Performance history buffer (rolling 5 minutes)
    // ══════════════════════════════════════════════════════════════

    private readonly List<PerformanceHistoryEntry> _perfHistory = new();
    private const int MaxPerfHistory = 60; // 5 min at 5s intervals

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

    public void RecordPerformanceHistory(PerformancePayload payload)
    {
        var (serverCpu, serverRam) = GetServerProcessStats();

        lock (_lock)
        {
            var ramTotalMb = payload.SystemInfo?.TotalRamMb ?? 0;
            var processRamPercent = ramTotalMb > 0 ? Math.Round((double)payload.MemoryMb / ramTotalMb * 100, 1) : 0;
            var systemRamPercent = ramTotalMb > 0 ? Math.Round((double)payload.SystemRamUsedMb / ramTotalMb * 100, 1) : 0;

            // Use system values as primary, fallback to process if system not available
            var useSysCpu = payload.SystemCpuPercent > 0;
            var useSysRam = payload.SystemRamUsedMb > 0;

            _perfHistory.Add(new PerformanceHistoryEntry
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
            });
            if (_perfHistory.Count > MaxPerfHistory)
                _perfHistory.RemoveRange(0, _perfHistory.Count - MaxPerfHistory);
        }
    }

    public List<PerformanceHistoryEntry> GetPerformanceHistory()
    {
        lock (_lock)
        {
            return [.. _perfHistory];
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

                // Map counts
                var mapName = !string.IsNullOrEmpty(raid.Summary.MapName) ? raid.Summary.MapName : raid.Summary.Map;
                if (!string.IsNullOrEmpty(mapName))
                    raidsByMap[mapName] = raidsByMap.GetValueOrDefault(mapName) + 1;

                // Player stats
                foreach (var p in raid.Players)
                {
                    totalKills += p.Kills;
                    pmcKills += p.KilledPmc;
                    scavKills += p.KilledScav;
                    bossKills += p.KilledBoss;
                    totalDamage += p.DamageDealt;
                    totalXp += p.XpEarned;
                }

                // Kill feed stats
                foreach (var k in raid.Kills)
                {
                    if (k.IsHeadshot) totalHeadshots++;
                    if (k.Distance > longestShot) longestShot = k.Distance;

                    // Weapon kill counts
                    var weapon = !string.IsNullOrEmpty(k.WeaponName) ? k.WeaponName : "Unknown";
                    killsByWeapon[weapon] = killsByWeapon.GetValueOrDefault(weapon) + 1;
                }

                // Damage stats
                if (raid.DamageStats != null)
                {
                    if (raid.DamageStats.LongestShot > longestShot)
                        longestShot = raid.DamageStats.LongestShot;
                }
            }

            // Top 10 weapons
            var topWeapons = killsByWeapon
                .OrderByDescending(kv => kv.Value)
                .Take(10)
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            // Favorite map
            var favoriteMap = raidsByMap.Count > 0
                ? raidsByMap.OrderByDescending(kv => kv.Value).First().Key
                : "";

            // Recent trend (last 20 raids)
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
    //  Raid history persistence
    // ══════════════════════════════════════════════════════════════

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private string GetRaidHistoryPath() =>
        Path.Combine(configService.ModPath, "data", "raid-history.json");

    /// <summary>Load persisted raid history from disk. Call once from OnLoad.</summary>
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
}

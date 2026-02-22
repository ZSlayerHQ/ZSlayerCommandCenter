using System.Text.RegularExpressions;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using ZSlayerCommandCenter.Models;

namespace ZSlayerCommandCenter.Services;

[Injectable(InjectionType.Singleton)]
public class TelemetryService(
    LocaleService localeService,
    ISptLogger<TelemetryService> logger)
{
    private readonly object _lock = new();

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

    // Raid history — max 50
    private readonly List<RaidHistoryRecord> _raidHistory = new();
    private const int MaxRaidHistory = 50;

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

    private string ResolveMapName(string locationId)
    {
        if (string.IsNullOrEmpty(locationId)) return "";
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

    public void UpdateRaidState(RaidStatePayload payload)
    {
        lock (_lock)
        {
            var wasActive = _currentRaidState != null && _currentRaidState.Status != "idle";
            var wasIdle = _currentRaidState == null || _currentRaidState.Status == "idle";
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
                _currentRaidId = "";
            }
        }

        logger.Info($"[Telemetry] Raid state → {payload.Status}, map: {payload.Map}, timer: {payload.RaidTimer}s");
    }

    public void UpdatePerformance(PerformancePayload payload)
    {
        lock (_lock)
        {
            _currentPerformance = payload;
        }
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

        logger.Info($"TelemetryService: Boss spawned — {payload.Boss} on {payload.Map} at {payload.RaidTime}s");
    }

    public void AddExtract(ExtractPayload payload)
    {
        lock (_lock)
        {
            _currentRaidExtracts.Add(payload);
        }
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
            if (_raidHistory.Count > MaxRaidHistory)
                _raidHistory.RemoveRange(MaxRaidHistory, _raidHistory.Count - MaxRaidHistory);

            // Clear current raid state
            _currentRaidKills.Clear();
            _currentRaidExtracts.Clear();
            _currentDamageStats = null;
            _currentRaidId = "";
        }

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

    public TelemetryCurrentDto GetCurrent()
    {
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
                DamageStats = _currentDamageStats
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

    // Internal storage record for raid history
    private record RaidHistoryRecord
    {
        public RaidHistorySummary Summary { get; set; } = new();
        public List<RaidSummaryPlayer> Players { get; set; } = [];
        public List<KillFeedEntry> Kills { get; set; } = [];
        public List<ExtractPayload> Extracts { get; set; } = [];
        public DamageStatsPayload? DamageStats { get; set; }
    }
}

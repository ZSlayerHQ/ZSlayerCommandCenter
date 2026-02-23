using System.Text.Json.Serialization;

namespace ZSlayerCommandCenter.Models;

// ══════════════════════════════════════════════════════════════════════
//  INBOUND — POSTed by the headless telemetry plugin
// ══════════════════════════════════════════════════════════════════════

public record TelemetryHelloPayload
{
    [JsonPropertyName("telemetryVersion")]
    public string TelemetryVersion { get; set; } = "";

    [JsonPropertyName("fikaClientVersion")]
    public string FikaClientVersion { get; set; } = "";
}

public record RaidStatePayload
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "idle"; // idle | loading | deploying | in-raid | extracting | post-raid

    [JsonPropertyName("map")]
    public string Map { get; set; } = "";

    [JsonPropertyName("raidTimer")]
    public int RaidTimer { get; set; }

    [JsonPropertyName("raidTimeLeft")]
    public int RaidTimeLeft { get; set; }

    [JsonPropertyName("timeOfDay")]
    public string TimeOfDay { get; set; } = "";

    [JsonPropertyName("weather")]
    public string Weather { get; set; } = "";

    [JsonPropertyName("players")]
    public RaidPlayerCounts? Players { get; set; }
}

public record RaidPlayerCounts
{
    [JsonPropertyName("pmcAlive")]
    public int PmcAlive { get; set; }

    [JsonPropertyName("pmcDead")]
    public int PmcDead { get; set; }

    [JsonPropertyName("scavAlive")]
    public int ScavAlive { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }
}

public record PerformancePayload
{
    [JsonPropertyName("fps")]
    public int Fps { get; set; }

    [JsonPropertyName("fpsAvg")]
    public int FpsAvg { get; set; }

    [JsonPropertyName("fpsMin")]
    public int FpsMin { get; set; }

    [JsonPropertyName("fpsMax")]
    public int FpsMax { get; set; }

    [JsonPropertyName("frameTimeMs")]
    public double FrameTimeMs { get; set; }

    [JsonPropertyName("memoryMb")]
    public long MemoryMb { get; set; }

    [JsonPropertyName("cpuUsage")]
    public double CpuUsage { get; set; }

    [JsonPropertyName("systemCpuPercent")]
    public double SystemCpuPercent { get; set; }

    [JsonPropertyName("systemRamUsedMb")]
    public long SystemRamUsedMb { get; set; }

    [JsonPropertyName("systemInfo")]
    public SystemInfoPayload? SystemInfo { get; set; }
}

public record SystemInfoPayload
{
    [JsonPropertyName("cpuModel")]
    public string CpuModel { get; set; } = "";

    [JsonPropertyName("cpuCores")]
    public int CpuCores { get; set; }

    [JsonPropertyName("cpuFrequencyMhz")]
    public int CpuFrequencyMhz { get; set; }

    [JsonPropertyName("gpuModel")]
    public string GpuModel { get; set; } = "";

    [JsonPropertyName("gpuVramMb")]
    public int GpuVramMb { get; set; }

    [JsonPropertyName("totalRamMb")]
    public int TotalRamMb { get; set; }

    [JsonPropertyName("os")]
    public string Os { get; set; } = "";
}

public record KillPayload
{
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("raidTime")]
    public int RaidTime { get; set; }

    [JsonPropertyName("map")]
    public string Map { get; set; } = "";

    [JsonPropertyName("killer")]
    public KillActorPayload Killer { get; set; } = new();

    [JsonPropertyName("victim")]
    public KillActorPayload Victim { get; set; } = new();

    [JsonPropertyName("weapon")]
    public string Weapon { get; set; } = ""; // template ID

    [JsonPropertyName("ammo")]
    public string Ammo { get; set; } = ""; // template ID

    [JsonPropertyName("bodyPart")]
    public string BodyPart { get; set; } = "";

    [JsonPropertyName("distance")]
    public double Distance { get; set; }

    [JsonPropertyName("isHeadshot")]
    public bool IsHeadshot { get; set; }
}

public record KillActorPayload
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = ""; // pmc | scav | boss | raider | follower | rogue

    [JsonPropertyName("level")]
    public int Level { get; set; }
}

public record PlayerStatusPayload
{
    [JsonPropertyName("map")]
    public string Map { get; set; } = "";

    [JsonPropertyName("players")]
    public List<PlayerStatusEntry> Players { get; set; } = [];
}

public record PlayerStatusEntry
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("profileId")]
    public string ProfileId { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("side")]
    public string Side { get; set; } = "";

    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("alive")]
    public bool Alive { get; set; }

    [JsonPropertyName("health")]
    public double Health { get; set; }

    [JsonPropertyName("pingMs")]
    public int? PingMs { get; set; }
}

public record BotCountPayload
{
    [JsonPropertyName("map")]
    public string Map { get; set; } = "";

    [JsonPropertyName("scavs")]
    public AliveDeadCount Scavs { get; set; } = new();

    [JsonPropertyName("raiders")]
    public AliveDeadCount Raiders { get; set; } = new();

    [JsonPropertyName("rogues")]
    public AliveDeadCount Rogues { get; set; } = new();

    [JsonPropertyName("aiPmcs")]
    public AliveDeadCount AiPmcs { get; set; } = new();

    [JsonPropertyName("bosses")]
    public List<BossStateEntry> Bosses { get; set; } = [];

    [JsonPropertyName("totalAI")]
    public AliveDeadCount TotalAI { get; set; } = new();
}

public record AliveDeadCount
{
    [JsonPropertyName("alive")]
    public int Alive { get; set; }

    [JsonPropertyName("dead")]
    public int Dead { get; set; }
}

public record BossStateEntry
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("alive")]
    public bool Alive { get; set; }

    [JsonPropertyName("killedBy")]
    public string? KilledBy { get; set; }
}

public record BossSpawnPayload
{
    [JsonPropertyName("map")]
    public string Map { get; set; } = "";

    [JsonPropertyName("boss")]
    public string Boss { get; set; } = "";

    [JsonPropertyName("followers")]
    public int Followers { get; set; }

    [JsonPropertyName("spawnPoint")]
    public string SpawnPoint { get; set; } = "";

    [JsonPropertyName("raidTime")]
    public int RaidTime { get; set; }
}

public record ExtractPayload
{
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("map")]
    public string Map { get; set; } = "";

    [JsonPropertyName("raidTime")]
    public int RaidTime { get; set; }

    [JsonPropertyName("player")]
    public ExtractPlayerInfo Player { get; set; } = new();

    [JsonPropertyName("outcome")]
    public string Outcome { get; set; } = ""; // survived | killed | run-through | mia

    [JsonPropertyName("extractPoint")]
    public string? ExtractPoint { get; set; }

    [JsonPropertyName("killedBy")]
    public string? KilledBy { get; set; }

    [JsonPropertyName("raidDuration")]
    public int RaidDuration { get; set; }
}

public record ExtractPlayerInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("profileId")]
    public string ProfileId { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("side")]
    public string Side { get; set; } = "";

    [JsonPropertyName("level")]
    public int Level { get; set; }
}

public record RaidSummaryPayload
{
    [JsonPropertyName("map")]
    public string Map { get; set; } = "";

    [JsonPropertyName("raidDuration")]
    public int RaidDuration { get; set; }

    [JsonPropertyName("players")]
    public List<RaidSummaryPlayer> Players { get; set; } = [];

    [JsonPropertyName("bosses")]
    public List<BossStateEntry> Bosses { get; set; } = [];

    [JsonPropertyName("totalKills")]
    public int TotalKills { get; set; }

    [JsonPropertyName("totalDeaths")]
    public int TotalDeaths { get; set; }
}

public record RaidSummaryPlayer
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("outcome")]
    public string Outcome { get; set; } = "";

    [JsonPropertyName("extractPoint")]
    public string? ExtractPoint { get; set; }

    [JsonPropertyName("kills")]
    public int Kills { get; set; }

    [JsonPropertyName("killedPmc")]
    public int KilledPmc { get; set; }

    [JsonPropertyName("killedScav")]
    public int KilledScav { get; set; }

    [JsonPropertyName("killedBoss")]
    public int KilledBoss { get; set; }

    [JsonPropertyName("deaths")]
    public int Deaths { get; set; }

    [JsonPropertyName("damageDealt")]
    public int DamageDealt { get; set; }

    [JsonPropertyName("damageReceived")]
    public int DamageReceived { get; set; }

    [JsonPropertyName("accuracy")]
    public double Accuracy { get; set; }

    [JsonPropertyName("xpEarned")]
    public int XpEarned { get; set; }

    [JsonPropertyName("lootValue")]
    public long LootValue { get; set; }

    // ── Enhanced stats from SessionCounters ──

    [JsonPropertyName("armorDamage")]
    public int ArmorDamage { get; set; }

    [JsonPropertyName("headshots")]
    public int Headshots { get; set; }

    [JsonPropertyName("hits")]
    public int Hits { get; set; }

    [JsonPropertyName("ammoUsed")]
    public int AmmoUsed { get; set; }

    [JsonPropertyName("longestKill")]
    public double LongestKill { get; set; }

    [JsonPropertyName("killStreak")]
    public int KillStreak { get; set; }

    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("side")]
    public string Side { get; set; } = "";

    // ── XP breakdown ──

    [JsonPropertyName("xpKill")]
    public int XpKill { get; set; }

    [JsonPropertyName("xpKillStreak")]
    public int XpKillStreak { get; set; }

    [JsonPropertyName("xpDamage")]
    public int XpDamage { get; set; }

    [JsonPropertyName("xpLooting")]
    public int XpLooting { get; set; }

    [JsonPropertyName("xpExitStatus")]
    public int XpExitStatus { get; set; }

    [JsonPropertyName("xpBodyPart")]
    public int XpBodyPart { get; set; }
}

public record DamageStatsPayload
{
    [JsonPropertyName("totalHits")]
    public int TotalHits { get; set; }

    [JsonPropertyName("totalDamageDealt")]
    public int TotalDamageDealt { get; set; }

    [JsonPropertyName("headshotCount")]
    public int HeadshotCount { get; set; }

    [JsonPropertyName("longestShot")]
    public double LongestShot { get; set; }

    [JsonPropertyName("avgDistance")]
    public double AvgDistance { get; set; }

    [JsonPropertyName("bodyParts")]
    public HitDistribution BodyParts { get; set; } = new();
}

public record HitDistribution
{
    [JsonPropertyName("head")]
    public int Head { get; set; }

    [JsonPropertyName("chest")]
    public int Chest { get; set; }

    [JsonPropertyName("stomach")]
    public int Stomach { get; set; }

    [JsonPropertyName("leftArm")]
    public int LeftArm { get; set; }

    [JsonPropertyName("rightArm")]
    public int RightArm { get; set; }

    [JsonPropertyName("leftLeg")]
    public int LeftLeg { get; set; }

    [JsonPropertyName("rightLeg")]
    public int RightLeg { get; set; }
}

public record RaidSummaryPayloadV2 : RaidSummaryPayload
{
    [JsonPropertyName("damageStats")]
    public DamageStatsPayload? DamageStats { get; set; }
}

// ══════════════════════════════════════════════════════════════════════
//  OUTBOUND — Served to the dashboard via GET endpoints
// ══════════════════════════════════════════════════════════════════════

public record TelemetryCurrentDto
{
    [JsonPropertyName("raidActive")]
    public bool RaidActive { get; set; }

    [JsonPropertyName("raidState")]
    public RaidStatePayload? RaidState { get; set; }

    [JsonPropertyName("performance")]
    public PerformancePayload? Performance { get; set; }

    [JsonPropertyName("players")]
    public List<PlayerStatusEntry> Players { get; set; } = [];

    [JsonPropertyName("bots")]
    public BotCountPayload? Bots { get; set; }

    [JsonPropertyName("damageStats")]
    public DamageStatsPayload? DamageStats { get; set; }

    [JsonPropertyName("positions")]
    public List<PlayerPositionEntry> Positions { get; set; } = [];

    [JsonPropertyName("season")]
    public string Season { get; set; } = "";
}

public record KillFeedEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("raidTime")]
    public int RaidTime { get; set; }

    [JsonPropertyName("map")]
    public string Map { get; set; } = "";

    [JsonPropertyName("killer")]
    public KillActorPayload Killer { get; set; } = new();

    [JsonPropertyName("victim")]
    public KillActorPayload Victim { get; set; } = new();

    [JsonPropertyName("weaponName")]
    public string WeaponName { get; set; } = "";

    [JsonPropertyName("ammoName")]
    public string AmmoName { get; set; } = "";

    [JsonPropertyName("bodyPart")]
    public string BodyPart { get; set; } = "";

    [JsonPropertyName("distance")]
    public double Distance { get; set; }

    [JsonPropertyName("isHeadshot")]
    public bool IsHeadshot { get; set; }
}

public record KillFeedResponse
{
    [JsonPropertyName("kills")]
    public List<KillFeedEntry> Kills { get; set; } = [];

    [JsonPropertyName("isLive")]
    public bool IsLive { get; set; }

    [JsonPropertyName("raidMap")]
    public string RaidMap { get; set; } = "";
}

public record RaidHistorySummary
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("map")]
    public string Map { get; set; } = "";

    [JsonPropertyName("mapName")]
    public string MapName { get; set; } = "";

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("duration")]
    public int Duration { get; set; }

    [JsonPropertyName("playerCount")]
    public int PlayerCount { get; set; }

    [JsonPropertyName("survived")]
    public int Survived { get; set; }

    [JsonPropertyName("totalKills")]
    public int TotalKills { get; set; }

    [JsonPropertyName("totalDeaths")]
    public int TotalDeaths { get; set; }

    [JsonPropertyName("bosses")]
    public List<BossStateEntry> Bosses { get; set; } = [];
}

public record RaidHistoryResponse
{
    [JsonPropertyName("raids")]
    public List<RaidHistorySummary> Raids { get; set; } = [];
}

public record RaidHistoryDetail
{
    [JsonPropertyName("summary")]
    public RaidHistorySummary Summary { get; set; } = new();

    [JsonPropertyName("players")]
    public List<RaidSummaryPlayer> Players { get; set; } = [];

    [JsonPropertyName("kills")]
    public List<KillFeedEntry> Kills { get; set; } = [];

    [JsonPropertyName("extracts")]
    public List<ExtractPayload> Extracts { get; set; } = [];

    [JsonPropertyName("damageStats")]
    public DamageStatsPayload? DamageStats { get; set; }
}

public record RaidHistoryStore
{
    [JsonPropertyName("raids")]
    public List<RaidHistoryRecord> Raids { get; set; } = [];
}

public record RaidHistoryRecord
{
    [JsonPropertyName("summary")]
    public RaidHistorySummary Summary { get; set; } = new();

    [JsonPropertyName("players")]
    public List<RaidSummaryPlayer> Players { get; set; } = [];

    [JsonPropertyName("kills")]
    public List<KillFeedEntry> Kills { get; set; } = [];

    [JsonPropertyName("extracts")]
    public List<ExtractPayload> Extracts { get; set; } = [];

    [JsonPropertyName("damageStats")]
    public DamageStatsPayload? DamageStats { get; set; }
}

// ══════════════════════════════════════════════════════════════════════
//  POSITIONS — Live minimap data
// ══════════════════════════════════════════════════════════════════════

public record PositionPayload
{
    [JsonPropertyName("map")]
    public string Map { get; set; } = "";

    [JsonPropertyName("positions")]
    public List<PlayerPositionEntry> Positions { get; set; } = [];
}

public record PlayerPositionEntry
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("profileId")]
    public string ProfileId { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }

    [JsonPropertyName("z")]
    public double Z { get; set; }

    [JsonPropertyName("rotation")]
    public double Rotation { get; set; }

    [JsonPropertyName("alive")]
    public bool Alive { get; set; }

    [JsonPropertyName("side")]
    public string Side { get; set; } = "";
}

// ══════════════════════════════════════════════════════════════════════
//  ALERTS — Raid event notifications
// ══════════════════════════════════════════════════════════════════════

public record AlertEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("map")]
    public string Map { get; set; } = "";

    [JsonPropertyName("icon")]
    public string Icon { get; set; } = "";
}

public record AlertResponse
{
    [JsonPropertyName("alerts")]
    public List<AlertEntry> Alerts { get; set; } = [];

    [JsonPropertyName("total")]
    public int Total { get; set; }
}

// ══════════════════════════════════════════════════════════════════════
//  LIFETIME STATS — Aggregated player statistics
// ══════════════════════════════════════════════════════════════════════

public record LifetimeStatsDto
{
    [JsonPropertyName("totalRaids")]
    public int TotalRaids { get; set; }

    [JsonPropertyName("survived")]
    public int Survived { get; set; }

    [JsonPropertyName("deaths")]
    public int Deaths { get; set; }

    [JsonPropertyName("survivalRate")]
    public double SurvivalRate { get; set; }

    [JsonPropertyName("totalKills")]
    public int TotalKills { get; set; }

    [JsonPropertyName("pmcKills")]
    public int PmcKills { get; set; }

    [JsonPropertyName("scavKills")]
    public int ScavKills { get; set; }

    [JsonPropertyName("bossKills")]
    public int BossKills { get; set; }

    [JsonPropertyName("avgKillsPerRaid")]
    public double AvgKillsPerRaid { get; set; }

    [JsonPropertyName("totalDamage")]
    public int TotalDamage { get; set; }

    [JsonPropertyName("totalXp")]
    public int TotalXp { get; set; }

    [JsonPropertyName("totalHeadshots")]
    public int TotalHeadshots { get; set; }

    [JsonPropertyName("longestShot")]
    public double LongestShot { get; set; }

    [JsonPropertyName("favoriteMap")]
    public string FavoriteMap { get; set; } = "";

    [JsonPropertyName("avgRaidDurationSec")]
    public int AvgRaidDurationSec { get; set; }

    [JsonPropertyName("totalPlayTimeSec")]
    public int TotalPlayTimeSec { get; set; }

    [JsonPropertyName("raidsByMap")]
    public Dictionary<string, int> RaidsByMap { get; set; } = new();

    [JsonPropertyName("killsByWeapon")]
    public Dictionary<string, int> KillsByWeapon { get; set; } = new();

    [JsonPropertyName("recentTrend")]
    public List<RaidTrendEntry> RecentTrend { get; set; } = [];
}

public record RaidTrendEntry
{
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("map")]
    public string Map { get; set; } = "";

    [JsonPropertyName("survived")]
    public bool Survived { get; set; }

    [JsonPropertyName("kills")]
    public int Kills { get; set; }

    [JsonPropertyName("duration")]
    public int Duration { get; set; }
}

// ══════════════════════════════════════════════════════════════════════
//  WEATHER — Forcing settings
// ══════════════════════════════════════════════════════════════════════

public record WeatherForceDto
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("hourOfDay")]
    public int HourOfDay { get; set; } = -1;

    [JsonPropertyName("cloudinessType")]
    public int CloudinessType { get; set; } = -1;

    [JsonPropertyName("rainType")]
    public int RainType { get; set; } = -1;

    [JsonPropertyName("fogType")]
    public int FogType { get; set; } = -1;

    [JsonPropertyName("windType")]
    public int WindType { get; set; } = -1;

    [JsonPropertyName("timeFlowType")]
    public int TimeFlowType { get; set; } = -1;

    [JsonPropertyName("randomWeather")]
    public bool RandomWeather { get; set; } = true;

    [JsonPropertyName("randomTime")]
    public bool RandomTime { get; set; } = true;
}

public record WeatherForceRequest
{
    [JsonPropertyName("hourOfDay")]
    public int? HourOfDay { get; set; }

    [JsonPropertyName("cloudinessType")]
    public int? CloudinessType { get; set; }

    [JsonPropertyName("rainType")]
    public int? RainType { get; set; }

    [JsonPropertyName("fogType")]
    public int? FogType { get; set; }

    [JsonPropertyName("windType")]
    public int? WindType { get; set; }

    [JsonPropertyName("timeFlowType")]
    public int? TimeFlowType { get; set; }

    [JsonPropertyName("randomWeather")]
    public bool? RandomWeather { get; set; }

    [JsonPropertyName("randomTime")]
    public bool? RandomTime { get; set; }
}

public record WeatherCurrentResponse
{
    [JsonPropertyName("forced")]
    public WeatherForceDto Forced { get; set; } = new();

    [JsonPropertyName("options")]
    public WeatherOptionsDto Options { get; set; } = new();
}

public record WeatherOptionsDto
{
    [JsonPropertyName("cloudiness")]
    public List<WeatherOptionItem> Cloudiness { get; set; } = [];

    [JsonPropertyName("rain")]
    public List<WeatherOptionItem> Rain { get; set; } = [];

    [JsonPropertyName("fog")]
    public List<WeatherOptionItem> Fog { get; set; } = [];

    [JsonPropertyName("wind")]
    public List<WeatherOptionItem> Wind { get; set; } = [];

    [JsonPropertyName("timeFlow")]
    public List<WeatherOptionItem> TimeFlow { get; set; } = [];
}

public record WeatherOptionItem
{
    [JsonPropertyName("value")]
    public int Value { get; set; }

    [JsonPropertyName("label")]
    public string Label { get; set; } = "";
}

// ══════════════════════════════════════════════════════════════════════
//  MAP REFRESH RATE
// ══════════════════════════════════════════════════════════════════════

public record MapRefreshRateRequest
{
    [JsonPropertyName("intervalSec")]
    public float IntervalSec { get; set; } = 1.0f;
}

// ══════════════════════════════════════════════════════════════════════
//  PERFORMANCE HISTORY — Rolling buffer for charts
// ══════════════════════════════════════════════════════════════════════

public record PerformanceHistoryEntry
{
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("cpuPercent")]
    public double CpuPercent { get; set; }

    [JsonPropertyName("ramPercent")]
    public double RamPercent { get; set; }

    [JsonPropertyName("ramUsedMb")]
    public long RamUsedMb { get; set; }

    [JsonPropertyName("ramTotalMb")]
    public int RamTotalMb { get; set; }

    [JsonPropertyName("fps")]
    public int Fps { get; set; }

    // Process-specific values (EFT.exe)
    [JsonPropertyName("processCpuPercent")]
    public double ProcessCpuPercent { get; set; }

    [JsonPropertyName("processRamPercent")]
    public double ProcessRamPercent { get; set; }

    [JsonPropertyName("processRamUsedMb")]
    public long ProcessRamUsedMb { get; set; }

    // SPT server process values
    [JsonPropertyName("serverCpuPercent")]
    public double ServerCpuPercent { get; set; }

    [JsonPropertyName("serverRamUsedMb")]
    public long ServerRamUsedMb { get; set; }
}

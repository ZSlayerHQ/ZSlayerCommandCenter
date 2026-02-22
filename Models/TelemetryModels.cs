using System.Text.Json.Serialization;

namespace ZSlayerCommandCenter.Models;

// ══════════════════════════════════════════════════════════════════════
//  INBOUND — POSTed by the headless telemetry plugin
// ══════════════════════════════════════════════════════════════════════

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

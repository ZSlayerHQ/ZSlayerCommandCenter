using System.Text.Json.Serialization;

namespace ZSlayerCommandCenter.Models;

// ── Roster ──

public record PlayerRosterDto
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("players")]
    public List<PlayerRosterEntry> Players { get; set; } = [];
}

public record PlayerRosterEntry
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = "";

    [JsonPropertyName("nickname")]
    public string Nickname { get; set; } = "";

    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("side")]
    public string Side { get; set; } = "";

    [JsonPropertyName("experience")]
    public int Experience { get; set; }

    [JsonPropertyName("online")]
    public bool Online { get; set; }

    [JsonPropertyName("roubles")]
    public long Roubles { get; set; }

    [JsonPropertyName("registrationDate")]
    public long RegistrationDate { get; set; }

    [JsonPropertyName("lastActivity")]
    public DateTime? LastActivity { get; set; }

    [JsonPropertyName("banned")]
    public bool Banned { get; set; }
}

// ── Profile ──

public record PlayerProfileDto
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = "";

    [JsonPropertyName("nickname")]
    public string Nickname { get; set; } = "";

    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("side")]
    public string Side { get; set; } = "";

    [JsonPropertyName("experience")]
    public int Experience { get; set; }

    [JsonPropertyName("experienceToNextLevel")]
    public int ExperienceToNextLevel { get; set; }

    [JsonPropertyName("stashValue")]
    public double StashValue { get; set; }

    [JsonPropertyName("currencies")]
    public PlayerCurrenciesDto Currencies { get; set; } = new();

    [JsonPropertyName("survivalRate")]
    public double SurvivalRate { get; set; }

    [JsonPropertyName("totalRaids")]
    public int TotalRaids { get; set; }

    [JsonPropertyName("survived")]
    public int Survived { get; set; }

    [JsonPropertyName("kia")]
    public int KIA { get; set; }

    [JsonPropertyName("mia")]
    public int MIA { get; set; }

    [JsonPropertyName("runThrough")]
    public int RunThrough { get; set; }

    [JsonPropertyName("kills")]
    public int Kills { get; set; }

    [JsonPropertyName("pmcKills")]
    public int PmcKills { get; set; }

    [JsonPropertyName("scavKills")]
    public int ScavKills { get; set; }

    [JsonPropertyName("bossKills")]
    public int BossKills { get; set; }

    [JsonPropertyName("kdRatio")]
    public double KdRatio { get; set; }

    [JsonPropertyName("headshots")]
    public int Headshots { get; set; }

    [JsonPropertyName("longestShot")]
    public double LongestShot { get; set; }

    [JsonPropertyName("overallAccuracy")]
    public double OverallAccuracy { get; set; }

    [JsonPropertyName("longestWinStreak")]
    public int LongestWinStreak { get; set; }

    [JsonPropertyName("onlineTimeSec")]
    public int OnlineTimeSec { get; set; }

    [JsonPropertyName("skills")]
    public List<PlayerSkillDto> Skills { get; set; } = [];

    [JsonPropertyName("quests")]
    public List<PlayerQuestDto> Quests { get; set; } = [];

    [JsonPropertyName("hideoutAreas")]
    public List<PlayerHideoutDto> HideoutAreas { get; set; } = [];

    [JsonPropertyName("traders")]
    public List<PlayerTraderDto> Traders { get; set; } = [];
}

public record PlayerCurrenciesDto
{
    [JsonPropertyName("roubles")]
    public long Roubles { get; set; }

    [JsonPropertyName("dollars")]
    public long Dollars { get; set; }

    [JsonPropertyName("euros")]
    public long Euros { get; set; }
}

public record PlayerSkillDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("progress")]
    public double Progress { get; set; }

    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("isElite")]
    public bool IsElite { get; set; }
}

public record PlayerQuestDto
{
    [JsonPropertyName("questId")]
    public string QuestId { get; set; } = "";

    [JsonPropertyName("questName")]
    public string QuestName { get; set; } = "";

    [JsonPropertyName("traderName")]
    public string TraderName { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("startTime")]
    public long StartTime { get; set; }

    [JsonPropertyName("statusTimers")]
    public Dictionary<string, long> StatusTimers { get; set; } = new();

    [JsonPropertyName("conditions")]
    public List<QuestConditionDto> Conditions { get; set; } = [];
}

public record QuestConditionDto
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("value")]
    public double Value { get; set; }

    [JsonPropertyName("completed")]
    public bool Completed { get; set; }
}

public record PlayerHideoutDto
{
    [JsonPropertyName("areaType")]
    public int AreaType { get; set; }

    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("active")]
    public bool Active { get; set; }

    [JsonPropertyName("constructing")]
    public bool Constructing { get; set; }
}

public record PlayerTraderDto
{
    [JsonPropertyName("traderId")]
    public string TraderId { get; set; } = "";

    [JsonPropertyName("traderName")]
    public string TraderName { get; set; } = "";

    [JsonPropertyName("loyaltyLevel")]
    public int LoyaltyLevel { get; set; }

    [JsonPropertyName("standing")]
    public double Standing { get; set; }

    [JsonPropertyName("salesSum")]
    public long SalesSum { get; set; }
}

// ── Full Stats ──

public record PlayerFullStatsDto
{
    // ── Raid Overview ──
    [JsonPropertyName("raids")] public int Raids { get; set; }
    [JsonPropertyName("survived")] public int Survived { get; set; }
    [JsonPropertyName("kia")] public int KIA { get; set; }
    [JsonPropertyName("mia")] public int MIA { get; set; }
    [JsonPropertyName("awol")] public int AWOL { get; set; }
    [JsonPropertyName("kills")] public int Kills { get; set; }
    [JsonPropertyName("onlineTimeSec")] public int OnlineTimeSec { get; set; }
    [JsonPropertyName("runThroughs")] public int RunThroughs { get; set; }
    [JsonPropertyName("survivalRate")] public double SurvivalRate { get; set; }
    [JsonPropertyName("avgLifeSpanSec")] public int AvgLifeSpanSec { get; set; }
    [JsonPropertyName("currentWinStreak")] public int CurrentWinStreak { get; set; }
    [JsonPropertyName("longestWinStreak")] public int LongestWinStreak { get; set; }
    [JsonPropertyName("leaveRate")] public double LeaveRate { get; set; }
    [JsonPropertyName("kdRatio")] public double KdRatio { get; set; }
    [JsonPropertyName("accountLifetimeDays")] public int AccountLifetimeDays { get; set; }

    // ── Common Stats ──
    [JsonPropertyName("registrationDate")] public long RegistrationDate { get; set; }
    [JsonPropertyName("lastSessionDate")] public long LastSessionDate { get; set; }
    [JsonPropertyName("survivorClass")] public string SurvivorClass { get; set; } = "";
    [JsonPropertyName("killExperience")] public int KillExperience { get; set; }
    [JsonPropertyName("lootingExperience")] public int LootingExperience { get; set; }
    [JsonPropertyName("healingExperience")] public int HealingExperience { get; set; }
    [JsonPropertyName("survivalExperience")] public int SurvivalExperience { get; set; }
    [JsonPropertyName("stashValue")] public double StashValue { get; set; }

    // ── Health & Physical Condition ──
    [JsonPropertyName("bloodLost")] public int BloodLost { get; set; }
    [JsonPropertyName("limbsLost")] public int LimbsLost { get; set; }
    [JsonPropertyName("leastDamagedArea")] public string LeastDamagedArea { get; set; } = "";
    [JsonPropertyName("hpHealed")] public double HpHealed { get; set; }
    [JsonPropertyName("fractures")] public int Fractures { get; set; }
    [JsonPropertyName("concussions")] public int Concussions { get; set; }
    [JsonPropertyName("dehydrations")] public int Dehydrations { get; set; }
    [JsonPropertyName("exhaustions")] public int Exhaustions { get; set; }
    [JsonPropertyName("drinksConsumed")] public int DrinksConsumed { get; set; }
    [JsonPropertyName("foodConsumed")] public int FoodConsumed { get; set; }
    [JsonPropertyName("medicineUsed")] public int MedicineUsed { get; set; }

    // ── Loot / Items Found ──
    [JsonPropertyName("usdFound")] public int UsdFound { get; set; }
    [JsonPropertyName("eurFound")] public int EurFound { get; set; }
    [JsonPropertyName("rubFound")] public int RubFound { get; set; }
    [JsonPropertyName("bodiesLooted")] public int BodiesLooted { get; set; }
    [JsonPropertyName("placesLooted")] public int PlacesLooted { get; set; }
    [JsonPropertyName("safesUnlocked")] public int SafesUnlocked { get; set; }
    [JsonPropertyName("weaponsFound")] public int WeaponsFound { get; set; }
    [JsonPropertyName("modsFound")] public int ModsFound { get; set; }
    [JsonPropertyName("throwablesFound")] public int ThrowablesFound { get; set; }
    [JsonPropertyName("specialItemsFound")] public int SpecialItemsFound { get; set; }
    [JsonPropertyName("provisionsFound")] public int ProvisionsFound { get; set; }
    [JsonPropertyName("keysFound")] public int KeysFound { get; set; }
    [JsonPropertyName("barterGoodsFound")] public int BarterGoodsFound { get; set; }
    [JsonPropertyName("equipmentFound")] public int EquipmentFound { get; set; }

    // ── Combat ──
    [JsonPropertyName("damageAbsorbedByArmor")] public int DamageAbsorbedByArmor { get; set; }
    [JsonPropertyName("ammoUsed")] public int AmmoUsed { get; set; }
    [JsonPropertyName("hitCount")] public int HitCount { get; set; }
    [JsonPropertyName("fatalHits")] public int FatalHits { get; set; }
    [JsonPropertyName("overallAccuracy")] public double OverallAccuracy { get; set; }
    [JsonPropertyName("killedLevel010")] public int KilledLevel010 { get; set; }
    [JsonPropertyName("killedLevel1130")] public int KilledLevel1130 { get; set; }
    [JsonPropertyName("killedLevel3150")] public int KilledLevel3150 { get; set; }
    [JsonPropertyName("killedLevel5170")] public int KilledLevel5170 { get; set; }
    [JsonPropertyName("killedLevel7199")] public int KilledLevel7199 { get; set; }
    [JsonPropertyName("killedLevel100")] public int KilledLevel100 { get; set; }
    [JsonPropertyName("bearsKilled")] public int BearsKilled { get; set; }
    [JsonPropertyName("usecsKilled")] public int UsecsKilled { get; set; }
    [JsonPropertyName("scavsKilled")] public int ScavsKilled { get; set; }
    [JsonPropertyName("pmcsKilled")] public int PmcsKilled { get; set; }
    [JsonPropertyName("bossesKilled")] public int BossesKilled { get; set; }
    [JsonPropertyName("headshots")] public int Headshots { get; set; }
    [JsonPropertyName("longestShot")] public double LongestShot { get; set; }
}

// ── Profile Raid Stats ──

public record ProfileRaidStatsDto
{
    [JsonPropertyName("totalRaids")] public int TotalRaids { get; set; }
    [JsonPropertyName("survived")] public int Survived { get; set; }
    [JsonPropertyName("deaths")] public int Deaths { get; set; }
    [JsonPropertyName("survivalRate")] public double SurvivalRate { get; set; }
    [JsonPropertyName("totalKills")] public int TotalKills { get; set; }
    [JsonPropertyName("pmcKills")] public int PmcKills { get; set; }
    [JsonPropertyName("scavKills")] public int ScavKills { get; set; }
    [JsonPropertyName("bossKills")] public int BossKills { get; set; }
    [JsonPropertyName("headshots")] public int Headshots { get; set; }
    [JsonPropertyName("kdRatio")] public double KdRatio { get; set; }
}

// ── Stash ──

public record PlayerStashDto
{
    [JsonPropertyName("totalItems")]
    public int TotalItems { get; set; }

    [JsonPropertyName("stashValue")]
    public double StashValue { get; set; }

    [JsonPropertyName("items")]
    public List<PlayerStashItemDto> Items { get; set; } = [];

    [JsonPropertyName("limit")]
    public int Limit { get; set; }

    [JsonPropertyName("offset")]
    public int Offset { get; set; }
}

public record PlayerStashItemDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("tpl")]
    public string Tpl { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("count")]
    public int Count { get; set; }
}

// ── Requests ──

public record PlayerMailRequest
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("items")]
    public List<GiveRequestItem> Items { get; set; } = [];

    [JsonPropertyName("roubles")]
    public int Roubles { get; set; }

    [JsonPropertyName("dollars")]
    public int Dollars { get; set; }

    [JsonPropertyName("euros")]
    public int Euros { get; set; }
}

public record BroadcastMailRequest
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("items")]
    public List<GiveRequestItem> Items { get; set; } = [];

    [JsonPropertyName("roubles")]
    public int Roubles { get; set; }

    [JsonPropertyName("dollars")]
    public int Dollars { get; set; }

    [JsonPropertyName("euros")]
    public int Euros { get; set; }
}

public record PlayerGiveRequest
{
    [JsonPropertyName("items")]
    public List<GiveRequestItem> Items { get; set; } = [];
}

public record PlayerGiveAllRequest
{
    [JsonPropertyName("items")]
    public List<GiveRequestItem> Items { get; set; } = [];
}

public record PlayerResetRequest
{
    [JsonPropertyName("categories")]
    public List<string> Categories { get; set; } = [];
}

public record PlayerModifyRequest
{
    [JsonPropertyName("level")]
    public int? Level { get; set; }

    [JsonPropertyName("experience")]
    public int? Experience { get; set; }

    [JsonPropertyName("faction")]
    public string? Faction { get; set; }

    [JsonPropertyName("roubles")]
    public int? Roubles { get; set; }

    [JsonPropertyName("dollars")]
    public int? Dollars { get; set; }

    [JsonPropertyName("euros")]
    public int? Euros { get; set; }

    [JsonPropertyName("traderStandings")]
    public Dictionary<string, double>? TraderStandings { get; set; }
}

public record BanRequest
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = "";

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = "";
}

public record UnbanRequest
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = "";
}

// ── Responses ──

public record PlayerActionResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

public record BanEntry
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = "";

    [JsonPropertyName("nickname")]
    public string Nickname { get; set; } = "";

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = "";

    [JsonPropertyName("bannedAt")]
    public DateTime BannedAt { get; set; }
}

public record BanListResponse
{
    [JsonPropertyName("entries")]
    public List<BanEntry> Entries { get; set; } = [];
}

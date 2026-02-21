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

    [JsonPropertyName("pmcKills")]
    public int PmcKills { get; set; }

    [JsonPropertyName("scavKills")]
    public int ScavKills { get; set; }

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

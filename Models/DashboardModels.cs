using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZSlayerCommandCenter.Models;

// ── Server Status ──

public record ServerStatusDto
{
    [JsonPropertyName("uptime")]
    public string Uptime { get; set; } = "";

    [JsonPropertyName("uptimeSeconds")]
    public long UptimeSeconds { get; set; }

    [JsonPropertyName("sptVersion")]
    public string SptVersion { get; set; } = "";

    [JsonPropertyName("ccVersion")]
    public string CcVersion { get; set; } = "";

    [JsonPropertyName("modCount")]
    public int ModCount { get; set; }

    [JsonPropertyName("mods")]
    public List<ModInfoDto> Mods { get; set; } = [];

    [JsonPropertyName("memoryMb")]
    public long MemoryMb { get; set; }

    [JsonPropertyName("workingSetMb")]
    public long WorkingSetMb { get; set; }
}

public record ModInfoDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    [JsonPropertyName("author")]
    public string Author { get; set; } = "";
}

// ── Players ──

public record PlayerOverviewDto
{
    [JsonPropertyName("totalProfiles")]
    public int TotalProfiles { get; set; }

    [JsonPropertyName("onlineCount")]
    public int OnlineCount { get; set; }

    [JsonPropertyName("players")]
    public List<PlayerSummaryDto> Players { get; set; } = [];
}

public record PlayerSummaryDto
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = "";

    [JsonPropertyName("nickname")]
    public string Nickname { get; set; } = "";

    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("side")]
    public string Side { get; set; } = "";

    [JsonPropertyName("online")]
    public bool Online { get; set; }

    [JsonPropertyName("roubles")]
    public long Roubles { get; set; }
}

// ── Economy ──

public record EconomyDto
{
    [JsonPropertyName("totalRoubles")]
    public long TotalRoubles { get; set; }

    [JsonPropertyName("averageWealth")]
    public long AverageWealth { get; set; }

    [JsonPropertyName("topPlayers")]
    public List<PlayerSummaryDto> TopPlayers { get; set; } = [];
}

// ── Raids ──

public record RaidEndRecord
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = "";

    [JsonPropertyName("nickname")]
    public string Nickname { get; set; } = "";

    [JsonPropertyName("map")]
    public string Map { get; set; } = "";

    [JsonPropertyName("result")]
    public string Result { get; set; } = "";

    [JsonPropertyName("playTimeSeconds")]
    public int PlayTimeSeconds { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public record RaidStatsDto
{
    [JsonPropertyName("totalRaids")]
    public int TotalRaids { get; set; }

    [JsonPropertyName("raidsByMap")]
    public Dictionary<string, int> RaidsByMap { get; set; } = new();

    [JsonPropertyName("survivalRate")]
    public double SurvivalRate { get; set; }

    [JsonPropertyName("avgPlayTimeSeconds")]
    public int AvgPlayTimeSeconds { get; set; }

    [JsonPropertyName("recentRaids")]
    public List<RaidEndRecord> RecentRaids { get; set; } = [];
}

// ── Activity Log ──

public record ActivityEntry
{
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("adminName")]
    public string AdminName { get; set; } = "";

    [JsonPropertyName("details")]
    public string Details { get; set; } = "";
}

public record ActivityResponse
{
    [JsonPropertyName("entries")]
    public List<ActivityEntry> Entries { get; set; } = [];

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("limit")]
    public int Limit { get; set; }

    [JsonPropertyName("offset")]
    public int Offset { get; set; }
}

// ── Console ──

public record ConsoleEntry
{
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("level")]
    public string Level { get; set; } = "info";

    [JsonPropertyName("source")]
    public string Source { get; set; } = "";

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";
}

public record ConsoleResponse
{
    [JsonPropertyName("entries")]
    public List<ConsoleEntry> Entries { get; set; } = [];

    [JsonPropertyName("since")]
    public DateTime Since { get; set; }

    [JsonPropertyName("configured")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Configured { get; set; }
}

// ── Dashboard Config (returned to UI) ──

public record DashboardConfigDto
{
    [JsonPropertyName("refreshIntervalSeconds")]
    public int RefreshIntervalSeconds { get; set; }

    [JsonPropertyName("consolePollingMs")]
    public int ConsolePollingMs { get; set; }

    [JsonPropertyName("headlessLogPath")]
    public string HeadlessLogPath { get; set; } = "";
}

// ── Broadcast ──

public record BroadcastRequest
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = "";
}

public record BroadcastResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("recipientCount")]
    public int RecipientCount { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

// ── Headless Process ──

public record HeadlessStatusDto
{
    [JsonPropertyName("available")]
    public bool Available { get; set; }

    [JsonPropertyName("running")]
    public bool Running { get; set; }

    [JsonPropertyName("pid")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Pid { get; set; }

    [JsonPropertyName("uptime")]
    public string Uptime { get; set; } = "";

    [JsonPropertyName("uptimeSeconds")]
    public long UptimeSeconds { get; set; }

    [JsonPropertyName("restartCount")]
    public int RestartCount { get; set; }

    [JsonPropertyName("lastCrashReason")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LastCrashReason { get; set; }

    [JsonPropertyName("autoStart")]
    public bool AutoStart { get; set; }

    [JsonPropertyName("autoStartDelaySec")]
    public int AutoStartDelaySec { get; set; }

    [JsonPropertyName("autoRestart")]
    public bool AutoRestart { get; set; }

    [JsonPropertyName("profileId")]
    public string ProfileId { get; set; } = "";

    [JsonPropertyName("exePath")]
    public string ExePath { get; set; } = "";
}

public record HeadlessConfigUpdateRequest
{
    [JsonPropertyName("autoStart")]
    public bool? AutoStart { get; set; }

    [JsonPropertyName("autoStartDelaySec")]
    public int? AutoStartDelaySec { get; set; }

    [JsonPropertyName("autoRestart")]
    public bool? AutoRestart { get; set; }

    [JsonPropertyName("profileId")]
    public string? ProfileId { get; set; }
}

// ── Action Types ──

public static class ActionType
{
    public const string ItemGive = "ItemGive";
    public const string PresetGive = "PresetGive";
    public const string Broadcast = "Broadcast";
    public const string ConfigChange = "ConfigChange";
    public const string ServerStart = "ServerStart";
    public const string PlayerMail = "PlayerMail";
    public const string PlayerGive = "PlayerGive";
    public const string PlayerReset = "PlayerReset";
    public const string PlayerModify = "PlayerModify";
    public const string PlayerBan = "PlayerBan";
    public const string PlayerUnban = "PlayerUnban";
    public const string PlayerGiveAll = "PlayerGiveAll";
    public const string BroadcastMail = "BroadcastMail";
}

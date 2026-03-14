using System.Text.Json.Serialization;

namespace ZSlayerCommandCenter.Models;

// ═══════════════════════════════════════════════════════
// CONFIG (persisted to config.json under "gameValues")
// ═══════════════════════════════════════════════════════

public record LocationOverride
{
    [JsonPropertyName("escapeTimeLimit")] public double? EscapeTimeLimit { get; set; }
    [JsonPropertyName("globalLootChanceModifier")] public double? GlobalLootChanceModifier { get; set; }
    [JsonPropertyName("globalContainerChanceModifier")] public double? GlobalContainerChanceModifier { get; set; }
    [JsonPropertyName("botMax")] public int? BotMax { get; set; }
    [JsonPropertyName("botMaxPlayer")] public int? BotMaxPlayer { get; set; }
    [JsonPropertyName("bossOverrides")] public Dictionary<string, BossOverride> BossOverrides { get; set; } = new();
    [JsonPropertyName("exitOverrides")] public Dictionary<string, ExitOverride> ExitOverrides { get; set; } = new();
}

public record BossOverride
{
    [JsonPropertyName("bossChance")] public double? BossChance { get; set; }
    [JsonPropertyName("bossEscortAmount")] public string? BossEscortAmount { get; set; }
    [JsonPropertyName("time")] public double? Time { get; set; }
}

public record ExitOverride
{
    [JsonPropertyName("chance")] public double? Chance { get; set; }
    [JsonPropertyName("exfiltrationTime")] public double? ExfiltrationTime { get; set; }
    [JsonPropertyName("chancePVE")] public double? ChancePVE { get; set; }
}

public record WeatherOverrideConfig
{
    [JsonPropertyName("acceleration")] public double? Acceleration { get; set; }
    [JsonPropertyName("seasonOverride")] public int? SeasonOverride { get; set; }
}

public record GlobalRaidSettingsConfig
{
    [JsonPropertyName("scavCooldownSeconds")] public int? ScavCooldownSeconds { get; set; }
    [JsonPropertyName("playerScavHostileChancePercent")] public double? PlayerScavHostileChancePercent { get; set; }
    [JsonPropertyName("carExtractBaseStandingGain")] public double? CarExtractBaseStandingGain { get; set; }
    [JsonPropertyName("coopExtractBaseStandingGain")] public double? CoopExtractBaseStandingGain { get; set; }
    [JsonPropertyName("scavExtractStandingGain")] public double? ScavExtractStandingGain { get; set; }
}

// ═══════════════════════════════════════════════════════
// API DTOs — LOCATIONS
// ═══════════════════════════════════════════════════════

public record DetectedModDto
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("guid")] public string Guid { get; set; } = "";
    [JsonPropertyName("version")] public string Version { get; set; } = "";
    [JsonPropertyName("webUiPath")] public string WebUiPath { get; set; } = "";
    [JsonPropertyName("isDetected")] public bool IsDetected { get; set; }
}

public record LocationListResponse
{
    [JsonPropertyName("locations")] public List<LocationSummaryDto> Locations { get; set; } = [];
    [JsonPropertyName("detectedMods")] public List<DetectedModDto> DetectedMods { get; set; } = [];
    [JsonPropertyName("weather")] public WeatherDto Weather { get; set; } = new();
    [JsonPropertyName("globalRaidSettings")] public GlobalRaidSettingsDto GlobalRaidSettings { get; set; } = new();
    [JsonPropertyName("totalModified")] public int TotalModified { get; set; }
}

public record GlobalRaidSettingsDto
{
    [JsonPropertyName("scavCooldownSeconds")] public int ScavCooldownSeconds { get; set; }
    [JsonPropertyName("playerScavHostileChancePercent")] public double PlayerScavHostileChancePercent { get; set; }
    [JsonPropertyName("carExtractBaseStandingGain")] public double CarExtractBaseStandingGain { get; set; }
    [JsonPropertyName("coopExtractBaseStandingGain")] public double CoopExtractBaseStandingGain { get; set; }
    [JsonPropertyName("scavExtractStandingGain")] public double ScavExtractStandingGain { get; set; }
    [JsonPropertyName("originalScavCooldownSeconds")] public int OriginalScavCooldownSeconds { get; set; }
    [JsonPropertyName("originalPlayerScavHostileChancePercent")] public double OriginalPlayerScavHostileChancePercent { get; set; }
    [JsonPropertyName("originalCarExtractBaseStandingGain")] public double OriginalCarExtractBaseStandingGain { get; set; }
    [JsonPropertyName("originalCoopExtractBaseStandingGain")] public double OriginalCoopExtractBaseStandingGain { get; set; }
    [JsonPropertyName("originalScavExtractStandingGain")] public double OriginalScavExtractStandingGain { get; set; }
    [JsonPropertyName("isModified")] public bool IsModified { get; set; }
}

public record LocationSummaryDto
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("displayName")] public string DisplayName { get; set; } = "";
    [JsonPropertyName("escapeTimeLimit")] public double EscapeTimeLimit { get; set; }
    [JsonPropertyName("globalLootChance")] public double GlobalLootChance { get; set; }
    [JsonPropertyName("globalContainerChance")] public double GlobalContainerChance { get; set; }
    [JsonPropertyName("botMax")] public int BotMax { get; set; }
    [JsonPropertyName("bossCount")] public int BossCount { get; set; }
    [JsonPropertyName("exitCount")] public int ExitCount { get; set; }
    [JsonPropertyName("isModified")] public bool IsModified { get; set; }
    [JsonPropertyName("mapThumbnail")] public string MapThumbnail { get; set; } = "";
}

public record LocationDetailDto
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("displayName")] public string DisplayName { get; set; } = "";
    [JsonPropertyName("escapeTimeLimit")] public double EscapeTimeLimit { get; set; }
    [JsonPropertyName("globalLootChance")] public double GlobalLootChance { get; set; }
    [JsonPropertyName("globalContainerChance")] public double GlobalContainerChance { get; set; }
    [JsonPropertyName("botMax")] public int BotMax { get; set; }
    [JsonPropertyName("botMaxPlayer")] public int? BotMaxPlayer { get; set; }
    [JsonPropertyName("isModified")] public bool IsModified { get; set; }
    [JsonPropertyName("mapThumbnail")] public string MapThumbnail { get; set; } = "";
    [JsonPropertyName("bosses")] public List<BossDto> Bosses { get; set; } = [];
    [JsonPropertyName("exits")] public List<ExitDto> Exits { get; set; } = [];
    [JsonPropertyName("original")] public LocationOriginalValues Original { get; set; } = new();
}

public record LocationOriginalValues
{
    [JsonPropertyName("escapeTimeLimit")] public double EscapeTimeLimit { get; set; }
    [JsonPropertyName("globalLootChance")] public double GlobalLootChance { get; set; }
    [JsonPropertyName("globalContainerChance")] public double GlobalContainerChance { get; set; }
    [JsonPropertyName("botMax")] public int BotMax { get; set; }
    [JsonPropertyName("botMaxPlayer")] public int? BotMaxPlayer { get; set; }
}

public record BossDto
{
    [JsonPropertyName("bossName")] public string BossName { get; set; } = "";
    [JsonPropertyName("displayName")] public string DisplayName { get; set; } = "";
    [JsonPropertyName("bossChance")] public double? BossChance { get; set; }
    [JsonPropertyName("bossZone")] public string BossZone { get; set; } = "";
    [JsonPropertyName("bossEscortAmount")] public string BossEscortAmount { get; set; } = "";
    [JsonPropertyName("time")] public double? Time { get; set; }
    [JsonPropertyName("originalChance")] public double? OriginalChance { get; set; }
    [JsonPropertyName("originalEscortAmount")] public string OriginalEscortAmount { get; set; } = "";
    [JsonPropertyName("originalTime")] public double? OriginalTime { get; set; }
    [JsonPropertyName("isModified")] public bool IsModified { get; set; }
    [JsonPropertyName("index")] public int Index { get; set; }
}

public record ExitDto
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("chance")] public double Chance { get; set; }
    [JsonPropertyName("exfiltrationTime")] public double ExfiltrationTime { get; set; }
    [JsonPropertyName("chancePVE")] public double ChancePVE { get; set; }
    [JsonPropertyName("passageRequirement")] public string PassageRequirement { get; set; } = "";
    [JsonPropertyName("originalChance")] public double OriginalChance { get; set; }
    [JsonPropertyName("originalExfiltrationTime")] public double OriginalExfiltrationTime { get; set; }
    [JsonPropertyName("originalChancePVE")] public double OriginalChancePVE { get; set; }
    [JsonPropertyName("isModified")] public bool IsModified { get; set; }
    [JsonPropertyName("count")] public int? Count { get; set; }
}

public record WeatherDto
{
    [JsonPropertyName("acceleration")] public double? Acceleration { get; set; }
    [JsonPropertyName("seasonOverride")] public int? SeasonOverride { get; set; }
    [JsonPropertyName("originalAcceleration")] public double? OriginalAcceleration { get; set; }
    [JsonPropertyName("seasonNames")] public Dictionary<int, string> SeasonNames { get; set; } = new();
    [JsonPropertyName("isModified")] public bool IsModified { get; set; }
}

// ═══════════════════════════════════════════════════════
// API REQUEST/RESPONSE
// ═══════════════════════════════════════════════════════

public record LocationUpdateRequest
{
    [JsonPropertyName("escapeTimeLimit")] public double? EscapeTimeLimit { get; set; }
    [JsonPropertyName("globalLootChanceModifier")] public double? GlobalLootChanceModifier { get; set; }
    [JsonPropertyName("globalContainerChanceModifier")] public double? GlobalContainerChanceModifier { get; set; }
    [JsonPropertyName("botMax")] public int? BotMax { get; set; }
    [JsonPropertyName("botMaxPlayer")] public int? BotMaxPlayer { get; set; }
    [JsonPropertyName("bossOverrides")] public Dictionary<string, BossOverride>? BossOverrides { get; set; }
    [JsonPropertyName("exitOverrides")] public Dictionary<string, ExitOverride>? ExitOverrides { get; set; }
}

public record WeatherUpdateRequest
{
    [JsonPropertyName("acceleration")] public double? Acceleration { get; set; }
    [JsonPropertyName("seasonOverride")] public int? SeasonOverride { get; set; }
}

public record LocationApplyResult
{
    [JsonPropertyName("success")] public bool Success { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
    [JsonPropertyName("locationsModified")] public int LocationsModified { get; set; }
}

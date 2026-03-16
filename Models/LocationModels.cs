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
    // Pass 4 — Map Controls
    [JsonPropertyName("enabled")] public bool? Enabled { get; set; }
    [JsonPropertyName("insurance")] public bool? Insurance { get; set; }
    [JsonPropertyName("disabledForScav")] public bool? DisabledForScav { get; set; }
    [JsonPropertyName("airdropOverride")] public AirdropOverride? AirdropOverride { get; set; }
    // Pass 5 — Bot Difficulty
    [JsonPropertyName("botEasy")] public int? BotEasy { get; set; }
    [JsonPropertyName("botNormal")] public int? BotNormal { get; set; }
    [JsonPropertyName("botHard")] public int? BotHard { get; set; }
    [JsonPropertyName("botImpossible")] public int? BotImpossible { get; set; }
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

public record AirdropOverride
{
    [JsonPropertyName("planeAirdropChance")] public double? PlaneAirdropChance { get; set; }
    [JsonPropertyName("cooldownMin")] public int? CooldownMin { get; set; }
    [JsonPropertyName("cooldownMax")] public int? CooldownMax { get; set; }
    [JsonPropertyName("startMin")] public int? StartMin { get; set; }
    [JsonPropertyName("startMax")] public int? StartMax { get; set; }
    [JsonPropertyName("end")] public int? End { get; set; }
    [JsonPropertyName("max")] public int? Max { get; set; }
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
    // Raid behavior
    [JsonPropertyName("keepFiRSecureContainerOnDeath")] public bool? KeepFiRSecureContainerOnDeath { get; set; }
    [JsonPropertyName("alwaysKeepFoundInRaidOnRaidEnd")] public bool? AlwaysKeepFoundInRaidOnRaidEnd { get; set; }
    // Raid menu defaults
    [JsonPropertyName("raidMenuAiAmount")] public string? RaidMenuAiAmount { get; set; }
    [JsonPropertyName("raidMenuAiDifficulty")] public string? RaidMenuAiDifficulty { get; set; }
    [JsonPropertyName("raidMenuBossEnabled")] public bool? RaidMenuBossEnabled { get; set; }
    [JsonPropertyName("raidMenuScavWars")] public bool? RaidMenuScavWars { get; set; }
    [JsonPropertyName("raidMenuTaggedAndCursed")] public bool? RaidMenuTaggedAndCursed { get; set; }
    [JsonPropertyName("raidMenuEnablePve")] public bool? RaidMenuEnablePve { get; set; }
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
    [JsonPropertyName("presets")] public List<LocationPresetInfo> Presets { get; set; } = [];
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
    // Raid behavior
    [JsonPropertyName("keepFiRSecureContainerOnDeath")] public bool KeepFiRSecureContainerOnDeath { get; set; }
    [JsonPropertyName("alwaysKeepFoundInRaidOnRaidEnd")] public bool AlwaysKeepFoundInRaidOnRaidEnd { get; set; }
    [JsonPropertyName("originalKeepFiRSecureContainerOnDeath")] public bool OriginalKeepFiRSecureContainerOnDeath { get; set; }
    [JsonPropertyName("originalAlwaysKeepFoundInRaidOnRaidEnd")] public bool OriginalAlwaysKeepFoundInRaidOnRaidEnd { get; set; }
    // Raid menu defaults
    [JsonPropertyName("raidMenuAiAmount")] public string RaidMenuAiAmount { get; set; } = "";
    [JsonPropertyName("raidMenuAiDifficulty")] public string RaidMenuAiDifficulty { get; set; } = "";
    [JsonPropertyName("raidMenuBossEnabled")] public bool RaidMenuBossEnabled { get; set; }
    [JsonPropertyName("raidMenuScavWars")] public bool RaidMenuScavWars { get; set; }
    [JsonPropertyName("raidMenuTaggedAndCursed")] public bool RaidMenuTaggedAndCursed { get; set; }
    [JsonPropertyName("raidMenuEnablePve")] public bool RaidMenuEnablePve { get; set; }
    [JsonPropertyName("originalRaidMenuAiAmount")] public string OriginalRaidMenuAiAmount { get; set; } = "";
    [JsonPropertyName("originalRaidMenuAiDifficulty")] public string OriginalRaidMenuAiDifficulty { get; set; } = "";
    [JsonPropertyName("originalRaidMenuBossEnabled")] public bool OriginalRaidMenuBossEnabled { get; set; }
    [JsonPropertyName("originalRaidMenuScavWars")] public bool OriginalRaidMenuScavWars { get; set; }
    [JsonPropertyName("originalRaidMenuTaggedAndCursed")] public bool OriginalRaidMenuTaggedAndCursed { get; set; }
    [JsonPropertyName("originalRaidMenuEnablePve")] public bool OriginalRaidMenuEnablePve { get; set; }
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
    // Pass 4
    [JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;
    [JsonPropertyName("insurance")] public bool Insurance { get; set; }
    [JsonPropertyName("disabledForScav")] public bool? DisabledForScav { get; set; }
    [JsonPropertyName("hasAirdrops")] public bool HasAirdrops { get; set; }
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
    // Pass 4
    [JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;
    [JsonPropertyName("insurance")] public bool Insurance { get; set; }
    [JsonPropertyName("disabledForScav")] public bool? DisabledForScav { get; set; }
    [JsonPropertyName("airdrop")] public AirdropDto? Airdrop { get; set; }
    // Pass 5
    [JsonPropertyName("botEasy")] public int? BotEasy { get; set; }
    [JsonPropertyName("botNormal")] public int? BotNormal { get; set; }
    [JsonPropertyName("botHard")] public int? BotHard { get; set; }
    [JsonPropertyName("botImpossible")] public int? BotImpossible { get; set; }
}

public record LocationOriginalValues
{
    [JsonPropertyName("escapeTimeLimit")] public double EscapeTimeLimit { get; set; }
    [JsonPropertyName("globalLootChance")] public double GlobalLootChance { get; set; }
    [JsonPropertyName("globalContainerChance")] public double GlobalContainerChance { get; set; }
    [JsonPropertyName("botMax")] public int BotMax { get; set; }
    [JsonPropertyName("botMaxPlayer")] public int? BotMaxPlayer { get; set; }
    // Pass 4
    [JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;
    [JsonPropertyName("insurance")] public bool Insurance { get; set; }
    [JsonPropertyName("disabledForScav")] public bool? DisabledForScav { get; set; }
    // Pass 5
    [JsonPropertyName("botEasy")] public int? BotEasy { get; set; }
    [JsonPropertyName("botNormal")] public int? BotNormal { get; set; }
    [JsonPropertyName("botHard")] public int? BotHard { get; set; }
    [JsonPropertyName("botImpossible")] public int? BotImpossible { get; set; }
}

public record AirdropDto
{
    [JsonPropertyName("planeAirdropChance")] public double? PlaneAirdropChance { get; set; }
    [JsonPropertyName("cooldownMin")] public int? CooldownMin { get; set; }
    [JsonPropertyName("cooldownMax")] public int? CooldownMax { get; set; }
    [JsonPropertyName("startMin")] public int? StartMin { get; set; }
    [JsonPropertyName("startMax")] public int? StartMax { get; set; }
    [JsonPropertyName("end")] public int? End { get; set; }
    [JsonPropertyName("max")] public int? Max { get; set; }
    [JsonPropertyName("originalPlaneAirdropChance")] public double? OriginalPlaneAirdropChance { get; set; }
    [JsonPropertyName("originalCooldownMin")] public int? OriginalCooldownMin { get; set; }
    [JsonPropertyName("originalCooldownMax")] public int? OriginalCooldownMax { get; set; }
    [JsonPropertyName("originalStartMin")] public int? OriginalStartMin { get; set; }
    [JsonPropertyName("originalStartMax")] public int? OriginalStartMax { get; set; }
    [JsonPropertyName("originalEnd")] public int? OriginalEnd { get; set; }
    [JsonPropertyName("originalMax")] public int? OriginalMax { get; set; }
    [JsonPropertyName("isModified")] public bool IsModified { get; set; }
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
    // Pass 4
    [JsonPropertyName("enabled")] public bool? Enabled { get; set; }
    [JsonPropertyName("insurance")] public bool? Insurance { get; set; }
    [JsonPropertyName("disabledForScav")] public bool? DisabledForScav { get; set; }
    [JsonPropertyName("airdropOverride")] public AirdropOverride? AirdropOverride { get; set; }
    // Pass 5
    [JsonPropertyName("botEasy")] public int? BotEasy { get; set; }
    [JsonPropertyName("botNormal")] public int? BotNormal { get; set; }
    [JsonPropertyName("botHard")] public int? BotHard { get; set; }
    [JsonPropertyName("botImpossible")] public int? BotImpossible { get; set; }
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

public record LocationPresetInfo
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("description")] public string Description { get; set; } = "";
    [JsonPropertyName("isBuiltIn")] public bool IsBuiltIn { get; set; }
}

// ═══════════════════════════════════════════════════════
// Pass 5 — BULK UPDATE
// ═══════════════════════════════════════════════════════

public record LocationBulkUpdateRequest
{
    [JsonPropertyName("lootMultiplier")] public double? LootMultiplier { get; set; }
    [JsonPropertyName("containerMultiplier")] public double? ContainerMultiplier { get; set; }
    [JsonPropertyName("raidTimeMinutes")] public double? RaidTimeMinutes { get; set; }
    [JsonPropertyName("bossChancePercent")] public double? BossChancePercent { get; set; }
}

// ═══════════════════════════════════════════════════════
// Pass 7 — EXPORT/IMPORT + COMPARE + PRESET PREVIEW
// ═══════════════════════════════════════════════════════

public record LocationExportData
{
    [JsonPropertyName("version")] public string Version { get; set; } = "";
    [JsonPropertyName("exportedAt")] public DateTime ExportedAt { get; set; } = DateTime.UtcNow;
    [JsonPropertyName("locationOverrides")] public Dictionary<string, LocationOverride> LocationOverrides { get; set; } = new();
    [JsonPropertyName("weatherOverride")] public WeatherOverrideConfig WeatherOverride { get; set; } = new();
    [JsonPropertyName("globalRaidSettings")] public GlobalRaidSettingsConfig GlobalRaidSettings { get; set; } = new();
}

public record LocationPresetDiffResponse
{
    [JsonPropertyName("presetName")] public string PresetName { get; set; } = "";
    [JsonPropertyName("entries")] public List<LocationDiffEntry> Entries { get; set; } = [];
}

public record LocationDiffEntry
{
    [JsonPropertyName("mapId")] public string MapId { get; set; } = "";
    [JsonPropertyName("mapName")] public string MapName { get; set; } = "";
    [JsonPropertyName("field")] public string Field { get; set; } = "";
    [JsonPropertyName("currentValue")] public string CurrentValue { get; set; } = "";
    [JsonPropertyName("newValue")] public string NewValue { get; set; } = "";
}

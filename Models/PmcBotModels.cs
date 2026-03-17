using System.Text.Json.Serialization;

namespace ZSlayerCommandCenter.Models;

// ═══════════════════════════════════════════════════════
// PMC & BOT CONFIG (persisted to config.json)
// ═══════════════════════════════════════════════════════

public record PmcBotConfig
{
    // ── A. PMC Configuration (extends 10C raid rules PMC) ──
    [JsonPropertyName("enablePmcHostility")] public bool EnablePmcHostility { get; set; }
    [JsonPropertyName("crossFactionHostility")] public double? CrossFactionHostility { get; set; }
    [JsonPropertyName("sameFactionHostility")] public double? SameFactionHostility { get; set; }
    [JsonPropertyName("pmcNamePrefixChance")] public double? PmcNamePrefixChance { get; set; }
    [JsonPropertyName("lootableMelee")] public bool LootableMelee { get; set; }

    // ── B. Bot Equipment Durability ──
    [JsonPropertyName("enableBotDurability")] public bool EnableBotDurability { get; set; }
    [JsonPropertyName("botDurabilities")] public Dictionary<string, BotDurabilityEntry>? BotDurabilities { get; set; }

    // ── C. Scav Karma / Faction Behavior ──
    [JsonPropertyName("enableScavKarma")] public bool EnableScavKarma { get; set; }
    [JsonPropertyName("hostileBossesToScavs")] public bool? HostileBossesToScavs { get; set; }
}

public record BotDurabilityEntry
{
    [JsonPropertyName("armorMin")] public int? ArmorMin { get; set; }
    [JsonPropertyName("armorMax")] public int? ArmorMax { get; set; }
    [JsonPropertyName("weaponMin")] public int? WeaponMin { get; set; }
    [JsonPropertyName("weaponMax")] public int? WeaponMax { get; set; }
}

// ═══════════════════════════════════════════════════════
// RESPONSE DTOs
// ═══════════════════════════════════════════════════════

public record PmcBotConfigResponse
{
    [JsonPropertyName("config")] public PmcBotConfig Config { get; set; } = new();
    [JsonPropertyName("defaults")] public PmcBotDefaults Defaults { get; set; } = new();

    /// <summary>All PMC/Bot settings are server-side — no client restart needed.</summary>
    [JsonPropertyName("clientRestartFields")] public List<string> ClientRestartFields { get; set; } = [];
}

public record PmcBotDefaults
{
    // A. PMC
    [JsonPropertyName("crossFactionHostility")] public double CrossFactionHostility { get; set; }
    [JsonPropertyName("sameFactionHostility")] public double SameFactionHostility { get; set; }
    [JsonPropertyName("pmcNamePrefixChance")] public double PmcNamePrefixChance { get; set; }
    [JsonPropertyName("meleeItemCount")] public int MeleeItemCount { get; set; }

    // B. Bot Durability
    [JsonPropertyName("botTypes")] public List<BotTypeDefaults> BotTypes { get; set; } = [];

    // C. Scav Karma
    [JsonPropertyName("hostileBossesToScavs")] public bool HostileBossesToScavs { get; set; }
}

public record BotTypeDefaults
{
    [JsonPropertyName("key")] public string Key { get; set; } = "";
    [JsonPropertyName("label")] public string Label { get; set; } = "";
    [JsonPropertyName("armorMin")] public int ArmorMin { get; set; }
    [JsonPropertyName("armorMax")] public int ArmorMax { get; set; }
    [JsonPropertyName("weaponMin")] public int WeaponMin { get; set; }
    [JsonPropertyName("weaponMax")] public int WeaponMax { get; set; }
}

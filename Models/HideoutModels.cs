using System.Text.Json.Serialization;

namespace ZSlayerCommandCenter.Models;

// ═══════════════════════════════════════════════════════
// CONFIG (persisted to config.json under "hideout")
// ═══════════════════════════════════════════════════════

public record HideoutEditorConfig
{
    // ── Construction ──
    [JsonPropertyName("constructionTimeMult")] public double ConstructionTimeMult { get; set; } = 1.0;
    [JsonPropertyName("removeItemRequirements")] public bool RemoveItemRequirements { get; set; }
    [JsonPropertyName("removeFirRequirements")] public bool RemoveFirRequirements { get; set; }
    [JsonPropertyName("removeSkillRequirements")] public bool RemoveSkillRequirements { get; set; }
    [JsonPropertyName("removeTraderRequirements")] public bool RemoveTraderRequirements { get; set; }
    [JsonPropertyName("removeCustomizationRequirements")] public bool RemoveCustomizationRequirements { get; set; }

    // ── Production & Crafting ──
    [JsonPropertyName("productionSpeedMult")] public double ProductionSpeedMult { get; set; } = 1.0;
    [JsonPropertyName("scavCaseTimeMult")] public double ScavCaseTimeMult { get; set; } = 1.0;
    [JsonPropertyName("scavCasePriceMult")] public double ScavCasePriceMult { get; set; } = 1.0;
    [JsonPropertyName("cultistCircleTimeMult")] public double CultistCircleTimeMult { get; set; } = 1.0;
    [JsonPropertyName("cultistCircleMaxRewards")] public int? CultistCircleMaxRewards { get; set; }
    [JsonPropertyName("removeArenaCrafts")] public bool RemoveArenaCrafts { get; set; }

    // ── Fuel & Power ──
    [JsonPropertyName("fuelConsumptionMult")] public double FuelConsumptionMult { get; set; } = 1.0;
    [JsonPropertyName("generatorNoFuelMult")] public double GeneratorNoFuelMult { get; set; } = 1.0;
    [JsonPropertyName("airFilterMult")] public double AirFilterMult { get; set; } = 1.0;
    [JsonPropertyName("gpuBoostMult")] public double GpuBoostMult { get; set; } = 1.0;

    // ── Farming ──
    [JsonPropertyName("bitcoinTimeMinutes")] public int? BitcoinTimeMinutes { get; set; }
    [JsonPropertyName("maxBitcoins")] public int? MaxBitcoins { get; set; }
    [JsonPropertyName("waterFilterTimeMinutes")] public int? WaterFilterTimeMinutes { get; set; }
    [JsonPropertyName("waterFilterRate")] public int? WaterFilterRate { get; set; }

    // ── Stash Size (client restart required) ──
    [JsonPropertyName("stashHeights")] public StashHeightsConfig StashHeights { get; set; } = new();

    // ── Health Regeneration ──
    [JsonPropertyName("healthRegenMult")] public double HealthRegenMult { get; set; } = 1.0;
    [JsonPropertyName("energyRegenRate")] public double? EnergyRegenRate { get; set; }
    [JsonPropertyName("hydrationRegenRate")] public double? HydrationRegenRate { get; set; }
    [JsonPropertyName("disableHideoutHealthRegen")] public bool DisableHideoutHealthRegen { get; set; }
    [JsonPropertyName("disableHideoutEnergyRegen")] public bool DisableHideoutEnergyRegen { get; set; }
    [JsonPropertyName("disableHideoutHydrationRegen")] public bool DisableHideoutHydrationRegen { get; set; }

    // ── G. Granular Multipliers ──
    [JsonPropertyName("perAreaConstructionMult")] public Dictionary<string, double> PerAreaConstructionMult { get; set; } = new();
    [JsonPropertyName("perStationProductionMult")] public Dictionary<string, double> PerStationProductionMult { get; set; } = new();
    [JsonPropertyName("constructionCostMult")] public double ConstructionCostMult { get; set; } = 1.0;

    // ── H. Recipe Lock Overrides ──
    [JsonPropertyName("recipeLockOverrides")] public Dictionary<string, bool> RecipeLockOverrides { get; set; } = new();

    // ── I. Bonus Value Overrides ──
    [JsonPropertyName("bonusValueOverrides")] public Dictionary<string, double> BonusValueOverrides { get; set; } = new();
}

public record StashHeightsConfig
{
    [JsonPropertyName("standard")] public int? Standard { get; set; }
    [JsonPropertyName("leftBehind")] public int? LeftBehind { get; set; }
    [JsonPropertyName("prepareForEscape")] public int? PrepareForEscape { get; set; }
    [JsonPropertyName("edgeOfDarkness")] public int? EdgeOfDarkness { get; set; }
    [JsonPropertyName("unheardEdition")] public int? UnheardEdition { get; set; }
}

// ═══════════════════════════════════════════════════════
// DTOs (sent to frontend)
// ═══════════════════════════════════════════════════════

public record HideoutEditorConfigResponse
{
    [JsonPropertyName("config")] public HideoutEditorConfig Config { get; set; } = new();
    [JsonPropertyName("defaults")] public HideoutDefaults Defaults { get; set; } = new();
}

public record HideoutDefaults
{
    // ── Fuel & Power originals ──
    [JsonPropertyName("fuelFlowRate")] public double FuelFlowRate { get; set; }
    [JsonPropertyName("generatorSpeedWithoutFuel")] public double GeneratorSpeedWithoutFuel { get; set; }
    [JsonPropertyName("airFilterFlowRate")] public double AirFilterFlowRate { get; set; }
    [JsonPropertyName("gpuBoostRate")] public double GpuBoostRate { get; set; }

    // ── Farming originals ──
    [JsonPropertyName("bitcoinTimeMinutes")] public int BitcoinTimeMinutes { get; set; }
    [JsonPropertyName("maxBitcoins")] public int MaxBitcoins { get; set; }
    [JsonPropertyName("waterFilterTimeMinutes")] public int WaterFilterTimeMinutes { get; set; }
    [JsonPropertyName("waterFilterRate")] public int WaterFilterRate { get; set; }

    // ── Cultist circle originals ──
    [JsonPropertyName("cultistCircleMaxRewards")] public int CultistCircleMaxRewards { get; set; }

    // ── Stash originals ──
    [JsonPropertyName("stashHeights")] public StashHeightsConfig StashHeights { get; set; } = new();

    // ── Health regen originals ──
    [JsonPropertyName("healthRegenValues")] public Dictionary<string, double> HealthRegenValues { get; set; } = new();
    [JsonPropertyName("energyRegenRate")] public double EnergyRegenRate { get; set; }
    [JsonPropertyName("hydrationRegenRate")] public double HydrationRegenRate { get; set; }
}

// ═══════════════════════════════════════════════════════
// Discovery DTOs
// ═══════════════════════════════════════════════════════

public record HideoutAreaDto
{
    [JsonPropertyName("areaType")] public string AreaType { get; set; } = "";
    [JsonPropertyName("areaName")] public string AreaName { get; set; } = "";
    [JsonPropertyName("stageCount")] public int StageCount { get; set; }
    [JsonPropertyName("maxLevel")] public int MaxLevel { get; set; }
}

public record HideoutBonusDto
{
    [JsonPropertyName("areaType")] public string AreaType { get; set; } = "";
    [JsonPropertyName("areaName")] public string AreaName { get; set; } = "";
    [JsonPropertyName("stageKey")] public string StageKey { get; set; } = "";
    [JsonPropertyName("bonusIndex")] public int BonusIndex { get; set; }
    [JsonPropertyName("bonusType")] public string BonusType { get; set; } = "";
    [JsonPropertyName("originalValue")] public double OriginalValue { get; set; }
    [JsonPropertyName("currentMultiplier")] public double CurrentMultiplier { get; set; } = 1.0;
    [JsonPropertyName("key")] public string Key { get; set; } = "";
}

public record HideoutRecipeDto
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("stationAreaType")] public string StationAreaType { get; set; } = "";
    [JsonPropertyName("stationName")] public string StationName { get; set; } = "";
    [JsonPropertyName("productTpl")] public string ProductTpl { get; set; } = "";
    [JsonPropertyName("productName")] public string ProductName { get; set; } = "";
    [JsonPropertyName("productionTime")] public double ProductionTime { get; set; }
    [JsonPropertyName("originalTime")] public double OriginalTime { get; set; }
    [JsonPropertyName("locked")] public bool Locked { get; set; }
    [JsonPropertyName("originalLocked")] public bool OriginalLocked { get; set; }
    [JsonPropertyName("continuous")] public bool Continuous { get; set; }
    [JsonPropertyName("limitCount")] public int? LimitCount { get; set; }
}

// ═══════════════════════════════════════════════════════
// Preset DTOs
// ═══════════════════════════════════════════════════════

public record HideoutPreset
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("description")] public string Description { get; set; } = "";
    [JsonPropertyName("createdUtc")] public DateTime CreatedUtc { get; set; }
    [JsonPropertyName("config")] public HideoutEditorConfig Config { get; set; } = new();
}

public record HideoutPresetSummary
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("description")] public string Description { get; set; } = "";
    [JsonPropertyName("createdUtc")] public DateTime CreatedUtc { get; set; }
}

public record HideoutPresetListResponse
{
    [JsonPropertyName("presets")] public List<HideoutPresetSummary> Presets { get; set; } = [];
    [JsonPropertyName("activePreset")] public string? ActivePreset { get; set; }
}

// ═══════════════════════════════════════════════════════
// Per-player DTOs
// ═══════════════════════════════════════════════════════

public record PlayerHideoutAreaDto
{
    [JsonPropertyName("areaType")] public string AreaType { get; set; } = "";
    [JsonPropertyName("areaName")] public string AreaName { get; set; } = "";
    [JsonPropertyName("level")] public int Level { get; set; }
    [JsonPropertyName("maxLevel")] public int MaxLevel { get; set; }
}

public record PlayerHideoutResponse
{
    [JsonPropertyName("sessionId")] public string SessionId { get; set; } = "";
    [JsonPropertyName("profileName")] public string ProfileName { get; set; } = "";
    [JsonPropertyName("areas")] public List<PlayerHideoutAreaDto> Areas { get; set; } = [];
}

public record HideoutProgressionEntry
{
    [JsonPropertyName("sessionId")] public string SessionId { get; set; } = "";
    [JsonPropertyName("profileName")] public string ProfileName { get; set; } = "";
    [JsonPropertyName("level")] public int Level { get; set; }
    [JsonPropertyName("areas")] public Dictionary<string, int> Areas { get; set; } = new();
}

public record HideoutSetAreaRequest
{
    [JsonPropertyName("areaType")] public string AreaType { get; set; } = "";
    [JsonPropertyName("level")] public int Level { get; set; }
}

// ═══════════════════════════════════════════════════════
// Request DTOs
// ═══════════════════════════════════════════════════════

public record HideoutPresetSaveRequest
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("description")] public string? Description { get; set; }
}

public record HideoutPresetNameRequest
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
}

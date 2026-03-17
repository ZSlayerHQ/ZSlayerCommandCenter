using System.Text.Json.Serialization;

namespace ZSlayerCommandCenter.Models;

// ═══════════════════════════════════════════════════════
// CONFIG (persisted to config.json under "itemStacks")
// ═══════════════════════════════════════════════════════

public record ItemStackConfig
{
    // ── A. Ammo Stacks ──
    [JsonPropertyName("enableAmmoStacks")] public bool EnableAmmoStacks { get; set; }
    [JsonPropertyName("ammoStacks")] public AmmoStackConfig AmmoStacks { get; set; } = new();
    [JsonPropertyName("perItemStackOverrides")] public Dictionary<string, int> PerItemStackOverrides { get; set; } = new();

    // ── B. Currency Stacks ──
    [JsonPropertyName("enableCurrencyStacks")] public bool EnableCurrencyStacks { get; set; }
    [JsonPropertyName("currencyStacks")] public CurrencyStackConfig CurrencyStacks { get; set; } = new();

    // ── C. Global Item Properties ──
    [JsonPropertyName("weightless")] public bool Weightless { get; set; }
    [JsonPropertyName("weightMult")] public double WeightMult { get; set; } = 1.0;
    [JsonPropertyName("examineTimeOverride")] public double? ExamineTimeOverride { get; set; }
    [JsonPropertyName("autoExamineAll")] public bool AutoExamineAll { get; set; }
    [JsonPropertyName("autoExamineKeysOnly")] public bool AutoExamineKeysOnly { get; set; }
    [JsonPropertyName("handbookPriceMult")] public double HandbookPriceMult { get; set; } = 1.0;
    [JsonPropertyName("lootXpMult")] public double LootXpMult { get; set; } = 1.0;
    [JsonPropertyName("examineXpMult")] public double ExamineXpMult { get; set; } = 1.0;
    [JsonPropertyName("ammoLoadSpeedMult")] public double AmmoLoadSpeedMult { get; set; } = 1.0;
    [JsonPropertyName("removeRaidRestrictions")] public bool RemoveRaidRestrictions { get; set; }
    [JsonPropertyName("backpackStackingLimit")] public int? BackpackStackingLimit { get; set; }

    // ── D. Key & Keycard Durability ──
    [JsonPropertyName("enableKeyChanges")] public bool EnableKeyChanges { get; set; }
    [JsonPropertyName("infiniteKeys")] public bool InfiniteKeys { get; set; }
    [JsonPropertyName("keyUseMult")] public double KeyUseMult { get; set; } = 1.0;
    [JsonPropertyName("keyDurabilityThreshold")] public int KeyDurabilityThreshold { get; set; } = 100;
    [JsonPropertyName("excludeSingleUseKeys")] public bool ExcludeSingleUseKeys { get; set; } = true;
    [JsonPropertyName("excludeMarkedKeys")] public bool ExcludeMarkedKeys { get; set; } = true;
    [JsonPropertyName("infiniteKeycards")] public bool InfiniteKeycards { get; set; }
    [JsonPropertyName("keycardUseMult")] public double KeycardUseMult { get; set; } = 1.0;
    [JsonPropertyName("keycardDurabilityThreshold")] public int KeycardDurabilityThreshold { get; set; } = 100;
    [JsonPropertyName("excludeAccessKeycard")] public bool ExcludeAccessKeycard { get; set; } = true;
    [JsonPropertyName("excludeResidentialKeycard")] public bool ExcludeResidentialKeycard { get; set; }
    [JsonPropertyName("excludeSingleUseKeycards")] public bool ExcludeSingleUseKeycards { get; set; } = true;

    // ── E. Weapon Mechanics ──
    [JsonPropertyName("malfunctionMult")] public double MalfunctionMult { get; set; } = 1.0;
    [JsonPropertyName("misfireMult")] public double MisfireMult { get; set; } = 1.0;
    [JsonPropertyName("fragmentationMult")] public double FragmentationMult { get; set; } = 1.0;
    [JsonPropertyName("heatFactorMult")] public double HeatFactorMult { get; set; } = 1.0;
    [JsonPropertyName("disableOverheat")] public bool DisableOverheat { get; set; }

    // ── F. Gear & Restriction Toggles ──
    [JsonPropertyName("removeGearPenalties")] public bool RemoveGearPenalties { get; set; }
    [JsonPropertyName("removeBackpackRestrictions")] public bool RemoveBackpackRestrictions { get; set; }
    [JsonPropertyName("removeSecureContainerFilters")] public bool RemoveSecureContainerFilters { get; set; }
    [JsonPropertyName("allowRigWithArmor")] public bool AllowRigWithArmor { get; set; }
    [JsonPropertyName("removeDiscardLimits")] public bool RemoveDiscardLimits { get; set; }

    // ── G. Secure Container Sizes ──
    [JsonPropertyName("enableSecureContainerSizes")] public bool EnableSecureContainerSizes { get; set; }
    [JsonPropertyName("secureContainerSizes")] public Dictionary<string, ContainerSizeConfig> SecureContainerSizes { get; set; } = new();

    // ── H. Case Sizes ──
    [JsonPropertyName("enableCaseSizes")] public bool EnableCaseSizes { get; set; }
    [JsonPropertyName("caseSizes")] public Dictionary<string, ContainerSizeConfig> CaseSizes { get; set; } = new();
}

public record AmmoStackConfig
{
    [JsonPropertyName("pistol")] public int Pistol { get; set; } = 50;
    [JsonPropertyName("rifle")] public int Rifle { get; set; } = 60;
    [JsonPropertyName("shotgun")] public int Shotgun { get; set; } = 20;
    [JsonPropertyName("marksman")] public int Marksman { get; set; } = 40;
    [JsonPropertyName("largeCaliber")] public int LargeCaliber { get; set; } = 10;
    [JsonPropertyName("other")] public int Other { get; set; } = 60;
}

public record CurrencyStackConfig
{
    [JsonPropertyName("roubles")] public int Roubles { get; set; } = 500_000;
    [JsonPropertyName("dollars")] public int Dollars { get; set; } = 50_000;
    [JsonPropertyName("euros")] public int Euros { get; set; } = 50_000;
    [JsonPropertyName("gpCoins")] public int GpCoins { get; set; } = 100;
}

public record ContainerSizeConfig
{
    [JsonPropertyName("w")] public int W { get; set; }
    [JsonPropertyName("h")] public int H { get; set; }
    [JsonPropertyName("removeFilter")] public bool RemoveFilter { get; set; }
}

// ═══════════════════════════════════════════════════════
// RESPONSE DTOs (sent to frontend)
// ═══════════════════════════════════════════════════════

public record ItemStackConfigResponse
{
    [JsonPropertyName("config")] public ItemStackConfig Config { get; set; } = new();
    [JsonPropertyName("defaults")] public ItemStackDefaults Defaults { get; set; } = new();
}

public record ItemStackDefaults
{
    // A. Ammo stack defaults (per category)
    [JsonPropertyName("ammoStacks")] public AmmoStackConfig AmmoStacks { get; set; } = new();
    [JsonPropertyName("ammoCategories")] public List<AmmoCategoryInfo> AmmoCategories { get; set; } = [];

    // B. Currency stack defaults
    [JsonPropertyName("currencyStacks")] public CurrencyStackConfig CurrencyStacks { get; set; } = new();

    // C. Global property defaults
    [JsonPropertyName("baseLoadTime")] public double BaseLoadTime { get; set; }
    [JsonPropertyName("baseUnloadTime")] public double BaseUnloadTime { get; set; }
    [JsonPropertyName("maxBackpackInserting")] public int MaxBackpackInserting { get; set; }
    [JsonPropertyName("raidRestrictionCount")] public int RaidRestrictionCount { get; set; }
    [JsonPropertyName("totalItemCount")] public int TotalItemCount { get; set; }
}

public record AmmoCategoryInfo
{
    [JsonPropertyName("category")] public string Category { get; set; } = "";
    [JsonPropertyName("label")] public string Label { get; set; } = "";
    [JsonPropertyName("count")] public int Count { get; set; }
    [JsonPropertyName("defaultStack")] public int DefaultStack { get; set; }
    [JsonPropertyName("examples")] public List<string> Examples { get; set; } = [];
}

// ═══════════════════════════════════════════════════════
// CONTAINER DTOs (for G/H — Pass 3)
// ═══════════════════════════════════════════════════════

public record ContainerDto
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("currentW")] public int CurrentW { get; set; }
    [JsonPropertyName("currentH")] public int CurrentH { get; set; }
    [JsonPropertyName("defaultW")] public int DefaultW { get; set; }
    [JsonPropertyName("defaultH")] public int DefaultH { get; set; }
}

public record ContainersResponse
{
    [JsonPropertyName("secureContainers")] public List<ContainerDto> SecureContainers { get; set; } = [];
    [JsonPropertyName("cases")] public List<ContainerDto> Cases { get; set; } = [];
}

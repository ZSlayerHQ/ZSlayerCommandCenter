using System.Text.Json.Serialization;

namespace ZSlayerCommandCenter.Models;

// ═══════════════════════════════════════════════════════════════════
//  CONFIG RECORDS (persisted to config.json under "progression")
// ═══════════════════════════════════════════════════════════════════

public record ProgressionConfig
{
    [JsonPropertyName("xp")]
    public XpConfig Xp { get; set; } = new();

    [JsonPropertyName("skills")]
    public SkillsConfig Skills { get; set; } = new();

    [JsonPropertyName("hideout")]
    public HideoutProgressionConfig Hideout { get; set; } = new();

    [JsonPropertyName("insurance")]
    public InsuranceQuickConfig Insurance { get; set; } = new();

    [JsonPropertyName("repair")]
    public RepairQuickConfig Repair { get; set; } = new();

    [JsonPropertyName("scav")]
    public ScavQuickConfig Scav { get; set; } = new();

    [JsonPropertyName("health")]
    public HealthQuickConfig Health { get; set; } = new();

    [JsonPropertyName("stamina")]
    public StaminaQuickConfig Stamina { get; set; } = new();

    [JsonPropertyName("traders")]
    public TraderQuickConfig Traders { get; set; } = new();

    [JsonPropertyName("loot")]
    public LootQuickConfig Loot { get; set; } = new();

    [JsonPropertyName("raid")]
    public RaidConfig Raid { get; set; } = new();

    [JsonPropertyName("airdrop")]
    public AirdropQuickConfig Airdrop { get; set; } = new();
}

public record XpConfig
{
    [JsonPropertyName("globalXpMultiplier")]
    public double GlobalXpMultiplier { get; set; } = 1.0;

    [JsonPropertyName("raidXpMultiplier")]
    public double RaidXpMultiplier { get; set; } = 1.0;

    [JsonPropertyName("questXpMultiplier")]
    public double QuestXpMultiplier { get; set; } = 1.0;

    [JsonPropertyName("craftXpMultiplier")]
    public double CraftXpMultiplier { get; set; } = 1.0;

    [JsonPropertyName("healXpMultiplier")]
    public double HealXpMultiplier { get; set; } = 1.0;

    [JsonPropertyName("examineXpMultiplier")]
    public double ExamineXpMultiplier { get; set; } = 1.0;
}

public record SkillsConfig
{
    [JsonPropertyName("globalSkillSpeedMultiplier")]
    public double GlobalSkillSpeedMultiplier { get; set; } = 1.0;

    [JsonPropertyName("skillFatigueMultiplier")]
    public double SkillFatigueMultiplier { get; set; } = 1.0;

    [JsonPropertyName("simultaneousStrengthEndurance")]
    public bool SimultaneousStrengthEndurance { get; set; }

    [JsonPropertyName("perSkillMultipliers")]
    public Dictionary<string, double> PerSkillMultipliers { get; set; } = new();
}

public record HideoutProgressionConfig
{
    [JsonPropertyName("buildTimeMultiplier")]
    public double BuildTimeMultiplier { get; set; } = 1.0;

    [JsonPropertyName("buildTimeOverrideSeconds")]
    public int? BuildTimeOverrideSeconds { get; set; }

    [JsonPropertyName("craftTimeMultiplier")]
    public double CraftTimeMultiplier { get; set; } = 1.0;

    [JsonPropertyName("craftTimeOverrideSeconds")]
    public int? CraftTimeOverrideSeconds { get; set; }

    [JsonPropertyName("fuelConsumptionMultiplier")]
    public double FuelConsumptionMultiplier { get; set; } = 1.0;
}

public record InsuranceQuickConfig
{
    [JsonPropertyName("costMultiplier")]
    public double CostMultiplier { get; set; } = 1.0;

    [JsonPropertyName("returnTimeMultiplier")]
    public double ReturnTimeMultiplier { get; set; } = 1.0;

    [JsonPropertyName("returnTimeOverrideHours")]
    public double? ReturnTimeOverrideHours { get; set; }

    [JsonPropertyName("returnChanceMultiplier")]
    public double ReturnChanceMultiplier { get; set; } = 1.0;
}

public record RepairQuickConfig
{
    [JsonPropertyName("costMultiplier")]
    public double CostMultiplier { get; set; } = 1.0;

    [JsonPropertyName("durabilityLossMultiplier")]
    public double DurabilityLossMultiplier { get; set; } = 1.0;
}

public record ScavQuickConfig
{
    [JsonPropertyName("cooldownSeconds")]
    public int? CooldownSeconds { get; set; }

    [JsonPropertyName("karmaGainMultiplier")]
    public double KarmaGainMultiplier { get; set; } = 1.0;

    [JsonPropertyName("karmaLossMultiplier")]
    public double KarmaLossMultiplier { get; set; } = 1.0;
}

public record HealthQuickConfig
{
    [JsonPropertyName("energyDrainMultiplier")]
    public double EnergyDrainMultiplier { get; set; } = 1.0;

    [JsonPropertyName("hydrationDrainMultiplier")]
    public double HydrationDrainMultiplier { get; set; } = 1.0;

    [JsonPropertyName("outOfRaidHealingSpeedMultiplier")]
    public double OutOfRaidHealingSpeedMultiplier { get; set; } = 1.0;

    [JsonPropertyName("instantOutOfRaidHealing")]
    public bool InstantOutOfRaidHealing { get; set; }
}

public record StaminaQuickConfig
{
    [JsonPropertyName("capacityMultiplier")]
    public double CapacityMultiplier { get; set; } = 1.0;

    [JsonPropertyName("recoveryMultiplier")]
    public double RecoveryMultiplier { get; set; } = 1.0;

    [JsonPropertyName("sprintDrainMultiplier")]
    public double SprintDrainMultiplier { get; set; } = 1.0;

    [JsonPropertyName("jumpCostMultiplier")]
    public double JumpCostMultiplier { get; set; } = 1.0;

    [JsonPropertyName("weightLimitMultiplier")]
    public double WeightLimitMultiplier { get; set; } = 1.0;
}

public record TraderQuickConfig
{
    [JsonPropertyName("globalLoyaltyRequirementMultiplier")]
    public double GlobalLoyaltyRequirementMultiplier { get; set; } = 1.0;

    [JsonPropertyName("fleaMarketLevel")]
    public int? FleaMarketLevel { get; set; }
}

public record LootQuickConfig
{
    [JsonPropertyName("looseLootMultiplier")]
    public double LooseLootMultiplier { get; set; } = 1.0;

    [JsonPropertyName("containerLootMultiplier")]
    public double ContainerLootMultiplier { get; set; } = 1.0;
}

public record RaidConfig
{
    [JsonPropertyName("raidTimeMultiplier")]
    public double RaidTimeMultiplier { get; set; } = 1.0;

    [JsonPropertyName("bossSpawnMultiplier")]
    public double BossSpawnMultiplier { get; set; } = 1.0;
}

public record AirdropQuickConfig
{
    [JsonPropertyName("airdropDuration")]
    public double? AirdropDuration { get; set; }

    [JsonPropertyName("flareWaitSeconds")]
    public double? FlareWaitSeconds { get; set; }

    [JsonPropertyName("planeSpeed")]
    public double? PlaneSpeed { get; set; }
}

// ═══════════════════════════════════════════════════════════════════
//  API RESPONSE / REQUEST MODELS
// ═══════════════════════════════════════════════════════════════════

public record ProgressionApplyResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("settingsModified")]
    public int SettingsModified { get; set; }

    [JsonPropertyName("applyTimeMs")]
    public int ApplyTimeMs { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

public record ProgressionStatusResponse
{
    [JsonPropertyName("config")]
    public ProgressionConfig Config { get; set; } = new();

    [JsonPropertyName("hasModifiers")]
    public bool HasModifiers { get; set; }

    [JsonPropertyName("settingsModified")]
    public int SettingsModified { get; set; }

    [JsonPropertyName("activePreset")]
    public string? ActivePreset { get; set; }
}

public record ProgressionPresetInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("isBuiltIn")]
    public bool IsBuiltIn { get; set; }
}

public record ProgressionPresetEntry
{
    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("config")]
    public ProgressionConfig Config { get; set; } = new();
}

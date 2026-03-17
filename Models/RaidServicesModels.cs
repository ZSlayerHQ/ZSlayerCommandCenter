using System.Text.Json.Serialization;

namespace ZSlayerCommandCenter.Models;

// ═══════════════════════════════════════════════════════
// RAID RULES CONFIG (persisted to config.json)
// ═══════════════════════════════════════════════════════

public record RaidRulesConfig
{
    // ── A. Raid Outcome Modifiers ──
    [JsonPropertyName("noRunThrough")] public bool NoRunThrough { get; set; }
    [JsonPropertyName("saveQuestItemsOnDeath")] public bool SaveQuestItemsOnDeath { get; set; }
    [JsonPropertyName("keepFirSecureOnDeath")] public bool? KeepFirSecureOnDeath { get; set; }
    [JsonPropertyName("alwaysKeepFirOnRaidEnd")] public bool? AlwaysKeepFirOnRaidEnd { get; set; }
    [JsonPropertyName("sandboxAccessLevel")] public int? SandboxAccessLevel { get; set; }

    // ── B. Lab Settings ──
    [JsonPropertyName("labInsurance")] public bool LabInsurance { get; set; }
    [JsonPropertyName("removeLabKey")] public bool RemoveLabKey { get; set; }
    [JsonPropertyName("scavLabAccess")] public bool ScavLabAccess { get; set; }

    // ── C. Extract Rules ──
    [JsonPropertyName("guaranteedExtracts")] public bool GuaranteedExtracts { get; set; }
    [JsonPropertyName("removeBackpackExtractReq")] public bool RemoveBackpackExtractReq { get; set; }
    [JsonPropertyName("removeGearExtractReq")] public bool RemoveGearExtractReq { get; set; }
    [JsonPropertyName("freeCarExtracts")] public bool FreeCarExtracts { get; set; }
    [JsonPropertyName("freeCoopExtracts")] public bool FreeCoopExtracts { get; set; }
    [JsonPropertyName("carExtractWaitTime")] public int? CarExtractWaitTime { get; set; }
    [JsonPropertyName("disableTransits")] public bool DisableTransits { get; set; }
    [JsonPropertyName("fenceCoopGift")] public bool? FenceCoopGift { get; set; }

    // ── D. BTR Settings ──
    [JsonPropertyName("enableBtr")] public bool EnableBtr { get; set; }
    [JsonPropertyName("btrWoodsChance")] public double? BtrWoodsChance { get; set; }
    [JsonPropertyName("btrStreetsChance")] public double? BtrStreetsChance { get; set; }
    [JsonPropertyName("btrWoodsTimeMin")] public double? BtrWoodsTimeMin { get; set; }
    [JsonPropertyName("btrWoodsTimeMax")] public double? BtrWoodsTimeMax { get; set; }
    [JsonPropertyName("btrStreetsTimeMin")] public double? BtrStreetsTimeMin { get; set; }
    [JsonPropertyName("btrStreetsTimeMax")] public double? BtrStreetsTimeMax { get; set; }
    [JsonPropertyName("btrTaxiPrice")] public double? BtrTaxiPrice { get; set; }
    [JsonPropertyName("btrCoverPrice")] public double? BtrCoverPrice { get; set; }
    [JsonPropertyName("btrBearMod")] public double? BtrBearMod { get; set; }
    [JsonPropertyName("btrUsecMod")] public double? BtrUsecMod { get; set; }
    [JsonPropertyName("btrScavMod")] public double? BtrScavMod { get; set; }
    [JsonPropertyName("btrDeliveryW")] public int? BtrDeliveryW { get; set; }
    [JsonPropertyName("btrDeliveryH")] public int? BtrDeliveryH { get; set; }
    [JsonPropertyName("forceBtrFriendly")] public bool ForceBtrFriendly { get; set; }

    // ── E. Transit Settings ──
    [JsonPropertyName("transitStashW")] public int? TransitStashW { get; set; }
    [JsonPropertyName("transitStashH")] public int? TransitStashH { get; set; }

    // ── F. Pre-Raid Menu Defaults ──
    [JsonPropertyName("defaultAiAmount")] public string? DefaultAiAmount { get; set; }
    [JsonPropertyName("defaultAiDifficulty")] public string? DefaultAiDifficulty { get; set; }
    [JsonPropertyName("defaultBossEnabled")] public bool? DefaultBossEnabled { get; set; }
    [JsonPropertyName("defaultScavWars")] public bool? DefaultScavWars { get; set; }
    [JsonPropertyName("defaultTaggedAndCursed")] public bool? DefaultTaggedAndCursed { get; set; }
    [JsonPropertyName("defaultRandomWeather")] public bool? DefaultRandomWeather { get; set; }
    [JsonPropertyName("defaultRandomTime")] public bool? DefaultRandomTime { get; set; }
    [JsonPropertyName("timeBeforeDeploy")] public double? TimeBeforeDeploy { get; set; }
    [JsonPropertyName("scavHostileChance")] public double? ScavHostileChance { get; set; }

    // ── G. Airdrops ──
    [JsonPropertyName("enableAirdrops")] public bool EnableAirdrops { get; set; }
    [JsonPropertyName("airdropWeaponCountMin")] public int? AirdropWeaponCountMin { get; set; }
    [JsonPropertyName("airdropWeaponCountMax")] public int? AirdropWeaponCountMax { get; set; }
    [JsonPropertyName("airdropArmorCountMin")] public int? AirdropArmorCountMin { get; set; }
    [JsonPropertyName("airdropArmorCountMax")] public int? AirdropArmorCountMax { get; set; }
    [JsonPropertyName("airdropItemCountMin")] public int? AirdropItemCountMin { get; set; }
    [JsonPropertyName("airdropItemCountMax")] public int? AirdropItemCountMax { get; set; }
    [JsonPropertyName("airdropAllowBossItems")] public bool? AirdropAllowBossItems { get; set; }

    // ── H. Raid Time ──
    [JsonPropertyName("raidTimeAddMinutes")] public int RaidTimeAddMinutes { get; set; }

    // ── I. Extract Rewards ──
    [JsonPropertyName("carExtractStanding")] public double? CarExtractStanding { get; set; }
    [JsonPropertyName("coopExtractStanding")] public double? CoopExtractStanding { get; set; }
    [JsonPropertyName("scavExtractStanding")] public double? ScavExtractStanding { get; set; }

    // ── J. Seasonal Events ──
    [JsonPropertyName("enableSeasonalDetection")] public bool? EnableSeasonalDetection { get; set; }
    [JsonPropertyName("disabledEvents")] public List<string> DisabledEvents { get; set; } = [];

    // ── K. PMC Settings ──
    [JsonPropertyName("enablePmc")] public bool EnablePmc { get; set; }
    [JsonPropertyName("pmcUsecRatio")] public double? PmcUsecRatio { get; set; }
    [JsonPropertyName("pmcUseDifficultyOverride")] public bool? PmcUseDifficultyOverride { get; set; }
    [JsonPropertyName("pmcDifficulty")] public string? PmcDifficulty { get; set; }
    [JsonPropertyName("pmcLevelDeltaMin")] public int? PmcLevelDeltaMin { get; set; }
    [JsonPropertyName("pmcLevelDeltaMax")] public int? PmcLevelDeltaMax { get; set; }
    [JsonPropertyName("pmcWeaponInBackpackChance")] public double? PmcWeaponInBackpackChance { get; set; }
    [JsonPropertyName("pmcWeaponEnhancementChance")] public double? PmcWeaponEnhancementChance { get; set; }
    [JsonPropertyName("pmcForceHealingInSecure")] public bool? PmcForceHealingInSecure { get; set; }
}

// ═══════════════════════════════════════════════════════
// SERVICE SETTINGS CONFIG (persisted to config.json)
// ═══════════════════════════════════════════════════════

public record ServiceSettingsConfig
{
    // ── L. Insurance ──
    [JsonPropertyName("enableInsurance")] public bool EnableInsurance { get; set; }
    [JsonPropertyName("praporReturnChance")] public double? PraporReturnChance { get; set; }
    [JsonPropertyName("therapistReturnChance")] public double? TherapistReturnChance { get; set; }
    [JsonPropertyName("praporStorageTime")] public int? PraporStorageTime { get; set; }
    [JsonPropertyName("therapistStorageTime")] public int? TherapistStorageTime { get; set; }
    [JsonPropertyName("praporReturnMin")] public int? PraporReturnMin { get; set; }
    [JsonPropertyName("praporReturnMax")] public int? PraporReturnMax { get; set; }
    [JsonPropertyName("therapistReturnMin")] public int? TherapistReturnMin { get; set; }
    [JsonPropertyName("therapistReturnMax")] public int? TherapistReturnMax { get; set; }
    [JsonPropertyName("attachmentRecoveryChance")] public double? AttachmentRecoveryChance { get; set; }
    [JsonPropertyName("insuranceInterval")] public double? InsuranceInterval { get; set; }
    [JsonPropertyName("insuranceReturnOverride")] public double? InsuranceReturnOverride { get; set; }
    [JsonPropertyName("praporPricePerLl")] public List<double>? PraporPricePerLl { get; set; }
    [JsonPropertyName("therapistPricePerLl")] public List<double>? TherapistPricePerLl { get; set; }

    // ── M. Healing ──
    [JsonPropertyName("enableHealing")] public bool EnableHealing { get; set; }
    [JsonPropertyName("freeHealRaids")] public int? FreeHealRaids { get; set; }
    [JsonPropertyName("freeHealLevel")] public int? FreeHealLevel { get; set; }
    [JsonPropertyName("therapistHealPerLl")] public List<double>? TherapistHealPerLl { get; set; }

    // ── N. Repair ──
    [JsonPropertyName("enableRepair")] public bool EnableRepair { get; set; }
    [JsonPropertyName("repairPriceMult")] public double? RepairPriceMult { get; set; }
    [JsonPropertyName("noArmorRepairDegradation")] public bool NoArmorRepairDegradation { get; set; }
    [JsonPropertyName("noWeaponRepairDegradation")] public bool NoWeaponRepairDegradation { get; set; }
    [JsonPropertyName("noRandomRepairLoss")] public bool NoRandomRepairLoss { get; set; }
    [JsonPropertyName("armorKitSkillMult")] public double? ArmorKitSkillMult { get; set; }
    [JsonPropertyName("weaponMaintenanceSkillMult")] public double? WeaponMaintenanceSkillMult { get; set; }
    [JsonPropertyName("intellectWeaponKitMult")] public double? IntellectWeaponKitMult { get; set; }
    [JsonPropertyName("intellectArmorKitMult")] public double? IntellectArmorKitMult { get; set; }
    [JsonPropertyName("maxIntellectKit")] public double? MaxIntellectKit { get; set; }
    [JsonPropertyName("maxIntellectTrader")] public double? MaxIntellectTrader { get; set; }
    [JsonPropertyName("minDurabilitySell")] public double? MinDurabilitySell { get; set; }
    [JsonPropertyName("repairPricePerLl")] public Dictionary<string, List<double>>? RepairPricePerLl { get; set; }

    // ── O. Clothing ──
    [JsonPropertyName("clothesAnyFaction")] public bool ClothesAnyFaction { get; set; }
    [JsonPropertyName("clothesFree")] public bool ClothesFree { get; set; }
    [JsonPropertyName("clothesRemoveReqs")] public bool ClothesRemoveReqs { get; set; }
}

// ═══════════════════════════════════════════════════════
// RESPONSE DTOs (sent to frontend)
// ═══════════════════════════════════════════════════════

public record RaidRulesConfigResponse
{
    [JsonPropertyName("config")] public RaidRulesConfig Config { get; set; } = new();
    [JsonPropertyName("defaults")] public RaidRulesDefaults Defaults { get; set; } = new();
}

public record RaidRulesDefaults
{
    // A
    [JsonPropertyName("survivedXpReq")] public double SurvivedXpReq { get; set; }
    [JsonPropertyName("survivedSecReq")] public double SurvivedSecReq { get; set; }
    [JsonPropertyName("questItemsLost")] public bool QuestItemsLost { get; set; }
    [JsonPropertyName("keepFirSecure")] public bool KeepFirSecure { get; set; }
    [JsonPropertyName("alwaysKeepFir")] public bool AlwaysKeepFir { get; set; }
    [JsonPropertyName("sandboxLevel")] public int SandboxLevel { get; set; }

    // B
    [JsonPropertyName("labInsurance")] public bool LabInsurance { get; set; }
    [JsonPropertyName("labAccessKeys")] public int LabAccessKeyCount { get; set; }
    [JsonPropertyName("labDisabledForScav")] public bool LabDisabledForScav { get; set; }

    // C
    [JsonPropertyName("fenceCoopGift")] public bool FenceCoopGift { get; set; }

    // D
    [JsonPropertyName("btrWoodsChance")] public double BtrWoodsChance { get; set; }
    [JsonPropertyName("btrStreetsChance")] public double BtrStreetsChance { get; set; }
    [JsonPropertyName("btrWoodsTimeMin")] public double BtrWoodsTimeMin { get; set; }
    [JsonPropertyName("btrWoodsTimeMax")] public double BtrWoodsTimeMax { get; set; }
    [JsonPropertyName("btrStreetsTimeMin")] public double BtrStreetsTimeMin { get; set; }
    [JsonPropertyName("btrStreetsTimeMax")] public double BtrStreetsTimeMax { get; set; }
    [JsonPropertyName("btrTaxiPrice")] public double BtrTaxiPrice { get; set; }
    [JsonPropertyName("btrCoverPrice")] public double BtrCoverPrice { get; set; }
    [JsonPropertyName("btrBearMod")] public double BtrBearMod { get; set; }
    [JsonPropertyName("btrUsecMod")] public double BtrUsecMod { get; set; }
    [JsonPropertyName("btrScavMod")] public double BtrScavMod { get; set; }
    [JsonPropertyName("btrDeliveryW")] public int BtrDeliveryW { get; set; }
    [JsonPropertyName("btrDeliveryH")] public int BtrDeliveryH { get; set; }

    // E
    [JsonPropertyName("transitStashW")] public int TransitStashW { get; set; }
    [JsonPropertyName("transitStashH")] public int TransitStashH { get; set; }

    // F
    [JsonPropertyName("aiAmount")] public string AiAmount { get; set; } = "";
    [JsonPropertyName("aiDifficulty")] public string AiDifficulty { get; set; } = "";
    [JsonPropertyName("bossEnabled")] public bool BossEnabled { get; set; }
    [JsonPropertyName("scavWars")] public bool ScavWars { get; set; }
    [JsonPropertyName("taggedAndCursed")] public bool TaggedAndCursed { get; set; }
    [JsonPropertyName("randomWeather")] public bool RandomWeather { get; set; }
    [JsonPropertyName("randomTime")] public bool RandomTime { get; set; }
    [JsonPropertyName("timeBeforeDeploy")] public double TimeBeforeDeploy { get; set; }
    [JsonPropertyName("scavHostileChance")] public double ScavHostileChance { get; set; }

    // G
    [JsonPropertyName("airdropWeaponCountMin")] public int AirdropWeaponCountMin { get; set; }
    [JsonPropertyName("airdropWeaponCountMax")] public int AirdropWeaponCountMax { get; set; }
    [JsonPropertyName("airdropArmorCountMin")] public int AirdropArmorCountMin { get; set; }
    [JsonPropertyName("airdropArmorCountMax")] public int AirdropArmorCountMax { get; set; }
    [JsonPropertyName("airdropItemCountMin")] public int AirdropItemCountMin { get; set; }
    [JsonPropertyName("airdropItemCountMax")] public int AirdropItemCountMax { get; set; }
    [JsonPropertyName("airdropAllowBossItems")] public bool AirdropAllowBossItems { get; set; }
    [JsonPropertyName("airdropTypes")] public List<AirdropTypeInfo> AirdropTypes { get; set; } = [];

    // H (no defaults needed, additive from 0)

    // I
    [JsonPropertyName("carExtractStanding")] public double CarExtractStanding { get; set; }
    [JsonPropertyName("coopExtractStanding")] public double CoopExtractStanding { get; set; }
    [JsonPropertyName("scavExtractStanding")] public double ScavExtractStanding { get; set; }

    // J
    [JsonPropertyName("seasonalDetection")] public bool SeasonalDetection { get; set; }
    [JsonPropertyName("seasonalEvents")] public List<SeasonalEventInfo> SeasonalEvents { get; set; } = [];

    // K
    [JsonPropertyName("pmcUsecRatio")] public double PmcUsecRatio { get; set; }
    [JsonPropertyName("pmcUseDifficultyOverride")] public bool PmcUseDifficultyOverride { get; set; }
    [JsonPropertyName("pmcDifficulty")] public string PmcDifficulty { get; set; } = "";
    [JsonPropertyName("pmcLevelDeltaMin")] public int PmcLevelDeltaMin { get; set; }
    [JsonPropertyName("pmcLevelDeltaMax")] public int PmcLevelDeltaMax { get; set; }
    [JsonPropertyName("pmcWeaponInBackpackChance")] public double PmcWeaponInBackpackChance { get; set; }
    [JsonPropertyName("pmcWeaponEnhancementChance")] public double PmcWeaponEnhancementChance { get; set; }
    [JsonPropertyName("pmcForceHealingInSecure")] public bool PmcForceHealingInSecure { get; set; }
}

public record AirdropTypeInfo
{
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("weight")] public double Weight { get; set; }
}

public record SeasonalEventInfo
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("enabled")] public bool Enabled { get; set; }
}

// ═══════════════════════════════════════════════════════
// SERVICE SETTINGS RESPONSE DTOs
// ═══════════════════════════════════════════════════════

public record ServiceSettingsConfigResponse
{
    [JsonPropertyName("config")] public ServiceSettingsConfig Config { get; set; } = new();
    [JsonPropertyName("defaults")] public ServiceSettingsDefaults Defaults { get; set; } = new();
}

public record ServiceSettingsDefaults
{
    // L. Insurance
    [JsonPropertyName("praporReturnChance")] public double PraporReturnChance { get; set; }
    [JsonPropertyName("therapistReturnChance")] public double TherapistReturnChance { get; set; }
    [JsonPropertyName("praporStorageTime")] public int PraporStorageTime { get; set; }
    [JsonPropertyName("therapistStorageTime")] public int TherapistStorageTime { get; set; }
    [JsonPropertyName("praporReturnMin")] public int PraporReturnMin { get; set; }
    [JsonPropertyName("praporReturnMax")] public int PraporReturnMax { get; set; }
    [JsonPropertyName("therapistReturnMin")] public int TherapistReturnMin { get; set; }
    [JsonPropertyName("therapistReturnMax")] public int TherapistReturnMax { get; set; }
    [JsonPropertyName("attachmentRecoveryChance")] public double AttachmentRecoveryChance { get; set; }
    [JsonPropertyName("insuranceInterval")] public double InsuranceInterval { get; set; }
    [JsonPropertyName("insuranceReturnOverride")] public double InsuranceReturnOverride { get; set; }
    [JsonPropertyName("praporPricePerLl")] public List<double> PraporPricePerLl { get; set; } = [];
    [JsonPropertyName("therapistPricePerLl")] public List<double> TherapistPricePerLl { get; set; } = [];

    // M. Healing
    [JsonPropertyName("freeHealRaids")] public int FreeHealRaids { get; set; }
    [JsonPropertyName("freeHealLevel")] public int FreeHealLevel { get; set; }
    [JsonPropertyName("therapistHealPerLl")] public List<double> TherapistHealPerLl { get; set; } = [];

    // N. Repair
    [JsonPropertyName("repairPriceMult")] public double RepairPriceMult { get; set; }
    [JsonPropertyName("noRandomRepairLoss")] public bool RandomRepairLoss { get; set; }
    [JsonPropertyName("armorKitSkillMult")] public double ArmorKitSkillMult { get; set; }
    [JsonPropertyName("weaponMaintenanceSkillMult")] public double WeaponMaintenanceSkillMult { get; set; }
    [JsonPropertyName("intellectWeaponKitMult")] public double IntellectWeaponKitMult { get; set; }
    [JsonPropertyName("intellectArmorKitMult")] public double IntellectArmorKitMult { get; set; }
    [JsonPropertyName("maxIntellectKit")] public double MaxIntellectKit { get; set; }
    [JsonPropertyName("maxIntellectTrader")] public double MaxIntellectTrader { get; set; }
    [JsonPropertyName("minDurabilitySell")] public double MinDurabilitySell { get; set; }
    [JsonPropertyName("repairTraders")] public List<RepairTraderInfo> RepairTraders { get; set; } = [];
    [JsonPropertyName("armorMaterialCount")] public int ArmorMaterialCount { get; set; }
    [JsonPropertyName("weaponRepairCount")] public int WeaponRepairCount { get; set; }

    // O. Clothing
    [JsonPropertyName("suitCount")] public int SuitCount { get; set; }
}

public record RepairTraderInfo
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("pricePerLl")] public List<double> PricePerLl { get; set; } = [];
}

using System.Text.Json.Serialization;

namespace ZSlayerCommandCenter.Models;

// ═══════════════════════════════════════════════════════
// CONFIG (persisted to config.json under "gameValues")
// ═══════════════════════════════════════════════════════

public record GameValuesConfig
{
    [JsonPropertyName("ammoOverrides")]
    public Dictionary<string, AmmoOverride> AmmoOverrides { get; set; } = new();

    [JsonPropertyName("armorOverrides")]
    public Dictionary<string, ArmorOverride> ArmorOverrides { get; set; } = new();

    [JsonPropertyName("weaponOverrides")]
    public Dictionary<string, WeaponOverride> WeaponOverrides { get; set; } = new();

    [JsonPropertyName("medicalOverrides")]
    public Dictionary<string, MedicalOverride> MedicalOverrides { get; set; } = new();

    [JsonPropertyName("backpackOverrides")]
    public Dictionary<string, BackpackOverride> BackpackOverrides { get; set; } = new();

    [JsonPropertyName("presets")]
    public Dictionary<string, GameValuesPresetEntry> Presets { get; set; } = new();
}

// ═══════════════════════════════════════════════════════
// AMMO
// ═══════════════════════════════════════════════════════

public record AmmoOverride
{
    [JsonPropertyName("damage")] public double? Damage { get; set; }
    [JsonPropertyName("penetrationPower")] public double? PenetrationPower { get; set; }
    [JsonPropertyName("armorDamage")] public double? ArmorDamage { get; set; }
    [JsonPropertyName("initialSpeed")] public double? InitialSpeed { get; set; }
    [JsonPropertyName("fragmentationChance")] public double? FragmentationChance { get; set; }
    [JsonPropertyName("ricochetChance")] public double? RicochetChance { get; set; }
    [JsonPropertyName("projectileCount")] public double? ProjectileCount { get; set; }
    [JsonPropertyName("bulletMassGram")] public double? BulletMassGram { get; set; }
    [JsonPropertyName("ballisticCoeficient")] public double? BallisticCoeficient { get; set; }
    [JsonPropertyName("misfireChance")] public double? MisfireChance { get; set; }
    [JsonPropertyName("heatFactor")] public double? HeatFactor { get; set; }
    [JsonPropertyName("durabilityBurnModificator")] public double? DurabilityBurnModificator { get; set; }
    [JsonPropertyName("lightBleedingDelta")] public double? LightBleedingDelta { get; set; }
    [JsonPropertyName("heavyBleedingDelta")] public double? HeavyBleedingDelta { get; set; }
    [JsonPropertyName("staminaBurnPerDamage")] public double? StaminaBurnPerDamage { get; set; }
    [JsonPropertyName("ammoAccr")] public double? AmmoAccr { get; set; }
    [JsonPropertyName("ammoRec")] public double? AmmoRec { get; set; }
}

// ═══════════════════════════════════════════════════════
// ARMOR
// ═══════════════════════════════════════════════════════

public record ArmorOverride
{
    [JsonPropertyName("armorClass")] public int? ArmorClass { get; set; }
    [JsonPropertyName("durability")] public double? Durability { get; set; }
    [JsonPropertyName("maxDurability")] public double? MaxDurability { get; set; }
    [JsonPropertyName("bluntThroughput")] public double? BluntThroughput { get; set; }
    [JsonPropertyName("speedPenaltyPercent")] public double? SpeedPenaltyPercent { get; set; }
    [JsonPropertyName("mousePenalty")] public double? MousePenalty { get; set; }
    [JsonPropertyName("weaponErgonomicPenalty")] public double? WeaponErgonomicPenalty { get; set; }
}

// ═══════════════════════════════════════════════════════
// WEAPONS
// ═══════════════════════════════════════════════════════

public record WeaponOverride
{
    [JsonPropertyName("ergonomics")] public double? Ergonomics { get; set; }
    [JsonPropertyName("recoilForceUp")] public double? RecoilForceUp { get; set; }
    [JsonPropertyName("recoilForceBack")] public double? RecoilForceBack { get; set; }
    [JsonPropertyName("recoilAngle")] public double? RecoilAngle { get; set; }
    [JsonPropertyName("bFirerate")] public double? BFirerate { get; set; }
    [JsonPropertyName("centerOfImpact")] public double? CenterOfImpact { get; set; }
    [JsonPropertyName("sightingRange")] public double? SightingRange { get; set; }
    [JsonPropertyName("durability")] public double? Durability { get; set; }
    [JsonPropertyName("maxDurability")] public double? MaxDurability { get; set; }
    [JsonPropertyName("heatFactorGun")] public double? HeatFactorGun { get; set; }
    [JsonPropertyName("coolFactorGun")] public double? CoolFactorGun { get; set; }
    [JsonPropertyName("baseMalfunctionChance")] public double? BaseMalfunctionChance { get; set; }
    [JsonPropertyName("velocity")] public double? Velocity { get; set; }
    [JsonPropertyName("deviationMax")] public double? DeviationMax { get; set; }
}

// ═══════════════════════════════════════════════════════
// MEDICAL
// ═══════════════════════════════════════════════════════

public record MedicalOverride
{
    [JsonPropertyName("maxHpResource")] public double? MaxHpResource { get; set; }
    [JsonPropertyName("hpResourceRate")] public double? HpResourceRate { get; set; }
    [JsonPropertyName("medUseTime")] public double? MedUseTime { get; set; }
    [JsonPropertyName("lightBleedingCost")] public double? LightBleedingCost { get; set; }
    [JsonPropertyName("heavyBleedingCost")] public double? HeavyBleedingCost { get; set; }
    [JsonPropertyName("fractureCost")] public double? FractureCost { get; set; }
    [JsonPropertyName("painDuration")] public double? PainDuration { get; set; }
    [JsonPropertyName("contusionDuration")] public double? ContusionDuration { get; set; }
    [JsonPropertyName("energyChange")] public double? EnergyChange { get; set; }
    [JsonPropertyName("hydrationChange")] public double? HydrationChange { get; set; }
}

// ═══════════════════════════════════════════════════════
// API DTOs — MEDICAL
// ═══════════════════════════════════════════════════════

public record MedicalDto
{
    [JsonPropertyName("tpl")] public string Tpl { get; set; } = "";
    [JsonPropertyName("shortName")] public string ShortName { get; set; } = "";
    [JsonPropertyName("fullName")] public string FullName { get; set; } = "";
    [JsonPropertyName("medType")] public string MedType { get; set; } = "";
    [JsonPropertyName("stimBuffName")] public string? StimBuffName { get; set; }
    [JsonPropertyName("maxHpResource")] public double MaxHpResource { get; set; }
    [JsonPropertyName("hpResourceRate")] public double HpResourceRate { get; set; }
    [JsonPropertyName("medUseTime")] public double MedUseTime { get; set; }
    [JsonPropertyName("lightBleedingCost")] public double? LightBleedingCost { get; set; }
    [JsonPropertyName("heavyBleedingCost")] public double? HeavyBleedingCost { get; set; }
    [JsonPropertyName("fractureCost")] public double? FractureCost { get; set; }
    [JsonPropertyName("painDuration")] public double? PainDuration { get; set; }
    [JsonPropertyName("contusionDuration")] public double? ContusionDuration { get; set; }
    [JsonPropertyName("energyChange")] public double? EnergyChange { get; set; }
    [JsonPropertyName("hydrationChange")] public double? HydrationChange { get; set; }
    [JsonPropertyName("treats")] public List<string> Treats { get; set; } = [];
    [JsonPropertyName("handbookPrice")] public double HandbookPrice { get; set; }
    [JsonPropertyName("original")] public MedicalOriginalValues Original { get; set; } = new();
    [JsonPropertyName("isModified")] public bool IsModified { get; set; }
}

public record MedicalOriginalValues
{
    [JsonPropertyName("maxHpResource")] public double MaxHpResource { get; set; }
    [JsonPropertyName("hpResourceRate")] public double HpResourceRate { get; set; }
    [JsonPropertyName("medUseTime")] public double MedUseTime { get; set; }
    [JsonPropertyName("lightBleedingCost")] public double? LightBleedingCost { get; set; }
    [JsonPropertyName("heavyBleedingCost")] public double? HeavyBleedingCost { get; set; }
    [JsonPropertyName("fractureCost")] public double? FractureCost { get; set; }
    [JsonPropertyName("painDuration")] public double? PainDuration { get; set; }
    [JsonPropertyName("contusionDuration")] public double? ContusionDuration { get; set; }
    [JsonPropertyName("energyChange")] public double? EnergyChange { get; set; }
    [JsonPropertyName("hydrationChange")] public double? HydrationChange { get; set; }
}

public record MedicalListResponse
{
    [JsonPropertyName("medical")] public List<MedicalDto> Medical { get; set; } = [];
    [JsonPropertyName("types")] public List<MedicalTypeInfo> Types { get; set; } = [];
    [JsonPropertyName("totalModified")] public int TotalModified { get; set; }
}

public record MedicalTypeInfo
{
    [JsonPropertyName("key")] public string Key { get; set; } = "";
    [JsonPropertyName("display")] public string Display { get; set; } = "";
    [JsonPropertyName("count")] public int Count { get; set; }
}

// ═══════════════════════════════════════════════════════
// BACKPACK
// ═══════════════════════════════════════════════════════

public record BackpackOverride
{
    [JsonPropertyName("weight")] public double? Weight { get; set; }
    [JsonPropertyName("speedPenaltyPercent")] public double? SpeedPenaltyPercent { get; set; }
    [JsonPropertyName("weaponErgonomicPenalty")] public double? WeaponErgonomicPenalty { get; set; }
    [JsonPropertyName("mousePenalty")] public double? MousePenalty { get; set; }
    [JsonPropertyName("gridWidth")] public int? GridWidth { get; set; }
    [JsonPropertyName("gridHeight")] public int? GridHeight { get; set; }
}

public record BackpackDto
{
    [JsonPropertyName("tpl")] public string Tpl { get; set; } = "";
    [JsonPropertyName("shortName")] public string ShortName { get; set; } = "";
    [JsonPropertyName("fullName")] public string FullName { get; set; } = "";
    [JsonPropertyName("weight")] public double Weight { get; set; }
    [JsonPropertyName("speedPenaltyPercent")] public double SpeedPenaltyPercent { get; set; }
    [JsonPropertyName("weaponErgonomicPenalty")] public double WeaponErgonomicPenalty { get; set; }
    [JsonPropertyName("mousePenalty")] public double MousePenalty { get; set; }
    [JsonPropertyName("gridWidth")] public int GridWidth { get; set; }
    [JsonPropertyName("gridHeight")] public int GridHeight { get; set; }
    [JsonPropertyName("totalSlots")] public int TotalSlots { get; set; }
    [JsonPropertyName("isMultiGrid")] public bool IsMultiGrid { get; set; }
    [JsonPropertyName("gridLayout")] public string GridLayout { get; set; } = "";
    [JsonPropertyName("handbookPrice")] public double HandbookPrice { get; set; }
    [JsonPropertyName("original")] public BackpackOriginalValues Original { get; set; } = new();
    [JsonPropertyName("isModified")] public bool IsModified { get; set; }
}

public record BackpackOriginalValues
{
    [JsonPropertyName("weight")] public double Weight { get; set; }
    [JsonPropertyName("speedPenaltyPercent")] public double SpeedPenaltyPercent { get; set; }
    [JsonPropertyName("weaponErgonomicPenalty")] public double WeaponErgonomicPenalty { get; set; }
    [JsonPropertyName("mousePenalty")] public double MousePenalty { get; set; }
    [JsonPropertyName("gridWidth")] public int GridWidth { get; set; }
    [JsonPropertyName("gridHeight")] public int GridHeight { get; set; }
    [JsonPropertyName("totalSlots")] public int TotalSlots { get; set; }
}

public record BackpackListResponse
{
    [JsonPropertyName("backpacks")] public List<BackpackDto> Backpacks { get; set; } = [];
    [JsonPropertyName("totalCount")] public int TotalCount { get; set; }
    [JsonPropertyName("totalModified")] public int TotalModified { get; set; }
}

// ═══════════════════════════════════════════════════════
// API DTOs — AMMO
// ═══════════════════════════════════════════════════════

public record AmmoDto
{
    [JsonPropertyName("tpl")] public string Tpl { get; set; } = "";
    [JsonPropertyName("shortName")] public string ShortName { get; set; } = "";
    [JsonPropertyName("fullName")] public string FullName { get; set; } = "";
    [JsonPropertyName("caliber")] public string Caliber { get; set; } = "";
    [JsonPropertyName("caliberDisplay")] public string CaliberDisplay { get; set; } = "";
    [JsonPropertyName("damage")] public double Damage { get; set; }
    [JsonPropertyName("penetrationPower")] public double PenetrationPower { get; set; }
    [JsonPropertyName("armorDamage")] public double ArmorDamage { get; set; }
    [JsonPropertyName("initialSpeed")] public double InitialSpeed { get; set; }
    [JsonPropertyName("fragmentationChance")] public double FragmentationChance { get; set; }
    [JsonPropertyName("ricochetChance")] public double RicochetChance { get; set; }
    [JsonPropertyName("projectileCount")] public double ProjectileCount { get; set; }
    [JsonPropertyName("bulletMassGram")] public double BulletMassGram { get; set; }
    [JsonPropertyName("ballisticCoeficient")] public double BallisticCoeficient { get; set; }
    [JsonPropertyName("misfireChance")] public double MisfireChance { get; set; }
    [JsonPropertyName("heatFactor")] public double HeatFactor { get; set; }
    [JsonPropertyName("durabilityBurnModificator")] public double DurabilityBurnModificator { get; set; }
    [JsonPropertyName("lightBleedingDelta")] public double LightBleedingDelta { get; set; }
    [JsonPropertyName("heavyBleedingDelta")] public double HeavyBleedingDelta { get; set; }
    [JsonPropertyName("staminaBurnPerDamage")] public double StaminaBurnPerDamage { get; set; }
    [JsonPropertyName("ammoAccr")] public double AmmoAccr { get; set; }
    [JsonPropertyName("ammoRec")] public double AmmoRec { get; set; }
    [JsonPropertyName("handbookPrice")] public double HandbookPrice { get; set; }
    [JsonPropertyName("original")] public AmmoOriginalValues Original { get; set; } = new();
    [JsonPropertyName("isModified")] public bool IsModified { get; set; }
}

public record AmmoOriginalValues
{
    [JsonPropertyName("damage")] public double Damage { get; set; }
    [JsonPropertyName("penetrationPower")] public double PenetrationPower { get; set; }
    [JsonPropertyName("armorDamage")] public double ArmorDamage { get; set; }
    [JsonPropertyName("initialSpeed")] public double InitialSpeed { get; set; }
    [JsonPropertyName("fragmentationChance")] public double FragmentationChance { get; set; }
    [JsonPropertyName("ricochetChance")] public double RicochetChance { get; set; }
    [JsonPropertyName("projectileCount")] public double ProjectileCount { get; set; }
    [JsonPropertyName("bulletMassGram")] public double BulletMassGram { get; set; }
    [JsonPropertyName("ballisticCoeficient")] public double BallisticCoeficient { get; set; }
    [JsonPropertyName("misfireChance")] public double MisfireChance { get; set; }
    [JsonPropertyName("heatFactor")] public double HeatFactor { get; set; }
    [JsonPropertyName("durabilityBurnModificator")] public double DurabilityBurnModificator { get; set; }
    [JsonPropertyName("lightBleedingDelta")] public double LightBleedingDelta { get; set; }
    [JsonPropertyName("heavyBleedingDelta")] public double HeavyBleedingDelta { get; set; }
    [JsonPropertyName("staminaBurnPerDamage")] public double StaminaBurnPerDamage { get; set; }
    [JsonPropertyName("ammoAccr")] public double AmmoAccr { get; set; }
    [JsonPropertyName("ammoRec")] public double AmmoRec { get; set; }
}

public record AmmoListResponse
{
    [JsonPropertyName("ammo")] public List<AmmoDto> Ammo { get; set; } = [];
    [JsonPropertyName("calibers")] public List<CaliberInfo> Calibers { get; set; } = [];
    [JsonPropertyName("totalModified")] public int TotalModified { get; set; }
}

public record CaliberInfo
{
    [JsonPropertyName("key")] public string Key { get; set; } = "";
    [JsonPropertyName("display")] public string Display { get; set; } = "";
    [JsonPropertyName("count")] public int Count { get; set; }
}

// ═══════════════════════════════════════════════════════
// API DTOs — ARMOR
// ═══════════════════════════════════════════════════════

public record ArmorDto
{
    [JsonPropertyName("tpl")] public string Tpl { get; set; } = "";
    [JsonPropertyName("shortName")] public string ShortName { get; set; } = "";
    [JsonPropertyName("fullName")] public string FullName { get; set; } = "";
    [JsonPropertyName("armorClass")] public int ArmorClass { get; set; }
    [JsonPropertyName("durability")] public double Durability { get; set; }
    [JsonPropertyName("maxDurability")] public double MaxDurability { get; set; }
    [JsonPropertyName("bluntThroughput")] public double BluntThroughput { get; set; }
    [JsonPropertyName("speedPenaltyPercent")] public double SpeedPenaltyPercent { get; set; }
    [JsonPropertyName("mousePenalty")] public double MousePenalty { get; set; }
    [JsonPropertyName("weaponErgonomicPenalty")] public double WeaponErgonomicPenalty { get; set; }
    [JsonPropertyName("armorMaterial")] public string ArmorMaterial { get; set; } = "";
    [JsonPropertyName("armorType")] public string ArmorType { get; set; } = "";
    [JsonPropertyName("isPlate")] public bool IsPlate { get; set; }
    [JsonPropertyName("handbookPrice")] public double HandbookPrice { get; set; }
    [JsonPropertyName("original")] public ArmorOriginalValues Original { get; set; } = new();
    [JsonPropertyName("isModified")] public bool IsModified { get; set; }
}

public record ArmorOriginalValues
{
    [JsonPropertyName("armorClass")] public int ArmorClass { get; set; }
    [JsonPropertyName("durability")] public double Durability { get; set; }
    [JsonPropertyName("maxDurability")] public double MaxDurability { get; set; }
    [JsonPropertyName("bluntThroughput")] public double BluntThroughput { get; set; }
    [JsonPropertyName("speedPenaltyPercent")] public double SpeedPenaltyPercent { get; set; }
    [JsonPropertyName("mousePenalty")] public double MousePenalty { get; set; }
    [JsonPropertyName("weaponErgonomicPenalty")] public double WeaponErgonomicPenalty { get; set; }
}

public record ArmorListResponse
{
    [JsonPropertyName("armor")] public List<ArmorDto> Armor { get; set; } = [];
    [JsonPropertyName("armorClasses")] public List<ArmorClassInfo> ArmorClasses { get; set; } = [];
    [JsonPropertyName("materials")] public List<string> Materials { get; set; } = [];
    [JsonPropertyName("totalModified")] public int TotalModified { get; set; }
}

public record ArmorClassInfo
{
    [JsonPropertyName("armorClass")] public int ArmorClass { get; set; }
    [JsonPropertyName("count")] public int Count { get; set; }
}

// ═══════════════════════════════════════════════════════
// API DTOs — WEAPONS
// ═══════════════════════════════════════════════════════

public record WeaponDto
{
    [JsonPropertyName("tpl")] public string Tpl { get; set; } = "";
    [JsonPropertyName("shortName")] public string ShortName { get; set; } = "";
    [JsonPropertyName("fullName")] public string FullName { get; set; } = "";
    [JsonPropertyName("weapClass")] public string WeapClass { get; set; } = "";
    [JsonPropertyName("weapClassDisplay")] public string WeapClassDisplay { get; set; } = "";
    [JsonPropertyName("ammoCaliber")] public string AmmoCaliber { get; set; } = "";
    [JsonPropertyName("ammoCaliberDisplay")] public string AmmoCaliberDisplay { get; set; } = "";
    [JsonPropertyName("fireTypes")] public List<string> FireTypes { get; set; } = [];
    [JsonPropertyName("boltAction")] public bool BoltAction { get; set; }
    [JsonPropertyName("ergonomics")] public double Ergonomics { get; set; }
    [JsonPropertyName("recoilForceUp")] public double RecoilForceUp { get; set; }
    [JsonPropertyName("recoilForceBack")] public double RecoilForceBack { get; set; }
    [JsonPropertyName("recoilAngle")] public double RecoilAngle { get; set; }
    [JsonPropertyName("bFirerate")] public double BFirerate { get; set; }
    [JsonPropertyName("centerOfImpact")] public double CenterOfImpact { get; set; }
    [JsonPropertyName("sightingRange")] public double SightingRange { get; set; }
    [JsonPropertyName("durability")] public double Durability { get; set; }
    [JsonPropertyName("maxDurability")] public double MaxDurability { get; set; }
    [JsonPropertyName("heatFactorGun")] public double HeatFactorGun { get; set; }
    [JsonPropertyName("coolFactorGun")] public double CoolFactorGun { get; set; }
    [JsonPropertyName("baseMalfunctionChance")] public double BaseMalfunctionChance { get; set; }
    [JsonPropertyName("velocity")] public double Velocity { get; set; }
    [JsonPropertyName("deviationMax")] public double DeviationMax { get; set; }
    [JsonPropertyName("handbookPrice")] public double HandbookPrice { get; set; }
    [JsonPropertyName("original")] public WeaponOriginalValues Original { get; set; } = new();
    [JsonPropertyName("isModified")] public bool IsModified { get; set; }
}

public record WeaponOriginalValues
{
    [JsonPropertyName("ergonomics")] public double Ergonomics { get; set; }
    [JsonPropertyName("recoilForceUp")] public double RecoilForceUp { get; set; }
    [JsonPropertyName("recoilForceBack")] public double RecoilForceBack { get; set; }
    [JsonPropertyName("recoilAngle")] public double RecoilAngle { get; set; }
    [JsonPropertyName("bFirerate")] public double BFirerate { get; set; }
    [JsonPropertyName("centerOfImpact")] public double CenterOfImpact { get; set; }
    [JsonPropertyName("sightingRange")] public double SightingRange { get; set; }
    [JsonPropertyName("durability")] public double Durability { get; set; }
    [JsonPropertyName("maxDurability")] public double MaxDurability { get; set; }
    [JsonPropertyName("heatFactorGun")] public double HeatFactorGun { get; set; }
    [JsonPropertyName("coolFactorGun")] public double CoolFactorGun { get; set; }
    [JsonPropertyName("baseMalfunctionChance")] public double BaseMalfunctionChance { get; set; }
    [JsonPropertyName("velocity")] public double Velocity { get; set; }
    [JsonPropertyName("deviationMax")] public double DeviationMax { get; set; }
}

public record WeaponListResponse
{
    [JsonPropertyName("weapons")] public List<WeaponDto> Weapons { get; set; } = [];
    [JsonPropertyName("weaponClasses")] public List<WeaponClassInfo> WeaponClasses { get; set; } = [];
    [JsonPropertyName("calibers")] public List<CaliberInfo> Calibers { get; set; } = [];
    [JsonPropertyName("totalModified")] public int TotalModified { get; set; }
}

public record WeaponClassInfo
{
    [JsonPropertyName("key")] public string Key { get; set; } = "";
    [JsonPropertyName("display")] public string Display { get; set; } = "";
    [JsonPropertyName("count")] public int Count { get; set; }
}

// ═══════════════════════════════════════════════════════
// SHARED REQUEST / RESPONSE
// ═══════════════════════════════════════════════════════

public record GameValuesUpdateRequest<T>
{
    [JsonPropertyName("overrides")] public Dictionary<string, T> Overrides { get; set; } = new();
}

public record GameValuesApplyResult
{
    [JsonPropertyName("success")] public bool Success { get; set; }
    [JsonPropertyName("itemsModified")] public int ItemsModified { get; set; }
    [JsonPropertyName("applyTimeMs")] public long ApplyTimeMs { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
}

public record GameValuesPresetEntry
{
    [JsonPropertyName("description")] public string Description { get; set; } = "";
    [JsonPropertyName("category")] public string Category { get; set; } = "all";
    [JsonPropertyName("ammoOverrides")] public Dictionary<string, AmmoOverride>? AmmoOverrides { get; set; }
    [JsonPropertyName("armorOverrides")] public Dictionary<string, ArmorOverride>? ArmorOverrides { get; set; }
    [JsonPropertyName("weaponOverrides")] public Dictionary<string, WeaponOverride>? WeaponOverrides { get; set; }
    [JsonPropertyName("medicalOverrides")] public Dictionary<string, MedicalOverride>? MedicalOverrides { get; set; }
    [JsonPropertyName("backpackOverrides")] public Dictionary<string, BackpackOverride>? BackpackOverrides { get; set; }
}

public record GameValuesPresetInfo
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("description")] public string Description { get; set; } = "";
    [JsonPropertyName("category")] public string Category { get; set; } = "";
    [JsonPropertyName("isBuiltIn")] public bool IsBuiltIn { get; set; }
}

// ═══════════════════════════════════════════════════════
// VALUE CLAMPING
// ═══════════════════════════════════════════════════════

public static class GameValuesClamps
{
    public static double Clamp(double value, double min, double max) => Math.Max(min, Math.Min(max, value));

    // Ammo
    public const double DamageMin = 0, DamageMax = 999;
    public const double PenetrationPowerMin = 0, PenetrationPowerMax = 100;
    public const double ArmorDamageMin = 0, ArmorDamageMax = 999;
    public const double InitialSpeedMin = 0, InitialSpeedMax = 5000;
    public const double FragmentationChanceMin = 0, FragmentationChanceMax = 1;
    public const double RicochetChanceMin = 0, RicochetChanceMax = 1;
    public const double ProjectileCountMin = 1, ProjectileCountMax = 100;
    public const double BulletMassGramMin = 0, BulletMassGramMax = 500;
    public const double BallisticCoeficientMin = 0, BallisticCoeficientMax = 2;
    public const double MisfireChanceMin = 0, MisfireChanceMax = 1;
    public const double HeatFactorMin = 0, HeatFactorMax = 10;
    public const double DurabilityBurnMin = 0, DurabilityBurnMax = 50;
    public const double BleedDeltaMin = 0, BleedDeltaMax = 1;
    public const double StaminaBurnMin = 0, StaminaBurnMax = 10;
    public const double AmmoAccrMin = -500, AmmoAccrMax = 500;
    public const double AmmoRecMin = -500, AmmoRecMax = 500;

    // Armor
    public const int ArmorClassMin = 0, ArmorClassMax = 10;
    public const double ArmorDurabilityMin = 0, ArmorDurabilityMax = 999;
    public const double BluntThroughputMin = 0, BluntThroughputMax = 1;
    public const double SpeedPenaltyMin = -100, SpeedPenaltyMax = 0;
    public const double MousePenaltyMin = -100, MousePenaltyMax = 0;
    public const double WeaponErgoPenaltyMin = -100, WeaponErgoPenaltyMax = 0;

    // Weapons
    public const double ErgonomicsMin = 0, ErgonomicsMax = 200;
    public const double RecoilMin = 0, RecoilMax = 999;
    public const double RecoilAngleMin = 0, RecoilAngleMax = 180;
    public const double FirerateMin = 0, FirerateMax = 3000;
    public const double CenterOfImpactMin = 0, CenterOfImpactMax = 10;
    public const double SightingRangeMin = 0, SightingRangeMax = 5000;
    public const double WeaponDurabilityMin = 0, WeaponDurabilityMax = 9999;
    public const double HeatFactorGunMin = 0, HeatFactorGunMax = 10;
    public const double CoolFactorGunMin = 0, CoolFactorGunMax = 10;
    public const double MalfunctionChanceMin = 0, MalfunctionChanceMax = 1;
    public const double VelocityMin = 0, VelocityMax = 5000;
    public const double DeviationMaxMin = 0, DeviationMaxMax = 100;

    // Medical
    public const double MaxHpResourceMin = 0, MaxHpResourceMax = 10000;
    public const double HpResourceRateMin = 0, HpResourceRateMax = 500;
    public const double MedUseTimeMin = 0.1, MedUseTimeMax = 60;
    public const double BleedCostMin = 0, BleedCostMax = 2000;
    public const double FractureCostMin = 0, FractureCostMax = 2000;
    public const double EffectDurationMin = 0, EffectDurationMax = 3600;
    public const double HealthChangeMin = -200, HealthChangeMax = 200;

    public static double ClampMedical(string field, double value) => field switch
    {
        "maxHpResource" => Clamp(value, MaxHpResourceMin, MaxHpResourceMax),
        "hpResourceRate" => Clamp(value, HpResourceRateMin, HpResourceRateMax),
        "medUseTime" => Clamp(value, MedUseTimeMin, MedUseTimeMax),
        "lightBleedingCost" => Clamp(value, BleedCostMin, BleedCostMax),
        "heavyBleedingCost" => Clamp(value, BleedCostMin, BleedCostMax),
        "fractureCost" => Clamp(value, FractureCostMin, FractureCostMax),
        "painDuration" => Clamp(value, EffectDurationMin, EffectDurationMax),
        "contusionDuration" => Clamp(value, EffectDurationMin, EffectDurationMax),
        "energyChange" => Clamp(value, HealthChangeMin, HealthChangeMax),
        "hydrationChange" => Clamp(value, HealthChangeMin, HealthChangeMax),
        _ => value
    };

    // Backpack
    public const double BpWeightMin = 0, BpWeightMax = 20;
    public const double BpSpeedPenaltyMin = -100, BpSpeedPenaltyMax = 0;
    public const double BpErgoPenaltyMin = -100, BpErgoPenaltyMax = 0;
    public const double BpMousePenaltyMin = -100, BpMousePenaltyMax = 0;
    public const int BpGridMin = 1, BpGridMax = 20;

    public static double ClampBackpack(string field, double value) => field switch
    {
        "weight" => Clamp(value, BpWeightMin, BpWeightMax),
        "speedPenaltyPercent" => Clamp(value, BpSpeedPenaltyMin, BpSpeedPenaltyMax),
        "weaponErgonomicPenalty" => Clamp(value, BpErgoPenaltyMin, BpErgoPenaltyMax),
        "mousePenalty" => Clamp(value, BpMousePenaltyMin, BpMousePenaltyMax),
        _ => value
    };

    public static double ClampAmmo(string field, double value) => field switch
    {
        "damage" => Clamp(value, DamageMin, DamageMax),
        "penetrationPower" => Clamp(value, PenetrationPowerMin, PenetrationPowerMax),
        "armorDamage" => Clamp(value, ArmorDamageMin, ArmorDamageMax),
        "initialSpeed" => Clamp(value, InitialSpeedMin, InitialSpeedMax),
        "fragmentationChance" => Clamp(value, FragmentationChanceMin, FragmentationChanceMax),
        "ricochetChance" => Clamp(value, RicochetChanceMin, RicochetChanceMax),
        "projectileCount" => Clamp(value, ProjectileCountMin, ProjectileCountMax),
        "bulletMassGram" => Clamp(value, BulletMassGramMin, BulletMassGramMax),
        "ballisticCoeficient" => Clamp(value, BallisticCoeficientMin, BallisticCoeficientMax),
        "misfireChance" => Clamp(value, MisfireChanceMin, MisfireChanceMax),
        "heatFactor" => Clamp(value, HeatFactorMin, HeatFactorMax),
        "durabilityBurnModificator" => Clamp(value, DurabilityBurnMin, DurabilityBurnMax),
        "lightBleedingDelta" => Clamp(value, BleedDeltaMin, BleedDeltaMax),
        "heavyBleedingDelta" => Clamp(value, BleedDeltaMin, BleedDeltaMax),
        "staminaBurnPerDamage" => Clamp(value, StaminaBurnMin, StaminaBurnMax),
        "ammoAccr" => Clamp(value, AmmoAccrMin, AmmoAccrMax),
        "ammoRec" => Clamp(value, AmmoRecMin, AmmoRecMax),
        _ => value
    };
}

using System.Text.Json.Serialization;

namespace ZSlayerCommandCenter.Models;

// ═══════════════════════════════════════════════════════
// ZOMBIE MOD DTOs — mirrors ZSlayerZombies config schema
// ═══════════════════════════════════════════════════════

public class ZombieConfigDto
{
    [JsonPropertyName("enabled")] public bool Enabled { get; set; }
    [JsonPropertyName("debug")] public bool Debug { get; set; }
    [JsonPropertyName("maps")] public ZombieMapInfection Maps { get; set; } = new();
    [JsonPropertyName("disableBosses")] public ZombieMapBool DisableBosses { get; set; } = new();
    [JsonPropertyName("zombieSettings")] public ZombieBehaviourDto ZombieSettings { get; set; } = new();
    [JsonPropertyName("infectionEffects")] public ZombieInfectionEffectsDto InfectionEffects { get; set; } = new();
    [JsonPropertyName("spawnWeights")] public Dictionary<string, ZombieSpawnWeightDto> SpawnWeights { get; set; } = new();
    [JsonPropertyName("bossZombies")] public Dictionary<string, ZombieBossDto> BossZombies { get; set; } = new();
    [JsonPropertyName("nightMode")] public ZombieNightModeDto NightMode { get; set; } = new();
    [JsonPropertyName("raidSettings")] public ZombieRaidSettingsDto RaidSettings { get; set; } = new();
    [JsonPropertyName("waveEscalation")] public ZombieWaveEscalationDto WaveEscalation { get; set; } = new();
    [JsonPropertyName("lootModifiers")] public ZombieLootModifiersDto LootModifiers { get; set; } = new();
    [JsonPropertyName("rewards")] public ZombieRewardsDto Rewards { get; set; } = new();
    [JsonPropertyName("difficultyScaling")] public ZombieDifficultyScalingDto DifficultyScaling { get; set; } = new();
    [JsonPropertyName("zombieAI")] public ZombieAIDto ZombieAI { get; set; } = new();
    [JsonPropertyName("zombieHealth")] public ZombieHealthDto ZombieHealth { get; set; } = new();
    [JsonPropertyName("spawnControl")] public ZombieSpawnControlDto SpawnControl { get; set; } = new();
    [JsonPropertyName("advancedMaps")] public Dictionary<string, ZombieAdvancedMapDto> AdvancedMaps { get; set; } = new();
}

public class ZombieMapInfection
{
    [JsonPropertyName("Labs")] public int Labs { get; set; }
    [JsonPropertyName("Customs")] public int Customs { get; set; }
    [JsonPropertyName("Factory")] public int Factory { get; set; }
    [JsonPropertyName("Interchange")] public int Interchange { get; set; }
    [JsonPropertyName("Lighthouse")] public int Lighthouse { get; set; }
    [JsonPropertyName("Reserve")] public int Reserve { get; set; }
    [JsonPropertyName("GroundZero")] public int GroundZero { get; set; }
    [JsonPropertyName("Shoreline")] public int Shoreline { get; set; }
    [JsonPropertyName("Streets")] public int Streets { get; set; }
    [JsonPropertyName("Woods")] public int Woods { get; set; }
}

public class ZombieMapBool
{
    [JsonPropertyName("Labs")] public bool Labs { get; set; }
    [JsonPropertyName("Customs")] public bool Customs { get; set; }
    [JsonPropertyName("Factory")] public bool Factory { get; set; }
    [JsonPropertyName("Interchange")] public bool Interchange { get; set; }
    [JsonPropertyName("Lighthouse")] public bool Lighthouse { get; set; }
    [JsonPropertyName("Reserve")] public bool Reserve { get; set; }
    [JsonPropertyName("GroundZero")] public bool GroundZero { get; set; }
    [JsonPropertyName("Shoreline")] public bool Shoreline { get; set; }
    [JsonPropertyName("Streets")] public bool Streets { get; set; }
    [JsonPropertyName("Woods")] public bool Woods { get; set; }
}

public class ZombieBehaviourDto
{
    [JsonPropertyName("replaceBotHostility")] public bool ReplaceBotHostility { get; set; }
    [JsonPropertyName("enableSummoning")] public bool EnableSummoning { get; set; }
    [JsonPropertyName("removeLabsKeycard")] public bool RemoveLabsKeycard { get; set; }
    [JsonPropertyName("disableNormalScavWaves")] public bool DisableNormalScavWaves { get; set; }
    [JsonPropertyName("zombieMultiplier")] public int ZombieMultiplier { get; set; }
    [JsonPropertyName("crowdsLimit")] public int CrowdsLimit { get; set; }
    [JsonPropertyName("maxCrowdAttackSpawnLimit")] public int MaxCrowdAttackSpawnLimit { get; set; }
    [JsonPropertyName("crowdCooldownPerPlayerSec")] public int CrowdCooldownPerPlayerSec { get; set; }
    [JsonPropertyName("crowdAttackBlockRadius")] public int CrowdAttackBlockRadius { get; set; }
    [JsonPropertyName("minSpawnDistToPlayer")] public int MinSpawnDistToPlayer { get; set; }
    [JsonPropertyName("targetPointSearchRadiusLimit")] public int TargetPointSearchRadiusLimit { get; set; }
    [JsonPropertyName("zombieCallDeltaRadius")] public int ZombieCallDeltaRadius { get; set; }
    [JsonPropertyName("zombieCallPeriodSec")] public int ZombieCallPeriodSec { get; set; }
    [JsonPropertyName("zombieCallRadiusLimit")] public int ZombieCallRadiusLimit { get; set; }
    [JsonPropertyName("infectedLookCoeff")] public double InfectedLookCoeff { get; set; }
    [JsonPropertyName("minInfectionPercentage")] public int MinInfectionPercentage { get; set; }
}

public class ZombieInfectionEffectsDto
{
    [JsonPropertyName("enabled")] public bool Enabled { get; set; }
    [JsonPropertyName("displayUI")] public bool DisplayUI { get; set; }
    [JsonPropertyName("zombieBleedMultiplier")] public double ZombieBleedMultiplier { get; set; }
    [JsonPropertyName("dehydrationRate")] public double DehydrationRate { get; set; }
    [JsonPropertyName("hearingDebuffPercentage")] public double HearingDebuffPercentage { get; set; }
}

public class ZombieSpawnWeightDto
{
    [JsonPropertyName("easy")] public int Easy { get; set; }
    [JsonPropertyName("normal")] public int Normal { get; set; }
    [JsonPropertyName("hard")] public int Hard { get; set; }
}

public class ZombieBossDto
{
    [JsonPropertyName("enabled")] public bool Enabled { get; set; }
    [JsonPropertyName("spawnChance")] public int SpawnChance { get; set; }
    [JsonPropertyName("maps")] public List<string> Maps { get; set; } = [];
    [JsonPropertyName("maxPerRaid")] public int MaxPerRaid { get; set; }
}

public class ZombieNightModeDto
{
    [JsonPropertyName("enabled")] public bool Enabled { get; set; }
    [JsonPropertyName("nightInfectionMultiplier")] public double NightInfectionMultiplier { get; set; }
    [JsonPropertyName("forceNightWeather")] public bool ForceNightWeather { get; set; }
    [JsonPropertyName("nightBossChanceMultiplier")] public double NightBossChanceMultiplier { get; set; }
}

public class ZombieRaidSettingsDto
{
    [JsonPropertyName("extendRaidTime")] public bool ExtendRaidTime { get; set; }
    [JsonPropertyName("raidTimeMultiplier")] public double RaidTimeMultiplier { get; set; }
    [JsonPropertyName("scavCooldownMultiplier")] public double ScavCooldownMultiplier { get; set; }
    [JsonPropertyName("scavsAffected")] public bool ScavsAffected { get; set; }
    [JsonPropertyName("forceWeather")] public string ForceWeather { get; set; } = "none";
    [JsonPropertyName("forceSeason")] public string ForceSeason { get; set; } = "none";
}

public class ZombieWaveEscalationDto
{
    [JsonPropertyName("enabled")] public bool Enabled { get; set; }
    [JsonPropertyName("startInfectionPercent")] public int StartInfectionPercent { get; set; }
    [JsonPropertyName("endInfectionPercent")] public int EndInfectionPercent { get; set; }
    [JsonPropertyName("escalationCurve")] public string EscalationCurve { get; set; } = "linear";
    [JsonPropertyName("hordeEventEnabled")] public bool HordeEventEnabled { get; set; }
    [JsonPropertyName("hordeIntervalMinutes")] public int HordeIntervalMinutes { get; set; }
    [JsonPropertyName("hordeMultiplier")] public double HordeMultiplier { get; set; }
}

public class ZombieLootModifiersDto
{
    [JsonPropertyName("enabled")] public bool Enabled { get; set; }
    [JsonPropertyName("globalLootMultiplier")] public double GlobalLootMultiplier { get; set; }
    [JsonPropertyName("medicalLootMultiplier")] public double MedicalLootMultiplier { get; set; }
    [JsonPropertyName("ammoLootMultiplier")] public double AmmoLootMultiplier { get; set; }
    [JsonPropertyName("valuableLootMultiplier")] public double ValuableLootMultiplier { get; set; }
    [JsonPropertyName("looseLootMultiplier")] public double LooseLootMultiplier { get; set; }
    [JsonPropertyName("containerLootMultiplier")] public double ContainerLootMultiplier { get; set; }
}

public class ZombieRewardsDto
{
    [JsonPropertyName("zombieKillXpMultiplier")] public double ZombieKillXpMultiplier { get; set; }
    [JsonPropertyName("survivalBonusXp")] public int SurvivalBonusXp { get; set; }
    [JsonPropertyName("raidXpMultiplier")] public double RaidXpMultiplier { get; set; }
}

public class ZombieDifficultyScalingDto
{
    [JsonPropertyName("enabled")] public bool Enabled { get; set; }
    [JsonPropertyName("scalingMode")] public string ScalingMode { get; set; } = "playerCount";
    [JsonPropertyName("playerCountInfectionScale")] public Dictionary<string, double> PlayerCountInfectionScale { get; set; } = new();
    [JsonPropertyName("levelScaling")] public ZombieLevelScalingDto LevelScaling { get; set; } = new();
}

public class ZombieLevelScalingDto
{
    [JsonPropertyName("minLevel")] public int MinLevel { get; set; }
    [JsonPropertyName("maxLevel")] public int MaxLevel { get; set; }
    [JsonPropertyName("minMultiplier")] public double MinMultiplier { get; set; }
    [JsonPropertyName("maxMultiplier")] public double MaxMultiplier { get; set; }
}

public class ZombieAdvancedMapDto
{
    [JsonPropertyName("zombieMultiplier")] public int? ZombieMultiplier { get; set; }
    [JsonPropertyName("crowdsLimit")] public int? CrowdsLimit { get; set; }
    [JsonPropertyName("maxCrowdAttackSpawnLimit")] public int? MaxCrowdAttackSpawnLimit { get; set; }
    [JsonPropertyName("crowdCooldownPerPlayerSec")] public int? CrowdCooldownPerPlayerSec { get; set; }
    [JsonPropertyName("crowdAttackBlockRadius")] public int? CrowdAttackBlockRadius { get; set; }
    [JsonPropertyName("minSpawnDistToPlayer")] public int? MinSpawnDistToPlayer { get; set; }
    [JsonPropertyName("targetPointSearchRadiusLimit")] public int? TargetPointSearchRadiusLimit { get; set; }
    [JsonPropertyName("zombieCallDeltaRadius")] public int? ZombieCallDeltaRadius { get; set; }
    [JsonPropertyName("zombieCallPeriodSec")] public int? ZombieCallPeriodSec { get; set; }
    [JsonPropertyName("zombieCallRadiusLimit")] public int? ZombieCallRadiusLimit { get; set; }
    [JsonPropertyName("infectedLookCoeff")] public double? InfectedLookCoeff { get; set; }
    [JsonPropertyName("minInfectionPercentage")] public int? MinInfectionPercentage { get; set; }
    [JsonPropertyName("lootModifiers")] public ZombieLootModifiersDto? LootModifiers { get; set; }
}

// ═══════════════════════════════════════════════════════
// ZOMBIE AI DTOs
// ═══════════════════════════════════════════════════════

public class ZombieDifficultyFloatDto
{
    [JsonPropertyName("easy")] public float Easy { get; set; }
    [JsonPropertyName("normal")] public float Normal { get; set; }
    [JsonPropertyName("hard")] public float Hard { get; set; }
}

public class ZombieAIDto
{
    [JsonPropertyName("enabled")] public bool Enabled { get; set; }
    [JsonPropertyName("sightRange")] public ZombieDifficultyFloatDto SightRange { get; set; } = new();
    [JsonPropertyName("fieldOfView")] public ZombieDifficultyFloatDto FieldOfView { get; set; } = new();
    [JsonPropertyName("hearingSensitivity")] public ZombieDifficultyFloatDto HearingSensitivity { get; set; } = new();
    [JsonPropertyName("hearingChance")] public ZombieDifficultyFloatDto HearingChance { get; set; } = new();
    [JsonPropertyName("aggressionChance")] public ZombieDifficultyFloatDto AggressionChance { get; set; } = new();
    [JsonPropertyName("rotateSpeed")] public float RotateSpeed { get; set; }
    [JsonPropertyName("reactionTime")] public ZombieDifficultyFloatDto ReactionTime { get; set; } = new();
    [JsonPropertyName("scatteringPerMeter")] public ZombieDifficultyFloatDto ScatteringPerMeter { get; set; } = new();
    [JsonPropertyName("affectedTypes")] public List<string> AffectedTypes { get; set; } = [];
}

// ═══════════════════════════════════════════════════════
// ZOMBIE HEALTH DTOs
// ═══════════════════════════════════════════════════════

public class ZombieBodyPartHealthDto
{
    [JsonPropertyName("head")] public double Head { get; set; }
    [JsonPropertyName("chest")] public double Chest { get; set; }
    [JsonPropertyName("stomach")] public double Stomach { get; set; }
    [JsonPropertyName("arms")] public double Arms { get; set; }
    [JsonPropertyName("legs")] public double Legs { get; set; }
}

public class ZombieHealthDto
{
    [JsonPropertyName("enabled")] public bool Enabled { get; set; }
    [JsonPropertyName("standard")] public ZombieBodyPartHealthDto Standard { get; set; } = new();
    [JsonPropertyName("tagilla")] public ZombieBodyPartHealthDto Tagilla { get; set; } = new();
    [JsonPropertyName("cursedAssault")] public ZombieBodyPartHealthDto CursedAssault { get; set; } = new();
    [JsonPropertyName("standardTypes")] public List<string> StandardTypes { get; set; } = [];
}

// ═══════════════════════════════════════════════════════
// SPAWN CONTROL DTOs
// ═══════════════════════════════════════════════════════

public class ZombieSpawnControlDto
{
    [JsonPropertyName("overrideBotCaps")] public bool OverrideBotCaps { get; set; }
    [JsonPropertyName("maxBotCap")] public Dictionary<string, int> MaxBotCap { get; set; } = new();
    [JsonPropertyName("maxBotsPerZone")] public int MaxBotsPerZone { get; set; }
    [JsonPropertyName("ignoreMaxBots")] public bool IgnoreMaxBots { get; set; }
    [JsonPropertyName("forceSpawn")] public bool ForceSpawn { get; set; }
    [JsonPropertyName("injectBossZombies")] public bool InjectBossZombies { get; set; }
    [JsonPropertyName("enableExtraWaves")] public bool EnableExtraWaves { get; set; }
    [JsonPropertyName("extraWavesPerMap")] public int ExtraWavesPerMap { get; set; }
    [JsonPropertyName("zombiesPerWave")] public int ZombiesPerWave { get; set; }
    [JsonPropertyName("waveSpawnChance")] public int WaveSpawnChance { get; set; }
    [JsonPropertyName("waveDifficulty")] public string WaveDifficulty { get; set; } = "normal";
    [JsonPropertyName("mapWaveOverrides")] public Dictionary<string, ZombieMapWaveOverrideDto> MapWaveOverrides { get; set; } = new();
}

public class ZombieMapWaveOverrideDto
{
    [JsonPropertyName("extraWaves")] public int? ExtraWaves { get; set; }
    [JsonPropertyName("zombiesPerWave")] public int? ZombiesPerWave { get; set; }
    [JsonPropertyName("waveSpawnChance")] public int? WaveSpawnChance { get; set; }
    [JsonPropertyName("maxBotsPerZone")] public int? MaxBotsPerZone { get; set; }
}

// ═══════════════════════════════════════════════════════
// CC INTEGRATION DTOs
// ═══════════════════════════════════════════════════════

public class ZombieStatusDto
{
    [JsonPropertyName("enabled")] public bool Enabled { get; set; }
    [JsonPropertyName("version")] public string Version { get; set; } = "";
    [JsonPropertyName("activeMaps")] public List<string> ActiveMaps { get; set; } = [];
    [JsonPropertyName("mapInfection")] public Dictionary<string, int> MapInfection { get; set; } = new();
    [JsonPropertyName("bossZombiesActive")] public List<string> BossZombiesActive { get; set; } = [];
    [JsonPropertyName("nightModeEnabled")] public bool NightModeEnabled { get; set; }
    [JsonPropertyName("waveEscalationEnabled")] public bool WaveEscalationEnabled { get; set; }
    [JsonPropertyName("lootModifiersEnabled")] public bool LootModifiersEnabled { get; set; }
    [JsonPropertyName("difficultyScalingEnabled")] public bool DifficultyScalingEnabled { get; set; }
}

public class ZombieDetectionDto
{
    [JsonPropertyName("detected")] public bool Detected { get; set; }
    [JsonPropertyName("version")] public string Version { get; set; } = "";
    [JsonPropertyName("config")] public ZombieConfigDto? Config { get; set; }
    [JsonPropertyName("status")] public ZombieStatusDto? Status { get; set; }
}

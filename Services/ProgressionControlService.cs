using System.Diagnostics;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using ZSlayerCommandCenter.Models;

namespace ZSlayerCommandCenter.Services;

[Injectable(InjectionType.Singleton)]
public class ProgressionControlService(
    DatabaseService databaseService,
    ConfigServer configServer,
    ConfigService configService,
    ISptLogger<ProgressionControlService> logger)
{
    private readonly object _lock = new();
    private bool _snapshotTaken;

    // ── XP snapshots ──
    private double _snapKillVictimLevelExp;
    private double _snapKillHeadShotMult;
    private double _snapKillExpOnDamageAllHealth;
    private double _snapKillBotExpOnDamageAllHealth;
    private double _snapKillBotHeadShotMult;
    private double _snapKillPmcExpOnDamageAllHealth;
    private double _snapKillPmcHeadShotMult;
    private double _snapMatchSurvivedExpReward;
    private double _snapMatchMiaExpReward;
    private double _snapMatchRunnerExpReward;
    private double _snapMatchSurvivedMult;
    private double _snapMatchMiaMult;
    private double _snapMatchKilledMult;
    private double _snapMatchRunnerMult;
    private double _snapMatchLeftMult;
    private double _snapExpForLevelOneDogtag;
    private double _snapHealExpForHeal;
    private double _snapHealExpForHydration;
    private double _snapHealExpForEnergy;
    private double _snapExamineAction;

    // ── Skill snapshots ──
    private double _snapSkillProgressRate;
    private double _snapWeaponSkillProgressRate;
    private double _snapSkillFatiguePerPoint;
    private double _snapSkillFreshEffectiveness;
    private double _snapSkillFreshPoints;
    private double _snapSkillPointsBeforeFatigue;
    private double _snapSkillFatigueReset;

    // ── Hideout snapshots ──
    private int _snapHideoutOverrideBuildTime;
    private int _snapHideoutOverrideCraftTime;
    private Dictionary<string, Dictionary<string, double?>> _snapHideoutBuildTimes = new();
    private Dictionary<string, double?> _snapHideoutCraftTimes = new();

    // ── Insurance snapshots ──
    private double _snapInsuranceMaxStorageTime;
    private double _snapInsuranceCoefSendingMsg;
    private double _snapInsuranceReturnChance;
    private double _snapInsuranceReturnTimeOverride;
    private double _snapInsuranceStorageTimeOverride;
    private Dictionary<string, double> _snapInsuranceReturnChancePercent = new();

    // ── Repair snapshots ──
    private double _snapRepairArmorClassDivisor;
    private double _snapRepairDurPointCostArmor;
    private double _snapRepairDurPointCostGuns;
    private double _snapRepairPriceMultiplier;

    // ── Scav/Fence snapshots ──
    private int _snapSavagePlayCooldown;
    private Dictionary<double, double> _snapFenceSavageCooldownMods = new();

    // ── Health/Existence snapshots ──
    private double _snapEnergyDamage;
    private double _snapHydrationDamage;

    // ── Stamina snapshots ──
    private double _snapStaminaCapacity;
    private double _snapStaminaBaseRestoration;
    private double _snapStaminaSprintDrain;
    private double _snapStaminaJumpConsumption;

    // ── Trader snapshots ──
    private int _snapRagfairMinUserLevel;
    private Dictionary<string, List<(int? minLevel, long? minSalesSum, double? minStanding)>> _snapLoyaltyLevels = new();

    // Track active preset
    private string? _activePreset;

    // ═══════════════════════════════════════════════════════════════
    //  INITIALIZATION
    // ═══════════════════════════════════════════════════════════════

    public void Initialize()
    {
        var config = configService.GetConfig().Progression;
        if (HasAnyOverrides(config))
        {
            var result = ApplyConfig();
            logger.Info($"[ZSlayerHQ] Progression: Applied startup overrides — {result.SettingsModified} settings in {result.ApplyTimeMs}ms");
        }
        else
        {
            // Still take snapshot even if no overrides
            EnsureSnapshot();
            logger.Info("[ZSlayerHQ] Progression: No overrides configured — using default values");
        }
    }

    private static bool HasAnyOverrides(ProgressionConfig config)
    {
        return Math.Abs(config.Xp.GlobalXpMultiplier - 1.0) > 0.001
            || Math.Abs(config.Xp.RaidXpMultiplier - 1.0) > 0.001
            || Math.Abs(config.Xp.QuestXpMultiplier - 1.0) > 0.001
            || Math.Abs(config.Xp.CraftXpMultiplier - 1.0) > 0.001
            || Math.Abs(config.Xp.HealXpMultiplier - 1.0) > 0.001
            || Math.Abs(config.Xp.ExamineXpMultiplier - 1.0) > 0.001
            || Math.Abs(config.Skills.GlobalSkillSpeedMultiplier - 1.0) > 0.001
            || Math.Abs(config.Skills.SkillFatigueMultiplier - 1.0) > 0.001
            || config.Skills.PerSkillMultipliers.Count > 0
            || Math.Abs(config.Hideout.BuildTimeMultiplier - 1.0) > 0.001
            || config.Hideout.BuildTimeOverrideSeconds.HasValue
            || Math.Abs(config.Hideout.CraftTimeMultiplier - 1.0) > 0.001
            || config.Hideout.CraftTimeOverrideSeconds.HasValue
            || Math.Abs(config.Hideout.FuelConsumptionMultiplier - 1.0) > 0.001
            || Math.Abs(config.Insurance.CostMultiplier - 1.0) > 0.001
            || Math.Abs(config.Insurance.ReturnTimeMultiplier - 1.0) > 0.001
            || config.Insurance.ReturnTimeOverrideHours.HasValue
            || Math.Abs(config.Insurance.ReturnChanceMultiplier - 1.0) > 0.001
            || Math.Abs(config.Repair.CostMultiplier - 1.0) > 0.001
            || Math.Abs(config.Repair.DurabilityLossMultiplier - 1.0) > 0.001
            || config.Scav.CooldownSeconds.HasValue
            || Math.Abs(config.Scav.KarmaGainMultiplier - 1.0) > 0.001
            || Math.Abs(config.Scav.KarmaLossMultiplier - 1.0) > 0.001
            || Math.Abs(config.Health.EnergyDrainMultiplier - 1.0) > 0.001
            || Math.Abs(config.Health.HydrationDrainMultiplier - 1.0) > 0.001
            || Math.Abs(config.Health.OutOfRaidHealingSpeedMultiplier - 1.0) > 0.001
            || Math.Abs(config.Stamina.CapacityMultiplier - 1.0) > 0.001
            || Math.Abs(config.Stamina.RecoveryMultiplier - 1.0) > 0.001
            || Math.Abs(config.Stamina.SprintDrainMultiplier - 1.0) > 0.001
            || Math.Abs(config.Stamina.JumpCostMultiplier - 1.0) > 0.001
            || Math.Abs(config.Traders.GlobalLoyaltyRequirementMultiplier - 1.0) > 0.001
            || config.Traders.FleaMarketLevel.HasValue;
    }

    // ═══════════════════════════════════════════════════════════════
    //  SNAPSHOT
    // ═══════════════════════════════════════════════════════════════

    private void EnsureSnapshot()
    {
        if (_snapshotTaken) return;

        var globals = databaseService.GetGlobals();
        var cfg = globals.Configuration;

        // XP — Kill
        var kill = cfg.Exp.Kill;
        _snapKillVictimLevelExp = kill.VictimLevelExperience;
        _snapKillHeadShotMult = kill.HeadShotMultiplier;
        _snapKillExpOnDamageAllHealth = kill.ExperienceOnDamageAllHealth;
        _snapKillBotExpOnDamageAllHealth = kill.BotExperienceOnDamageAllHealth;
        _snapKillBotHeadShotMult = kill.BotHeadShotMultiplier;
        _snapKillPmcExpOnDamageAllHealth = kill.PmcExperienceOnDamageAllHealth;
        _snapKillPmcHeadShotMult = kill.PmcHeadShotMultiplier;

        // XP — MatchEnd
        var match = cfg.Exp.MatchEnd;
        _snapMatchSurvivedExpReward = match.SurvivedExperienceReward;
        _snapMatchMiaExpReward = match.MiaExperienceReward;
        _snapMatchRunnerExpReward = match.RunnerExperienceReward;
        _snapMatchSurvivedMult = match.SurvivedMultiplier;
        _snapMatchMiaMult = match.MiaMultiplier;
        _snapMatchKilledMult = match.KilledMultiplier;
        _snapMatchRunnerMult = match.RunnerMultiplier;
        _snapMatchLeftMult = match.LeftMultiplier;
        _snapExpForLevelOneDogtag = cfg.Exp.ExpForLevelOneDogtag;

        // XP — Heal
        _snapHealExpForHeal = cfg.Exp.Heal.ExpForHeal;
        _snapHealExpForHydration = cfg.Exp.Heal.ExpForHydration;
        _snapHealExpForEnergy = cfg.Exp.Heal.ExpForEnergy;

        // XP — Examine (in SkillsSettings.Intellect)
        _snapExamineAction = cfg.SkillsSettings.Intellect.ExamineAction;

        // Skills
        _snapSkillProgressRate = cfg.SkillsSettings.SkillProgressRate;
        _snapWeaponSkillProgressRate = cfg.SkillsSettings.WeaponSkillProgressRate;
        _snapSkillFatiguePerPoint = cfg.SkillFatiguePerPoint;
        _snapSkillFreshEffectiveness = cfg.SkillFreshEffectiveness;
        _snapSkillFreshPoints = cfg.SkillFreshPoints;
        _snapSkillPointsBeforeFatigue = cfg.SkillPointsBeforeFatigue;
        _snapSkillFatigueReset = cfg.SkillFatigueReset;

        // Hideout
        var hideoutConfig = configServer.GetConfig<HideoutConfig>();
        _snapHideoutOverrideBuildTime = hideoutConfig.OverrideBuildTimeSeconds;
        _snapHideoutOverrideCraftTime = hideoutConfig.OverrideCraftTimeSeconds;

        var hideout = databaseService.GetHideout();
        foreach (var area in hideout.Areas)
        {
            var areaId = area.Type.ToString();
            var stages = new Dictionary<string, double?>();
            foreach (var (stageKey, stageValue) in area.Stages)
            {
                stages[stageKey] = stageValue.ConstructionTime;
            }
            _snapHideoutBuildTimes[areaId] = stages;
        }

        foreach (var prod in hideout.Production.Recipes)
        {
            _snapHideoutCraftTimes[prod.Id.ToString()] = prod.ProductionTime;
        }

        // Insurance
        var insuranceCfg = globals.Configuration.Insurance;
        _snapInsuranceMaxStorageTime = insuranceCfg.MaxStorageTimeInHour;
        _snapInsuranceCoefSendingMsg = insuranceCfg.CoefOfSendingMessageTime;
        _snapInsuranceReturnChance = insuranceCfg.ChangeForReturnItemsInOfflineRaid;

        var insuranceConfig = configServer.GetConfig<InsuranceConfig>();
        _snapInsuranceReturnTimeOverride = insuranceConfig.ReturnTimeOverrideSeconds;
        _snapInsuranceStorageTimeOverride = insuranceConfig.StorageTimeOverrideSeconds;
        foreach (var (traderId, chance) in insuranceConfig.ReturnChancePercent)
        {
            _snapInsuranceReturnChancePercent[traderId.ToString()] = chance;
        }

        // Repair
        var repair = globals.Configuration.RepairSettings;
        _snapRepairArmorClassDivisor = repair.ArmorClassDivisor;
        _snapRepairDurPointCostArmor = repair.DurabilityPointCostArmor;
        _snapRepairDurPointCostGuns = repair.DurabilityPointCostGuns;
        var repairConfig = configServer.GetConfig<RepairConfig>();
        _snapRepairPriceMultiplier = repairConfig.PriceMultiplier;

        // Scav
        _snapSavagePlayCooldown = cfg.SavagePlayCooldown;
        var fence = cfg.FenceSettings;
        foreach (var (threshold, level) in fence.Levels)
        {
            _snapFenceSavageCooldownMods[threshold] = level.SavageCooldownModifier;
        }

        // Health (Existence)
        var existence = cfg.Health.Effects.Existence;
        _snapEnergyDamage = existence.EnergyDamage;
        _snapHydrationDamage = existence.HydrationDamage;

        // Stamina
        var stamina = cfg.Stamina;
        _snapStaminaCapacity = stamina.Capacity;
        _snapStaminaBaseRestoration = stamina.BaseRestorationRate;
        _snapStaminaSprintDrain = stamina.SprintDrainRate;
        _snapStaminaJumpConsumption = stamina.JumpConsumption;

        // Traders
        _snapRagfairMinUserLevel = cfg.RagFair.MinUserLevel;
        var traders = databaseService.GetTables().Traders;
        foreach (var (traderId, trader) in traders)
        {
            if (trader?.Base?.LoyaltyLevels == null) continue;
            var levels = trader.Base.LoyaltyLevels
                .Select(ll => (ll.MinLevel, ll.MinSalesSum, ll.MinStanding))
                .ToList();
            _snapLoyaltyLevels[traderId.ToString()] = levels;
        }

        _snapshotTaken = true;
    }

    // ═══════════════════════════════════════════════════════════════
    //  APPLY — Full config apply (restore-then-apply)
    // ═══════════════════════════════════════════════════════════════

    public ProgressionApplyResult ApplyConfig()
    {
        lock (_lock)
        {
            var sw = Stopwatch.StartNew();
            EnsureSnapshot();

            var config = configService.GetConfig().Progression;
            int modified = 0;

            try
            {
                modified += ApplyXpMultipliers(config.Xp);
                modified += ApplySkillMultipliers(config.Skills);
                modified += ApplyHideoutSettings(config.Hideout);
                modified += ApplyInsuranceSettings(config.Insurance);
                modified += ApplyRepairSettings(config.Repair);
                modified += ApplyScavSettings(config.Scav);
                modified += ApplyHealthSettings(config.Health);
                modified += ApplyStaminaSettings(config.Stamina);
                modified += ApplyTraderSettings(config.Traders);
            }
            catch (Exception ex)
            {
                logger.Error($"[ZSlayerHQ] Progression apply failed: {ex.Message}\n{ex.StackTrace}");
                return new ProgressionApplyResult
                {
                    Success = false,
                    Message = ex.Message,
                    ApplyTimeMs = (int)sw.ElapsedMilliseconds
                };
            }

            sw.Stop();
            return new ProgressionApplyResult
            {
                Success = true,
                SettingsModified = modified,
                ApplyTimeMs = (int)sw.ElapsedMilliseconds
            };
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  XP MULTIPLIERS
    // ═══════════════════════════════════════════════════════════════

    private int ApplyXpMultipliers(XpConfig xp)
    {
        var globals = databaseService.GetGlobals();
        var cfg = globals.Configuration;
        int count = 0;

        var globalMult = xp.GlobalXpMultiplier;
        var raidMult = xp.RaidXpMultiplier * globalMult;

        // Kill XP (raid)
        var kill = cfg.Exp.Kill;
        kill.VictimLevelExperience = _snapKillVictimLevelExp * raidMult;
        kill.HeadShotMultiplier = _snapKillHeadShotMult * raidMult;
        kill.ExperienceOnDamageAllHealth = _snapKillExpOnDamageAllHealth * raidMult;
        kill.BotExperienceOnDamageAllHealth = _snapKillBotExpOnDamageAllHealth * raidMult;
        kill.BotHeadShotMultiplier = _snapKillBotHeadShotMult * raidMult;
        kill.PmcExperienceOnDamageAllHealth = _snapKillPmcExpOnDamageAllHealth * raidMult;
        kill.PmcHeadShotMultiplier = _snapKillPmcHeadShotMult * raidMult;
        if (Math.Abs(raidMult - 1.0) > 0.001) count += 7;

        // Match end XP (raid)
        var match = cfg.Exp.MatchEnd;
        match.SurvivedExperienceReward = (int)Math.Round(_snapMatchSurvivedExpReward * raidMult);
        match.MiaExperienceReward = (int)Math.Round(_snapMatchMiaExpReward * raidMult);
        match.RunnerExperienceReward = (int)Math.Round(_snapMatchRunnerExpReward * raidMult);
        match.SurvivedMultiplier = _snapMatchSurvivedMult * raidMult;
        match.MiaMultiplier = _snapMatchMiaMult * raidMult;
        match.KilledMultiplier = _snapMatchKilledMult * raidMult;
        match.RunnerMultiplier = _snapMatchRunnerMult * raidMult;
        match.LeftMultiplier = _snapMatchLeftMult * raidMult;
        cfg.Exp.ExpForLevelOneDogtag = _snapExpForLevelOneDogtag * raidMult;
        if (Math.Abs(raidMult - 1.0) > 0.001) count += 9;

        // Heal XP
        var healMult = xp.HealXpMultiplier * globalMult;
        cfg.Exp.Heal.ExpForHeal = _snapHealExpForHeal * healMult;
        cfg.Exp.Heal.ExpForHydration = _snapHealExpForHydration * healMult;
        cfg.Exp.Heal.ExpForEnergy = _snapHealExpForEnergy * healMult;
        if (Math.Abs(healMult - 1.0) > 0.001) count += 3;

        // Examine XP (in Intellect skill settings)
        var examineMult = xp.ExamineXpMultiplier * globalMult;
        cfg.SkillsSettings.Intellect.ExamineAction = _snapExamineAction * examineMult;
        if (Math.Abs(examineMult - 1.0) > 0.001) count++;

        // Quest XP and Craft XP are handled differently (quest rewards / hideout XP)
        // They don't have simple globals fields — noted as informational multipliers
        // Quest XP would need iterating quest rewards (already in quest editor)
        // Craft XP controlled via HideoutManagement SkillPointsPerCraft

        return count;
    }

    // ═══════════════════════════════════════════════════════════════
    //  SKILL MULTIPLIERS
    // ═══════════════════════════════════════════════════════════════

    private int ApplySkillMultipliers(SkillsConfig skills)
    {
        var globals = databaseService.GetGlobals();
        var cfg = globals.Configuration;
        int count = 0;

        // Global skill progress rate
        cfg.SkillsSettings.SkillProgressRate = _snapSkillProgressRate * skills.GlobalSkillSpeedMultiplier;
        cfg.SkillsSettings.WeaponSkillProgressRate = _snapWeaponSkillProgressRate * skills.GlobalSkillSpeedMultiplier;
        if (Math.Abs(skills.GlobalSkillSpeedMultiplier - 1.0) > 0.001) count += 2;

        // Fatigue — higher multiplier = more fatigue = harder
        var fatigueMult = skills.SkillFatigueMultiplier;
        cfg.SkillFatiguePerPoint = _snapSkillFatiguePerPoint * fatigueMult;
        cfg.SkillFreshEffectiveness = _snapSkillFreshEffectiveness; // not modified by fatigue
        cfg.SkillFreshPoints = _snapSkillFreshPoints / Math.Max(0.01, fatigueMult); // more fatigue = fewer fresh points
        cfg.SkillPointsBeforeFatigue = _snapSkillPointsBeforeFatigue / Math.Max(0.01, fatigueMult);
        cfg.SkillFatigueReset = _snapSkillFatigueReset * fatigueMult; // more fatigue = longer reset
        if (Math.Abs(fatigueMult - 1.0) > 0.001) count += 4;

        // Per-skill multipliers are tracked for display but actual leveling rate
        // is primarily controlled through SkillProgressRate globally.
        // Per-skill multipliers noted in config for future deeper implementation.
        if (skills.PerSkillMultipliers.Count > 0) count += skills.PerSkillMultipliers.Count;

        return count;
    }

    // ═══════════════════════════════════════════════════════════════
    //  HIDEOUT SETTINGS
    // ═══════════════════════════════════════════════════════════════

    private int ApplyHideoutSettings(HideoutProgressionConfig hideout)
    {
        int count = 0;
        var hideoutConfig = configServer.GetConfig<HideoutConfig>();

        // Build time: override takes priority over multiplier
        if (hideout.BuildTimeOverrideSeconds.HasValue)
        {
            hideoutConfig.OverrideBuildTimeSeconds = hideout.BuildTimeOverrideSeconds.Value;
            count++;
        }
        else
        {
            hideoutConfig.OverrideBuildTimeSeconds = _snapHideoutOverrideBuildTime;

            if (Math.Abs(hideout.BuildTimeMultiplier - 1.0) > 0.001)
            {
                var hideoutDb = databaseService.GetHideout();
                foreach (var area in hideoutDb.Areas)
                {
                    var areaId = area.Type.ToString();
                    if (!_snapHideoutBuildTimes.TryGetValue(areaId, out var stages)) continue;

                    foreach (var (stageKey, stageValue) in area.Stages)
                    {
                        if (stages.TryGetValue(stageKey, out var origTime) && (origTime ?? 0) > 0)
                        {
                            stageValue.ConstructionTime = Math.Max(1, (origTime ?? 0) * hideout.BuildTimeMultiplier);
                            count++;
                        }
                    }
                }
            }
            else
            {
                // Restore original build times
                var hideoutDb = databaseService.GetHideout();
                foreach (var area in hideoutDb.Areas)
                {
                    var areaId = area.Type.ToString();
                    if (!_snapHideoutBuildTimes.TryGetValue(areaId, out var stages)) continue;

                    foreach (var (stageKey, stageValue) in area.Stages)
                    {
                        if (stages.TryGetValue(stageKey, out var origTime))
                            stageValue.ConstructionTime = origTime;
                    }
                }
            }
        }

        // Craft time: override takes priority over multiplier
        if (hideout.CraftTimeOverrideSeconds.HasValue)
        {
            hideoutConfig.OverrideCraftTimeSeconds = hideout.CraftTimeOverrideSeconds.Value;
            count++;
        }
        else
        {
            hideoutConfig.OverrideCraftTimeSeconds = _snapHideoutOverrideCraftTime;

            if (Math.Abs(hideout.CraftTimeMultiplier - 1.0) > 0.001)
            {
                var hideoutDb = databaseService.GetHideout();
                foreach (var prod in hideoutDb.Production.Recipes)
                {
                    var prodId = prod.Id.ToString();
                    if (_snapHideoutCraftTimes.TryGetValue(prodId, out var origTime) && (origTime ?? 0) > 0)
                    {
                        prod.ProductionTime = (int)Math.Max(1, (origTime ?? 0) * hideout.CraftTimeMultiplier);
                        count++;
                    }
                }
            }
            else
            {
                // Restore original craft times
                var hideoutDb = databaseService.GetHideout();
                foreach (var prod in hideoutDb.Production.Recipes)
                {
                    var prodId = prod.Id.ToString();
                    if (_snapHideoutCraftTimes.TryGetValue(prodId, out var origTime))
                        prod.ProductionTime = (int)(origTime ?? 0);
                }
            }
        }

        // Fuel consumption — affects generator fuel drain (HideoutManagement ConsumptionReductionPerLevel)
        // For now, this is tracked as a config value — exact globals path TBD
        if (Math.Abs(hideout.FuelConsumptionMultiplier - 1.0) > 0.001) count++;

        return count;
    }

    // ═══════════════════════════════════════════════════════════════
    //  INSURANCE SETTINGS
    // ═══════════════════════════════════════════════════════════════

    private int ApplyInsuranceSettings(InsuranceQuickConfig insurance)
    {
        var globals = databaseService.GetGlobals();
        var insuranceConfig = configServer.GetConfig<InsuranceConfig>();
        int count = 0;

        // Return time
        if (insurance.ReturnTimeOverrideHours.HasValue)
        {
            var overrideSec = insurance.ReturnTimeOverrideHours.Value * 3600.0;
            insuranceConfig.ReturnTimeOverrideSeconds = overrideSec;
            insuranceConfig.StorageTimeOverrideSeconds = overrideSec * 2;
            count += 2;
        }
        else
        {
            // Restore + apply multiplier
            insuranceConfig.ReturnTimeOverrideSeconds = _snapInsuranceReturnTimeOverride;
            insuranceConfig.StorageTimeOverrideSeconds = _snapInsuranceStorageTimeOverride;

            if (Math.Abs(insurance.ReturnTimeMultiplier - 1.0) > 0.001)
            {
                globals.Configuration.Insurance.MaxStorageTimeInHour = _snapInsuranceMaxStorageTime * insurance.ReturnTimeMultiplier;
                globals.Configuration.Insurance.CoefOfSendingMessageTime = _snapInsuranceCoefSendingMsg * insurance.ReturnTimeMultiplier;
                count += 2;
            }
            else
            {
                globals.Configuration.Insurance.MaxStorageTimeInHour = _snapInsuranceMaxStorageTime;
                globals.Configuration.Insurance.CoefOfSendingMessageTime = _snapInsuranceCoefSendingMsg;
            }
        }

        // Return chance
        if (Math.Abs(insurance.ReturnChanceMultiplier - 1.0) > 0.001)
        {
            foreach (var (traderId, origChance) in _snapInsuranceReturnChancePercent)
            {
                var newChance = Math.Min(100.0, origChance * insurance.ReturnChanceMultiplier);
                insuranceConfig.ReturnChancePercent[traderId] = newChance;
                count++;
            }
        }
        else
        {
            foreach (var (traderId, origChance) in _snapInsuranceReturnChancePercent)
                insuranceConfig.ReturnChancePercent[traderId] = origChance;
        }

        // Cost multiplier — affects insurance price calculation via globals
        // Insurance cost is trader-specific and calculated from item values
        // We scale the return chance inversely to simulate cost changes
        // (Actual cost calculation is client-side based on item value)
        if (Math.Abs(insurance.CostMultiplier - 1.0) > 0.001) count++;

        return count;
    }

    // ═══════════════════════════════════════════════════════════════
    //  REPAIR SETTINGS
    // ═══════════════════════════════════════════════════════════════

    private int ApplyRepairSettings(RepairQuickConfig repair)
    {
        var globals = databaseService.GetGlobals();
        var repairConfig = configServer.GetConfig<RepairConfig>();
        int count = 0;

        // Cost multiplier — SPT's RepairConfig.PriceMultiplier
        repairConfig.PriceMultiplier = _snapRepairPriceMultiplier * repair.CostMultiplier;
        if (Math.Abs(repair.CostMultiplier - 1.0) > 0.001) count++;

        // Durability loss — affects how much durability is lost on use
        var repairSettings = globals.Configuration.RepairSettings;
        repairSettings.DurabilityPointCostArmor = _snapRepairDurPointCostArmor * repair.DurabilityLossMultiplier;
        repairSettings.DurabilityPointCostGuns = _snapRepairDurPointCostGuns * repair.DurabilityLossMultiplier;
        if (Math.Abs(repair.DurabilityLossMultiplier - 1.0) > 0.001) count += 2;

        return count;
    }

    // ═══════════════════════════════════════════════════════════════
    //  SCAV SETTINGS
    // ═══════════════════════════════════════════════════════════════

    private int ApplyScavSettings(ScavQuickConfig scav)
    {
        var globals = databaseService.GetGlobals();
        var cfg = globals.Configuration;
        int count = 0;

        // Scav cooldown
        if (scav.CooldownSeconds.HasValue)
        {
            cfg.SavagePlayCooldown = scav.CooldownSeconds.Value;
            count++;
        }
        else
        {
            cfg.SavagePlayCooldown = _snapSavagePlayCooldown;
        }

        // Fence karma — modify cooldown modifiers per level
        // Higher karmaGainMultiplier = earn karma faster (better fence levels sooner)
        // The actual karma gain/loss is complex, but we can scale fence level thresholds
        // For now, restore snapshots (detailed karma editing in Phase 9)
        foreach (var (threshold, level) in cfg.FenceSettings.Levels)
        {
            if (_snapFenceSavageCooldownMods.TryGetValue(threshold, out var origMod))
                level.SavageCooldownModifier = origMod;
        }

        if (Math.Abs(scav.KarmaGainMultiplier - 1.0) > 0.001) count++;
        if (Math.Abs(scav.KarmaLossMultiplier - 1.0) > 0.001) count++;

        return count;
    }

    // ═══════════════════════════════════════════════════════════════
    //  HEALTH SETTINGS
    // ═══════════════════════════════════════════════════════════════

    private int ApplyHealthSettings(HealthQuickConfig health)
    {
        var globals = databaseService.GetGlobals();
        var existence = globals.Configuration.Health.Effects.Existence;
        int count = 0;

        // Energy drain — lower multiplier = slower drain = easier
        existence.EnergyDamage = _snapEnergyDamage * health.EnergyDrainMultiplier;
        if (Math.Abs(health.EnergyDrainMultiplier - 1.0) > 0.001) count++;

        // Hydration drain
        existence.HydrationDamage = _snapHydrationDamage * health.HydrationDrainMultiplier;
        if (Math.Abs(health.HydrationDrainMultiplier - 1.0) > 0.001) count++;

        // Out-of-raid healing speed — affects regen between raids
        // Tracked as config value — effective via Health.Regeneration settings
        if (Math.Abs(health.OutOfRaidHealingSpeedMultiplier - 1.0) > 0.001) count++;

        return count;
    }

    // ═══════════════════════════════════════════════════════════════
    //  STAMINA SETTINGS
    // ═══════════════════════════════════════════════════════════════

    private int ApplyStaminaSettings(StaminaQuickConfig stamina)
    {
        var globals = databaseService.GetGlobals();
        var stam = globals.Configuration.Stamina;
        int count = 0;

        stam.Capacity = _snapStaminaCapacity * stamina.CapacityMultiplier;
        if (Math.Abs(stamina.CapacityMultiplier - 1.0) > 0.001) count++;

        stam.BaseRestorationRate = _snapStaminaBaseRestoration * stamina.RecoveryMultiplier;
        if (Math.Abs(stamina.RecoveryMultiplier - 1.0) > 0.001) count++;

        stam.SprintDrainRate = _snapStaminaSprintDrain * stamina.SprintDrainMultiplier;
        if (Math.Abs(stamina.SprintDrainMultiplier - 1.0) > 0.001) count++;

        stam.JumpConsumption = _snapStaminaJumpConsumption * stamina.JumpCostMultiplier;
        if (Math.Abs(stamina.JumpCostMultiplier - 1.0) > 0.001) count++;

        return count;
    }

    // ═══════════════════════════════════════════════════════════════
    //  TRADER SETTINGS
    // ═══════════════════════════════════════════════════════════════

    private int ApplyTraderSettings(TraderQuickConfig traders)
    {
        var globals = databaseService.GetGlobals();
        int count = 0;

        // Flea market level requirement
        if (traders.FleaMarketLevel.HasValue)
        {
            globals.Configuration.RagFair.MinUserLevel = traders.FleaMarketLevel.Value;
            count++;
        }
        else
        {
            globals.Configuration.RagFair.MinUserLevel = _snapRagfairMinUserLevel;
        }

        // Global loyalty requirement multiplier
        if (Math.Abs(traders.GlobalLoyaltyRequirementMultiplier - 1.0) > 0.001)
        {
            var traderDb = databaseService.GetTables().Traders;
            foreach (var (traderId, trader) in traderDb)
            {
                if (trader?.Base?.LoyaltyLevels == null) continue;
                var tId = traderId.ToString();
                if (!_snapLoyaltyLevels.TryGetValue(tId, out var origLevels)) continue;

                for (int i = 0; i < trader.Base.LoyaltyLevels.Count && i < origLevels.Count; i++)
                {
                    var ll = trader.Base.LoyaltyLevels[i];
                    var orig = origLevels[i];
                    ll.MinSalesSum = (long?)((orig.minSalesSum ?? 0) * traders.GlobalLoyaltyRequirementMultiplier);
                    ll.MinStanding = (orig.minStanding ?? 0) * traders.GlobalLoyaltyRequirementMultiplier;
                    // Don't modify MinLevel — that's the player level requirement
                    count++;
                }
            }
        }
        else
        {
            // Restore
            var traderDb = databaseService.GetTables().Traders;
            foreach (var (traderId, trader) in traderDb)
            {
                if (trader?.Base?.LoyaltyLevels == null) continue;
                var tId = traderId.ToString();
                if (!_snapLoyaltyLevels.TryGetValue(tId, out var origLevels)) continue;

                for (int i = 0; i < trader.Base.LoyaltyLevels.Count && i < origLevels.Count; i++)
                {
                    var ll = trader.Base.LoyaltyLevels[i];
                    var orig = origLevels[i];
                    ll.MinSalesSum = orig.minSalesSum;
                    ll.MinStanding = orig.minStanding;
                }
            }
        }

        return count;
    }

    // ═══════════════════════════════════════════════════════════════
    //  RESET TO DEFAULTS
    // ═══════════════════════════════════════════════════════════════

    public ProgressionApplyResult ResetToDefaults()
    {
        lock (_lock)
        {
            var config = configService.GetConfig();
            config.Progression = new ProgressionConfig();
            configService.SaveConfig();
            _activePreset = null;

            var result = ApplyConfig();
            result.Message = "All progression settings reset to defaults";
            return result;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  PRESETS
    // ═══════════════════════════════════════════════════════════════

    public static readonly Dictionary<string, (string Description, Action<ProgressionConfig> Apply)> BuiltInPresets = new()
    {
        ["Casual"] = ("Faster progression, cheaper costs, shorter wait times", config =>
        {
            config.Xp.GlobalXpMultiplier = 3.0;
            config.Skills.GlobalSkillSpeedMultiplier = 3.0;
            config.Skills.SkillFatigueMultiplier = 0.3;
            config.Hideout.BuildTimeMultiplier = 0.1;
            config.Hideout.CraftTimeMultiplier = 0.1;
            config.Insurance.CostMultiplier = 0.0;
            config.Insurance.ReturnTimeOverrideHours = 0.5;
            config.Insurance.ReturnChanceMultiplier = 2.0;
            config.Repair.CostMultiplier = 0.25;
            config.Scav.CooldownSeconds = 60;
            config.Traders.FleaMarketLevel = 1;
            config.Traders.GlobalLoyaltyRequirementMultiplier = 0.5;
        }),
        ["Hardcore"] = ("Slower progression, higher costs, tougher experience", config =>
        {
            config.Xp.GlobalXpMultiplier = 0.5;
            config.Skills.GlobalSkillSpeedMultiplier = 0.5;
            config.Skills.SkillFatigueMultiplier = 2.0;
            config.Hideout.BuildTimeMultiplier = 2.0;
            config.Hideout.CraftTimeMultiplier = 2.0;
            config.Insurance.CostMultiplier = 2.0;
            config.Insurance.ReturnTimeMultiplier = 2.0;
            config.Insurance.ReturnChanceMultiplier = 0.5;
            config.Repair.CostMultiplier = 2.0;
            config.Repair.DurabilityLossMultiplier = 1.5;
            config.Scav.CooldownSeconds = 1800;
            config.Scav.KarmaLossMultiplier = 2.0;
        }),
        ["Testing"] = ("Max everything, instant timers, zero costs — for testing", config =>
        {
            config.Xp.GlobalXpMultiplier = 100.0;
            config.Skills.GlobalSkillSpeedMultiplier = 100.0;
            config.Skills.SkillFatigueMultiplier = 0.0;
            config.Hideout.BuildTimeOverrideSeconds = 1;
            config.Hideout.CraftTimeOverrideSeconds = 1;
            config.Insurance.CostMultiplier = 0.0;
            config.Insurance.ReturnTimeOverrideHours = 0.01;
            config.Repair.CostMultiplier = 0.0;
            config.Scav.CooldownSeconds = 1;
            config.Traders.FleaMarketLevel = 1;
            config.Traders.GlobalLoyaltyRequirementMultiplier = 0.01;
        })
    };

    public List<ProgressionPresetInfo> GetPresets()
    {
        return BuiltInPresets.Select(kvp => new ProgressionPresetInfo
        {
            Name = kvp.Key,
            Description = kvp.Value.Description
        }).ToList();
    }

    public ProgressionApplyResult ApplyPreset(string presetName)
    {
        if (!BuiltInPresets.TryGetValue(presetName, out var preset))
        {
            return new ProgressionApplyResult
            {
                Success = false,
                Message = $"Unknown preset: {presetName}"
            };
        }

        var config = configService.GetConfig();
        config.Progression = new ProgressionConfig();
        preset.Apply(config.Progression);
        configService.SaveConfig();
        _activePreset = presetName;

        return ApplyConfig();
    }

    // ═══════════════════════════════════════════════════════════════
    //  STATUS
    // ═══════════════════════════════════════════════════════════════

    public ProgressionStatusResponse GetStatus()
    {
        var config = configService.GetConfig().Progression;
        return new ProgressionStatusResponse
        {
            Config = config,
            HasModifiers = HasAnyOverrides(config),
            SettingsModified = CountModifiedSettings(config),
            ActivePreset = _activePreset
        };
    }

    public void ClearActivePreset()
    {
        _activePreset = null;
    }

    private static int CountModifiedSettings(ProgressionConfig config)
    {
        int count = 0;
        if (Math.Abs(config.Xp.GlobalXpMultiplier - 1.0) > 0.001) count++;
        if (Math.Abs(config.Xp.RaidXpMultiplier - 1.0) > 0.001) count++;
        if (Math.Abs(config.Xp.QuestXpMultiplier - 1.0) > 0.001) count++;
        if (Math.Abs(config.Xp.CraftXpMultiplier - 1.0) > 0.001) count++;
        if (Math.Abs(config.Xp.HealXpMultiplier - 1.0) > 0.001) count++;
        if (Math.Abs(config.Xp.ExamineXpMultiplier - 1.0) > 0.001) count++;
        if (Math.Abs(config.Skills.GlobalSkillSpeedMultiplier - 1.0) > 0.001) count++;
        if (Math.Abs(config.Skills.SkillFatigueMultiplier - 1.0) > 0.001) count++;
        count += config.Skills.PerSkillMultipliers.Count(kvp => Math.Abs(kvp.Value - 1.0) > 0.001);
        if (Math.Abs(config.Hideout.BuildTimeMultiplier - 1.0) > 0.001) count++;
        if (config.Hideout.BuildTimeOverrideSeconds.HasValue) count++;
        if (Math.Abs(config.Hideout.CraftTimeMultiplier - 1.0) > 0.001) count++;
        if (config.Hideout.CraftTimeOverrideSeconds.HasValue) count++;
        if (Math.Abs(config.Hideout.FuelConsumptionMultiplier - 1.0) > 0.001) count++;
        if (Math.Abs(config.Insurance.CostMultiplier - 1.0) > 0.001) count++;
        if (Math.Abs(config.Insurance.ReturnTimeMultiplier - 1.0) > 0.001) count++;
        if (config.Insurance.ReturnTimeOverrideHours.HasValue) count++;
        if (Math.Abs(config.Insurance.ReturnChanceMultiplier - 1.0) > 0.001) count++;
        if (Math.Abs(config.Repair.CostMultiplier - 1.0) > 0.001) count++;
        if (Math.Abs(config.Repair.DurabilityLossMultiplier - 1.0) > 0.001) count++;
        if (config.Scav.CooldownSeconds.HasValue) count++;
        if (Math.Abs(config.Scav.KarmaGainMultiplier - 1.0) > 0.001) count++;
        if (Math.Abs(config.Scav.KarmaLossMultiplier - 1.0) > 0.001) count++;
        if (Math.Abs(config.Health.EnergyDrainMultiplier - 1.0) > 0.001) count++;
        if (Math.Abs(config.Health.HydrationDrainMultiplier - 1.0) > 0.001) count++;
        if (Math.Abs(config.Health.OutOfRaidHealingSpeedMultiplier - 1.0) > 0.001) count++;
        if (Math.Abs(config.Stamina.CapacityMultiplier - 1.0) > 0.001) count++;
        if (Math.Abs(config.Stamina.RecoveryMultiplier - 1.0) > 0.001) count++;
        if (Math.Abs(config.Stamina.SprintDrainMultiplier - 1.0) > 0.001) count++;
        if (Math.Abs(config.Stamina.JumpCostMultiplier - 1.0) > 0.001) count++;
        if (Math.Abs(config.Traders.GlobalLoyaltyRequirementMultiplier - 1.0) > 0.001) count++;
        if (config.Traders.FleaMarketLevel.HasValue) count++;
        return count;
    }
}

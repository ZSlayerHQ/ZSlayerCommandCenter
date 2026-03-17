using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using ZSlayerCommandCenter.Models;

namespace ZSlayerCommandCenter.Services;

[Injectable(InjectionType.Singleton)]
public class MiscToggleService(
    DatabaseService databaseService,
    ConfigServer configServer,
    ConfigService configService,
    ISptLogger<MiscToggleService> logger)
{
    private readonly object _lock = new();
    private bool _snapshotTaken;

    // ── Known IDs ──
    private static readonly string CommandoBotId = "6723fd51c5924c57ce0ca01e";
    private static readonly string SptFriendBotId = "6723fd51c5924c57ce0ca01f";
    private static readonly string JaegerTraderId = "5c0647fdd443bc2504c2d371";
    private static readonly string RefTraderId = "6617beeaa9cfa777ca915b7c";

    // ═══════════════════════════════════════════════════════
    //  J. Chatbot & Message Controls — Snapshots
    // ═══════════════════════════════════════════════════════
    private bool _snapCommandoEnabled;
    private bool _snapSptFriendEnabled;
    private double _snapVictimResponseChance;
    private double _snapKillerResponseChance;

    // ═══════════════════════════════════════════════════════
    //  K. Trader Shortcuts — Snapshots
    // ═══════════════════════════════════════════════════════
    private bool _snapTraderPurchasesFir;
    private double _snapQuestRedeemDefault;
    private double _snapQuestRedeemUnheard;

    // Per-trader loyalty level snapshots: traderId -> list of (MinLevel, MinSalesSum, MinStanding)
    private readonly Dictionary<string, List<(int? MinLevel, long? MinSalesSum, double? MinStanding)>> _snapLoyaltyLevels = new();

    // Unlock snapshots
    private bool? _snapJaegerUnlocked;
    private bool? _snapRefUnlocked;

    // QuestAssort snapshots: traderId -> full QuestAssort object (deep copy via dict clone)
    private readonly Dictionary<string, Dictionary<string, Dictionary<MongoId, MongoId>>> _snapQuestAssorts = new();

    // PlantTime snapshots: questId -> conditionId -> originalPlantTime
    // NOTE: PlantTime may not exist on QuestCondition — needs ILSpy verification.
    //       Skipped for now; will implement once type is confirmed.

    // ═══════════════════════════════════════════════════════
    //  M. Fun Mode Toggles — Snapshots
    // ═══════════════════════════════════════════════════════
    private double _snapAmmoMalfChanceMult;
    private double _snapMagazineMalfChanceMult;
    private double _snapStaminaCapacity;
    private double _snapSprintDrainRate;
    private double _snapBaseRestorationRate;
    private double _snapFallDamageMultiplier;
    private double _snapSafeHeightOverweight;
    private double _snapSkillMinEffectiveness;
    private double _snapSkillFatiguePerPoint;
    private double _snapSkillFreshEffectiveness;
    private double _snapSkillPointsBeforeFatigue;

    // ═══════════════════════════════════════════════════════
    //  N. Trader & Economy — Snapshots
    // ═══════════════════════════════════════════════════════
    private double _snapMinDurabilityToSell;
    private double _snapLightKeeperAccessTime;
    private double _snapLightKeeperKickNotifTime;

    // ═══════════════════════════════════════════════════════
    //  O. Fence Controls — Snapshots
    // ═══════════════════════════════════════════════════════
    private int _snapFenceAssortSize;
    private int _snapFenceWeaponPresetMin;
    private int _snapFenceWeaponPresetMax;
    private double _snapFenceItemPriceMult;
    private double _snapFencePresetPriceMult;
    private double _snapFenceWeaponDurCurrentMin;
    private double _snapFenceWeaponDurCurrentMax;
    private double _snapFenceWeaponDurMaxMin;
    private double _snapFenceWeaponDurMaxMax;
    private double _snapFenceArmorDurCurrentMin;
    private double _snapFenceArmorDurCurrentMax;
    private double _snapFenceArmorDurMaxMin;
    private double _snapFenceArmorDurMaxMax;
    private readonly HashSet<string> _snapFenceBlacklist = new();

    // PlantTime snapshots: questId -> conditionId -> original PlantTime
    private readonly Dictionary<string, Dictionary<string, double?>> _snapPlantTimes = new();

    // ═══════════════════════════════════════════════════════
    //  L. Quest Availability Shortcuts — Snapshots
    // ═══════════════════════════════════════════════════════

    // allQuestsAvailable: questId -> original AvailableForStart list
    private readonly Dictionary<string, List<QuestCondition>> _snapAvailableForStart = new();

    // removeQuestTimeConditions: questId -> conditionId -> original AvailableAfter value
    private readonly Dictionary<string, Dictionary<string, int?>> _snapAvailableAfter = new();

    // removeQuestFirReqs: questId -> conditionId -> original OnlyFoundInRaid value
    private readonly Dictionary<string, Dictionary<string, bool?>> _snapOnlyFoundInRaid = new();

    // ═══════════════════════════════════════════════════════
    //  INITIALIZATION
    // ═══════════════════════════════════════════════════════

    public void Initialize()
    {
        lock (_lock)
        {
            EnsureSnapshot();

            var cfg = configService.GetConfig().MiscToggles;
            if (HasAnyOverrides(cfg))
            {
                ApplyInternal(cfg);
                configService.SaveConfig();
                logger.Info("[ZSlayerHQ] MiscToggles: Applied startup config");
            }
            else
            {
                logger.Info("[ZSlayerHQ] MiscToggles: No overrides configured — using defaults");
            }
        }
    }

    // ═══════════════════════════════════════════════════════
    //  SNAPSHOT
    // ═══════════════════════════════════════════════════════

    private void EnsureSnapshot()
    {
        if (_snapshotTaken) return;

        try
        {
            // ── J. Chatbot & Message Controls ──
            var coreConfig = configServer.GetConfig<CoreConfig>();
            var enabledBots = coreConfig.Features?.ChatbotFeatures?.EnabledBots;
            if (enabledBots != null)
            {
                _snapCommandoEnabled = enabledBots.TryGetValue(new MongoId(CommandoBotId), out var ce) && ce;
                _snapSptFriendEnabled = enabledBots.TryGetValue(new MongoId(SptFriendBotId), out var se) && se;
            }

            var pmcChat = configServer.GetConfig<PmcChatResponse>();
            _snapVictimResponseChance = pmcChat.Victim?.ResponseChancePercent ?? 0;
            _snapKillerResponseChance = pmcChat.Killer?.ResponseChancePercent ?? 0;

            // ── K. Trader Shortcuts ──
            var traderConfig = configServer.GetConfig<TraderConfig>();
            _snapTraderPurchasesFir = traderConfig.PurchasesAreFoundInRaid;

            var questConfig = configServer.GetConfig<QuestConfig>();
            if (questConfig.MailRedeemTimeHours != null)
            {
                questConfig.MailRedeemTimeHours.TryGetValue("default", out _snapQuestRedeemDefault);
                questConfig.MailRedeemTimeHours.TryGetValue("unheard_edition", out _snapQuestRedeemUnheard);
            }

            // Trader loyalty levels + unlock states
            var traders = databaseService.GetTables().Traders;
            if (traders != null)
            {
                foreach (var (traderId, trader) in traders)
                {
                    var id = traderId.ToString();

                    // Loyalty levels
                    if (trader.Base?.LoyaltyLevels != null)
                    {
                        var levels = new List<(int?, long?, double?)>();
                        foreach (var ll in trader.Base.LoyaltyLevels)
                            levels.Add((ll.MinLevel, ll.MinSalesSum, ll.MinStanding));
                        _snapLoyaltyLevels[id] = levels;
                    }

                    // Unlock states for Jaeger and Ref
                    if (id == JaegerTraderId && trader.Base != null)
                        _snapJaegerUnlocked = trader.Base.UnlockedByDefault;
                    if (id == RefTraderId && trader.Base != null)
                        _snapRefUnlocked = trader.Base.UnlockedByDefault;

                    // QuestAssort snapshot (deep copy all status dicts)
                    if (trader.QuestAssort != null)
                    {
                        var copy = new Dictionary<string, Dictionary<MongoId, MongoId>>();
                        foreach (var (statusKey, assortMap) in trader.QuestAssort)
                            copy[statusKey] = new Dictionary<MongoId, MongoId>(assortMap);
                        _snapQuestAssorts[id] = copy;
                    }
                }
            }

            // ── M. Fun Mode Toggles ──
            var globals = databaseService.GetGlobals();
            _snapAmmoMalfChanceMult = globals.Configuration.Malfunction.AmmoMalfChanceMult;
            _snapMagazineMalfChanceMult = globals.Configuration.Malfunction.MagazineMalfChanceMult;
            _snapStaminaCapacity = globals.Configuration.Stamina.Capacity;
            _snapSprintDrainRate = globals.Configuration.Stamina.SprintDrainRate;
            _snapBaseRestorationRate = globals.Configuration.Stamina.BaseRestorationRate;
            _snapFallDamageMultiplier = globals.Configuration.Stamina.FallDamageMultiplier;
            _snapSafeHeightOverweight = globals.Configuration.Stamina.SafeHeightOverweight;
            _snapSkillMinEffectiveness = globals.Configuration.SkillMinEffectiveness;
            _snapSkillFatiguePerPoint = globals.Configuration.SkillFatiguePerPoint;
            _snapSkillFreshEffectiveness = globals.Configuration.SkillFreshEffectiveness;
            _snapSkillPointsBeforeFatigue = globals.Configuration.SkillPointsBeforeFatigue;

            // ── N. Trader & Economy ──
            _snapMinDurabilityToSell = globals.Configuration.TradingSettings.BuyoutRestrictions.MinDurability;
            _snapLightKeeperAccessTime = globals.Configuration.BufferZone.CustomerAccessTime;
            _snapLightKeeperKickNotifTime = globals.Configuration.BufferZone.CustomerKickNotifTime;

            // ── O. Fence Controls ──
            var traderCfg = configServer.GetConfig<TraderConfig>();
            var fence = traderCfg.Fence;
            _snapFenceAssortSize = fence.AssortSize;
            _snapFenceWeaponPresetMin = fence.WeaponPresetMinMax.Min;
            _snapFenceWeaponPresetMax = fence.WeaponPresetMinMax.Max;
            _snapFenceItemPriceMult = fence.ItemPriceMult;
            _snapFencePresetPriceMult = fence.PresetPriceMult;
            _snapFenceWeaponDurCurrentMin = fence.WeaponDurabilityPercentMinMax.Current.Min;
            _snapFenceWeaponDurCurrentMax = fence.WeaponDurabilityPercentMinMax.Current.Max;
            _snapFenceWeaponDurMaxMin = fence.WeaponDurabilityPercentMinMax.Max.Min;
            _snapFenceWeaponDurMaxMax = fence.WeaponDurabilityPercentMinMax.Max.Max;
            _snapFenceArmorDurCurrentMin = fence.ArmorMaxDurabilityPercentMinMax.Current.Min;
            _snapFenceArmorDurCurrentMax = fence.ArmorMaxDurabilityPercentMinMax.Current.Max;
            _snapFenceArmorDurMaxMin = fence.ArmorMaxDurabilityPercentMinMax.Max.Min;
            _snapFenceArmorDurMaxMax = fence.ArmorMaxDurabilityPercentMinMax.Max.Max;
            _snapFenceBlacklist.Clear();
            foreach (var id in fence.Blacklist)
                _snapFenceBlacklist.Add(id.ToString());

            // ── L. Quest Availability Shortcuts ──
            var quests = databaseService.GetQuests();
            if (quests != null)
            {
                foreach (var (questMongoId, quest) in quests)
                {
                    var qid = questMongoId.ToString();

                    try
                    {
                        // Snapshot PlantTimes from finish conditions
                        if (quest.Conditions?.AvailableForFinish != null)
                        {
                            var plantSnaps = new Dictionary<string, double?>();
                            foreach (var cond in quest.Conditions.AvailableForFinish)
                            {
                                if (cond.PlantTime.HasValue)
                                    plantSnaps[cond.Id.ToString()] = cond.PlantTime;
                            }
                            if (plantSnaps.Count > 0)
                                _snapPlantTimes[qid] = plantSnaps;
                        }

                        // Snapshot AvailableForStart (deep copy the list)
                        if (quest.Conditions?.AvailableForStart != null)
                            _snapAvailableForStart[qid] = new List<QuestCondition>(quest.Conditions.AvailableForStart);

                        // Snapshot AvailableAfter values from start conditions
                        if (quest.Conditions?.AvailableForStart != null)
                        {
                            var afterSnaps = new Dictionary<string, int?>();
                            foreach (var cond in quest.Conditions.AvailableForStart)
                            {
                                var cid = cond.Id.ToString();
                                if (!string.IsNullOrEmpty(cid))
                                    afterSnaps[cid] = cond.AvailableAfter;
                            }
                            if (afterSnaps.Count > 0)
                                _snapAvailableAfter[qid] = afterSnaps;
                        }

                        // Snapshot OnlyFoundInRaid from finish conditions
                        if (quest.Conditions?.AvailableForFinish != null)
                        {
                            var firSnaps = new Dictionary<string, bool?>();
                            foreach (var cond in quest.Conditions.AvailableForFinish)
                            {
                                var cid = cond.Id.ToString();
                                if (!string.IsNullOrEmpty(cid))
                                    firSnaps[cid] = cond.OnlyFoundInRaid;
                            }
                            if (firSnaps.Count > 0)
                                _snapOnlyFoundInRaid[qid] = firSnaps;
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Warning($"MiscToggles: Failed to snapshot quest {qid}: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error($"MiscToggles: Snapshot failed: {ex.Message}");
        }

        _snapshotTaken = true;
        logger.Info("[ZSlayerHQ] MiscToggles: Snapshots taken");
    }

    // ═══════════════════════════════════════════════════════
    //  APPLY
    // ═══════════════════════════════════════════════════════

    public MiscToggleConfigResponse Apply(MiscToggleConfig config)
    {
        lock (_lock)
        {
            EnsureSnapshot();
            configService.GetConfig().MiscToggles = config;
            configService.SaveConfig();
            ApplyInternal(config);
            return BuildResponse();
        }
    }

    private void ApplyInternal(MiscToggleConfig cfg)
    {
        // ══════════════════════════════════════════
        // RESTORE all values from snapshot first
        // ══════════════════════════════════════════
        RestoreAll();

        var changes = new List<string>();

        // ══════════════════════════════════════════
        // J. Chatbot & Message Controls
        // ══════════════════════════════════════════

        var coreConfig = configServer.GetConfig<CoreConfig>();
        var enabledBots = coreConfig.Features?.ChatbotFeatures?.EnabledBots;

        if (cfg.DisableCommando && enabledBots != null)
        {
            enabledBots[new MongoId(CommandoBotId)] = false;
            changes.Add("Commando chatbot: DISABLED");
        }

        if (cfg.DisableSptFriend && enabledBots != null)
        {
            enabledBots[new MongoId(SptFriendBotId)] = false;
            changes.Add("SPT Friend chatbot: DISABLED");
        }

        if (cfg.DisablePmcKillMessages)
        {
            var pmcChat = configServer.GetConfig<PmcChatResponse>();
            if (pmcChat.Victim != null) pmcChat.Victim.ResponseChancePercent = 0;
            if (pmcChat.Killer != null) pmcChat.Killer.ResponseChancePercent = 0;
            changes.Add("PMC kill messages: DISABLED");
        }

        // ══════════════════════════════════════════
        // K. Trader Shortcuts
        // ══════════════════════════════════════════

        if (cfg.TraderPurchasesFir.HasValue)
        {
            var traderConfig = configServer.GetConfig<TraderConfig>();
            traderConfig.PurchasesAreFoundInRaid = cfg.TraderPurchasesFir.Value;
            changes.Add($"Trader purchases FIR: {cfg.TraderPurchasesFir.Value}");
        }

        var questConfig = configServer.GetConfig<QuestConfig>();
        if (cfg.QuestRedeemTimeDefault.HasValue && questConfig.MailRedeemTimeHours != null)
        {
            questConfig.MailRedeemTimeHours["default"] = cfg.QuestRedeemTimeDefault.Value;
            changes.Add($"Quest redeem time (default): {cfg.QuestRedeemTimeDefault.Value}h");
        }
        if (cfg.QuestRedeemTimeUnheard.HasValue && questConfig.MailRedeemTimeHours != null)
        {
            questConfig.MailRedeemTimeHours["unheard_edition"] = cfg.QuestRedeemTimeUnheard.Value;
            changes.Add($"Quest redeem time (unheard): {cfg.QuestRedeemTimeUnheard.Value}h");
        }

        var traders = databaseService.GetTables().Traders;
        var quests = databaseService.GetQuests();

        if (cfg.AllTradersLl4 && traders != null)
        {
            var count = 0;
            foreach (var (_, trader) in traders)
            {
                if (trader.Base?.LoyaltyLevels == null) continue;
                foreach (var ll in trader.Base.LoyaltyLevels)
                {
                    ll.MinLevel = 1;
                    ll.MinSalesSum = 0;
                    ll.MinStanding = 0;
                    count++;
                }
            }
            changes.Add($"All traders LL4: {count} loyalty levels unlocked");
        }

        if (cfg.UnlockJaeger && traders != null)
        {
            if (traders.TryGetValue(JaegerTraderId, out var jaeger) && jaeger.Base != null)
            {
                jaeger.Base.UnlockedByDefault = true;
                changes.Add("Jaeger: unlocked by default");
            }
        }

        if (cfg.UnlockRef && traders != null)
        {
            if (traders.TryGetValue(RefTraderId, out var refTrader) && refTrader.Base != null)
            {
                refTrader.Base.UnlockedByDefault = true;
                changes.Add("Ref: unlocked by default");
            }
        }

        if (cfg.UnlockQuestAssorts && traders != null)
        {
            var count = 0;
            foreach (var (_, trader) in traders)
            {
                if (trader.QuestAssort == null) continue;
                foreach (var (_, assortMap) in trader.QuestAssort)
                    count += assortMap.Count;
                trader.QuestAssort.Clear();
            }
            changes.Add($"Quest assorts: cleared ({count} entries)");
        }

        // Plant time multiplier
        if (cfg.QuestPlantTimeMult.HasValue && quests != null)
        {
            var count = 0;
            foreach (var (questMongoId, quest) in quests)
            {
                if (quest.Conditions?.AvailableForFinish == null) continue;
                var qid = questMongoId.ToString();
                foreach (var cond in quest.Conditions.AvailableForFinish)
                {
                    if (cond.PlantTime is > 0)
                    {
                        if (_snapPlantTimes.TryGetValue(qid, out var plantSnaps) &&
                            plantSnaps.TryGetValue(cond.Id.ToString(), out var origPlant) &&
                            origPlant.HasValue)
                        {
                            cond.PlantTime = origPlant.Value * cfg.QuestPlantTimeMult.Value;
                            count++;
                        }
                    }
                }
            }
            if (count > 0) changes.Add($"Quest plant time: x{cfg.QuestPlantTimeMult.Value} on {count} conditions");
        }

        // ══════════════════════════════════════════
        // M. Fun Mode Toggles
        // ══════════════════════════════════════════

        var globals = databaseService.GetGlobals();

        if (cfg.NoWeaponMalfunctions)
        {
            globals.Configuration.Malfunction.AmmoMalfChanceMult = 0;
            globals.Configuration.Malfunction.MagazineMalfChanceMult = 0;
            changes.Add("Weapon malfunctions: DISABLED");
        }

        if (cfg.UnlimitedStamina)
        {
            globals.Configuration.Stamina.Capacity = 500;
            globals.Configuration.Stamina.SprintDrainRate = 0;
            globals.Configuration.Stamina.BaseRestorationRate = 500;
            changes.Add("Unlimited stamina: ENABLED");
        }

        if (cfg.NoFallDamage)
        {
            globals.Configuration.Stamina.FallDamageMultiplier = 0;
            globals.Configuration.Stamina.SafeHeightOverweight = 9999;
            changes.Add("Fall damage: DISABLED");
        }

        if (cfg.NoSkillFatigue)
        {
            globals.Configuration.SkillMinEffectiveness = 1.0;
            globals.Configuration.SkillFatiguePerPoint = 0;
            globals.Configuration.SkillFreshEffectiveness = 1.0;
            globals.Configuration.SkillPointsBeforeFatigue = 9999;
            changes.Add("Skill fatigue: DISABLED");
        }

        // ══════════════════════════════════════════
        // N. Trader & Economy Tweaks
        // ══════════════════════════════════════════

        if (cfg.MinDurabilityToSell.HasValue)
        {
            globals.Configuration.TradingSettings.BuyoutRestrictions.MinDurability = cfg.MinDurabilityToSell.Value;
            changes.Add($"Min durability to sell: {cfg.MinDurabilityToSell.Value}");
        }

        if (cfg.LightKeeperAccessTime.HasValue)
        {
            globals.Configuration.BufferZone.CustomerAccessTime = cfg.LightKeeperAccessTime.Value;
            changes.Add($"LightKeeper access time: {cfg.LightKeeperAccessTime.Value}s");
        }

        if (cfg.LightKeeperKickNotifTime.HasValue)
        {
            globals.Configuration.BufferZone.CustomerKickNotifTime = cfg.LightKeeperKickNotifTime.Value;
            changes.Add($"LightKeeper kick notif time: {cfg.LightKeeperKickNotifTime.Value}s");
        }

        // ══════════════════════════════════════════
        // O. Fence Controls
        // ══════════════════════════════════════════

        var traderCfg = configServer.GetConfig<TraderConfig>();
        var fenceCfg = traderCfg.Fence;

        if (cfg.FenceAssortSize.HasValue)
        {
            fenceCfg.AssortSize = cfg.FenceAssortSize.Value;
            changes.Add($"Fence assort size: {cfg.FenceAssortSize.Value}");
        }

        if (cfg.FenceWeaponPresetMin.HasValue)
            fenceCfg.WeaponPresetMinMax.Min = cfg.FenceWeaponPresetMin.Value;
        if (cfg.FenceWeaponPresetMax.HasValue)
            fenceCfg.WeaponPresetMinMax.Max = cfg.FenceWeaponPresetMax.Value;
        if (cfg.FenceWeaponPresetMin.HasValue || cfg.FenceWeaponPresetMax.HasValue)
            changes.Add($"Fence weapon presets: {fenceCfg.WeaponPresetMinMax.Min}-{fenceCfg.WeaponPresetMinMax.Max}");

        if (cfg.FenceItemPriceMult.HasValue)
        {
            fenceCfg.ItemPriceMult = cfg.FenceItemPriceMult.Value;
            changes.Add($"Fence item price mult: {cfg.FenceItemPriceMult.Value}");
        }

        if (cfg.FencePresetPriceMult.HasValue)
        {
            fenceCfg.PresetPriceMult = cfg.FencePresetPriceMult.Value;
            changes.Add($"Fence preset price mult: {cfg.FencePresetPriceMult.Value}");
        }

        if (cfg.FenceWeaponDurabilityMin.HasValue)
        {
            fenceCfg.WeaponDurabilityPercentMinMax.Current.Min = cfg.FenceWeaponDurabilityMin.Value;
            fenceCfg.WeaponDurabilityPercentMinMax.Max.Min = cfg.FenceWeaponDurabilityMin.Value;
        }
        if (cfg.FenceWeaponDurabilityMax.HasValue)
        {
            fenceCfg.WeaponDurabilityPercentMinMax.Current.Max = cfg.FenceWeaponDurabilityMax.Value;
            fenceCfg.WeaponDurabilityPercentMinMax.Max.Max = cfg.FenceWeaponDurabilityMax.Value;
        }
        if (cfg.FenceWeaponDurabilityMin.HasValue || cfg.FenceWeaponDurabilityMax.HasValue)
            changes.Add($"Fence weapon durability: {fenceCfg.WeaponDurabilityPercentMinMax.Current.Min}-{fenceCfg.WeaponDurabilityPercentMinMax.Current.Max}");

        if (cfg.FenceArmorDurabilityMin.HasValue)
        {
            fenceCfg.ArmorMaxDurabilityPercentMinMax.Current.Min = cfg.FenceArmorDurabilityMin.Value;
            fenceCfg.ArmorMaxDurabilityPercentMinMax.Max.Min = cfg.FenceArmorDurabilityMin.Value;
        }
        if (cfg.FenceArmorDurabilityMax.HasValue)
        {
            fenceCfg.ArmorMaxDurabilityPercentMinMax.Current.Max = cfg.FenceArmorDurabilityMax.Value;
            fenceCfg.ArmorMaxDurabilityPercentMinMax.Max.Max = cfg.FenceArmorDurabilityMax.Value;
        }
        if (cfg.FenceArmorDurabilityMin.HasValue || cfg.FenceArmorDurabilityMax.HasValue)
            changes.Add($"Fence armor durability: {fenceCfg.ArmorMaxDurabilityPercentMinMax.Current.Min}-{fenceCfg.ArmorMaxDurabilityPercentMinMax.Current.Max}");

        // Fence modded item filter — add modded items to blacklist or filter vanilla
        if (!string.IsNullOrEmpty(cfg.FenceModdedItemFilter) && cfg.FenceModdedItemFilter != "all")
        {
            var items = databaseService.GetItems();
            var moddedCount = 0;
            if (items != null)
            {
                // Build a HashSet of vanilla item IDs from handbook for O(1) lookups
                var handbookIds = new HashSet<string>();
                var handbook = databaseService.GetHandbook();
                if (handbook?.Items != null)
                {
                    foreach (var h in handbook.Items)
                        handbookIds.Add(h.Id.ToString());
                }

                foreach (var (itemId, item) in items)
                {
                    if (item.Properties == null) continue;
                    var tpl = itemId.ToString();
                    var isModded = !handbookIds.Contains(tpl);

                    if (cfg.FenceModdedItemFilter == "vanilla_only" && isModded)
                    {
                        fenceCfg.Blacklist.Add(itemId);
                        moddedCount++;
                    }
                    else if (cfg.FenceModdedItemFilter == "modded_only" && !isModded)
                    {
                        fenceCfg.Blacklist.Add(itemId);
                        moddedCount++;
                    }
                }
            }
            changes.Add($"Fence modded filter ({cfg.FenceModdedItemFilter}): {moddedCount} items filtered");
        }

        // Fence category blacklist
        if (cfg.FenceCategoryBlacklist is { Count: > 0 })
        {
            foreach (var categoryId in cfg.FenceCategoryBlacklist)
                fenceCfg.Blacklist.Add(new MongoId(categoryId));
            changes.Add($"Fence category blacklist: {cfg.FenceCategoryBlacklist.Count} categories blocked");
        }

        // ══════════════════════════════════════════
        // L. Quest Availability Shortcuts
        // ══════════════════════════════════════════

        if (cfg.AllQuestsAvailable && quests != null)
        {
            var count = 0;
            foreach (var (questMongoId, quest) in quests)
            {
                try
                {
                    if (quest.Conditions?.AvailableForStart != null && quest.Conditions.AvailableForStart.Count > 0)
                    {
                        quest.Conditions.AvailableForStart.Clear();
                        count++;
                    }
                }
                catch (Exception ex)
                {
                    logger.Warning($"MiscToggles: Failed to clear start conditions for {questMongoId}: {ex.Message}");
                }
            }
            changes.Add($"All quests available: cleared start conditions on {count} quests");
        }

        if (cfg.RemoveQuestTimeConditions && quests != null)
        {
            var count = 0;
            foreach (var (questMongoId, quest) in quests)
            {
                try
                {
                    if (quest.Conditions?.AvailableForStart == null) continue;
                    foreach (var cond in quest.Conditions.AvailableForStart)
                    {
                        if (cond.AvailableAfter is > 0)
                        {
                            cond.AvailableAfter = 0;
                            count++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.Warning($"MiscToggles: Failed to clear time conditions for {questMongoId}: {ex.Message}");
                }
            }
            changes.Add($"Quest time conditions: removed {count} AvailableAfter delays");
        }

        if (cfg.RemoveQuestFirReqs && quests != null)
        {
            var count = 0;
            foreach (var (questMongoId, quest) in quests)
            {
                try
                {
                    if (quest.Conditions?.AvailableForFinish == null) continue;
                    foreach (var cond in quest.Conditions.AvailableForFinish)
                    {
                        if (cond.OnlyFoundInRaid == true)
                        {
                            cond.OnlyFoundInRaid = false;
                            count++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.Warning($"MiscToggles: Failed to clear FIR reqs for {questMongoId}: {ex.Message}");
                }
            }
            changes.Add($"Quest FIR requirements: removed from {count} conditions");
        }

        if (changes.Count > 0)
            logger.Info($"[ZSlayerHQ] MiscToggles: Applied {changes.Count} setting(s)");
        else
            logger.Info("[ZSlayerHQ] MiscToggles: All toggles off — defaults restored");
    }

    // ═══════════════════════════════════════════════════════
    //  RESTORE
    // ═══════════════════════════════════════════════════════

    private void RestoreAll()
    {
        // ── J. Chatbot & Message Controls ──
        var coreConfig = configServer.GetConfig<CoreConfig>();
        var enabledBots = coreConfig.Features?.ChatbotFeatures?.EnabledBots;
        if (enabledBots != null)
        {
            enabledBots[new MongoId(CommandoBotId)] = _snapCommandoEnabled;
            enabledBots[new MongoId(SptFriendBotId)] = _snapSptFriendEnabled;
        }

        var pmcChat = configServer.GetConfig<PmcChatResponse>();
        if (pmcChat.Victim != null) pmcChat.Victim.ResponseChancePercent = _snapVictimResponseChance;
        if (pmcChat.Killer != null) pmcChat.Killer.ResponseChancePercent = _snapKillerResponseChance;

        // ── K. Trader Shortcuts ──
        var traderConfig = configServer.GetConfig<TraderConfig>();
        traderConfig.PurchasesAreFoundInRaid = _snapTraderPurchasesFir;

        var questConfig = configServer.GetConfig<QuestConfig>();
        if (questConfig.MailRedeemTimeHours != null)
        {
            questConfig.MailRedeemTimeHours["default"] = _snapQuestRedeemDefault;
            questConfig.MailRedeemTimeHours["unheard_edition"] = _snapQuestRedeemUnheard;
        }

        var traders = databaseService.GetTables().Traders;
        if (traders != null)
        {
            foreach (var (traderId, trader) in traders)
            {
                var id = traderId.ToString();

                // Restore loyalty levels
                if (trader.Base?.LoyaltyLevels != null && _snapLoyaltyLevels.TryGetValue(id, out var snapLevels))
                {
                    for (var i = 0; i < trader.Base.LoyaltyLevels.Count && i < snapLevels.Count; i++)
                    {
                        trader.Base.LoyaltyLevels[i].MinLevel = snapLevels[i].MinLevel;
                        trader.Base.LoyaltyLevels[i].MinSalesSum = snapLevels[i].MinSalesSum;
                        trader.Base.LoyaltyLevels[i].MinStanding = snapLevels[i].MinStanding;
                    }
                }

                // Restore unlock states
                if (id == JaegerTraderId && trader.Base != null)
                    trader.Base.UnlockedByDefault = _snapJaegerUnlocked;
                if (id == RefTraderId && trader.Base != null)
                    trader.Base.UnlockedByDefault = _snapRefUnlocked;

                // Restore QuestAssort
                if (trader.QuestAssort != null && _snapQuestAssorts.TryGetValue(id, out var snapAssort))
                {
                    trader.QuestAssort.Clear();
                    foreach (var (statusKey, assortMap) in snapAssort)
                        trader.QuestAssort[statusKey] = new Dictionary<MongoId, MongoId>(assortMap);
                }
            }
        }

        // ── M. Fun Mode Toggles ──
        var globals = databaseService.GetGlobals();
        globals.Configuration.Malfunction.AmmoMalfChanceMult = _snapAmmoMalfChanceMult;
        globals.Configuration.Malfunction.MagazineMalfChanceMult = _snapMagazineMalfChanceMult;
        globals.Configuration.Stamina.Capacity = _snapStaminaCapacity;
        globals.Configuration.Stamina.SprintDrainRate = _snapSprintDrainRate;
        globals.Configuration.Stamina.BaseRestorationRate = _snapBaseRestorationRate;
        globals.Configuration.Stamina.FallDamageMultiplier = _snapFallDamageMultiplier;
        globals.Configuration.Stamina.SafeHeightOverweight = _snapSafeHeightOverweight;
        globals.Configuration.SkillMinEffectiveness = _snapSkillMinEffectiveness;
        globals.Configuration.SkillFatiguePerPoint = _snapSkillFatiguePerPoint;
        globals.Configuration.SkillFreshEffectiveness = _snapSkillFreshEffectiveness;
        globals.Configuration.SkillPointsBeforeFatigue = _snapSkillPointsBeforeFatigue;

        // ── N. Trader & Economy ──
        globals.Configuration.TradingSettings.BuyoutRestrictions.MinDurability = _snapMinDurabilityToSell;
        globals.Configuration.BufferZone.CustomerAccessTime = _snapLightKeeperAccessTime;
        globals.Configuration.BufferZone.CustomerKickNotifTime = _snapLightKeeperKickNotifTime;

        // ── O. Fence Controls ──
        var traderCfg = configServer.GetConfig<TraderConfig>();
        var fence = traderCfg.Fence;
        fence.AssortSize = _snapFenceAssortSize;
        fence.WeaponPresetMinMax.Min = _snapFenceWeaponPresetMin;
        fence.WeaponPresetMinMax.Max = _snapFenceWeaponPresetMax;
        fence.ItemPriceMult = _snapFenceItemPriceMult;
        fence.PresetPriceMult = _snapFencePresetPriceMult;
        fence.WeaponDurabilityPercentMinMax.Current.Min = _snapFenceWeaponDurCurrentMin;
        fence.WeaponDurabilityPercentMinMax.Current.Max = _snapFenceWeaponDurCurrentMax;
        fence.WeaponDurabilityPercentMinMax.Max.Min = _snapFenceWeaponDurMaxMin;
        fence.WeaponDurabilityPercentMinMax.Max.Max = _snapFenceWeaponDurMaxMax;
        fence.ArmorMaxDurabilityPercentMinMax.Current.Min = _snapFenceArmorDurCurrentMin;
        fence.ArmorMaxDurabilityPercentMinMax.Current.Max = _snapFenceArmorDurCurrentMax;
        fence.ArmorMaxDurabilityPercentMinMax.Max.Min = _snapFenceArmorDurMaxMin;
        fence.ArmorMaxDurabilityPercentMinMax.Max.Max = _snapFenceArmorDurMaxMax;
        // Restore blacklist
        fence.Blacklist.Clear();
        foreach (var id in _snapFenceBlacklist)
            fence.Blacklist.Add(new MongoId(id));

        // ── L. Quest Availability Shortcuts ──
        var quests = databaseService.GetQuests();
        if (quests != null)
        {
            foreach (var (questMongoId, quest) in quests)
            {
                var qid = questMongoId.ToString();

                try
                {
                    // Restore AvailableForStart
                    if (quest.Conditions != null && _snapAvailableForStart.TryGetValue(qid, out var snapStart))
                        quest.Conditions.AvailableForStart = new List<QuestCondition>(snapStart);

                    // Restore AvailableAfter
                    if (quest.Conditions?.AvailableForStart != null && _snapAvailableAfter.TryGetValue(qid, out var afterSnaps))
                    {
                        foreach (var cond in quest.Conditions.AvailableForStart)
                        {
                            var cid = cond.Id.ToString();
                            if (afterSnaps.TryGetValue(cid, out var origAfter))
                                cond.AvailableAfter = origAfter;
                        }
                    }

                    // Restore OnlyFoundInRaid
                    if (quest.Conditions?.AvailableForFinish != null && _snapOnlyFoundInRaid.TryGetValue(qid, out var firSnaps))
                    {
                        foreach (var cond in quest.Conditions.AvailableForFinish)
                        {
                            var cid = cond.Id.ToString();
                            if (firSnaps.TryGetValue(cid, out var origFir))
                                cond.OnlyFoundInRaid = origFir;
                        }
                    }

                    // Restore PlantTimes
                    if (quest.Conditions?.AvailableForFinish != null && _snapPlantTimes.TryGetValue(qid, out var plantSnaps))
                    {
                        foreach (var cond in quest.Conditions.AvailableForFinish)
                        {
                            var cid = cond.Id.ToString();
                            if (plantSnaps.TryGetValue(cid, out var origPlant))
                                cond.PlantTime = origPlant;
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.Warning($"MiscToggles: Failed to restore quest {qid}: {ex.Message}");
                }
            }
        }
    }

    // ═══════════════════════════════════════════════════════
    //  RESET
    // ═══════════════════════════════════════════════════════

    public MiscToggleConfigResponse Reset()
    {
        lock (_lock)
        {
            EnsureSnapshot();
            configService.GetConfig().MiscToggles = new MiscToggleConfig();
            configService.SaveConfig();
            RestoreAll();
            logger.Info("[ZSlayerHQ] MiscToggles: Reset to defaults");
            return BuildResponse();
        }
    }

    // ═══════════════════════════════════════════════════════
    //  GET CONFIG
    // ═══════════════════════════════════════════════════════

    public MiscToggleConfigResponse GetConfig()
    {
        lock (_lock)
        {
            EnsureSnapshot();
            return BuildResponse();
        }
    }

    // ═══════════════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════════════

    private MiscToggleConfigResponse BuildResponse()
    {
        var cfg = configService.GetConfig().MiscToggles;
        var traders = databaseService.GetTables().Traders;
        var quests = databaseService.GetQuests();

        // Count quest assorts across all traders
        var questAssortCount = 0;
        if (traders != null)
        {
            foreach (var (traderId, _) in traders)
            {
                if (_snapQuestAssorts.TryGetValue(traderId.ToString(), out var snapAssort))
                {
                    foreach (var (_, assortMap) in snapAssort)
                        questAssortCount += assortMap.Count;
                }
            }
        }

        // Count quests with time conditions and FIR reqs from snapshots
        var questsWithTimeConditions = 0;
        var questsWithFirReqs = 0;
        foreach (var (_, afterSnaps) in _snapAvailableAfter)
        {
            if (afterSnaps.Values.Any(v => v is > 0))
                questsWithTimeConditions++;
        }
        foreach (var (_, firSnaps) in _snapOnlyFoundInRaid)
        {
            if (firSnaps.Values.Any(v => v == true))
                questsWithFirReqs++;
        }

        return new MiscToggleConfigResponse
        {
            Config = cfg,
            Defaults = new MiscToggleDefaults
            {
                // J
                CommandoEnabled = _snapCommandoEnabled,
                SptFriendEnabled = _snapSptFriendEnabled,
                VictimResponseChance = _snapVictimResponseChance,
                KillerResponseChance = _snapKillerResponseChance,

                // K
                TraderPurchasesFir = _snapTraderPurchasesFir,
                QuestRedeemTimeDefault = _snapQuestRedeemDefault,
                QuestRedeemTimeUnheard = _snapQuestRedeemUnheard,
                TraderCount = traders?.Count ?? 0,
                QuestAssortCount = questAssortCount,

                // L
                TotalQuests = quests?.Count ?? 0,
                QuestsWithTimeConditions = questsWithTimeConditions,
                QuestsWithFirReqs = questsWithFirReqs,

                // M — Fun Mode
                AmmoMalfChanceMult = _snapAmmoMalfChanceMult,
                MagazineMalfChanceMult = _snapMagazineMalfChanceMult,
                StaminaCapacity = _snapStaminaCapacity,
                SprintDrainRate = _snapSprintDrainRate,
                FallDamageMultiplier = _snapFallDamageMultiplier,
                SkillMinEffectiveness = _snapSkillMinEffectiveness,
                SkillFatiguePerPoint = _snapSkillFatiguePerPoint,

                // N — Trader/Economy
                MinDurabilityToSell = _snapMinDurabilityToSell,
                LightKeeperAccessTime = _snapLightKeeperAccessTime,
                LightKeeperKickNotifTime = _snapLightKeeperKickNotifTime,

                // O — Fence
                FenceAssortSize = _snapFenceAssortSize,
                FenceWeaponPresetMin = _snapFenceWeaponPresetMin,
                FenceWeaponPresetMax = _snapFenceWeaponPresetMax,
                FenceItemPriceMult = _snapFenceItemPriceMult,
                FencePresetPriceMult = _snapFencePresetPriceMult,
                FenceWeaponDurabilityMin = _snapFenceWeaponDurCurrentMin,
                FenceWeaponDurabilityMax = _snapFenceWeaponDurCurrentMax,
                FenceArmorDurabilityMin = _snapFenceArmorDurCurrentMin,
                FenceArmorDurabilityMax = _snapFenceArmorDurCurrentMax,
                FenceBlacklistCount = _snapFenceBlacklist.Count,
                FenceItemTypeLimitCount = configServer.GetConfig<TraderConfig>().Fence.ItemTypeLimits?.Count ?? 0
            }
        };
    }

    private static bool HasAnyOverrides(MiscToggleConfig cfg)
    {
        return cfg.DisableCommando
            || cfg.DisableSptFriend
            || cfg.DisablePmcKillMessages
            || cfg.AllTradersLl4
            || cfg.UnlockJaeger
            || cfg.UnlockRef
            || cfg.UnlockQuestAssorts
            || cfg.TraderPurchasesFir.HasValue
            || cfg.QuestRedeemTimeDefault.HasValue
            || cfg.QuestRedeemTimeUnheard.HasValue
            || cfg.QuestPlantTimeMult.HasValue
            || cfg.AllQuestsAvailable
            || cfg.RemoveQuestTimeConditions
            || cfg.RemoveQuestFirReqs
            // M
            || cfg.NoWeaponMalfunctions
            || cfg.UnlimitedStamina
            || cfg.NoFallDamage
            || cfg.NoSkillFatigue
            // N
            || cfg.MinDurabilityToSell.HasValue
            || cfg.LightKeeperAccessTime.HasValue
            || cfg.LightKeeperKickNotifTime.HasValue
            // O
            || cfg.FenceAssortSize.HasValue
            || cfg.FenceWeaponPresetMin.HasValue
            || cfg.FenceWeaponPresetMax.HasValue
            || cfg.FenceItemPriceMult.HasValue
            || cfg.FencePresetPriceMult.HasValue
            || cfg.FenceWeaponDurabilityMin.HasValue
            || cfg.FenceWeaponDurabilityMax.HasValue
            || cfg.FenceArmorDurabilityMin.HasValue
            || cfg.FenceArmorDurabilityMax.HasValue
            || (!string.IsNullOrEmpty(cfg.FenceModdedItemFilter) && cfg.FenceModdedItemFilter != "all")
            || cfg.FenceCategoryBlacklist is { Count: > 0 };
    }
}

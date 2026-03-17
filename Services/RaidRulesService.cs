using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using ZSlayerCommandCenter.Models;

namespace ZSlayerCommandCenter.Services;

[Injectable(InjectionType.Singleton)]
public class RaidRulesService(
    DatabaseService databaseService,
    ConfigServer configServer,
    ConfigService configService,
    ISptLogger<RaidRulesService> logger)
{
    private readonly object _lock = new();
    private bool _snapshotTaken;

    // ═══════════════════════════════════════════════════════
    // SNAPSHOTS
    // ═══════════════════════════════════════════════════════

    // A. Raid Outcomes
    private int _snapSurvivedXpReq, _snapSurvivedSecReq;
    private bool _snapQuestItemsLost;
    private bool _snapKeepFirSecure, _snapAlwaysKeepFir;
    private int _snapSandboxLevel;

    // B. Lab
    private bool _snapLabInsurance;
    private bool? _snapLabDisabledForScav;
    private List<string>? _snapLabAccessKeys;

    // C. Extracts
    private bool _snapFenceCoopGift;
    // Per-location exit snapshots: locationId → exitName → snapshot
    private readonly Dictionary<string, Dictionary<string, ExitSnapshot>> _exitSnapshots = new();
    private List<Alpinist>? _snapAlpinists;
    private readonly Dictionary<string, List<Transit>?> _transitSnapshots = new();

    // D. BTR
    private double _snapBtrWoodsChance, _snapBtrStreetsChance;
    private double _snapBtrWoodsTimeMinX, _snapBtrWoodsTimeMaxY;
    private double _snapBtrStreetsTimeMinX, _snapBtrStreetsTimeMaxY;
    private double _snapBtrTaxiPrice, _snapBtrCoverPrice;
    private double _snapBtrBearMod, _snapBtrUsecMod, _snapBtrScavMod;
    private readonly Dictionary<double, (double X, double Y)> _snapDeliveryGrid = new();
    private readonly Dictionary<double, bool> _snapCanInteractBtr = new();

    // E. Transit
    private readonly Dictionary<double, (double X, double Y)> _snapTransitGrid = new();

    // F. Pre-Raid
    private string _snapAiAmount = "", _snapAiDifficulty = "";
    private bool _snapBossEnabled, _snapScavWars, _snapTaggedAndCursed;
    private bool _snapRandomWeather, _snapRandomTime;
    private double _snapTimeBeforeDeploy, _snapTimeBeforeDeployLocal;
    private double _snapScavHostileChance;

    // G. Airdrops — snapshot per loot type
    private readonly Dictionary<string, AirdropLootSnapshot> _airdropSnapshots = new();
    private readonly Dictionary<string, double> _airdropTypeWeights = new();

    // H. Raid Time — per location: original EscapeTimeLimit values
    private readonly Dictionary<string, RaidTimeSnapshot> _raidTimeSnapshots = new();

    // I. Extract Rewards
    private double _snapCarStanding, _snapCoopStanding, _snapScavStanding;

    // J. Seasonal Events
    private bool _snapSeasonalDetection;

    // K. PMC
    private double _snapPmcUsecRatio;
    private bool _snapPmcUseDiffOverride;
    private string _snapPmcDifficulty = "";
    private int _snapPmcLevelDeltaMin, _snapPmcLevelDeltaMax;
    private double _snapPmcWeaponBackpack, _snapPmcWeaponEnhance;
    private bool _snapPmcForceHealing;

    // Snapshot helper records
    private record ExitSnapshot(
        double? Chance, double? ChancePvE,
        RequirementState PassageRequirement,
        ExfiltrationType? ExfiltrationType,
        double? ExfiltrationTime, double? ExfiltrationTimePvE,
        string? Id, int? Count, int? PlayersCount,
        string? RequirementTip, EquipmentSlots? RequiredSlot);

    private record AirdropLootSnapshot(
        int WeaponMin, int WeaponMax,
        int ArmorMin, int ArmorMax,
        int ItemMin, int ItemMax,
        bool AllowBossItems);

    private record RaidTimeSnapshot(
        double? EscapeTimeLimit, int? EscapeTimeLimitCoop,
        int? EscapeTimeLimitPvE, int? ExitAccessTime);

    // Location IDs for iteration
    private static readonly string[] PlayableLocationIds =
    [
        "bigmap", "factory4_day", "factory4_night", "Interchange",
        "laboratory", "Lighthouse", "RezervBase", "Shoreline",
        "TarkovStreets", "Woods", "Sandbox", "Sandbox_high",
        "city_center"
    ];

    // ═══════════════════════════════════════════════════════
    // INITIALIZE
    // ═══════════════════════════════════════════════════════

    public void Initialize()
    {
        lock (_lock)
        {
            TakeSnapshot();
            ApplyAll();
        }
    }

    // ═══════════════════════════════════════════════════════
    // SNAPSHOT
    // ═══════════════════════════════════════════════════════

    private void TakeSnapshot()
    {
        if (_snapshotTaken) return;

        var globals = databaseService.GetGlobals();
        var cfg = globals.Configuration;
        var lostOnDeath = configServer.GetConfig<LostOnDeathConfig>();
        var inRaid = configServer.GetConfig<InRaidConfig>();
        var traderConfig = configServer.GetConfig<TraderConfig>();
        var locations = databaseService.GetLocations();

        // A. Raid Outcomes
        _snapSurvivedXpReq = cfg.Exp.MatchEnd.SurvivedExperienceRequirement;
        _snapSurvivedSecReq = cfg.Exp.MatchEnd.SurvivedSecondsRequirement;
        _snapQuestItemsLost = lostOnDeath.QuestItems;
        _snapKeepFirSecure = inRaid.KeepFiRSecureContainerOnDeath;
        _snapAlwaysKeepFir = inRaid.AlwaysKeepFoundInRaidOnRaidEnd;
        _snapSandboxLevel = locations.Sandbox?.Base?.RequiredPlayerLevelMax ?? 20;

        // B. Lab
        var lab = locations.Laboratory;
        if (lab?.Base != null)
        {
            _snapLabInsurance = lab.Base.Insurance;
            _snapLabDisabledForScav = lab.Base.DisabledForScav;
            _snapLabAccessKeys = lab.Base.AccessKeys?.ToList();
        }

        // C. Extracts
        _snapFenceCoopGift = traderConfig.Fence.CoopExtractGift.SendGift;

        // Snapshot all exits across all locations
        foreach (var locId in PlayableLocationIds)
        {
            try
            {
                var loc = databaseService.GetLocation(locId);
                if (loc?.Base?.Exits == null) continue;

                var exitMap = new Dictionary<string, ExitSnapshot>();
                foreach (var exit in loc.Base.Exits)
                {
                    var name = exit.Name ?? "";
                    exitMap[name] = new ExitSnapshot(
                        exit.Chance, exit.ChancePVE,
                        exit.PassageRequirement, exit.ExfiltrationType,
                        exit.ExfiltrationTime, exit.ExfiltrationTimePVE,
                        exit.Id, exit.Count, exit.PlayersCount,
                        exit.RequirementTip, exit.RequiredSlot);
                }
                _exitSnapshots[locId] = exitMap;

                // Transit snapshots
                _transitSnapshots[locId] = loc.Base.Transits?.ToList();
            }
            catch { /* skip missing locations */ }
        }

        // Alpinists
        try
        {
            var alpinists = cfg.RequirementReferences?.Alpinists;
            _snapAlpinists = alpinists?.ToList();
        }
        catch { _snapAlpinists = null; }

        // D. BTR
        try
        {
            var locServices = databaseService.GetLocationServices();
            var btrSettings = locServices?.BtrServerSettings;
            if (btrSettings?.ServerMapBTRSettings != null)
            {
                if (btrSettings.ServerMapBTRSettings.TryGetValue("Woods", out var woods))
                {
                    _snapBtrWoodsChance = woods.ChanceSpawn;
                    _snapBtrWoodsTimeMinX = woods.SpawnPeriod?.X ?? 0;
                    _snapBtrWoodsTimeMaxY = woods.SpawnPeriod?.Y ?? 0;
                }
                if (btrSettings.ServerMapBTRSettings.TryGetValue("TarkovStreets", out var streets))
                {
                    _snapBtrStreetsChance = streets.ChanceSpawn;
                    _snapBtrStreetsTimeMinX = streets.SpawnPeriod?.X ?? 0;
                    _snapBtrStreetsTimeMaxY = streets.SpawnPeriod?.Y ?? 0;
                }
            }
        }
        catch (Exception ex) { logger.Warning($"[ZSlayerHQ] BTR snapshot failed: {ex.Message}"); }

        _snapBtrTaxiPrice = cfg.BTRSettings?.BasePriceTaxi ?? 0;
        _snapBtrCoverPrice = cfg.BTRSettings?.CleanUpPrice ?? 0;
        _snapBtrBearMod = cfg.BTRSettings?.BearPriceMod ?? 0;
        _snapBtrUsecMod = cfg.BTRSettings?.UsecPriceMod ?? 0;
        _snapBtrScavMod = cfg.BTRSettings?.ScavPriceMod ?? 0;

        // D+E. Fence levels (delivery grid, transit grid, BTR interaction)
        if (cfg.FenceSettings?.Levels != null)
        {
            foreach (var (rep, level) in cfg.FenceSettings.Levels)
            {
                _snapDeliveryGrid[rep] = (level.DeliveryGridSize?.X ?? 0, level.DeliveryGridSize?.Y ?? 0);
                _snapTransitGrid[rep] = (level.TransitGridSize?.X ?? 0, level.TransitGridSize?.Y ?? 0);
                _snapCanInteractBtr[rep] = level.CanInteractWithBtr;
            }
        }

        // F. Pre-Raid
        var menu = inRaid.RaidMenuSettings;
        _snapAiAmount = menu.AiAmount ?? "AsOnline";
        _snapAiDifficulty = menu.AiDifficulty ?? "AsOnline";
        _snapBossEnabled = menu.BossEnabled;
        _snapScavWars = menu.ScavWars;
        _snapTaggedAndCursed = menu.TaggedAndCursed;
        _snapRandomWeather = menu.RandomWeather;
        _snapRandomTime = menu.RandomTime;
        _snapTimeBeforeDeploy = cfg.TimeBeforeDeploy;
        _snapTimeBeforeDeployLocal = cfg.TimeBeforeDeployLocal;
        _snapScavHostileChance = inRaid.PlayerScavHostileChancePercent;

        // G. Airdrops
        try
        {
            var airdropConfig = configServer.GetConfig<AirdropConfig>();
            if (airdropConfig.AirdropTypeWeightings != null)
            {
                foreach (var (type, weight) in airdropConfig.AirdropTypeWeightings)
                    _airdropTypeWeights[type.ToString()] = weight;
            }
            if (airdropConfig.Loot != null)
            {
                foreach (var (type, loot) in airdropConfig.Loot)
                {
                    _airdropSnapshots[type] = new AirdropLootSnapshot(
                        loot.WeaponPresetCount?.Min ?? 0, loot.WeaponPresetCount?.Max ?? 0,
                        loot.ArmorPresetCount?.Min ?? 0, loot.ArmorPresetCount?.Max ?? 0,
                        loot.ItemCount?.Min ?? 0, loot.ItemCount?.Max ?? 0,
                        loot.AllowBossItems);
                }
            }
        }
        catch (Exception ex) { logger.Warning($"[ZSlayerHQ] Airdrop snapshot failed: {ex.Message}"); }

        // H. Raid Time
        foreach (var locId in PlayableLocationIds)
        {
            try
            {
                var loc = databaseService.GetLocation(locId);
                if (loc?.Base == null) continue;
                _raidTimeSnapshots[locId] = new RaidTimeSnapshot(
                    loc.Base.EscapeTimeLimit, loc.Base.EscapeTimeLimitCoop,
                    loc.Base.EscapeTimeLimitPVE, loc.Base.ExitAccessTime);
            }
            catch { /* skip */ }
        }

        // I. Extract Rewards
        _snapCarStanding = inRaid.CarExtractBaseStandingGain;
        _snapCoopStanding = inRaid.CoopExtractBaseStandingGain;
        _snapScavStanding = inRaid.ScavExtractStandingGain;

        // J. Seasonal Events
        try
        {
            var seasonal = configServer.GetConfig<SeasonalEventConfig>();
            _snapSeasonalDetection = seasonal.EnableSeasonalEventDetection;
        }
        catch { _snapSeasonalDetection = true; }

        // K. PMC
        try
        {
            var pmc = configServer.GetConfig<PmcConfig>();
            _snapPmcUsecRatio = pmc.IsUsec;
            _snapPmcUseDiffOverride = pmc.UseDifficultyOverride;
            _snapPmcDifficulty = pmc.Difficulty ?? "AsOnline";
            _snapPmcLevelDeltaMin = pmc.BotRelativeLevelDelta?.Min ?? 0;
            _snapPmcLevelDeltaMax = pmc.BotRelativeLevelDelta?.Max ?? 0;
            _snapPmcWeaponBackpack = pmc.LooseWeaponInBackpackChancePercent;
            _snapPmcWeaponEnhance = pmc.WeaponHasEnhancementChancePercent;
            _snapPmcForceHealing = pmc.ForceHealingItemsIntoSecure;
        }
        catch (Exception ex) { logger.Warning($"[ZSlayerHQ] PMC snapshot failed: {ex.Message}"); }

        _snapshotTaken = true;
        logger.Info("[ZSlayerHQ] Raid rules snapshots taken");
    }

    // ═══════════════════════════════════════════════════════
    // APPLY ALL
    // ═══════════════════════════════════════════════════════

    public void ApplyAll()
    {
        lock (_lock)
        {
            TakeSnapshot();
            var config = configService.GetConfig().RaidRules;
            ApplyConfig(config);
        }
    }

    private void ApplyConfig(RaidRulesConfig c)
    {
        var globals = databaseService.GetGlobals();
        var cfg = globals.Configuration;
        var lostOnDeath = configServer.GetConfig<LostOnDeathConfig>();
        var inRaid = configServer.GetConfig<InRaidConfig>();
        var traderConfig = configServer.GetConfig<TraderConfig>();
        var locations = databaseService.GetLocations();

        // ── RESTORE ALL from snapshots first ──

        // A
        cfg.Exp.MatchEnd.SurvivedExperienceRequirement = _snapSurvivedXpReq;
        cfg.Exp.MatchEnd.SurvivedSecondsRequirement = _snapSurvivedSecReq;
        lostOnDeath.QuestItems = _snapQuestItemsLost;
        inRaid.KeepFiRSecureContainerOnDeath = _snapKeepFirSecure;
        inRaid.AlwaysKeepFoundInRaidOnRaidEnd = _snapAlwaysKeepFir;
        if (locations.Sandbox?.Base != null)
            locations.Sandbox.Base.RequiredPlayerLevelMax = _snapSandboxLevel;

        // B
        var lab = locations.Laboratory;
        if (lab?.Base != null)
        {
            lab.Base.Insurance = _snapLabInsurance;
            lab.Base.DisabledForScav = _snapLabDisabledForScav;
            if (_snapLabAccessKeys != null)
                lab.Base.AccessKeys = new List<string>(_snapLabAccessKeys);
        }

        // C — Restore exits
        foreach (var (locId, exitMap) in _exitSnapshots)
        {
            try
            {
                var loc = databaseService.GetLocation(locId);
                if (loc?.Base?.Exits == null) continue;
                foreach (var exit in loc.Base.Exits)
                {
                    var name = exit.Name ?? "";
                    if (!exitMap.TryGetValue(name, out var snap)) continue;
                    exit.Chance = snap.Chance;
                    exit.ChancePVE = snap.ChancePvE;
                    exit.PassageRequirement = snap.PassageRequirement;
                    exit.ExfiltrationType = snap.ExfiltrationType;
                    exit.ExfiltrationTime = snap.ExfiltrationTime;
                    exit.ExfiltrationTimePVE = snap.ExfiltrationTimePvE;
                    exit.Id = snap.Id;
                    exit.Count = snap.Count;
                    exit.PlayersCount = snap.PlayersCount;
                    exit.RequirementTip = snap.RequirementTip;
                    exit.RequiredSlot = snap.RequiredSlot;
                }
            }
            catch { /* skip */ }
        }

        // Restore Alpinists
        if (_snapAlpinists != null && cfg.RequirementReferences?.Alpinists != null)
        {
            cfg.RequirementReferences.Alpinists = new List<Alpinist>(_snapAlpinists);
        }

        // Restore transits
        foreach (var (locId, transits) in _transitSnapshots)
        {
            try
            {
                var loc = databaseService.GetLocation(locId);
                if (loc?.Base == null) continue;
                loc.Base.Transits = transits != null ? new List<Transit>(transits) : new List<Transit>();
            }
            catch { /* skip */ }
        }

        traderConfig.Fence.CoopExtractGift.SendGift = _snapFenceCoopGift;

        // D — Restore BTR
        try
        {
            var locServices = databaseService.GetLocationServices();
            var btr = locServices?.BtrServerSettings;
            if (btr?.ServerMapBTRSettings != null)
            {
                if (btr.ServerMapBTRSettings.TryGetValue("Woods", out var woods))
                {
                    woods.ChanceSpawn = _snapBtrWoodsChance;
                    if (woods.SpawnPeriod != null) { woods.SpawnPeriod.X = (float)_snapBtrWoodsTimeMinX; woods.SpawnPeriod.Y = (float)_snapBtrWoodsTimeMaxY; }
                }
                if (btr.ServerMapBTRSettings.TryGetValue("TarkovStreets", out var streets))
                {
                    streets.ChanceSpawn = _snapBtrStreetsChance;
                    if (streets.SpawnPeriod != null) { streets.SpawnPeriod.X = (float)_snapBtrStreetsTimeMinX; streets.SpawnPeriod.Y = (float)_snapBtrStreetsTimeMaxY; }
                }
            }
        }
        catch { /* skip */ }

        if (cfg.BTRSettings != null)
        {
            cfg.BTRSettings.BasePriceTaxi = _snapBtrTaxiPrice;
            cfg.BTRSettings.CleanUpPrice = _snapBtrCoverPrice;
            cfg.BTRSettings.BearPriceMod = _snapBtrBearMod;
            cfg.BTRSettings.UsecPriceMod = _snapBtrUsecMod;
            cfg.BTRSettings.ScavPriceMod = _snapBtrScavMod;
        }

        // D+E — Restore fence levels
        if (cfg.FenceSettings?.Levels != null)
        {
            foreach (var (rep, level) in cfg.FenceSettings.Levels)
            {
                if (_snapDeliveryGrid.TryGetValue(rep, out var dg) && level.DeliveryGridSize != null)
                { level.DeliveryGridSize.X = (float)dg.X; level.DeliveryGridSize.Y = (float)dg.Y; }
                if (_snapTransitGrid.TryGetValue(rep, out var tg) && level.TransitGridSize != null)
                { level.TransitGridSize.X = (float)tg.X; level.TransitGridSize.Y = (float)tg.Y; }
                if (_snapCanInteractBtr.TryGetValue(rep, out var btrFlag))
                    level.CanInteractWithBtr = btrFlag;
            }
        }

        // F — Restore pre-raid
        var menu = inRaid.RaidMenuSettings;
        menu.AiAmount = _snapAiAmount;
        menu.AiDifficulty = _snapAiDifficulty;
        menu.BossEnabled = _snapBossEnabled;
        menu.ScavWars = _snapScavWars;
        menu.TaggedAndCursed = _snapTaggedAndCursed;
        menu.RandomWeather = _snapRandomWeather;
        menu.RandomTime = _snapRandomTime;
        cfg.TimeBeforeDeploy = _snapTimeBeforeDeploy;
        cfg.TimeBeforeDeployLocal = _snapTimeBeforeDeployLocal;
        inRaid.PlayerScavHostileChancePercent = _snapScavHostileChance;

        // G — Restore airdrops
        try
        {
            var airdropConfig = configServer.GetConfig<AirdropConfig>();
            if (airdropConfig.AirdropTypeWeightings != null)
            {
                foreach (var (typeStr, weight) in _airdropTypeWeights)
                {
                    if (Enum.TryParse<SptAirdropTypeEnum>(typeStr, out var enumVal))
                        airdropConfig.AirdropTypeWeightings[enumVal] = weight;
                }
            }
            if (airdropConfig.Loot != null)
            {
                foreach (var (type, snap) in _airdropSnapshots)
                {
                    if (!airdropConfig.Loot.TryGetValue(type, out var loot)) continue;
                    if (loot.WeaponPresetCount != null) { loot.WeaponPresetCount.Min = snap.WeaponMin; loot.WeaponPresetCount.Max = snap.WeaponMax; }
                    if (loot.ArmorPresetCount != null) { loot.ArmorPresetCount.Min = snap.ArmorMin; loot.ArmorPresetCount.Max = snap.ArmorMax; }
                    if (loot.ItemCount != null) { loot.ItemCount.Min = snap.ItemMin; loot.ItemCount.Max = snap.ItemMax; }
                    loot.AllowBossItems = snap.AllowBossItems;
                }
            }
        }
        catch { /* skip */ }

        // H — Restore raid times
        foreach (var (locId, snap) in _raidTimeSnapshots)
        {
            try
            {
                var loc = databaseService.GetLocation(locId);
                if (loc?.Base == null) continue;
                loc.Base.EscapeTimeLimit = snap.EscapeTimeLimit;
                loc.Base.EscapeTimeLimitCoop = snap.EscapeTimeLimitCoop;
                loc.Base.EscapeTimeLimitPVE = snap.EscapeTimeLimitPvE;
                loc.Base.ExitAccessTime = snap.ExitAccessTime;
            }
            catch { /* skip */ }
        }

        // I — Restore extract rewards
        inRaid.CarExtractBaseStandingGain = _snapCarStanding;
        inRaid.CoopExtractBaseStandingGain = _snapCoopStanding;
        inRaid.ScavExtractStandingGain = _snapScavStanding;

        // J — Restore seasonal
        try
        {
            var seasonal = configServer.GetConfig<SeasonalEventConfig>();
            seasonal.EnableSeasonalEventDetection = _snapSeasonalDetection;
        }
        catch { /* skip */ }

        // K — Restore PMC
        try
        {
            var pmc = configServer.GetConfig<PmcConfig>();
            pmc.IsUsec = _snapPmcUsecRatio;
            pmc.UseDifficultyOverride = _snapPmcUseDiffOverride;
            pmc.Difficulty = _snapPmcDifficulty;
            if (pmc.BotRelativeLevelDelta != null) { pmc.BotRelativeLevelDelta.Min = _snapPmcLevelDeltaMin; pmc.BotRelativeLevelDelta.Max = _snapPmcLevelDeltaMax; }
            pmc.LooseWeaponInBackpackChancePercent = _snapPmcWeaponBackpack;
            pmc.WeaponHasEnhancementChancePercent = _snapPmcWeaponEnhance;
            pmc.ForceHealingItemsIntoSecure = _snapPmcForceHealing;
        }
        catch { /* skip */ }

        // ── NOW APPLY user config on top ──

        // A. Raid Outcomes
        if (c.NoRunThrough)
        {
            cfg.Exp.MatchEnd.SurvivedExperienceRequirement = 0;
            cfg.Exp.MatchEnd.SurvivedSecondsRequirement = 0;
        }
        if (c.SaveQuestItemsOnDeath) lostOnDeath.QuestItems = false; // inverted
        if (c.KeepFirSecureOnDeath.HasValue) inRaid.KeepFiRSecureContainerOnDeath = c.KeepFirSecureOnDeath.Value;
        if (c.AlwaysKeepFirOnRaidEnd.HasValue) inRaid.AlwaysKeepFoundInRaidOnRaidEnd = c.AlwaysKeepFirOnRaidEnd.Value;
        if (c.SandboxAccessLevel.HasValue && locations.Sandbox?.Base != null)
            locations.Sandbox.Base.RequiredPlayerLevelMax = c.SandboxAccessLevel.Value;

        // B. Lab
        if (lab?.Base != null)
        {
            if (c.LabInsurance) lab.Base.Insurance = true;
            if (c.RemoveLabKey) lab.Base.AccessKeys = [];
            if (c.ScavLabAccess) lab.Base.DisabledForScav = false;
        }

        // C. Extracts
        if (c.FenceCoopGift.HasValue) traderConfig.Fence.CoopExtractGift.SendGift = c.FenceCoopGift.Value;

        foreach (var locId in PlayableLocationIds)
        {
            try
            {
                var loc = databaseService.GetLocation(locId);
                if (loc?.Base?.Exits == null) continue;

                foreach (var exit in loc.Base.Exits)
                {
                    // Guaranteed extracts
                    if (c.GuaranteedExtracts && (exit.Chance ?? 0) > 0)
                    {
                        exit.Chance = 100;
                        exit.ChancePVE = 100;
                    }

                    // Free car extracts
                    if (c.FreeCarExtracts && exit.PassageRequirement == RequirementState.TransferItem)
                        FreeExit(exit);

                    // Free coop extracts
                    if (c.FreeCoopExtracts && exit.PassageRequirement == RequirementState.ScavCooperation)
                        FreeExit(exit);

                    // Remove backpack requirement
                    if (c.RemoveBackpackExtractReq && exit.PassageRequirement == RequirementState.Empty)
                        FreeExit(exit);

                    // Car extract wait time
                    if (c.CarExtractWaitTime.HasValue && exit.PassageRequirement == RequirementState.TransferItem)
                    {
                        exit.ExfiltrationTime = c.CarExtractWaitTime.Value;
                        exit.ExfiltrationTimePVE = c.CarExtractWaitTime.Value;
                    }
                }

                // Disable transits
                if (c.DisableTransits) loc.Base.Transits = [];
            }
            catch { /* skip */ }
        }

        // Remove gear/armor extract req (Alpinists manipulation)
        if (c.RemoveGearExtractReq && cfg.RequirementReferences?.Alpinists != null)
        {
            cfg.RequirementReferences.Alpinists = [];
        }

        // D. BTR
        if (c.EnableBtr)
        {
            try
            {
                var locServices = databaseService.GetLocationServices();
                var btr = locServices?.BtrServerSettings;
                if (btr?.ServerMapBTRSettings != null)
                {
                    if (btr.ServerMapBTRSettings.TryGetValue("Woods", out var woods))
                    {
                        if (c.BtrWoodsChance.HasValue) woods.ChanceSpawn = c.BtrWoodsChance.Value;
                        if (woods.SpawnPeriod != null)
                        {
                            if (c.BtrWoodsTimeMin.HasValue) woods.SpawnPeriod.X = (float)(c.BtrWoodsTimeMin.Value * 60);
                            if (c.BtrWoodsTimeMax.HasValue) woods.SpawnPeriod.Y = (float)(c.BtrWoodsTimeMax.Value * 60);
                        }
                    }
                    if (btr.ServerMapBTRSettings.TryGetValue("TarkovStreets", out var streets))
                    {
                        if (c.BtrStreetsChance.HasValue) streets.ChanceSpawn = c.BtrStreetsChance.Value;
                        if (streets.SpawnPeriod != null)
                        {
                            if (c.BtrStreetsTimeMin.HasValue) streets.SpawnPeriod.X = (float)(c.BtrStreetsTimeMin.Value * 60);
                            if (c.BtrStreetsTimeMax.HasValue) streets.SpawnPeriod.Y = (float)(c.BtrStreetsTimeMax.Value * 60);
                        }
                    }
                }
            }
            catch { /* skip */ }

            if (cfg.BTRSettings != null)
            {
                if (c.BtrTaxiPrice.HasValue) cfg.BTRSettings.BasePriceTaxi = c.BtrTaxiPrice.Value;
                if (c.BtrCoverPrice.HasValue) cfg.BTRSettings.CleanUpPrice = c.BtrCoverPrice.Value;
                if (c.BtrBearMod.HasValue) cfg.BTRSettings.BearPriceMod = c.BtrBearMod.Value;
                if (c.BtrUsecMod.HasValue) cfg.BTRSettings.UsecPriceMod = c.BtrUsecMod.Value;
                if (c.BtrScavMod.HasValue) cfg.BTRSettings.ScavPriceMod = c.BtrScavMod.Value;
            }

            // Delivery grid + force friendly
            if (cfg.FenceSettings?.Levels != null)
            {
                foreach (var (_, level) in cfg.FenceSettings.Levels)
                {
                    if (level.DeliveryGridSize != null)
                    {
                        if (c.BtrDeliveryW.HasValue) level.DeliveryGridSize.X = c.BtrDeliveryW.Value;
                        if (c.BtrDeliveryH.HasValue) level.DeliveryGridSize.Y = c.BtrDeliveryH.Value;
                    }
                    if (c.ForceBtrFriendly) level.CanInteractWithBtr = true;
                }
            }
        }

        // E. Transit stash
        if ((c.TransitStashW.HasValue || c.TransitStashH.HasValue) && cfg.FenceSettings?.Levels != null)
        {
            foreach (var (_, level) in cfg.FenceSettings.Levels)
            {
                if (level.TransitGridSize == null) continue;
                if (c.TransitStashW.HasValue) level.TransitGridSize.X = c.TransitStashW.Value;
                if (c.TransitStashH.HasValue) level.TransitGridSize.Y = c.TransitStashH.Value;
            }
        }

        // F. Pre-Raid Defaults
        if (c.DefaultAiAmount != null) menu.AiAmount = c.DefaultAiAmount;
        if (c.DefaultAiDifficulty != null) menu.AiDifficulty = c.DefaultAiDifficulty;
        if (c.DefaultBossEnabled.HasValue) menu.BossEnabled = c.DefaultBossEnabled.Value;
        if (c.DefaultScavWars.HasValue) menu.ScavWars = c.DefaultScavWars.Value;
        if (c.DefaultTaggedAndCursed.HasValue) menu.TaggedAndCursed = c.DefaultTaggedAndCursed.Value;
        if (c.DefaultRandomWeather.HasValue) menu.RandomWeather = c.DefaultRandomWeather.Value;
        if (c.DefaultRandomTime.HasValue) menu.RandomTime = c.DefaultRandomTime.Value;
        if (c.TimeBeforeDeploy.HasValue)
        {
            cfg.TimeBeforeDeploy = c.TimeBeforeDeploy.Value;
            cfg.TimeBeforeDeployLocal = c.TimeBeforeDeploy.Value;
        }
        if (c.ScavHostileChance.HasValue) inRaid.PlayerScavHostileChancePercent = c.ScavHostileChance.Value;

        // G. Airdrops
        if (c.EnableAirdrops)
        {
            try
            {
                var airdropConfig = configServer.GetConfig<AirdropConfig>();
                if (airdropConfig.Loot != null)
                {
                    foreach (var (type, loot) in airdropConfig.Loot)
                    {
                        if (c.AirdropWeaponCountMin.HasValue && loot.WeaponPresetCount != null) loot.WeaponPresetCount.Min = c.AirdropWeaponCountMin.Value;
                        if (c.AirdropWeaponCountMax.HasValue && loot.WeaponPresetCount != null) loot.WeaponPresetCount.Max = c.AirdropWeaponCountMax.Value;
                        if (c.AirdropArmorCountMin.HasValue && loot.ArmorPresetCount != null) loot.ArmorPresetCount.Min = c.AirdropArmorCountMin.Value;
                        if (c.AirdropArmorCountMax.HasValue && loot.ArmorPresetCount != null) loot.ArmorPresetCount.Max = c.AirdropArmorCountMax.Value;
                        if (c.AirdropItemCountMin.HasValue && loot.ItemCount != null) loot.ItemCount.Min = c.AirdropItemCountMin.Value;
                        if (c.AirdropItemCountMax.HasValue && loot.ItemCount != null) loot.ItemCount.Max = c.AirdropItemCountMax.Value;
                        if (c.AirdropAllowBossItems.HasValue) loot.AllowBossItems = c.AirdropAllowBossItems.Value;
                    }
                }
            }
            catch { /* skip */ }
        }

        // H. Raid Time
        if (c.RaidTimeAddMinutes != 0)
        {
            var addSec = c.RaidTimeAddMinutes * 60.0;
            foreach (var (locId, snap) in _raidTimeSnapshots)
            {
                try
                {
                    var loc = databaseService.GetLocation(locId);
                    if (loc?.Base == null) continue;
                    loc.Base.EscapeTimeLimit = (snap.EscapeTimeLimit ?? 0) + addSec;
                    loc.Base.EscapeTimeLimitCoop = (int)((snap.EscapeTimeLimitCoop ?? 0) + addSec);
                    loc.Base.EscapeTimeLimitPVE = (int)((snap.EscapeTimeLimitPvE ?? 0) + addSec);
                    loc.Base.ExitAccessTime = (int)((snap.ExitAccessTime ?? 0) + addSec);
                }
                catch { /* skip */ }
            }
        }

        // I. Extract Rewards
        if (c.CarExtractStanding.HasValue) inRaid.CarExtractBaseStandingGain = c.CarExtractStanding.Value;
        if (c.CoopExtractStanding.HasValue) inRaid.CoopExtractBaseStandingGain = c.CoopExtractStanding.Value;
        if (c.ScavExtractStanding.HasValue) inRaid.ScavExtractStandingGain = c.ScavExtractStanding.Value;

        // J. Seasonal Events
        if (c.EnableSeasonalDetection.HasValue)
        {
            try
            {
                var seasonal = configServer.GetConfig<SeasonalEventConfig>();
                seasonal.EnableSeasonalEventDetection = c.EnableSeasonalDetection.Value;
            }
            catch { /* skip */ }
        }

        // K. PMC
        if (c.EnablePmc)
        {
            try
            {
                var pmc = configServer.GetConfig<PmcConfig>();
                if (c.PmcUsecRatio.HasValue) pmc.IsUsec = c.PmcUsecRatio.Value;
                if (c.PmcUseDifficultyOverride.HasValue) pmc.UseDifficultyOverride = c.PmcUseDifficultyOverride.Value;
                if (c.PmcDifficulty != null) pmc.Difficulty = c.PmcDifficulty;
                if (pmc.BotRelativeLevelDelta != null)
                {
                    if (c.PmcLevelDeltaMin.HasValue) pmc.BotRelativeLevelDelta.Min = c.PmcLevelDeltaMin.Value;
                    if (c.PmcLevelDeltaMax.HasValue) pmc.BotRelativeLevelDelta.Max = c.PmcLevelDeltaMax.Value;
                }
                if (c.PmcWeaponInBackpackChance.HasValue) pmc.LooseWeaponInBackpackChancePercent = c.PmcWeaponInBackpackChance.Value;
                if (c.PmcWeaponEnhancementChance.HasValue) pmc.WeaponHasEnhancementChancePercent = c.PmcWeaponEnhancementChance.Value;
                if (c.PmcForceHealingInSecure.HasValue) pmc.ForceHealingItemsIntoSecure = c.PmcForceHealingInSecure.Value;
            }
            catch { /* skip */ }
        }

        logger.Info("[ZSlayerHQ] Raid rules applied");
    }

    private static void FreeExit(Exit exit)
    {
        exit.PassageRequirement = RequirementState.None;
        exit.ExfiltrationType = ExfiltrationType.Individual;
        exit.Id = "";
        exit.Count = 0;
        exit.PlayersCount = 0;
        exit.RequirementTip = "";
        exit.RequiredSlot = null;
    }

    // ═══════════════════════════════════════════════════════
    // GET CONFIG (for frontend)
    // ═══════════════════════════════════════════════════════

    public RaidRulesConfigResponse GetConfig()
    {
        lock (_lock)
        {
            TakeSnapshot();
            var config = configService.GetConfig().RaidRules;

            // Build first airdrop loot snapshot for defaults (use first available type)
            var firstAirdrop = _airdropSnapshots.Values.FirstOrDefault();

            var defaults = new RaidRulesDefaults
            {
                // A
                SurvivedXpReq = _snapSurvivedXpReq,
                SurvivedSecReq = _snapSurvivedSecReq,
                QuestItemsLost = _snapQuestItemsLost,
                KeepFirSecure = _snapKeepFirSecure,
                AlwaysKeepFir = _snapAlwaysKeepFir,
                SandboxLevel = _snapSandboxLevel,
                // B
                LabInsurance = _snapLabInsurance,
                LabAccessKeyCount = _snapLabAccessKeys?.Count ?? 0,
                LabDisabledForScav = _snapLabDisabledForScav ?? false,
                // C
                FenceCoopGift = _snapFenceCoopGift,
                // D
                BtrWoodsChance = _snapBtrWoodsChance,
                BtrStreetsChance = _snapBtrStreetsChance,
                BtrWoodsTimeMin = _snapBtrWoodsTimeMinX / 60.0,
                BtrWoodsTimeMax = _snapBtrWoodsTimeMaxY / 60.0,
                BtrStreetsTimeMin = _snapBtrStreetsTimeMinX / 60.0,
                BtrStreetsTimeMax = _snapBtrStreetsTimeMaxY / 60.0,
                BtrTaxiPrice = _snapBtrTaxiPrice,
                BtrCoverPrice = _snapBtrCoverPrice,
                BtrBearMod = _snapBtrBearMod,
                BtrUsecMod = _snapBtrUsecMod,
                BtrScavMod = _snapBtrScavMod,
                BtrDeliveryW = _snapDeliveryGrid.Values.FirstOrDefault().X is var dw and > 0 ? (int)dw : 0,
                BtrDeliveryH = _snapDeliveryGrid.Values.FirstOrDefault().Y is var dh and > 0 ? (int)dh : 0,
                // E
                TransitStashW = _snapTransitGrid.Values.FirstOrDefault().X is var tw and > 0 ? (int)tw : 0,
                TransitStashH = _snapTransitGrid.Values.FirstOrDefault().Y is var th and > 0 ? (int)th : 0,
                // F
                AiAmount = _snapAiAmount,
                AiDifficulty = _snapAiDifficulty,
                BossEnabled = _snapBossEnabled,
                ScavWars = _snapScavWars,
                TaggedAndCursed = _snapTaggedAndCursed,
                RandomWeather = _snapRandomWeather,
                RandomTime = _snapRandomTime,
                TimeBeforeDeploy = _snapTimeBeforeDeploy,
                ScavHostileChance = _snapScavHostileChance,
                // G
                AirdropWeaponCountMin = firstAirdrop?.WeaponMin ?? 0,
                AirdropWeaponCountMax = firstAirdrop?.WeaponMax ?? 0,
                AirdropArmorCountMin = firstAirdrop?.ArmorMin ?? 0,
                AirdropArmorCountMax = firstAirdrop?.ArmorMax ?? 0,
                AirdropItemCountMin = firstAirdrop?.ItemMin ?? 0,
                AirdropItemCountMax = firstAirdrop?.ItemMax ?? 0,
                AirdropAllowBossItems = firstAirdrop?.AllowBossItems ?? false,
                AirdropTypes = _airdropTypeWeights.Select(kv => new AirdropTypeInfo { Type = kv.Key, Weight = kv.Value }).ToList(),
                // I
                CarExtractStanding = _snapCarStanding,
                CoopExtractStanding = _snapCoopStanding,
                ScavExtractStanding = _snapScavStanding,
                // J
                SeasonalDetection = _snapSeasonalDetection,
                SeasonalEvents = GetSeasonalEvents(),
                // K
                PmcUsecRatio = _snapPmcUsecRatio,
                PmcUseDifficultyOverride = _snapPmcUseDiffOverride,
                PmcDifficulty = _snapPmcDifficulty,
                PmcLevelDeltaMin = _snapPmcLevelDeltaMin,
                PmcLevelDeltaMax = _snapPmcLevelDeltaMax,
                PmcWeaponInBackpackChance = _snapPmcWeaponBackpack,
                PmcWeaponEnhancementChance = _snapPmcWeaponEnhance,
                PmcForceHealingInSecure = _snapPmcForceHealing,
            };

            return new RaidRulesConfigResponse { Config = config, Defaults = defaults };
        }
    }

    private List<SeasonalEventInfo> GetSeasonalEvents()
    {
        try
        {
            var seasonal = configServer.GetConfig<SeasonalEventConfig>();
            if (seasonal.Events == null) return [];
            return seasonal.Events.Select(e => new SeasonalEventInfo
            {
                Name = e.Name ?? e.Type.ToString(),
                Type = e.Type.ToString(),
                Enabled = true // All enabled by default in SPT
            }).ToList();
        }
        catch { return []; }
    }

    // ═══════════════════════════════════════════════════════
    // RESET
    // ═══════════════════════════════════════════════════════

    public void Reset()
    {
        lock (_lock)
        {
            var ccConfig = configService.GetConfig();
            ccConfig.RaidRules = new RaidRulesConfig();
            configService.SaveConfig();
            ApplyConfig(ccConfig.RaidRules);
        }
    }

    // ═══════════════════════════════════════════════════════
    // APPLY FROM REQUEST
    // ═══════════════════════════════════════════════════════

    public void Apply(RaidRulesConfig incoming)
    {
        lock (_lock)
        {
            var ccConfig = configService.GetConfig();
            ccConfig.RaidRules = incoming;
            configService.SaveConfig();
            ApplyConfig(incoming);
        }
    }
}

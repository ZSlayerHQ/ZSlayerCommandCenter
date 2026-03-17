using System.Reflection;
using System.Text.Json;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Hideout;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using ZSlayerCommandCenter.Models;
using HideoutCfg = SPTarkov.Server.Core.Models.Spt.Config.HideoutConfig;
using IOPath = System.IO.Path;

namespace ZSlayerCommandCenter.Services;

[Injectable(InjectionType.Singleton)]
public class HideoutService(
    DatabaseService databaseService,
    ConfigServer configServer,
    ConfigService configService,
    SaveServer saveServer,
    LocaleService localeService,
    ModHelper modHelper,
    ISptLogger<HideoutService> logger)
{
    private readonly object _lock = new();
    private bool _snapshotTaken;

    // ── Snapshot records (types match SPT's nullable API) ──
    private record ConstructionSnapshot(double? ConstructionTime);
    private record ProductionSnapshot(double? ProductionTime, bool? Continuous, int? ProductionLimitCount, bool? Locked);
    private record ScavRecipeSnapshot(double? ProductionTime, int? Count, string? TemplateId);
    private record CultistSnapshot(int HideoutTaskRewardTime, List<int> CraftTimeThresholds, List<int> DirectRewardTimes, int MaxRewardItemCount);
    private record FuelSnapshot(double? FuelFlowRate, double? GeneratorSpeedWithoutFuel, double? AirFilterFlowRate, double? GpuBoostRate);
    private record FarmingSnapshot(double? BitcoinTime, int? MaxBitcoins, double? WaterFilterTime, int? WaterFilterRate);
    private record StashSnapshot(string StashId, int CellsV);
    private record HealthRegenSnapshot(double Head, double Chest, double Stomach, double LeftArm, double RightArm, double LeftLeg, double RightLeg, double Energy, double Hydration);
    private record BonusSnapshot(string AreaType, string StageKey, int BonusIndex, string BonusTypeName, double? OriginalValue);
    private record RequirementCountSnapshot(int? Count);

    // ── Snapshot storage ──
    private readonly Dictionary<string, ConstructionSnapshot> _constructionSnapshots = new();
    private readonly Dictionary<string, List<StageRequirement>> _requirementSnapshots = new();
    private readonly Dictionary<string, ProductionSnapshot> _productionSnapshots = new();
    private readonly Dictionary<string, ScavRecipeSnapshot> _scavRecipeSnapshots = new();
    private CultistSnapshot? _cultistSnapshot;
    private FuelSnapshot? _fuelSnapshot;
    private FarmingSnapshot? _farmingSnapshot;
    private readonly List<StashSnapshot> _stashSnapshots = [];
    private HealthRegenSnapshot? _healthRegenSnapshot;
    private readonly List<BonusSnapshot> _bonusSnapshots = [];
    private readonly Dictionary<int, List<QuestCondition>> _customisationSnapshots = new();
    private readonly Dictionary<string, List<RequirementCountSnapshot>> _requirementCountSnapshots = new();
    private readonly HashSet<string> _arenaRecipeIds = new();
    private readonly List<HideoutProduction> _arenaRecipeObjects = [];

    // ── Area name lookup ──
    private static readonly Dictionary<string, string> AreaNames = new()
    {
        ["NotSet"] = "Not Set", ["Vents"] = "Vents", ["Security"] = "Security",
        ["WaterCloset"] = "Lavatory", ["Stash"] = "Stash", ["Generator"] = "Generator",
        ["Heating"] = "Heating", ["WaterCollector"] = "Water Collector", ["MedStation"] = "Medstation",
        ["Kitchen"] = "Nutrition Unit", ["RestSpace"] = "Rest Space", ["Workbench"] = "Workbench",
        ["IntelligenceCenter"] = "Intelligence Center", ["ShootingRange"] = "Shooting Range",
        ["Library"] = "Library", ["ScavCase"] = "Scav Case", ["Illumination"] = "Illumination",
        ["PlaceOfFame"] = "Place of Fame", ["AirFilteringUnit"] = "Air Filtering Unit",
        ["SolarPower"] = "Solar Power", ["BoozeGenerator"] = "Booze Generator",
        ["BitcoinFarm"] = "Bitcoin Farm", ["ChristmasIllumination"] = "Christmas Tree",
        ["EmergencyWall"] = "Emergency Wall", ["Gym"] = "Gym",
        ["WeaponStand"] = "Weapon Stand", ["WeaponStandSecondary"] = "Weapon Stand 2",
        ["EquipmentPresetsStand"] = "Equipment Stand", ["CircleOfCultists"] = "Cultist Circle"
    };

    // ── Non-editable bonus types (unlock types, text types — Value is meaningless) ──
    private static readonly HashSet<BonusType> NonEditableBonusTypes =
    [
        BonusType.UnlockItemCraft, BonusType.UnlockItemPassiveCreation, BonusType.UnlockRandomItemCreation,
        BonusType.UnlockWeaponModification, BonusType.UnlockScavPlay, BonusType.UnlockAddOffer,
        BonusType.UnlockItemCharge, BonusType.UnlockUniqueId, BonusType.UnlockWeaponRepair,
        BonusType.UnlockArmorRepair, BonusType.TextBonus, BonusType.StashRows
    ];

    // ── Arena crate item IDs ──
    private static readonly HashSet<string> ArenaCrateIds =
    [
        "67899e04e15e3c5f5c04dac9", "67899e04e15e3c5f5c04dacd", "67899e04e15e3c5f5c04dad1",
        "67899e04e15e3c5f5c04dad5", "67899e04e15e3c5f5c04dad9", "67899e04e15e3c5f5c04dadd",
        "67899e04e15e3c5f5c04dae1", "67899e04e15e3c5f5c04dae5", "67899e04e15e3c5f5c04dae9",
        "67899e04e15e3c5f5c04daed", "67899e04e15e3c5f5c04daf1", "67899e04e15e3c5f5c04daf5"
    ];

    private static readonly (string Id, string Name)[] StashEditions =
    [
        ("566abbc34bdc2d92178b4576", "Standard"),
        ("5811ce572459770cba1a34ea", "Left Behind"),
        ("5811ce662459770f6f490f32", "Prepare for Escape"),
        ("5811ce772459770e9e5f9532", "Edge of Darkness"),
        ("6602bcf19cc643f44a04274b", "The Unheard Edition")
    ];

    private static readonly HashSet<string> CurrencyIds =
    [
        "5449016a4bdc2d6f028b456f", // Roubles
        "5696686a4bdc2da3298b456a", // Dollars
        "569668774bdc2da2298b4568"  // Euros
    ];

    private const string BitcoinRecipeId = "5d5c205bd582a50d042a3c0e";
    private const string WaterFilterRecipeId = "5d5589c1f934db045e6c5492";

    private static readonly JsonSerializerOptions PresetJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public void Initialize()
    {
        lock (_lock)
        {
            EnsureSnapshot();
            var count = ApplyAll();
            if (count > 0)
                logger.Success($"[ZSlayerHQ] Hideout: applied {count} override(s)");
            else
                logger.Info("[ZSlayerHQ] Hideout: initialized (no overrides)");
        }
    }

    private static string GetAreaName(string areaType) =>
        AreaNames.TryGetValue(areaType, out var name) ? name : areaType;

    // ═══════════════════════════════════════════════════════
    // SNAPSHOT
    // ═══════════════════════════════════════════════════════

    private void EnsureSnapshot()
    {
        if (_snapshotTaken) return;

        var hideout = databaseService.GetHideout();
        var globals = databaseService.GetGlobals();
        var items = databaseService.GetItems();
        var hideoutConfig = configServer.GetConfig<HideoutCfg>();

        // ── A. Construction snapshots ──
        foreach (var area in hideout.Areas)
        {
            var areaType = area.Type?.ToString() ?? "";
            foreach (var (stageKey, stage) in area.Stages)
            {
                var key = $"{areaType}|{stageKey}";
                _constructionSnapshots[key] = new ConstructionSnapshot(stage.ConstructionTime);
                _requirementSnapshots[key] = stage.Requirements.ToList();

                // Requirement count snapshots for cost scaling
                var counts = new List<RequirementCountSnapshot>();
                foreach (var req in stage.Requirements)
                    counts.Add(new RequirementCountSnapshot(req.Count));
                _requirementCountSnapshots[key] = counts;
            }

            // Snapshot ALL bonuses with non-null Value (expanded from regen-only)
            foreach (var (stageKey, stage) in area.Stages)
            {
                for (int i = 0; i < stage.Bonuses.Count; i++)
                {
                    var bonus = stage.Bonuses[i];
                    if (bonus.Value != null)
                        _bonusSnapshots.Add(new BonusSnapshot(areaType, stageKey, i, bonus.Type?.ToString() ?? "", bonus.Value));
                }
            }
        }

        // Customisation snapshots
        if (hideout.Customisation?.Globals != null)
        {
            for (int i = 0; i < hideout.Customisation.Globals.Count; i++)
            {
                var custom = hideout.Customisation.Globals[i];
                _customisationSnapshots[i] = custom.Conditions?.ToList() ?? [];
            }
        }

        // ── B. Production snapshots (now includes Locked state) ──
        foreach (var recipe in hideout.Production.Recipes)
        {
            var id = recipe.Id.ToString();
            _productionSnapshots[id] = new ProductionSnapshot(
                recipe.ProductionTime,
                recipe.Continuous,
                recipe.ProductionLimitCount,
                recipe.Locked);

            // Identify arena crate recipes for removal support
            foreach (var req in recipe.Requirements)
            {
                if (req.TemplateId is { } arenaTpl && !arenaTpl.IsEmpty && ArenaCrateIds.Contains(arenaTpl.ToString()))
                {
                    _arenaRecipeIds.Add(id);
                    _arenaRecipeObjects.Add(recipe);
                    break;
                }
            }
        }

        foreach (var recipe in hideout.Production.ScavRecipes)
        {
            var id = recipe.Id.ToString();
            var firstReq = recipe.Requirements.Count > 0 ? recipe.Requirements[0] : null;
            _scavRecipeSnapshots[id] = new ScavRecipeSnapshot(
                recipe.ProductionTime,
                firstReq?.Count,
                firstReq?.TemplateId is { } tpl && !tpl.IsEmpty ? tpl.ToString() : null);
        }

        // Cultist circle
        _cultistSnapshot = new CultistSnapshot(
            hideoutConfig.CultistCircle.HideoutTaskRewardTimeSeconds,
            hideoutConfig.CultistCircle.CraftTimeThresholds.Select(c => c.CraftTimeSeconds).ToList(),
            hideoutConfig.CultistCircle.DirectRewards.Select(c => c.CraftTimeSeconds).ToList(),
            hideoutConfig.CultistCircle.MaxRewardItemCount);

        // ── C. Fuel & Power snapshots ──
        _fuelSnapshot = new FuelSnapshot(
            hideout.Settings.GeneratorFuelFlowRate,
            hideout.Settings.GeneratorSpeedWithoutFuel,
            hideout.Settings.AirFilterUnitFlowRate,
            hideout.Settings.GpuBoostRate);

        // ── D. Farming snapshots ──
        double? btcTime = 0; int? maxBtc = 0; double? wfTime = 0; int? wfRate = 0;
        foreach (var recipe in hideout.Production.Recipes)
        {
            var id = recipe.Id.ToString();
            if (id == BitcoinRecipeId)
            {
                btcTime = recipe.ProductionTime;
                maxBtc = recipe.ProductionLimitCount ?? 3;
            }
            else if (id == WaterFilterRecipeId)
            {
                wfTime = recipe.ProductionTime;
                wfRate = recipe.Requirements.Count > 1 ? (int?)recipe.Requirements[1].Resource ?? 0 : 0;
            }
        }
        _farmingSnapshot = new FarmingSnapshot(btcTime, maxBtc, wfTime, wfRate);

        // ── E. Stash snapshots ──
        _stashSnapshots.Clear();
        foreach (var (stashId, _) in StashEditions)
        {
            if (items.TryGetValue(stashId, out var item) && item.Properties?.Grids != null)
            {
                var grids = item.Properties.Grids.ToList();
                if (grids.Count > 0)
                    _stashSnapshots.Add(new StashSnapshot(stashId, grids[0].Properties.CellsV ?? 0));
            }
        }

        // ── F. Health regen snapshots ──
        var bodyHealth = globals.Configuration.Health.Effects.Regeneration.BodyHealth;
        _healthRegenSnapshot = new HealthRegenSnapshot(
            bodyHealth.Head.Value,
            bodyHealth.Chest.Value,
            bodyHealth.Stomach.Value,
            bodyHealth.LeftArm.Value,
            bodyHealth.RightArm.Value,
            bodyHealth.LeftLeg.Value,
            bodyHealth.RightLeg.Value,
            globals.Configuration.Health.Effects.Regeneration.Energy,
            globals.Configuration.Health.Effects.Regeneration.Hydration);

        _snapshotTaken = true;
        logger.Info("[ZSlayerHQ] Hideout: snapshots taken");
    }

    // ═══════════════════════════════════════════════════════
    // APPLY
    // ═══════════════════════════════════════════════════════

    public int ApplyAll()
    {
        lock (_lock)
        {
            if (!_snapshotTaken) EnsureSnapshot();
            return ApplyInternal();
        }
    }

    private int ApplyInternal()
    {
        var cfg = configService.GetConfig().Hideout;
        var hideout = databaseService.GetHideout();
        var globals = databaseService.GetGlobals();
        var items = databaseService.GetItems();
        var hideoutConfig = configServer.GetConfig<HideoutCfg>();
        int changes = 0;

        // ══════════════════════════════════════════
        // RESTORE all values from snapshot first
        // ══════════════════════════════════════════

        foreach (var area in hideout.Areas)
        {
            var areaType = area.Type?.ToString() ?? "";
            foreach (var (stageKey, stage) in area.Stages)
            {
                var key = $"{areaType}|{stageKey}";
                if (_constructionSnapshots.TryGetValue(key, out var snap))
                    stage.ConstructionTime = snap.ConstructionTime;
                if (_requirementSnapshots.TryGetValue(key, out var reqSnap))
                    stage.Requirements = reqSnap.ToList();

                // Restore requirement counts from snapshot
                if (_requirementCountSnapshots.TryGetValue(key, out var countSnaps))
                {
                    for (int i = 0; i < stage.Requirements.Count && i < countSnaps.Count; i++)
                        stage.Requirements[i].Count = countSnaps[i].Count;
                }
            }
        }

        if (hideout.Customisation?.Globals != null)
        {
            for (int i = 0; i < hideout.Customisation.Globals.Count; i++)
            {
                if (_customisationSnapshots.TryGetValue(i, out var origConds))
                    hideout.Customisation.Globals[i].Conditions = origConds.ToList();
            }
        }

        // Re-add any arena recipes that were removed in the previous apply cycle
        if (_arenaRecipeObjects.Count > 0)
        {
            var existingIds = new HashSet<string>();
            foreach (var r in hideout.Production.Recipes)
                existingIds.Add(r.Id.ToString());
            foreach (var arenaRecipe in _arenaRecipeObjects)
            {
                if (!existingIds.Contains(arenaRecipe.Id.ToString()))
                    hideout.Production.Recipes.Add(arenaRecipe);
            }
        }

        // Restore recipe production time, limit, AND locked state from snapshot
        foreach (var recipe in hideout.Production.Recipes)
        {
            var id = recipe.Id.ToString();
            if (_productionSnapshots.TryGetValue(id, out var snap))
            {
                recipe.ProductionTime = snap.ProductionTime;
                recipe.ProductionLimitCount = snap.ProductionLimitCount;
                recipe.Locked = snap.Locked ?? false;
            }
        }

        foreach (var recipe in hideout.Production.ScavRecipes)
        {
            var id = recipe.Id.ToString();
            if (_scavRecipeSnapshots.TryGetValue(id, out var snap))
            {
                recipe.ProductionTime = snap.ProductionTime;
                if (recipe.Requirements.Count > 0)
                    recipe.Requirements[0].Count = snap.Count;
            }
        }

        if (_cultistSnapshot != null)
        {
            hideoutConfig.CultistCircle.HideoutTaskRewardTimeSeconds = _cultistSnapshot.HideoutTaskRewardTime;
            hideoutConfig.CultistCircle.MaxRewardItemCount = _cultistSnapshot.MaxRewardItemCount;
            for (int i = 0; i < hideoutConfig.CultistCircle.CraftTimeThresholds.Count && i < _cultistSnapshot.CraftTimeThresholds.Count; i++)
                hideoutConfig.CultistCircle.CraftTimeThresholds[i].CraftTimeSeconds = _cultistSnapshot.CraftTimeThresholds[i];
            for (int i = 0; i < hideoutConfig.CultistCircle.DirectRewards.Count && i < _cultistSnapshot.DirectRewardTimes.Count; i++)
                hideoutConfig.CultistCircle.DirectRewards[i].CraftTimeSeconds = _cultistSnapshot.DirectRewardTimes[i];
        }

        if (_fuelSnapshot != null)
        {
            hideout.Settings.GeneratorFuelFlowRate = _fuelSnapshot.FuelFlowRate;
            hideout.Settings.GeneratorSpeedWithoutFuel = _fuelSnapshot.GeneratorSpeedWithoutFuel;
            hideout.Settings.AirFilterUnitFlowRate = _fuelSnapshot.AirFilterFlowRate;
            hideout.Settings.GpuBoostRate = _fuelSnapshot.GpuBoostRate;
        }

        foreach (var snap in _stashSnapshots)
        {
            if (items.TryGetValue(snap.StashId, out var item) && item.Properties?.Grids != null)
            {
                var grids = item.Properties.Grids.ToList();
                if (grids.Count > 0)
                {
                    grids[0].Properties.CellsV = snap.CellsV;
                    item.Properties.Grids = grids;
                }
            }
        }

        if (_healthRegenSnapshot != null)
        {
            var bh = globals.Configuration.Health.Effects.Regeneration.BodyHealth;
            bh.Head.Value = _healthRegenSnapshot.Head;
            bh.Chest.Value = _healthRegenSnapshot.Chest;
            bh.Stomach.Value = _healthRegenSnapshot.Stomach;
            bh.LeftArm.Value = _healthRegenSnapshot.LeftArm;
            bh.RightArm.Value = _healthRegenSnapshot.RightArm;
            bh.LeftLeg.Value = _healthRegenSnapshot.LeftLeg;
            bh.RightLeg.Value = _healthRegenSnapshot.RightLeg;
            globals.Configuration.Health.Effects.Regeneration.Energy = _healthRegenSnapshot.Energy;
            globals.Configuration.Health.Effects.Regeneration.Hydration = _healthRegenSnapshot.Hydration;
        }

        // Restore ALL bonus values from snapshot
        foreach (var snap in _bonusSnapshots)
        {
            foreach (var area in hideout.Areas)
            {
                if ((area.Type?.ToString() ?? "") != snap.AreaType) continue;
                if (!area.Stages.TryGetValue(snap.StageKey, out var stage)) continue;
                if (snap.BonusIndex < stage.Bonuses.Count)
                    stage.Bonuses[snap.BonusIndex].Value = snap.OriginalValue;
            }
        }

        // ══════════════════════════════════════════
        // APPLY fresh from config
        // ══════════════════════════════════════════

        // ── A. Construction time (per-area overrides take priority over global) ──
        foreach (var area in hideout.Areas)
        {
            var areaType = area.Type?.ToString() ?? "";
            var mult = cfg.PerAreaConstructionMult.TryGetValue(areaType, out var perArea) ? perArea : cfg.ConstructionTimeMult;
            if (mult == 1.0) continue;
            foreach (var (_, stage) in area.Stages)
            {
                if (stage.ConstructionTime > 0)
                {
                    stage.ConstructionTime = Math.Max(2, (int)((stage.ConstructionTime ?? 0) * mult));
                    changes++;
                }
            }
        }

        // A2. Construction cost scaling
        if (cfg.ConstructionCostMult != 1.0)
        {
            foreach (var area in hideout.Areas)
            {
                foreach (var (_, stage) in area.Stages)
                {
                    foreach (var req in stage.Requirements)
                    {
                        if (req.Count is > 0)
                        {
                            req.Count = Math.Max(1, (int)((req.Count ?? 1) * cfg.ConstructionCostMult));
                            changes++;
                        }
                    }
                }
            }
        }

        // A3. Requirement removal (existing)
        if (cfg.RemoveItemRequirements || cfg.RemoveSkillRequirements || cfg.RemoveTraderRequirements || cfg.RemoveFirRequirements)
        {
            foreach (var area in hideout.Areas)
            {
                foreach (var (_, stage) in area.Stages)
                {
                    if (cfg.RemoveFirRequirements)
                    {
                        foreach (var req in stage.Requirements)
                        {
                            if (req.IsSpawnedInSession != null)
                                req.IsSpawnedInSession = false;
                        }
                        changes++;
                    }

                    if (cfg.RemoveItemRequirements || cfg.RemoveSkillRequirements || cfg.RemoveTraderRequirements)
                    {
                        var filtered = new List<StageRequirement>();
                        foreach (var req in stage.Requirements)
                        {
                            if (req.AreaType != null) { filtered.Add(req); continue; }
                            if (cfg.RemoveItemRequirements && !req.TemplateId.IsEmpty) continue;
                            if (cfg.RemoveSkillRequirements && req.SkillName != null) continue;
                            if (cfg.RemoveTraderRequirements && !req.TraderId.IsEmpty) continue;
                            filtered.Add(req);
                        }
                        stage.Requirements = filtered;
                        changes++;
                    }
                }
            }
        }

        if (cfg.RemoveCustomizationRequirements && hideout.Customisation?.Globals != null)
        {
            foreach (var custom in hideout.Customisation.Globals)
            {
                custom.Conditions = [];
                changes++;
            }
        }

        // ── B. Production & Crafting (per-station overrides take priority) ──
        foreach (var recipe in hideout.Production.Recipes)
        {
            var id = recipe.Id.ToString();
            if (id is BitcoinRecipeId or WaterFilterRecipeId) continue;
            if (recipe.Continuous == true || (recipe.ProductionTime ?? 0) < 10) continue;

            var stationType = recipe.AreaType?.ToString() ?? "";
            var mult = cfg.PerStationProductionMult.TryGetValue(stationType, out var perStation)
                ? perStation : cfg.ProductionSpeedMult;
            if (mult == 1.0) continue;

            recipe.ProductionTime = Math.Max(2, (int)((recipe.ProductionTime ?? 0) * mult));
            changes++;
        }

        if (cfg.ScavCaseTimeMult != 1.0)
        {
            foreach (var recipe in hideout.Production.ScavRecipes)
            {
                recipe.ProductionTime = Math.Max(2, (int)((recipe.ProductionTime ?? 0) * cfg.ScavCaseTimeMult));
                changes++;
            }
        }

        if (cfg.ScavCasePriceMult != 1.0)
        {
            foreach (var recipe in hideout.Production.ScavRecipes)
            {
                if (recipe.Requirements.Count > 0)
                {
                    var firstReq = recipe.Requirements[0];
                    var tplStr = firstReq.TemplateId is { } tplId && !tplId.IsEmpty ? tplId.ToString() : "";
                    if (CurrencyIds.Contains(tplStr))
                    {
                        firstReq.Count = Math.Max(1, (int)((firstReq.Count ?? 0) * cfg.ScavCasePriceMult));
                        changes++;
                    }
                }
            }
        }

        if (cfg.CultistCircleTimeMult != 1.0)
        {
            hideoutConfig.CultistCircle.HideoutTaskRewardTimeSeconds = Math.Max(1, (int)(hideoutConfig.CultistCircle.HideoutTaskRewardTimeSeconds * cfg.CultistCircleTimeMult));
            foreach (var c in hideoutConfig.CultistCircle.CraftTimeThresholds)
                c.CraftTimeSeconds = Math.Max(1, (int)(c.CraftTimeSeconds * cfg.CultistCircleTimeMult));
            foreach (var c in hideoutConfig.CultistCircle.DirectRewards)
                c.CraftTimeSeconds = Math.Max(1, (int)(c.CraftTimeSeconds * cfg.CultistCircleTimeMult));
            changes++;
        }

        if (cfg.CultistCircleMaxRewards.HasValue)
        {
            hideoutConfig.CultistCircle.MaxRewardItemCount = cfg.CultistCircleMaxRewards.Value;
            changes++;
        }

        if (cfg.RemoveArenaCrafts && _arenaRecipeIds.Count > 0)
        {
            var toRemove = new List<HideoutProduction>();
            foreach (var recipe in hideout.Production.Recipes)
            {
                if (_arenaRecipeIds.Contains(recipe.Id.ToString()))
                    toRemove.Add(recipe);
            }
            foreach (var recipe in toRemove)
            {
                hideout.Production.Recipes.Remove(recipe);
                changes++;
            }
        }

        // B6. Recipe lock overrides
        if (cfg.RecipeLockOverrides.Count > 0)
        {
            foreach (var recipe in hideout.Production.Recipes)
            {
                var id = recipe.Id.ToString();
                if (cfg.RecipeLockOverrides.TryGetValue(id, out var locked))
                {
                    recipe.Locked = locked;
                    changes++;
                }
            }
        }

        // ── C. Fuel & Power ──
        if (cfg.FuelConsumptionMult != 1.0)
        {
            hideout.Settings.GeneratorFuelFlowRate *= cfg.FuelConsumptionMult;
            changes++;
        }
        if (cfg.GeneratorNoFuelMult != 1.0)
        {
            hideout.Settings.GeneratorSpeedWithoutFuel *= cfg.GeneratorNoFuelMult;
            changes++;
        }
        if (cfg.AirFilterMult != 1.0)
        {
            hideout.Settings.AirFilterUnitFlowRate *= cfg.AirFilterMult;
            changes++;
        }
        if (cfg.GpuBoostMult != 1.0)
        {
            hideout.Settings.GpuBoostRate *= cfg.GpuBoostMult;
            changes++;
        }

        // ── D. Farming ──
        if (cfg.BitcoinTimeMinutes.HasValue || cfg.MaxBitcoins.HasValue)
        {
            foreach (var recipe in hideout.Production.Recipes)
            {
                if (recipe.Id.ToString() != BitcoinRecipeId) continue;
                if (cfg.BitcoinTimeMinutes.HasValue)
                    recipe.ProductionTime = cfg.BitcoinTimeMinutes.Value * 60;
                if (cfg.MaxBitcoins.HasValue)
                    recipe.ProductionLimitCount = cfg.MaxBitcoins.Value;
                changes++;
            }
        }

        if (cfg.WaterFilterTimeMinutes.HasValue || cfg.WaterFilterRate.HasValue)
        {
            foreach (var recipe in hideout.Production.Recipes)
            {
                if (recipe.Id.ToString() != WaterFilterRecipeId) continue;
                if (cfg.WaterFilterTimeMinutes.HasValue)
                    recipe.ProductionTime = cfg.WaterFilterTimeMinutes.Value * 60;
                if (cfg.WaterFilterRate.HasValue && recipe.Requirements.Count > 1)
                    recipe.Requirements[1].Resource = cfg.WaterFilterRate.Value;
                changes++;
            }
        }

        // ── E. Stash Sizes ──
        var heights = cfg.StashHeights;
        int?[] editionHeights = [heights.Standard, heights.LeftBehind, heights.PrepareForEscape, heights.EdgeOfDarkness, heights.UnheardEdition];
        for (int i = 0; i < StashEditions.Length; i++)
        {
            if (editionHeights[i] == null) continue;
            var stashId = StashEditions[i].Id;
            if (items.TryGetValue(stashId, out var item) && item.Properties?.Grids != null)
            {
                var grids = item.Properties.Grids.ToList();
                if (grids.Count > 0)
                {
                    grids[0].Properties.CellsV = editionHeights[i]!.Value;
                    item.Properties.Grids = grids;
                    changes++;
                }
            }
        }

        // ── F. Health Regeneration ──
        if (cfg.HealthRegenMult != 1.0)
        {
            var bh = globals.Configuration.Health.Effects.Regeneration.BodyHealth;
            bh.Head.Value *= cfg.HealthRegenMult;
            bh.Chest.Value *= cfg.HealthRegenMult;
            bh.Stomach.Value *= cfg.HealthRegenMult;
            bh.LeftArm.Value *= cfg.HealthRegenMult;
            bh.RightArm.Value *= cfg.HealthRegenMult;
            bh.LeftLeg.Value *= cfg.HealthRegenMult;
            bh.RightLeg.Value *= cfg.HealthRegenMult;
            changes++;
        }

        if (cfg.EnergyRegenRate.HasValue)
        {
            globals.Configuration.Health.Effects.Regeneration.Energy = cfg.EnergyRegenRate.Value;
            changes++;
        }

        if (cfg.HydrationRegenRate.HasValue)
        {
            globals.Configuration.Health.Effects.Regeneration.Hydration = cfg.HydrationRegenRate.Value;
            changes++;
        }

        if (cfg.DisableHideoutHealthRegen || cfg.DisableHideoutEnergyRegen || cfg.DisableHideoutHydrationRegen)
        {
            foreach (var area in hideout.Areas)
            {
                foreach (var (_, stage) in area.Stages)
                {
                    foreach (var bonus in stage.Bonuses)
                    {
                        if (cfg.DisableHideoutHealthRegen && bonus.Type == BonusType.HealthRegeneration)
                            bonus.Value = 0;
                        if (cfg.DisableHideoutEnergyRegen && bonus.Type == BonusType.EnergyRegeneration)
                            bonus.Value = 0;
                        if (cfg.DisableHideoutHydrationRegen && bonus.Type == BonusType.HydrationRegeneration)
                            bonus.Value = 0;
                    }
                }
            }
            changes++;
        }

        // ── G. Bonus value overrides (applied LAST so they override regen disable) ──
        if (cfg.BonusValueOverrides.Count > 0)
        {
            foreach (var snap in _bonusSnapshots)
            {
                var key = $"{snap.AreaType}|{snap.StageKey}|{snap.BonusIndex}";
                if (!cfg.BonusValueOverrides.TryGetValue(key, out var mult)) continue;
                if (Math.Abs(mult - 1.0) < 0.001) continue;

                foreach (var area in hideout.Areas)
                {
                    if ((area.Type?.ToString() ?? "") != snap.AreaType) continue;
                    if (!area.Stages.TryGetValue(snap.StageKey, out var stage)) continue;
                    if (snap.BonusIndex < stage.Bonuses.Count && snap.OriginalValue.HasValue)
                    {
                        stage.Bonuses[snap.BonusIndex].Value = snap.OriginalValue.Value * mult;
                        changes++;
                    }
                }
            }
        }

        return changes;
    }

    // ═══════════════════════════════════════════════════════
    // API: GET config
    // ═══════════════════════════════════════════════════════

    public HideoutEditorConfigResponse GetConfig()
    {
        lock (_lock)
        {
            if (!_snapshotTaken) EnsureSnapshot();

            var defaults = new HideoutDefaults();

            if (_fuelSnapshot != null)
            {
                defaults.FuelFlowRate = _fuelSnapshot.FuelFlowRate ?? 0;
                defaults.GeneratorSpeedWithoutFuel = _fuelSnapshot.GeneratorSpeedWithoutFuel ?? 0;
                defaults.AirFilterFlowRate = _fuelSnapshot.AirFilterFlowRate ?? 0;
                defaults.GpuBoostRate = _fuelSnapshot.GpuBoostRate ?? 0;
            }

            if (_farmingSnapshot != null)
            {
                defaults.BitcoinTimeMinutes = (int)(_farmingSnapshot.BitcoinTime ?? 0) / 60;
                defaults.MaxBitcoins = _farmingSnapshot.MaxBitcoins ?? 3;
                defaults.WaterFilterTimeMinutes = (int)(_farmingSnapshot.WaterFilterTime ?? 0) / 60;
                defaults.WaterFilterRate = _farmingSnapshot.WaterFilterRate ?? 0;
            }

            if (_cultistSnapshot != null)
                defaults.CultistCircleMaxRewards = _cultistSnapshot.MaxRewardItemCount;

            var stashDefaults = new StashHeightsConfig();
            foreach (var snap in _stashSnapshots)
            {
                var idx = Array.FindIndex(StashEditions, e => e.Id == snap.StashId);
                switch (idx)
                {
                    case 0: stashDefaults.Standard = snap.CellsV; break;
                    case 1: stashDefaults.LeftBehind = snap.CellsV; break;
                    case 2: stashDefaults.PrepareForEscape = snap.CellsV; break;
                    case 3: stashDefaults.EdgeOfDarkness = snap.CellsV; break;
                    case 4: stashDefaults.UnheardEdition = snap.CellsV; break;
                }
            }
            defaults.StashHeights = stashDefaults;

            if (_healthRegenSnapshot != null)
            {
                defaults.HealthRegenValues = new Dictionary<string, double>
                {
                    ["Head"] = _healthRegenSnapshot.Head,
                    ["Chest"] = _healthRegenSnapshot.Chest,
                    ["Stomach"] = _healthRegenSnapshot.Stomach,
                    ["LeftArm"] = _healthRegenSnapshot.LeftArm,
                    ["RightArm"] = _healthRegenSnapshot.RightArm,
                    ["LeftLeg"] = _healthRegenSnapshot.LeftLeg,
                    ["RightLeg"] = _healthRegenSnapshot.RightLeg
                };
                defaults.EnergyRegenRate = _healthRegenSnapshot.Energy;
                defaults.HydrationRegenRate = _healthRegenSnapshot.Hydration;
            }

            return new HideoutEditorConfigResponse
            {
                Config = configService.GetConfig().Hideout,
                Defaults = defaults
            };
        }
    }

    // ═══════════════════════════════════════════════════════
    // API: POST apply
    // ═══════════════════════════════════════════════════════

    public int ApplyConfig(HideoutEditorConfig newConfig)
    {
        lock (_lock)
        {
            if (!_snapshotTaken) EnsureSnapshot();

            var config = configService.GetConfig();
            config.Hideout = newConfig;
            configService.SaveConfig();

            return ApplyInternal();
        }
    }

    // ═══════════════════════════════════════════════════════
    // API: POST reset
    // ═══════════════════════════════════════════════════════

    public void ResetConfig()
    {
        lock (_lock)
        {
            if (!_snapshotTaken) EnsureSnapshot();

            var config = configService.GetConfig();
            config.Hideout = new HideoutEditorConfig();
            config.ActiveHideoutPreset = null;
            configService.SaveConfig();

            ApplyInternal();
        }
    }

    // ═══════════════════════════════════════════════════════
    // DISCOVERY: Areas
    // ═══════════════════════════════════════════════════════

    public List<HideoutAreaDto> GetAreas()
    {
        lock (_lock)
        {
            if (!_snapshotTaken) EnsureSnapshot();
            var hideout = databaseService.GetHideout();
            var result = new List<HideoutAreaDto>();
            foreach (var area in hideout.Areas)
            {
                var areaType = area.Type?.ToString() ?? "";
                var maxLevel = area.Stages.Count > 0 ? area.Stages.Keys.Max(k => int.TryParse(k, out var v) ? v : 0) : 0;
                result.Add(new HideoutAreaDto
                {
                    AreaType = areaType,
                    AreaName = GetAreaName(areaType),
                    StageCount = area.Stages.Count,
                    MaxLevel = maxLevel
                });
            }
            result.Sort((a, b) => string.Compare(a.AreaName, b.AreaName, StringComparison.OrdinalIgnoreCase));
            return result;
        }
    }

    // ═══════════════════════════════════════════════════════
    // DISCOVERY: Bonuses
    // ═══════════════════════════════════════════════════════

    public List<HideoutBonusDto> GetBonuses()
    {
        lock (_lock)
        {
            if (!_snapshotTaken) EnsureSnapshot();
            var cfg = configService.GetConfig().Hideout;
            var result = new List<HideoutBonusDto>();

            foreach (var snap in _bonusSnapshots)
            {
                if (snap.OriginalValue == null) continue;
                if (Enum.TryParse<BonusType>(snap.BonusTypeName, out var bt) && NonEditableBonusTypes.Contains(bt)) continue;

                var key = $"{snap.AreaType}|{snap.StageKey}|{snap.BonusIndex}";
                var currentMult = cfg.BonusValueOverrides.TryGetValue(key, out var m) ? m : 1.0;

                result.Add(new HideoutBonusDto
                {
                    AreaType = snap.AreaType,
                    AreaName = GetAreaName(snap.AreaType),
                    StageKey = snap.StageKey,
                    BonusIndex = snap.BonusIndex,
                    BonusType = snap.BonusTypeName,
                    OriginalValue = snap.OriginalValue.Value,
                    CurrentMultiplier = currentMult,
                    Key = key
                });
            }

            return result;
        }
    }

    // ═══════════════════════════════════════════════════════
    // DISCOVERY: Recipes
    // ═══════════════════════════════════════════════════════

    public List<HideoutRecipeDto> GetRecipes(string? station, string? search)
    {
        lock (_lock)
        {
            if (!_snapshotTaken) EnsureSnapshot();
            var hideout = databaseService.GetHideout();
            var locales = localeService.GetLocaleDb("en");
            var cfg = configService.GetConfig().Hideout;
            var result = new List<HideoutRecipeDto>();

            foreach (var recipe in hideout.Production.Recipes)
            {
                var id = recipe.Id.ToString();
                var areaType = recipe.AreaType?.ToString() ?? "";
                var areaName = GetAreaName(areaType);

                var productTpl = recipe.EndProduct is { } ep && !ep.IsEmpty ? ep.ToString() : "";
                var productName = "";
                if (!string.IsNullOrEmpty(productTpl))
                {
                    locales.TryGetValue($"{productTpl} Name", out productName!);
                    if (string.IsNullOrEmpty(productName))
                        locales.TryGetValue($"{productTpl} ShortName", out productName!);
                    productName ??= productTpl;
                }

                // Station filter
                if (!string.IsNullOrEmpty(station) && !areaType.Equals(station, StringComparison.OrdinalIgnoreCase)
                    && !areaName.Equals(station, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Search filter
                if (!string.IsNullOrEmpty(search) && !productName.Contains(search, StringComparison.OrdinalIgnoreCase))
                    continue;

                var origTime = _productionSnapshots.TryGetValue(id, out var snap) ? snap.ProductionTime ?? 0 : recipe.ProductionTime ?? 0;
                var origLocked = snap?.Locked ?? false;

                result.Add(new HideoutRecipeDto
                {
                    Id = id,
                    StationAreaType = areaType,
                    StationName = areaName,
                    ProductTpl = productTpl,
                    ProductName = productName,
                    ProductionTime = recipe.ProductionTime ?? 0,
                    OriginalTime = origTime,
                    Locked = recipe.Locked ?? false,
                    OriginalLocked = origLocked,
                    Continuous = recipe.Continuous ?? false,
                    LimitCount = recipe.ProductionLimitCount
                });
            }

            result.Sort((a, b) => string.Compare(a.ProductName, b.ProductName, StringComparison.OrdinalIgnoreCase));
            return result;
        }
    }

    // ═══════════════════════════════════════════════════════
    // PRESETS
    // ═══════════════════════════════════════════════════════

    private string GetPresetsDir()
    {
        var modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        var dir = IOPath.Combine(modPath, "config", "hideout-presets");
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        return dir;
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = IOPath.GetInvalidFileNameChars();
        var clean = new string(name.Where(c => !invalid.Contains(c)).ToArray()).Trim();
        return string.IsNullOrEmpty(clean) ? "preset" : clean;
    }

    private static readonly Dictionary<string, HideoutEditorConfig> BuiltInPresets = new()
    {
        ["Fresh Wipe"] = new HideoutEditorConfig(),
        ["Endgame"] = new HideoutEditorConfig
        {
            ConstructionTimeMult = 0.1,
            ProductionSpeedMult = 0.25,
            BitcoinTimeMinutes = 30,
            MaxBitcoins = 10,
            HealthRegenMult = 5.0,
            StashHeights = new StashHeightsConfig
            {
                Standard = 68, LeftBehind = 68, PrepareForEscape = 68,
                EdgeOfDarkness = 72, UnheardEdition = 72
            }
        },
        ["Speed Run"] = new HideoutEditorConfig
        {
            ConstructionTimeMult = 0.01,
            ProductionSpeedMult = 0.1,
            RemoveItemRequirements = true,
            RemoveSkillRequirements = true,
            RemoveTraderRequirements = true,
            BitcoinTimeMinutes = 5
        },
        ["Realistic"] = new HideoutEditorConfig
        {
            ConstructionTimeMult = 2.0,
            ProductionSpeedMult = 1.5,
            FuelConsumptionMult = 2.0,
            DisableHideoutHealthRegen = true
        }
    };

    public HideoutPresetListResponse ListPresets()
    {
        var dir = GetPresetsDir();
        var presets = new List<HideoutPresetSummary>();

        // Built-in presets
        foreach (var (name, _) in BuiltInPresets)
        {
            presets.Add(new HideoutPresetSummary
            {
                Name = name,
                Description = "(built-in)",
                CreatedUtc = DateTime.MinValue
            });
        }

        // User presets from files
        foreach (var file in Directory.GetFiles(dir, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var preset = JsonSerializer.Deserialize<HideoutPreset>(json, PresetJsonOptions);
                if (preset != null)
                {
                    presets.Add(new HideoutPresetSummary
                    {
                        Name = preset.Name,
                        Description = preset.Description,
                        CreatedUtc = preset.CreatedUtc
                    });
                }
            }
            catch (Exception ex)
            {
                logger.Debug($"ZSlayerCC Hideout: Skipping invalid preset file '{IOPath.GetFileName(file)}': {ex.Message}");
            }
        }

        presets.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return new HideoutPresetListResponse
        {
            Presets = presets,
            ActivePreset = configService.GetConfig().ActiveHideoutPreset
        };
    }

    public HideoutPreset SavePreset(string name, string description)
    {
        var preset = new HideoutPreset
        {
            Name = name,
            Description = description,
            CreatedUtc = DateTime.UtcNow,
            Config = configService.GetConfig().Hideout
        };
        var dir = GetPresetsDir();
        var filePath = IOPath.Combine(dir, SanitizeFileName(name) + ".json");
        var json = JsonSerializer.Serialize(preset, PresetJsonOptions);
        File.WriteAllText(filePath, json);
        logger.Info($"ZSlayerCC Hideout: Saved preset '{name}'");
        return preset;
    }

    public int LoadPreset(string name)
    {
        lock (_lock)
        {
            if (!_snapshotTaken) EnsureSnapshot();

            HideoutEditorConfig cfg;
            if (BuiltInPresets.TryGetValue(name, out var builtIn))
            {
                cfg = builtIn;
            }
            else
            {
                var dir = GetPresetsDir();
                var filePath = IOPath.Combine(dir, SanitizeFileName(name) + ".json");
                if (!File.Exists(filePath)) return -1;
                var json = File.ReadAllText(filePath);
                var preset = JsonSerializer.Deserialize<HideoutPreset>(json, PresetJsonOptions);
                if (preset == null) return -1;
                cfg = preset.Config;
            }

            var config = configService.GetConfig();
            config.Hideout = cfg;
            config.ActiveHideoutPreset = name;
            configService.SaveConfig();

            return ApplyInternal();
        }
    }

    public bool DeletePreset(string name)
    {
        if (BuiltInPresets.ContainsKey(name)) return false;
        var dir = GetPresetsDir();
        var filePath = IOPath.Combine(dir, SanitizeFileName(name) + ".json");
        if (!File.Exists(filePath)) return false;
        File.Delete(filePath);
        if (configService.GetConfig().ActiveHideoutPreset == name)
        {
            configService.GetConfig().ActiveHideoutPreset = null;
            configService.SaveConfig();
        }
        logger.Info($"ZSlayerCC Hideout: Deleted preset '{name}'");
        return true;
    }

    public string? ExportPreset(string name)
    {
        if (BuiltInPresets.TryGetValue(name, out var builtIn))
        {
            var bp = new HideoutPreset { Name = name, Description = "(built-in)", CreatedUtc = DateTime.UtcNow, Config = builtIn };
            return JsonSerializer.Serialize(bp, PresetJsonOptions);
        }
        var dir = GetPresetsDir();
        var filePath = IOPath.Combine(dir, SanitizeFileName(name) + ".json");
        return File.Exists(filePath) ? File.ReadAllText(filePath) : null;
    }

    public HideoutPreset? ImportPreset(HideoutPreset preset)
    {
        if (string.IsNullOrWhiteSpace(preset.Name)) return null;
        preset.CreatedUtc = DateTime.UtcNow;
        var dir = GetPresetsDir();
        var filePath = IOPath.Combine(dir, SanitizeFileName(preset.Name) + ".json");
        var json = JsonSerializer.Serialize(preset, PresetJsonOptions);
        File.WriteAllText(filePath, json);
        logger.Info($"ZSlayerCC Hideout: Imported preset '{preset.Name}'");
        return preset;
    }

    // ═══════════════════════════════════════════════════════
    // PER-PLAYER HIDEOUT
    // ═══════════════════════════════════════════════════════

    public PlayerHideoutResponse? GetPlayerHideout(string sessionId)
    {
        var profiles = saveServer.GetProfiles();
        if (!profiles.TryGetValue(sessionId, out var profile)) return null;
        var pmc = profile.CharacterData?.PmcData;
        if (pmc?.Hideout?.Areas == null) return null;

        var hideout = databaseService.GetHideout();
        var maxLevels = new Dictionary<string, int>();
        foreach (var area in hideout.Areas)
        {
            var at = area.Type?.ToString() ?? "";
            maxLevels[at] = area.Stages.Count > 0 ? area.Stages.Keys.Max(k => int.TryParse(k, out var v) ? v : 0) : 0;
        }

        var areas = new List<PlayerHideoutAreaDto>();
        foreach (var area in pmc.Hideout.Areas)
        {
            var at = area.Type.ToString();
            areas.Add(new PlayerHideoutAreaDto
            {
                AreaType = at,
                AreaName = GetAreaName(at),
                Level = area.Level ?? 0,
                MaxLevel = maxLevels.TryGetValue(at, out var ml) ? ml : 0
            });
        }
        areas.Sort((a, b) => string.Compare(a.AreaName, b.AreaName, StringComparison.OrdinalIgnoreCase));

        return new PlayerHideoutResponse
        {
            SessionId = sessionId,
            ProfileName = pmc.Info?.Nickname ?? sessionId,
            Areas = areas
        };
    }

    public bool SetPlayerArea(string sessionId, string areaType, int level)
    {
        var profiles = saveServer.GetProfiles();
        if (!profiles.TryGetValue(sessionId, out var profile)) return false;
        var pmc = profile.CharacterData?.PmcData;
        if (pmc?.Hideout?.Areas == null) return false;

        foreach (var area in pmc.Hideout.Areas)
        {
            if (area.Type.ToString() != areaType) continue;
            area.Level = Math.Max(0, level);
            _ = saveServer.SaveProfileAsync(sessionId);
            logger.Info($"ZSlayerCC Hideout: Set {areaType} to level {level} for {pmc.Info?.Nickname ?? sessionId}");
            return true;
        }
        return false;
    }

    public bool UnlockAllAreas(string sessionId)
    {
        var profiles = saveServer.GetProfiles();
        if (!profiles.TryGetValue(sessionId, out var profile)) return false;
        var pmc = profile.CharacterData?.PmcData;
        if (pmc?.Hideout?.Areas == null) return false;

        var hideout = databaseService.GetHideout();
        var maxLevels = new Dictionary<string, int>();
        foreach (var area in hideout.Areas)
        {
            var at = area.Type?.ToString() ?? "";
            maxLevels[at] = area.Stages.Count > 0 ? area.Stages.Keys.Max(k => int.TryParse(k, out var v) ? v : 0) : 0;
        }

        foreach (var area in pmc.Hideout.Areas)
        {
            var at = area.Type.ToString();
            if (maxLevels.TryGetValue(at, out var maxLevel))
                area.Level = maxLevel;
        }

        _ = saveServer.SaveProfileAsync(sessionId);
        logger.Info($"ZSlayerCC Hideout: Unlocked all areas for {pmc.Info?.Nickname ?? sessionId}");
        return true;
    }

    public List<HideoutProgressionEntry> GetHideoutProgression()
    {
        var profiles = saveServer.GetProfiles();
        var hideout = databaseService.GetHideout();
        var result = new List<HideoutProgressionEntry>();

        foreach (var (sid, profile) in profiles)
        {
            var pmc = profile.CharacterData?.PmcData;
            if (pmc?.Info?.Nickname == null || pmc.Hideout?.Areas == null) continue;

            var entry = new HideoutProgressionEntry
            {
                SessionId = sid,
                ProfileName = pmc.Info.Nickname,
                Level = pmc.Info.Level ?? 0,
                Areas = new Dictionary<string, int>()
            };

            foreach (var area in pmc.Hideout.Areas)
                entry.Areas[area.Type.ToString()] = area.Level ?? 0;

            result.Add(entry);
        }

        result.Sort((a, b) => string.Compare(a.ProfileName, b.ProfileName, StringComparison.OrdinalIgnoreCase));
        return result;
    }
}

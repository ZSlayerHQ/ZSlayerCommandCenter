using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using ZSlayerCommandCenter.Models;

namespace ZSlayerCommandCenter.Services;

[Injectable(InjectionType.Singleton)]
public class ServicesSettingsService(
    DatabaseService databaseService,
    ConfigServer configServer,
    ConfigService configService,
    ISptLogger<ServicesSettingsService> logger)
{
    private readonly object _lock = new();
    private bool _snapshotTaken;

    // Well-known trader IDs
    private const string PraporId = "54cb50c76803fa8b248b4571";
    private const string TherapistId = "54cb57776803fa99248b456e";
    private const string SkierId = "58330581ace78e27b8b10cee";
    private const string MechanicId = "5a7c2eca46aef81a7ca2145d";
    private const string RagmanId = "5ac3b934156ae10c4430e83c";

    // Customization parent IDs for suits
    private const string SuitParent1 = "5cd944ca1388ce03a44dc2a4";
    private const string SuitParent2 = "5cd944d01388ce000a659df9";

    // ═══════════════════════════════════════════════════════
    // SNAPSHOTS
    // ═══════════════════════════════════════════════════════

    // L. Insurance — InsuranceConfig
    private double _snapPraporReturnChance, _snapTherapistReturnChance;
    private double _snapAttachmentRecoveryChance;
    private double _snapInsuranceInterval, _snapInsuranceReturnOverride;

    // L. Insurance — trader insurance settings
    private double? _snapPraporStorageTime, _snapTherapistStorageTime;
    private int? _snapPraporReturnMin, _snapPraporReturnMax;
    private int? _snapTherapistReturnMin, _snapTherapistReturnMax;

    // L. Insurance — per-LL price coefficients
    private readonly List<double?> _snapPraporInsurancePrice = [];
    private readonly List<double?> _snapTherapistInsurancePrice = [];

    // M. Healing
    private double _snapTrialRaids, _snapTrialLevels;
    private readonly List<double?> _snapTherapistHealPrice = [];

    // N. Repair — RepairConfig
    private double _snapRepairPriceMult;
    private bool _snapApplyRandomDurabilityLoss;
    private double _snapArmorKitSkillMult;
    private double _snapWeaponMaintenanceSkillMult;
    private double _snapIntellectWeaponKitMult, _snapIntellectArmorKitMult;
    private double _snapMaxIntellectKit, _snapMaxIntellectTrader;
    private double _snapMinDurabilitySell;

    // N. Repair — per-trader per-LL repair price
    private readonly Dictionary<string, List<double?>> _snapRepairPricePerLl = new();

    // N. Repair — armor material degradation
    private readonly Dictionary<ArmorMaterial, (double MinR, double MaxR, double MinK, double MaxK)> _snapArmorDegradation = new();

    // N. Repair — weapon item degradation (tpl → values)
    private readonly Dictionary<MongoId, (double? MinR, double? MaxR, double? MinK, double? MaxK)> _snapWeaponDegradation = new();

    // O. Clothing — per-suit snapshot
    private readonly Dictionary<MongoId, SuitSnapshot> _suitSnapshots = new();
    private readonly Dictionary<MongoId, SuitReqSnapshot> _suitReqSnapshots = new();

    private record SuitSnapshot(List<string> Side, bool AvailableAsDefault);
    private record SuitReqSnapshot(
        List<ItemRequirement>? ItemRequirements,
        double? LoyaltyLevel, double? ProfileLevel,
        double? Standing, double? PrestigeLevel,
        List<string>? QuestRequirements, List<string>? AchievementRequirements);

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
        var insuranceConfig = configServer.GetConfig<InsuranceConfig>();
        var repairConfig = configServer.GetConfig<RepairConfig>();

        // ── L. Insurance ──
        if (insuranceConfig.ReturnChancePercent != null)
        {
            if (insuranceConfig.ReturnChancePercent.TryGetValue(PraporId, out var pc))
                _snapPraporReturnChance = pc;
            if (insuranceConfig.ReturnChancePercent.TryGetValue(TherapistId, out var tc))
                _snapTherapistReturnChance = tc;
        }
        _snapAttachmentRecoveryChance = insuranceConfig.ChanceNoAttachmentsTakenPercent;
        _snapInsuranceInterval = insuranceConfig.RunIntervalSeconds;
        _snapInsuranceReturnOverride = insuranceConfig.ReturnTimeOverrideSeconds;

        // Trader insurance settings
        try
        {
            var prapor = databaseService.GetTrader(PraporId);
            if (prapor?.Base?.Insurance != null)
            {
                _snapPraporStorageTime = prapor.Base.Insurance.MaxStorageTime;
                _snapPraporReturnMin = prapor.Base.Insurance.MinReturnHour;
                _snapPraporReturnMax = prapor.Base.Insurance.MaxReturnHour;
            }
            if (prapor?.Base?.LoyaltyLevels != null)
            {
                _snapPraporInsurancePrice.Clear();
                foreach (var ll in prapor.Base.LoyaltyLevels)
                    _snapPraporInsurancePrice.Add(ll.InsurancePriceCoefficient);
            }
        }
        catch { /* skip */ }

        try
        {
            var therapist = databaseService.GetTrader(TherapistId);
            if (therapist?.Base?.Insurance != null)
            {
                _snapTherapistStorageTime = therapist.Base.Insurance.MaxStorageTime;
                _snapTherapistReturnMin = therapist.Base.Insurance.MinReturnHour;
                _snapTherapistReturnMax = therapist.Base.Insurance.MaxReturnHour;
            }
            if (therapist?.Base?.LoyaltyLevels != null)
            {
                _snapTherapistInsurancePrice.Clear();
                foreach (var ll in therapist.Base.LoyaltyLevels)
                    _snapTherapistInsurancePrice.Add(ll.InsurancePriceCoefficient);
            }
        }
        catch { /* skip */ }

        // ── M. Healing ──
        _snapTrialRaids = cfg.Health?.HealPrice?.TrialRaids ?? 0;
        _snapTrialLevels = cfg.Health?.HealPrice?.TrialLevels ?? 0;

        try
        {
            var therapist = databaseService.GetTrader(TherapistId);
            if (therapist?.Base?.LoyaltyLevels != null)
            {
                _snapTherapistHealPrice.Clear();
                foreach (var ll in therapist.Base.LoyaltyLevels)
                    _snapTherapistHealPrice.Add(ll.HealPriceCoefficient);
            }
        }
        catch { /* skip */ }

        // ── N. Repair ──
        _snapRepairPriceMult = repairConfig.PriceMultiplier;
        _snapApplyRandomDurabilityLoss = repairConfig.ApplyRandomizeDurabilityLoss;
        _snapArmorKitSkillMult = repairConfig.ArmorKitSkillPointGainPerRepairPointMultiplier;
        _snapWeaponMaintenanceSkillMult = repairConfig.WeaponTreatment.PointGainMultiplier;
        _snapIntellectWeaponKitMult = repairConfig.RepairKitIntellectGainMultiplier.Weapon;
        _snapIntellectArmorKitMult = repairConfig.RepairKitIntellectGainMultiplier.Armor;
        _snapMaxIntellectKit = repairConfig.MaxIntellectGainPerRepair.Kit;
        _snapMaxIntellectTrader = repairConfig.MaxIntellectGainPerRepair.Trader;
        _snapMinDurabilitySell = cfg.TradingSettings?.BuyoutRestrictions?.MinDurability ?? 0;

        // Per-trader repair price per LL
        foreach (var traderId in new[] { PraporId, SkierId, MechanicId })
        {
            try
            {
                var trader = databaseService.GetTrader(traderId);
                if (trader?.Base?.LoyaltyLevels == null) continue;
                var prices = new List<double?>();
                foreach (var ll in trader.Base.LoyaltyLevels)
                    prices.Add(ll.RepairPriceCoefficient);
                _snapRepairPricePerLl[traderId] = prices;
            }
            catch { /* skip */ }
        }

        // Armor material degradation
        if (cfg.ArmorMaterials != null)
        {
            foreach (var (mat, armorType) in cfg.ArmorMaterials)
            {
                _snapArmorDegradation[mat] = (
                    armorType.MinRepairDegradation, armorType.MaxRepairDegradation,
                    armorType.MinRepairKitDegradation, armorType.MaxRepairKitDegradation);
            }
        }

        // Weapon repair degradation — find all weapon items with degradation values
        try
        {
            var items = databaseService.GetItems();
            foreach (var (tpl, item) in items)
            {
                var props = item?.Properties;
                if (props == null) continue;
                // Only snapshot items that have repair degradation properties set
                if (props.MinRepairDegradation == null && props.MaxRepairDegradation == null
                    && props.MinRepairKitDegradation == null && props.MaxRepairKitDegradation == null) continue;
                // Only weapons (parent chain check via _parent containing weapon categories)
                // Simple heuristic: items with degradation fields are weapons/armor
                _snapWeaponDegradation[tpl] = (
                    props.MinRepairDegradation, props.MaxRepairDegradation,
                    props.MinRepairKitDegradation, props.MaxRepairKitDegradation);
            }
        }
        catch (Exception ex) { logger.Warning($"[ZSlayerHQ] Weapon degradation snapshot failed: {ex.Message}"); }

        // ── O. Clothing ──
        try
        {
            var customization = databaseService.GetCustomization();
            foreach (var (id, cust) in customization)
            {
                if (cust?.Properties == null) continue;
                if (cust.Parent != SuitParent1 && cust.Parent != SuitParent2) continue;
                _suitSnapshots[id] = new SuitSnapshot(
                    new List<string>(cust.Properties.Side ?? []),
                    cust.Properties.AvailableAsDefault);
            }
        }
        catch (Exception ex) { logger.Warning($"[ZSlayerHQ] Clothing snapshot failed: {ex.Message}"); }

        // Ragman suit requirements
        try
        {
            var ragman = databaseService.GetTrader(RagmanId);
            if (ragman?.Suits != null)
            {
                foreach (var suit in ragman.Suits)
                {
                    var req = suit.Requirements;
                    if (req == null) continue;
                    _suitReqSnapshots[suit.Id] = new SuitReqSnapshot(
                        req.ItemRequirements != null ? new List<ItemRequirement>(req.ItemRequirements) : null,
                        req.LoyaltyLevel, req.ProfileLevel,
                        req.Standing, req.PrestigeLevel,
                        req.QuestRequirements != null ? new List<string>(req.QuestRequirements) : null,
                        req.AchievementRequirements != null ? new List<string>(req.AchievementRequirements) : null);
                }
            }
        }
        catch (Exception ex) { logger.Warning($"[ZSlayerHQ] Ragman suit snapshot failed: {ex.Message}"); }

        _snapshotTaken = true;
        logger.Info("[ZSlayerHQ] Service settings snapshots taken");
    }

    // ═══════════════════════════════════════════════════════
    // APPLY ALL
    // ═══════════════════════════════════════════════════════

    public void ApplyAll()
    {
        lock (_lock)
        {
            TakeSnapshot();
            var config = configService.GetConfig().ServiceSettings;
            ApplyConfig(config);
        }
    }

    private void ApplyConfig(ServiceSettingsConfig c)
    {
        var globals = databaseService.GetGlobals();
        var cfg = globals.Configuration;
        var insuranceConfig = configServer.GetConfig<InsuranceConfig>();
        var repairConfig = configServer.GetConfig<RepairConfig>();

        // ══════════════ RESTORE ALL from snapshots first ══════════════

        // L. Insurance — InsuranceConfig
        if (insuranceConfig.ReturnChancePercent != null)
        {
            insuranceConfig.ReturnChancePercent[PraporId] = _snapPraporReturnChance;
            insuranceConfig.ReturnChancePercent[TherapistId] = _snapTherapistReturnChance;
        }
        insuranceConfig.ChanceNoAttachmentsTakenPercent = _snapAttachmentRecoveryChance;
        insuranceConfig.RunIntervalSeconds = _snapInsuranceInterval;
        insuranceConfig.ReturnTimeOverrideSeconds = _snapInsuranceReturnOverride;

        // L. Insurance — trader settings
        try
        {
            var prapor = databaseService.GetTrader(PraporId);
            if (prapor?.Base?.Insurance != null)
            {
                prapor.Base.Insurance.MaxStorageTime = _snapPraporStorageTime;
                prapor.Base.Insurance.MinReturnHour = _snapPraporReturnMin;
                prapor.Base.Insurance.MaxReturnHour = _snapPraporReturnMax;
            }
            if (prapor?.Base?.LoyaltyLevels != null)
            {
                for (var i = 0; i < prapor.Base.LoyaltyLevels.Count && i < _snapPraporInsurancePrice.Count; i++)
                    prapor.Base.LoyaltyLevels[i].InsurancePriceCoefficient = _snapPraporInsurancePrice[i];
            }
        }
        catch { /* skip */ }

        try
        {
            var therapist = databaseService.GetTrader(TherapistId);
            if (therapist?.Base?.Insurance != null)
            {
                therapist.Base.Insurance.MaxStorageTime = _snapTherapistStorageTime;
                therapist.Base.Insurance.MinReturnHour = _snapTherapistReturnMin;
                therapist.Base.Insurance.MaxReturnHour = _snapTherapistReturnMax;
            }
            if (therapist?.Base?.LoyaltyLevels != null)
            {
                for (var i = 0; i < therapist.Base.LoyaltyLevels.Count && i < _snapTherapistInsurancePrice.Count; i++)
                    therapist.Base.LoyaltyLevels[i].InsurancePriceCoefficient = _snapTherapistInsurancePrice[i];
            }
        }
        catch { /* skip */ }

        // M. Healing
        if (cfg.Health?.HealPrice != null)
        {
            cfg.Health.HealPrice.TrialRaids = _snapTrialRaids;
            cfg.Health.HealPrice.TrialLevels = _snapTrialLevels;
        }

        try
        {
            var therapist = databaseService.GetTrader(TherapistId);
            if (therapist?.Base?.LoyaltyLevels != null)
            {
                for (var i = 0; i < therapist.Base.LoyaltyLevels.Count && i < _snapTherapistHealPrice.Count; i++)
                    therapist.Base.LoyaltyLevels[i].HealPriceCoefficient = _snapTherapistHealPrice[i];
            }
        }
        catch { /* skip */ }

        // N. Repair — RepairConfig
        repairConfig.PriceMultiplier = _snapRepairPriceMult;
        repairConfig.ApplyRandomizeDurabilityLoss = _snapApplyRandomDurabilityLoss;
        repairConfig.ArmorKitSkillPointGainPerRepairPointMultiplier = _snapArmorKitSkillMult;
        repairConfig.WeaponTreatment.PointGainMultiplier = _snapWeaponMaintenanceSkillMult;
        repairConfig.RepairKitIntellectGainMultiplier.Weapon = _snapIntellectWeaponKitMult;
        repairConfig.RepairKitIntellectGainMultiplier.Armor = _snapIntellectArmorKitMult;
        repairConfig.MaxIntellectGainPerRepair.Kit = _snapMaxIntellectKit;
        repairConfig.MaxIntellectGainPerRepair.Trader = _snapMaxIntellectTrader;
        if (cfg.TradingSettings?.BuyoutRestrictions != null)
            cfg.TradingSettings.BuyoutRestrictions.MinDurability = _snapMinDurabilitySell;

        // N. Repair — per-trader per-LL
        foreach (var (traderId, prices) in _snapRepairPricePerLl)
        {
            try
            {
                var trader = databaseService.GetTrader(traderId);
                if (trader?.Base?.LoyaltyLevels == null) continue;
                for (var i = 0; i < trader.Base.LoyaltyLevels.Count && i < prices.Count; i++)
                    trader.Base.LoyaltyLevels[i].RepairPriceCoefficient = prices[i];
            }
            catch { /* skip */ }
        }

        // N. Repair — armor material degradation
        if (cfg.ArmorMaterials != null)
        {
            foreach (var (mat, snap) in _snapArmorDegradation)
            {
                if (!cfg.ArmorMaterials.TryGetValue(mat, out var armorType)) continue;
                armorType.MinRepairDegradation = snap.MinR;
                armorType.MaxRepairDegradation = snap.MaxR;
                armorType.MinRepairKitDegradation = snap.MinK;
                armorType.MaxRepairKitDegradation = snap.MaxK;
            }
        }

        // N. Repair — weapon degradation
        try
        {
            var items = databaseService.GetItems();
            foreach (var (tpl, snap) in _snapWeaponDegradation)
            {
                if (!items.TryGetValue(tpl, out var item)) continue;
                var props = item?.Properties;
                if (props == null) continue;
                props.MinRepairDegradation = snap.MinR;
                props.MaxRepairDegradation = snap.MaxR;
                props.MinRepairKitDegradation = snap.MinK;
                props.MaxRepairKitDegradation = snap.MaxK;
            }
        }
        catch { /* skip */ }

        // O. Clothing — restore customization
        try
        {
            var customization = databaseService.GetCustomization();
            foreach (var (id, snap) in _suitSnapshots)
            {
                if (!customization.TryGetValue(id, out var cust)) continue;
                if (cust?.Properties == null) continue;
                cust.Properties.Side = new List<string>(snap.Side);
                cust.Properties.AvailableAsDefault = snap.AvailableAsDefault;
            }
        }
        catch { /* skip */ }

        // O. Clothing — restore Ragman suit requirements
        try
        {
            var ragman = databaseService.GetTrader(RagmanId);
            if (ragman?.Suits != null)
            {
                foreach (var suit in ragman.Suits)
                {
                    if (!_suitReqSnapshots.TryGetValue(suit.Id, out var snap)) continue;
                    if (suit.Requirements == null) continue;
                    suit.Requirements.ItemRequirements = snap.ItemRequirements != null
                        ? new List<ItemRequirement>(snap.ItemRequirements) : null;
                    suit.Requirements.LoyaltyLevel = snap.LoyaltyLevel;
                    suit.Requirements.ProfileLevel = snap.ProfileLevel;
                    suit.Requirements.Standing = snap.Standing;
                    suit.Requirements.PrestigeLevel = snap.PrestigeLevel;
                    suit.Requirements.QuestRequirements = snap.QuestRequirements != null
                        ? new List<string>(snap.QuestRequirements) : null;
                    suit.Requirements.AchievementRequirements = snap.AchievementRequirements != null
                        ? new List<string>(snap.AchievementRequirements) : null;
                }
            }
        }
        catch { /* skip */ }

        // ══════════════ NOW APPLY user config on top ══════════════

        // L. Insurance
        if (c.EnableInsurance)
        {
            if (insuranceConfig.ReturnChancePercent != null)
            {
                if (c.PraporReturnChance.HasValue)
                    insuranceConfig.ReturnChancePercent[PraporId] = c.PraporReturnChance.Value;
                if (c.TherapistReturnChance.HasValue)
                    insuranceConfig.ReturnChancePercent[TherapistId] = c.TherapistReturnChance.Value;
            }
            if (c.AttachmentRecoveryChance.HasValue)
                insuranceConfig.ChanceNoAttachmentsTakenPercent = c.AttachmentRecoveryChance.Value;
            if (c.InsuranceInterval.HasValue)
                insuranceConfig.RunIntervalSeconds = c.InsuranceInterval.Value;
            if (c.InsuranceReturnOverride.HasValue)
                insuranceConfig.ReturnTimeOverrideSeconds = c.InsuranceReturnOverride.Value;

            // Prapor trader insurance
            try
            {
                var prapor = databaseService.GetTrader(PraporId);
                if (prapor?.Base?.Insurance != null)
                {
                    if (c.PraporStorageTime.HasValue) prapor.Base.Insurance.MaxStorageTime = c.PraporStorageTime.Value;
                    if (c.PraporReturnMin.HasValue) prapor.Base.Insurance.MinReturnHour = c.PraporReturnMin.Value;
                    if (c.PraporReturnMax.HasValue) prapor.Base.Insurance.MaxReturnHour = c.PraporReturnMax.Value;
                }
                if (c.PraporPricePerLl != null && prapor?.Base?.LoyaltyLevels != null)
                {
                    for (var i = 0; i < prapor.Base.LoyaltyLevels.Count && i < c.PraporPricePerLl.Count; i++)
                        prapor.Base.LoyaltyLevels[i].InsurancePriceCoefficient = c.PraporPricePerLl[i];
                }
            }
            catch { /* skip */ }

            // Therapist trader insurance
            try
            {
                var therapist = databaseService.GetTrader(TherapistId);
                if (therapist?.Base?.Insurance != null)
                {
                    if (c.TherapistStorageTime.HasValue) therapist.Base.Insurance.MaxStorageTime = c.TherapistStorageTime.Value;
                    if (c.TherapistReturnMin.HasValue) therapist.Base.Insurance.MinReturnHour = c.TherapistReturnMin.Value;
                    if (c.TherapistReturnMax.HasValue) therapist.Base.Insurance.MaxReturnHour = c.TherapistReturnMax.Value;
                }
                if (c.TherapistPricePerLl != null && therapist?.Base?.LoyaltyLevels != null)
                {
                    for (var i = 0; i < therapist.Base.LoyaltyLevels.Count && i < c.TherapistPricePerLl.Count; i++)
                        therapist.Base.LoyaltyLevels[i].InsurancePriceCoefficient = c.TherapistPricePerLl[i];
                }
            }
            catch { /* skip */ }
        }

        // M. Healing
        if (c.EnableHealing)
        {
            if (cfg.Health?.HealPrice != null)
            {
                if (c.FreeHealRaids.HasValue) cfg.Health.HealPrice.TrialRaids = c.FreeHealRaids.Value;
                if (c.FreeHealLevel.HasValue) cfg.Health.HealPrice.TrialLevels = c.FreeHealLevel.Value;
            }

            if (c.TherapistHealPerLl != null)
            {
                try
                {
                    var therapist = databaseService.GetTrader(TherapistId);
                    if (therapist?.Base?.LoyaltyLevels != null)
                    {
                        for (var i = 0; i < therapist.Base.LoyaltyLevels.Count && i < c.TherapistHealPerLl.Count; i++)
                            therapist.Base.LoyaltyLevels[i].HealPriceCoefficient = c.TherapistHealPerLl[i];
                    }
                }
                catch { /* skip */ }
            }
        }

        // N. Repair
        if (c.EnableRepair)
        {
            if (c.RepairPriceMult.HasValue) repairConfig.PriceMultiplier = c.RepairPriceMult.Value;
            if (c.NoRandomRepairLoss) repairConfig.ApplyRandomizeDurabilityLoss = false; // inverted
            if (c.ArmorKitSkillMult.HasValue) repairConfig.ArmorKitSkillPointGainPerRepairPointMultiplier = c.ArmorKitSkillMult.Value;
            if (c.WeaponMaintenanceSkillMult.HasValue) repairConfig.WeaponTreatment.PointGainMultiplier = c.WeaponMaintenanceSkillMult.Value;
            if (c.IntellectWeaponKitMult.HasValue) repairConfig.RepairKitIntellectGainMultiplier.Weapon = c.IntellectWeaponKitMult.Value;
            if (c.IntellectArmorKitMult.HasValue) repairConfig.RepairKitIntellectGainMultiplier.Armor = c.IntellectArmorKitMult.Value;
            if (c.MaxIntellectKit.HasValue) repairConfig.MaxIntellectGainPerRepair.Kit = c.MaxIntellectKit.Value;
            if (c.MaxIntellectTrader.HasValue) repairConfig.MaxIntellectGainPerRepair.Trader = c.MaxIntellectTrader.Value;
            if (c.MinDurabilitySell.HasValue && cfg.TradingSettings?.BuyoutRestrictions != null)
                cfg.TradingSettings.BuyoutRestrictions.MinDurability = c.MinDurabilitySell.Value;

            // No armor repair degradation
            if (c.NoArmorRepairDegradation && cfg.ArmorMaterials != null)
            {
                foreach (var (_, armorType) in cfg.ArmorMaterials)
                {
                    armorType.MinRepairDegradation = 0;
                    armorType.MaxRepairDegradation = 0;
                    armorType.MinRepairKitDegradation = 0;
                    armorType.MaxRepairKitDegradation = 0;
                }
            }

            // No weapon repair degradation
            if (c.NoWeaponRepairDegradation)
            {
                try
                {
                    var items = databaseService.GetItems();
                    foreach (var (tpl, _) in _snapWeaponDegradation)
                    {
                        if (!items.TryGetValue(tpl, out var item)) continue;
                        var props = item?.Properties;
                        if (props == null) continue;
                        props.MinRepairDegradation = 0;
                        props.MaxRepairDegradation = 0;
                        props.MinRepairKitDegradation = 0;
                        props.MaxRepairKitDegradation = 0;
                    }
                }
                catch { /* skip */ }
            }

            // Per-trader repair price
            if (c.RepairPricePerLl != null)
            {
                foreach (var (traderId, prices) in c.RepairPricePerLl)
                {
                    try
                    {
                        var trader = databaseService.GetTrader(traderId);
                        if (trader?.Base?.LoyaltyLevels == null) continue;
                        for (var i = 0; i < trader.Base.LoyaltyLevels.Count && i < prices.Count; i++)
                            trader.Base.LoyaltyLevels[i].RepairPriceCoefficient = prices[i];
                    }
                    catch { /* skip */ }
                }
            }
        }

        // O. Clothing
        if (c.ClothesAnyFaction)
        {
            try
            {
                var customization = databaseService.GetCustomization();
                foreach (var (id, _) in _suitSnapshots)
                {
                    if (!customization.TryGetValue(id, out var cust)) continue;
                    if (cust?.Properties == null) continue;
                    cust.Properties.Side = ["Bear", "Usec", "Savage"];
                    cust.Properties.AvailableAsDefault = true;
                }
            }
            catch { /* skip */ }
        }

        if (c.ClothesFree)
        {
            try
            {
                var ragman = databaseService.GetTrader(RagmanId);
                if (ragman?.Suits != null)
                {
                    foreach (var suit in ragman.Suits)
                    {
                        if (suit.Requirements != null)
                            suit.Requirements.ItemRequirements = [];
                    }
                }
            }
            catch { /* skip */ }
        }

        if (c.ClothesRemoveReqs)
        {
            try
            {
                var ragman = databaseService.GetTrader(RagmanId);
                if (ragman?.Suits != null)
                {
                    foreach (var suit in ragman.Suits)
                    {
                        if (suit.Requirements == null) continue;
                        suit.Requirements.LoyaltyLevel = 1;
                        suit.Requirements.ProfileLevel = 1;
                        suit.Requirements.Standing = 0;
                        suit.Requirements.PrestigeLevel = 0;
                        suit.Requirements.QuestRequirements = [];
                        suit.Requirements.AchievementRequirements = [];
                    }
                }
            }
            catch { /* skip */ }
        }

        logger.Info("[ZSlayerHQ] Service settings applied");
    }

    // ═══════════════════════════════════════════════════════
    // GET CONFIG (for frontend)
    // ═══════════════════════════════════════════════════════

    public ServiceSettingsConfigResponse GetConfig()
    {
        lock (_lock)
        {
            TakeSnapshot();
            var config = configService.GetConfig().ServiceSettings;

            // Build repair traders list
            var repairTraders = new List<RepairTraderInfo>();
            foreach (var (traderId, traderName) in new[] { (PraporId, "Prapor"), (SkierId, "Skier"), (MechanicId, "Mechanic") })
            {
                if (!_snapRepairPricePerLl.TryGetValue(traderId, out var prices)) continue;
                repairTraders.Add(new RepairTraderInfo
                {
                    Id = traderId,
                    Name = traderName,
                    PricePerLl = prices.Select(p => p ?? 0).ToList()
                });
            }

            var defaults = new ServiceSettingsDefaults
            {
                // L. Insurance
                PraporReturnChance = _snapPraporReturnChance,
                TherapistReturnChance = _snapTherapistReturnChance,
                PraporStorageTime = (int)(_snapPraporStorageTime ?? 0),
                TherapistStorageTime = (int)(_snapTherapistStorageTime ?? 0),
                PraporReturnMin = _snapPraporReturnMin ?? 0,
                PraporReturnMax = _snapPraporReturnMax ?? 0,
                TherapistReturnMin = _snapTherapistReturnMin ?? 0,
                TherapistReturnMax = _snapTherapistReturnMax ?? 0,
                AttachmentRecoveryChance = _snapAttachmentRecoveryChance,
                InsuranceInterval = _snapInsuranceInterval,
                InsuranceReturnOverride = _snapInsuranceReturnOverride,
                PraporPricePerLl = _snapPraporInsurancePrice.Select(p => p ?? 0).ToList(),
                TherapistPricePerLl = _snapTherapistInsurancePrice.Select(p => p ?? 0).ToList(),

                // M. Healing
                FreeHealRaids = (int)_snapTrialRaids,
                FreeHealLevel = (int)_snapTrialLevels,
                TherapistHealPerLl = _snapTherapistHealPrice.Select(p => p ?? 0).ToList(),

                // N. Repair
                RepairPriceMult = _snapRepairPriceMult,
                RandomRepairLoss = _snapApplyRandomDurabilityLoss,
                ArmorKitSkillMult = _snapArmorKitSkillMult,
                WeaponMaintenanceSkillMult = _snapWeaponMaintenanceSkillMult,
                IntellectWeaponKitMult = _snapIntellectWeaponKitMult,
                IntellectArmorKitMult = _snapIntellectArmorKitMult,
                MaxIntellectKit = _snapMaxIntellectKit,
                MaxIntellectTrader = _snapMaxIntellectTrader,
                MinDurabilitySell = _snapMinDurabilitySell,
                RepairTraders = repairTraders,
                ArmorMaterialCount = _snapArmorDegradation.Count,
                WeaponRepairCount = _snapWeaponDegradation.Count,

                // O. Clothing
                SuitCount = _suitSnapshots.Count,
            };

            return new ServiceSettingsConfigResponse { Config = config, Defaults = defaults };
        }
    }

    // ═══════════════════════════════════════════════════════
    // RESET
    // ═══════════════════════════════════════════════════════

    public void Reset()
    {
        lock (_lock)
        {
            var ccConfig = configService.GetConfig();
            ccConfig.ServiceSettings = new ServiceSettingsConfig();
            configService.SaveConfig();
            ApplyConfig(ccConfig.ServiceSettings);
        }
    }

    // ═══════════════════════════════════════════════════════
    // APPLY FROM REQUEST
    // ═══════════════════════════════════════════════════════

    public void Apply(ServiceSettingsConfig incoming)
    {
        lock (_lock)
        {
            var ccConfig = configService.GetConfig();
            ccConfig.ServiceSettings = incoming;
            configService.SaveConfig();
            ApplyConfig(incoming);
        }
    }
}

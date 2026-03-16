using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Hideout;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using ZSlayerCommandCenter.Models;

namespace ZSlayerCommandCenter.Services;

[Injectable(InjectionType.Singleton)]
public class FirService(
    DatabaseService databaseService,
    ConfigServer configServer,
    ConfigService configService,
    ISptLogger<FirService> logger)
{
    private readonly object _lock = new();
    private bool _snapshotTaken;

    // ── Simple config snapshots ──
    private bool _snapFleaPurchasesFir;
    private bool _snapTraderPurchasesFir;

    // ── Globals snapshots ──
    private bool _snapTradingUnlimited;
    private bool _snapMaxLoyalty;
    private bool _snapDiscardLimitsEnabled;
    private bool _snapSkillAtrophy;
    private bool _snapTieredFleaEnabled;

    // ── Lost on Death snapshots ──
    private bool _snapLodHeadwear, _snapLodEarpiece, _snapLodFaceCover, _snapLodArmorVest;
    private bool _snapLodEyewear, _snapLodTacticalVest, _snapLodPocketItems, _snapLodBackpack;
    private bool _snapLodHolster, _snapLodFirstPrimary, _snapLodSecondPrimary;
    private bool _snapLodScabbard, _snapLodArmBand, _snapLodCompass, _snapLodSecuredContainer;
    private bool _snapLodQuestItems, _snapLodSpecialSlotItems;

    // ── Barter OnlyFunctional snapshot ──
    private readonly Dictionary<string, bool?> _barterSnapshot = new();

    // ── Hideout IsSpawnedInSession snapshot ──
    private readonly Dictionary<string, bool?> _hideoutSnapshot = new();

    // ── Purchase limit snapshot ──
    private readonly Dictionary<string, int?> _buyLimitSnapshot = new();

    public void Initialize()
    {
        lock (_lock)
        {
            TakeSnapshot();
            ApplyConfig();
        }
    }

    private void TakeSnapshot()
    {
        if (_snapshotTaken) return;

        var ragfairConfig = configServer.GetConfig<RagfairConfig>();
        _snapFleaPurchasesFir = ragfairConfig.Dynamic.PurchasesAreFoundInRaid;
        _snapTieredFleaEnabled = ragfairConfig.TieredFlea.Enabled;

        var traderConfig = configServer.GetConfig<TraderConfig>();
        _snapTraderPurchasesFir = traderConfig.PurchasesAreFoundInRaid;

        // Globals
        var globals = databaseService.GetGlobals().Configuration;
        _snapTradingUnlimited = globals.TradingUnlimitedItems;
        _snapMaxLoyalty = globals.MaxLoyaltyLevelForAll;
        _snapDiscardLimitsEnabled = globals.DiscardLimitsEnabled;
        _snapSkillAtrophy = globals.SkillAtrophy;

        // Lost on Death
        var lod = configServer.GetConfig<LostOnDeathConfig>();
        _snapLodHeadwear = lod.Equipment.Headwear;
        _snapLodEarpiece = lod.Equipment.Earpiece;
        _snapLodFaceCover = lod.Equipment.FaceCover;
        _snapLodArmorVest = lod.Equipment.ArmorVest;
        _snapLodEyewear = lod.Equipment.Eyewear;
        _snapLodTacticalVest = lod.Equipment.TacticalVest;
        _snapLodPocketItems = lod.Equipment.PocketItems;
        _snapLodBackpack = lod.Equipment.Backpack;
        _snapLodHolster = lod.Equipment.Holster;
        _snapLodFirstPrimary = lod.Equipment.FirstPrimaryWeapon;
        _snapLodSecondPrimary = lod.Equipment.SecondPrimaryWeapon;
        _snapLodScabbard = lod.Equipment.Scabbard;
        _snapLodArmBand = lod.Equipment.ArmBand;
        _snapLodCompass = lod.Equipment.Compass;
        _snapLodSecuredContainer = lod.Equipment.SecuredContainer;
        _snapLodQuestItems = lod.QuestItems;
        _snapLodSpecialSlotItems = lod.SpecialSlotItems;

        // Snapshot barter scheme OnlyFunctional values
        foreach (var (traderId, trader) in databaseService.GetTables().Traders)
        {
            if (trader.Assort?.BarterScheme == null) continue;
            foreach (var (itemId, paymentOptions) in trader.Assort.BarterScheme)
            {
                for (var oi = 0; oi < paymentOptions.Count; oi++)
                {
                    for (var ri = 0; ri < paymentOptions[oi].Count; ri++)
                        _barterSnapshot[$"{traderId}|{itemId}|{oi}|{ri}"] = paymentOptions[oi][ri].OnlyFunctional;
                }
            }

            // Snapshot purchase limit (BuyRestrictionMax) per assort root item
            if (trader.Assort?.Items != null)
            {
                foreach (var item in trader.Assort.Items)
                {
                    if (item.ParentId != "hideout") continue;
                    _buyLimitSnapshot[$"{traderId}|{item.Id}"] = item.Upd?.BuyRestrictionMax;
                }
            }
        }

        // Snapshot hideout stage requirement IsSpawnedInSession values
        foreach (var area in databaseService.GetHideout().Areas)
        {
            var areaType = area.Type?.ToString() ?? "";
            foreach (var (stageKey, stage) in area.Stages)
            {
                if (stage.Requirements == null) continue;
                for (var ri = 0; ri < stage.Requirements.Count; ri++)
                    _hideoutSnapshot[$"{areaType}|{stageKey}|{ri}"] = stage.Requirements[ri].IsSpawnedInSession;
            }
        }

        _snapshotTaken = true;
        logger.Info("FirService: snapshots taken (FIR + globals + lost-on-death)");
    }

    public FirApplyResult ApplyConfig()
    {
        lock (_lock)
        {
            if (!_snapshotTaken) TakeSnapshot();
            return ApplyConfigInternal();
        }
    }

    private FirApplyResult ApplyConfigInternal()
    {
        var cfg = configService.GetConfig().Fir;
        var changes = new List<string>();

        // ══════════════════════════════════════════
        // RESTORE all values to snapshot first
        // ══════════════════════════════════════════

        var ragfairConfig = configServer.GetConfig<RagfairConfig>();
        ragfairConfig.Dynamic.PurchasesAreFoundInRaid = _snapFleaPurchasesFir;
        ragfairConfig.TieredFlea.Enabled = _snapTieredFleaEnabled;

        var traderConfig = configServer.GetConfig<TraderConfig>();
        traderConfig.PurchasesAreFoundInRaid = _snapTraderPurchasesFir;

        // Restore globals
        var globals = databaseService.GetGlobals().Configuration;
        globals.TradingUnlimitedItems = _snapTradingUnlimited;
        globals.MaxLoyaltyLevelForAll = _snapMaxLoyalty;
        globals.DiscardLimitsEnabled = _snapDiscardLimitsEnabled;
        globals.SkillAtrophy = _snapSkillAtrophy;

        // Restore lost-on-death
        var lod = configServer.GetConfig<LostOnDeathConfig>();
        lod.Equipment.Headwear = _snapLodHeadwear;
        lod.Equipment.Earpiece = _snapLodEarpiece;
        lod.Equipment.FaceCover = _snapLodFaceCover;
        lod.Equipment.ArmorVest = _snapLodArmorVest;
        lod.Equipment.Eyewear = _snapLodEyewear;
        lod.Equipment.TacticalVest = _snapLodTacticalVest;
        lod.Equipment.PocketItems = _snapLodPocketItems;
        lod.Equipment.Backpack = _snapLodBackpack;
        lod.Equipment.Holster = _snapLodHolster;
        lod.Equipment.FirstPrimaryWeapon = _snapLodFirstPrimary;
        lod.Equipment.SecondPrimaryWeapon = _snapLodSecondPrimary;
        lod.Equipment.Scabbard = _snapLodScabbard;
        lod.Equipment.ArmBand = _snapLodArmBand;
        lod.Equipment.Compass = _snapLodCompass;
        lod.Equipment.SecuredContainer = _snapLodSecuredContainer;
        lod.QuestItems = _snapLodQuestItems;
        lod.SpecialSlotItems = _snapLodSpecialSlotItems;

        // Restore barter/trader/hideout values
        var traders = databaseService.GetTables().Traders;
        foreach (var (traderId, trader) in traders)
        {
            if (trader.Assort?.BarterScheme == null) continue;
            foreach (var (itemId, paymentOptions) in trader.Assort.BarterScheme)
            {
                for (var oi = 0; oi < paymentOptions.Count; oi++)
                {
                    for (var ri = 0; ri < paymentOptions[oi].Count; ri++)
                    {
                        if (_barterSnapshot.TryGetValue($"{traderId}|{itemId}|{oi}|{ri}", out var orig))
                            paymentOptions[oi][ri].OnlyFunctional = orig;
                    }
                }
            }

            if (trader.Assort?.Items != null)
            {
                foreach (var item in trader.Assort.Items)
                {
                    if (item.ParentId != "hideout") continue;
                    if (_buyLimitSnapshot.TryGetValue($"{traderId}|{item.Id}", out var origLimit))
                    {
                        if (item.Upd != null)
                            item.Upd.BuyRestrictionMax = origLimit;
                    }
                }
            }
        }

        var hideout = databaseService.GetHideout();
        foreach (var area in hideout.Areas)
        {
            var areaType = area.Type?.ToString() ?? "";
            foreach (var (stageKey, stage) in area.Stages)
            {
                if (stage.Requirements == null) continue;
                for (var ri = 0; ri < stage.Requirements.Count; ri++)
                {
                    if (_hideoutSnapshot.TryGetValue($"{areaType}|{stageKey}|{ri}", out var orig))
                        stage.Requirements[ri].IsSpawnedInSession = orig;
                }
            }
        }

        // ══════════════════════════════════════════
        // APPLY new config values
        // ══════════════════════════════════════════

        // ── FIR toggles ──
        if (cfg.FleaPurchasesFir)
        {
            ragfairConfig.Dynamic.PurchasesAreFoundInRaid = true;
            changes.Add("Flea purchases: marked as FIR");
        }

        if (cfg.TraderPurchasesFir)
        {
            traderConfig.PurchasesAreFoundInRaid = true;
            changes.Add("Trader purchases: marked as FIR");
        }

        if (cfg.BartersRequireFunctional)
        {
            var count = 0;
            foreach (var (_, trader) in traders)
            {
                if (trader.Assort?.BarterScheme == null) continue;
                foreach (var (_, paymentOptions) in trader.Assort.BarterScheme)
                {
                    foreach (var option in paymentOptions)
                    {
                        foreach (var cost in option)
                        {
                            if (TraderDiscoveryService.IsCurrencyTemplate(cost.Template.ToString())) continue;
                            cost.OnlyFunctional = true;
                            count++;
                        }
                    }
                }
            }
            changes.Add($"Barters: {count} items require functional condition");
        }

        if (cfg.HideoutConstructionFir)
        {
            var count = 0;
            foreach (var area in hideout.Areas)
            {
                foreach (var (_, stage) in area.Stages)
                {
                    if (stage.Requirements == null) continue;
                    foreach (var req in stage.Requirements)
                    {
                        if (req.IsSpawnedInSession == null) continue;
                        req.IsSpawnedInSession = true;
                        count++;
                    }
                }
            }
            changes.Add($"Hideout: {count} construction requirements need FIR");
        }

        // ── Purchase quantity limits ──
        if (cfg.PurchaseLimitEnabled && cfg.PurchaseLimitPerReset > 0)
        {
            var count = 0;
            foreach (var (_, trader) in traders)
            {
                if (trader.Assort?.Items == null) continue;
                foreach (var item in trader.Assort.Items)
                {
                    if (item.ParentId != "hideout") continue;
                    if (item.Upd == null) continue;

                    var existing = item.Upd.BuyRestrictionMax;
                    if (existing == null || existing > cfg.PurchaseLimitPerReset)
                    {
                        item.Upd.BuyRestrictionMax = cfg.PurchaseLimitPerReset;
                        item.Upd.BuyRestrictionCurrent ??= 0;
                        count++;
                    }
                }
            }
            changes.Add($"Purchase limits: {count} items capped at {cfg.PurchaseLimitPerReset}/reset");
        }

        // ── Global gameplay toggles ──
        if (cfg.TradingUnlimitedItems == true)
        {
            globals.TradingUnlimitedItems = true;
            changes.Add("Unlimited trader stock: ON");
        }

        if (cfg.MaxLoyaltyLevelForAll == true)
        {
            globals.MaxLoyaltyLevelForAll = true;
            changes.Add("All loyalty levels unlocked: ON");
        }

        if (cfg.DiscardLimitsDisabled == true)
        {
            globals.DiscardLimitsEnabled = false;
            changes.Add("Discard limits: DISABLED");
        }

        if (cfg.SkillAtrophyDisabled == true)
        {
            globals.SkillAtrophy = false;
            changes.Add("Skill atrophy/decay: DISABLED");
        }

        if (cfg.TieredFleaEnabled == true)
        {
            ragfairConfig.TieredFlea.Enabled = true;
            changes.Add("Tiered flea market: ON");
        }
        else if (cfg.TieredFleaEnabled == false)
        {
            ragfairConfig.TieredFlea.Enabled = false;
            changes.Add("Tiered flea market: OFF");
        }

        // ── Lost on Death ──
        if (cfg.LostOnDeath is { Enabled: true })
        {
            var d = cfg.LostOnDeath;
            lod.Equipment.Headwear = d.Headwear;
            lod.Equipment.Earpiece = d.Earpiece;
            lod.Equipment.FaceCover = d.FaceCover;
            lod.Equipment.ArmorVest = d.ArmorVest;
            lod.Equipment.Eyewear = d.Eyewear;
            lod.Equipment.TacticalVest = d.TacticalVest;
            lod.Equipment.PocketItems = d.PocketItems;
            lod.Equipment.Backpack = d.Backpack;
            lod.Equipment.Holster = d.Holster;
            lod.Equipment.FirstPrimaryWeapon = d.FirstPrimaryWeapon;
            lod.Equipment.SecondPrimaryWeapon = d.SecondPrimaryWeapon;
            lod.Equipment.Scabbard = d.Scabbard;
            lod.Equipment.ArmBand = d.ArmBand;
            lod.Equipment.Compass = d.Compass;
            lod.Equipment.SecuredContainer = d.SecuredContainer;
            lod.QuestItems = d.QuestItems;
            lod.SpecialSlotItems = d.SpecialSlotItems;

            var kept = new List<string>();
            if (!d.Headwear) kept.Add("headwear");
            if (!d.ArmorVest) kept.Add("armor");
            if (!d.Backpack) kept.Add("backpack");
            if (!d.FirstPrimaryWeapon) kept.Add("weapons");
            if (!d.TacticalVest) kept.Add("rig");
            if (d.SecuredContainer) kept.Add("lose secure container");

            var summary = kept.Count > 0 ? string.Join(", ", kept) : "custom slots";
            changes.Add($"Lost on death: overridden ({summary})");
        }

        // ── Runtime-enforced features (just report) ──
        if (cfg.BarterItemsMustBeFir)
            changes.Add("Barter items must be FIR (server-enforced)");
        if (cfg.SellToTraderRequiresFir)
            changes.Add("Selling to traders requires FIR (server-enforced)");
        if (cfg.NonFirSellPenaltyPercent > 0 && !cfg.SellToTraderRequiresFir)
            changes.Add($"Non-FIR sell penalty: {cfg.NonFirSellPenaltyPercent}% price reduction");
        if (cfg.SecureContainerWipeOnDeath)
            changes.Add("Secure container wipe on death (keys & cases preserved)");
        if (cfg.DeathTaxPercent > 0)
            changes.Add($"Death tax: {cfg.DeathTaxPercent}% of stash rubles on death");

        var msg = changes.Count > 0
            ? $"Applied {changes.Count} FIR/economy setting(s)"
            : "All FIR/economy toggles are off — defaults restored";
        logger.Info($"FirService: {msg}");

        return new FirApplyResult { Success = true, Message = msg, Changes = changes };
    }

    public FirApplyResult Reset()
    {
        lock (_lock)
        {
            configService.GetConfig().Fir = new FirConfig();
            configService.SaveConfig();
            return ApplyConfigInternal() with { Message = "FIR/economy settings reset to defaults" };
        }
    }

    public FirStatusResponse GetStatus() => new() { Config = configService.GetConfig().Fir };
}

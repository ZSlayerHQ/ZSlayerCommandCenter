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

    // ── Barter OnlyFunctional snapshot ──
    // Key: "{traderId}|{itemId}|{optionIdx}|{reqIdx}" → original OnlyFunctional
    private readonly Dictionary<string, bool?> _barterSnapshot = new();

    // ── Hideout IsSpawnedInSession snapshot ──
    // Key: "{areaType}|{stageKey}|{reqIdx}" → original IsSpawnedInSession
    private readonly Dictionary<string, bool?> _hideoutSnapshot = new();

    // ── Purchase limit snapshot ──
    // Key: "{traderId}|{itemId}" → original BuyRestrictionMax (null if none)
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

        var traderConfig = configServer.GetConfig<TraderConfig>();
        _snapTraderPurchasesFir = traderConfig.PurchasesAreFoundInRaid;

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
        logger.Info("FirService: snapshots taken");
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

        // ── Restore all values to snapshot first ──
        var ragfairConfig = configServer.GetConfig<RagfairConfig>();
        ragfairConfig.Dynamic.PurchasesAreFoundInRaid = _snapFleaPurchasesFir;

        var traderConfig = configServer.GetConfig<TraderConfig>();
        traderConfig.PurchasesAreFoundInRaid = _snapTraderPurchasesFir;

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

            // Restore purchase limits from snapshot
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

        // ── Apply new config values ──
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

                    // Only set limit on items that don't already have a tighter restriction
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

        // Note: BarterItemsMustBeFir, SellToTraderRequiresFir, NonFirSellPenaltyPercent, and DeathTaxPercent
        // are enforced at runtime by FirTradeController / RaidDataRouter — no DB changes needed
        if (cfg.BarterItemsMustBeFir)
            changes.Add("Barter items must be FIR (server-enforced)");
        if (cfg.SellToTraderRequiresFir)
            changes.Add("Selling to traders requires FIR (server-enforced)");
        if (cfg.NonFirSellPenaltyPercent > 0 && !cfg.SellToTraderRequiresFir)
            changes.Add($"Non-FIR sell penalty: {cfg.NonFirSellPenaltyPercent}% price reduction");
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

using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils;
using ZSlayerCommandCenter.Services;

namespace ZSlayerCommandCenter.Overrides;

/// <summary>
/// Subclass of SPT's RagfairPriceService that applies CC's flea buy multiplier
/// to every flea price lookup. Replaces the original via DI TypeOverride.
///
/// Why: previously CC mutated databaseService.GetPrices() to bake the multiplier
/// into the price dict. That worked in theory (RagfairPriceService.GetFleaPriceForItem
/// → ItemHelper.GetDynamicItemPrice → databaseService.GetPrices()) but was fragile
/// against startup-ordering races and silent regenerate failures. Hooking at the
/// price-service boundary applies the multiplier on EVERY offer generation
/// regardless of dict state.
/// </summary>
[Injectable(
    InjectionType = InjectionType.Singleton,
    TypeOverride = typeof(RagfairPriceService),
    TypePriority = OnLoadOrder.Watermark)]
public class RagfairPriceServiceOverride : RagfairPriceService
{
    private readonly FleaPriceService _fleaPriceService;
    private readonly ISptLogger<RagfairPriceServiceOverride> _ccLogger;

    public RagfairPriceServiceOverride(
        ISptLogger<RagfairPriceService> logger,
        RandomUtil randomUtil,
        HandbookHelper handbookHelper,
        TraderHelper traderHelper,
        PresetHelper presetHelper,
        ItemHelper itemHelper,
        DatabaseService databaseService,
        DatabaseServer databaseServer,
        ServerLocalisationService serverLocalisationService,
        ConfigServer configServer,
        FleaPriceService fleaPriceService,
        ISptLogger<RagfairPriceServiceOverride> ccLogger)
        : base(logger, randomUtil, handbookHelper, traderHelper, presetHelper,
               itemHelper, databaseService, databaseServer, serverLocalisationService, configServer)
    {
        _fleaPriceService = fleaPriceService;
        _ccLogger = ccLogger;
        _ccLogger.Info("ZSlayerCC Flea: RagfairPriceService override active — multiplier applied at service boundary");
    }

    /// <summary>
    /// Apply CC's effective buy multiplier on top of the base flea price.
    /// Called by SPT's offer generator for every dynamic offer.
    /// </summary>
    public override double GetFleaPriceForItem(MongoId tplId)
    {
        var basePrice = base.GetFleaPriceForItem(tplId);
        var (mult, _) = _fleaPriceService.GetEffectiveBuyMultiplier(tplId.ToString());
        if (mult == 1.0) return basePrice;
        return System.Math.Max(1.0, basePrice * mult);
    }
}

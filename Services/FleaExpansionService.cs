using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using ZSlayerCommandCenter.Models;

namespace ZSlayerCommandCenter.Services;

[Injectable(InjectionType.Singleton)]
public class FleaExpansionService(
    DatabaseService databaseService,
    ConfigServer configServer,
    ConfigService configService,
    ISptLogger<FleaExpansionService> logger)
{
    private readonly object _lock = new();
    private bool _snapshotTaken;

    // ═══════════════════════════════════════════════════════
    // SNAPSHOTS — D. FIR & Sell Controls
    // ═══════════════════════════════════════════════════════

    private bool _snapIsOnlyFirAllowed;
    private bool _snapPurchasesAreFir;
    private bool _snapFeesEnabled;
    private int _snapSellChanceBase;
    private double _snapSellChanceMult;
    private double _snapRepGainPerSale;
    private double _snapRepLossPerCancel;
    private bool _snapTieredFleaEnabled;
    private bool _snapRemoveSeasonalItems;

    // ═══════════════════════════════════════════════════════
    // SNAPSHOTS — E. Dynamic Offer Configuration
    // ═══════════════════════════════════════════════════════

    private double _snapBarterChance;
    private double _snapBundleChance;
    private int _snapExpiredOfferThreshold;
    private int _snapOfferItemCountMin;
    private int _snapOfferItemCountMax;
    private double _snapPriceRangeDefaultMin;
    private double _snapPriceRangeDefaultMax;
    private double _snapPriceRangePresetMin;
    private double _snapPriceRangePresetMax;
    private double _snapPriceRangePackMin;
    private double _snapPriceRangePackMax;
    private int _snapEndTimeSecondsMin;
    private int _snapEndTimeSecondsMax;
    private int _snapNonStackableCountMin;
    private int _snapNonStackableCountMax;
    private double _snapStackablePercentMin;
    private double _snapStackablePercentMax;
    private double _snapCurrencyRub;
    private double _snapCurrencyUsd;
    private double _snapCurrencyEur;

    // ═══════════════════════════════════════════════════════
    // SNAPSHOTS — F. Per-Category Conditions
    // ═══════════════════════════════════════════════════════

    // ═══════════════════════════════════════════════════════
    // SNAPSHOTS — Flea Globals
    // ═══════════════════════════════════════════════════════

    private int _snapMinUserLevel;
    private readonly List<double> _snapMaxActiveOfferCounts = new();

    private readonly Dictionary<string, (double Min, double Max)> _categoryConditionSnapshots = new();

    // ═══════════════════════════════════════════════════════
    // SNAPSHOTS — G. Blacklist Control
    // ═══════════════════════════════════════════════════════

    private bool _snapEnableBsgList;
    private bool _snapEnableQuestList;
    private bool _snapEnableCustomItemCategoryList;
    private bool _snapTraderItems;

    // Category parent IDs for condition editing
    private static readonly Dictionary<string, string> CategoryNames = new()
    {
        ["5422acb9af1c889c16000029"] = "Weapons",
        ["543be5664bdc2dd4348b4569"] = "Medical",
        ["5447e0e74bdc2d3c308b4567"] = "Special Equipment",
        ["543be5e94bdc2df1348b4568"] = "Keys",
        ["5448e5284bdc2dcb718b4567"] = "Vests",
        ["57bef4c42459772e8d35a53b"] = "Armor",
        ["543be6674bdc2df1348b4569"] = "Food"
    };

    // Currency template IDs
    private static readonly MongoId RubTpl = new("5449016a4bdc2d6f028b456f");
    private static readonly MongoId UsdTpl = new("5696686a4bdc2da3298b456a");
    private static readonly MongoId EurTpl = new("569668774bdc2da2298b4568");

    // ═══════════════════════════════════════════════════════
    // INITIALIZE
    // ═══════════════════════════════════════════════════════

    public void Initialize()
    {
        lock (_lock)
        {
            EnsureSnapshot();
            var cfg = configService.GetConfig().FleaExpansion;
            if (cfg.EnableFleaExpansion)
            {
                ApplyInternal(cfg);
                logger.Success("[ZSlayerHQ] Flea Expansion: applied settings");
            }
            else
            {
                logger.Info("[ZSlayerHQ] Flea Expansion: initialized (disabled)");
            }
        }
    }

    // ═══════════════════════════════════════════════════════
    // SNAPSHOT
    // ═══════════════════════════════════════════════════════

    private void EnsureSnapshot()
    {
        if (_snapshotTaken) return;

        var globals = databaseService.GetGlobals();
        var ragfairConfig = configServer.GetConfig<RagfairConfig>();

        // ── Flea Globals ──
        _snapMinUserLevel = globals.Configuration.RagFair.MinUserLevel;
        _snapMaxActiveOfferCounts.Clear();
        if (globals.Configuration.RagFair.MaxActiveOfferCount != null)
        {
            foreach (var entry in globals.Configuration.RagFair.MaxActiveOfferCount)
                _snapMaxActiveOfferCounts.Add(entry.Count);
        }

        // ── D. FIR & Sell Controls ──
        _snapIsOnlyFirAllowed = globals.Configuration.RagFair.IsOnlyFoundInRaidAllowed;
        _snapPurchasesAreFir = ragfairConfig.Dynamic.PurchasesAreFoundInRaid;
        _snapFeesEnabled = ragfairConfig.Sell.Fees;
        _snapSellChanceBase = ragfairConfig.Sell.Chance.Base;
        _snapSellChanceMult = ragfairConfig.Sell.Chance.SellMultiplier;
        _snapRepGainPerSale = globals.Configuration.RagFair.RatingIncreaseCount;
        _snapRepLossPerCancel = globals.Configuration.RagFair.RatingDecreaseCount;
        _snapTieredFleaEnabled = ragfairConfig.TieredFlea.Enabled;
        _snapRemoveSeasonalItems = ragfairConfig.Dynamic.RemoveSeasonalItemsWhenNotInEvent;

        // ── E. Dynamic Offer Configuration ──
        _snapBarterChance = ragfairConfig.Dynamic.Barter.ChancePercent;
        _snapBundleChance = ragfairConfig.Dynamic.Pack.ChancePercent;
        _snapExpiredOfferThreshold = ragfairConfig.Dynamic.ExpiredOfferThreshold;
        _snapOfferItemCountMin = ragfairConfig.Dynamic.OfferItemCount["default"].Min;
        _snapOfferItemCountMax = ragfairConfig.Dynamic.OfferItemCount["default"].Max;
        _snapPriceRangeDefaultMin = ragfairConfig.Dynamic.PriceRanges.Default.Min;
        _snapPriceRangeDefaultMax = ragfairConfig.Dynamic.PriceRanges.Default.Max;
        _snapPriceRangePresetMin = ragfairConfig.Dynamic.PriceRanges.Preset.Min;
        _snapPriceRangePresetMax = ragfairConfig.Dynamic.PriceRanges.Preset.Max;
        _snapPriceRangePackMin = ragfairConfig.Dynamic.PriceRanges.Pack.Min;
        _snapPriceRangePackMax = ragfairConfig.Dynamic.PriceRanges.Pack.Max;
        _snapEndTimeSecondsMin = ragfairConfig.Dynamic.EndTimeSeconds.Min;
        _snapEndTimeSecondsMax = ragfairConfig.Dynamic.EndTimeSeconds.Max;
        _snapNonStackableCountMin = ragfairConfig.Dynamic.NonStackableCount.Min;
        _snapNonStackableCountMax = ragfairConfig.Dynamic.NonStackableCount.Max;
        _snapStackablePercentMin = ragfairConfig.Dynamic.StackablePercent.Min;
        _snapStackablePercentMax = ragfairConfig.Dynamic.StackablePercent.Max;

        // Currency ratios
        if (ragfairConfig.Dynamic.OfferCurrencyChangePercent.TryGetValue(RubTpl, out var rubVal))
            _snapCurrencyRub = rubVal;
        if (ragfairConfig.Dynamic.OfferCurrencyChangePercent.TryGetValue(UsdTpl, out var usdVal))
            _snapCurrencyUsd = usdVal;
        if (ragfairConfig.Dynamic.OfferCurrencyChangePercent.TryGetValue(EurTpl, out var eurVal))
            _snapCurrencyEur = eurVal;

        // ── F. Per-Category Conditions ──
        _categoryConditionSnapshots.Clear();
        foreach (var (parentId, _) in CategoryNames)
        {
            var mongoId = new MongoId(parentId);
            if (ragfairConfig.Dynamic.Condition.TryGetValue(mongoId, out var condition))
            {
                _categoryConditionSnapshots[parentId] = (condition.Max.Min, condition.Max.Max);
            }
        }

        // ── G. Blacklist Control ──
        _snapEnableBsgList = ragfairConfig.Dynamic.Blacklist.EnableBsgList;
        _snapEnableQuestList = ragfairConfig.Dynamic.Blacklist.EnableQuestList;
        _snapEnableCustomItemCategoryList = ragfairConfig.Dynamic.Blacklist.EnableCustomItemCategoryList;
        _snapTraderItems = ragfairConfig.Dynamic.Blacklist.TraderItems;

        _snapshotTaken = true;
        logger.Info("FleaExpansionService: snapshots taken (FIR/sell, dynamic offers, category conditions)");
    }

    // ═══════════════════════════════════════════════════════
    // RESTORE
    // ═══════════════════════════════════════════════════════

    private void RestoreAll()
    {
        var globals = databaseService.GetGlobals();
        var ragfairConfig = configServer.GetConfig<RagfairConfig>();

        // ── Restore Flea Globals ──
        globals.Configuration.RagFair.MinUserLevel = _snapMinUserLevel;
        if (globals.Configuration.RagFair.MaxActiveOfferCount != null)
        {
            var i = 0;
            foreach (var entry in globals.Configuration.RagFair.MaxActiveOfferCount)
            {
                if (i < _snapMaxActiveOfferCounts.Count)
                    entry.Count = _snapMaxActiveOfferCounts[i];
                i++;
            }
        }

        // ── D. Restore FIR & Sell Controls ──
        globals.Configuration.RagFair.IsOnlyFoundInRaidAllowed = _snapIsOnlyFirAllowed;
        ragfairConfig.Dynamic.PurchasesAreFoundInRaid = _snapPurchasesAreFir;
        ragfairConfig.Sell.Fees = _snapFeesEnabled;
        ragfairConfig.Sell.Chance.Base = _snapSellChanceBase;
        ragfairConfig.Sell.Chance.SellMultiplier = _snapSellChanceMult;
        globals.Configuration.RagFair.RatingIncreaseCount = _snapRepGainPerSale;
        globals.Configuration.RagFair.RatingDecreaseCount = _snapRepLossPerCancel;
        ragfairConfig.TieredFlea.Enabled = _snapTieredFleaEnabled;
        ragfairConfig.Dynamic.RemoveSeasonalItemsWhenNotInEvent = _snapRemoveSeasonalItems;

        // ── E. Restore Dynamic Offer Configuration ──
        ragfairConfig.Dynamic.Barter.ChancePercent = _snapBarterChance;
        ragfairConfig.Dynamic.Pack.ChancePercent = _snapBundleChance;
        ragfairConfig.Dynamic.ExpiredOfferThreshold = _snapExpiredOfferThreshold;
        ragfairConfig.Dynamic.OfferItemCount["default"].Min = _snapOfferItemCountMin;
        ragfairConfig.Dynamic.OfferItemCount["default"].Max = _snapOfferItemCountMax;
        ragfairConfig.Dynamic.PriceRanges.Default.Min = _snapPriceRangeDefaultMin;
        ragfairConfig.Dynamic.PriceRanges.Default.Max = _snapPriceRangeDefaultMax;
        ragfairConfig.Dynamic.PriceRanges.Preset.Min = _snapPriceRangePresetMin;
        ragfairConfig.Dynamic.PriceRanges.Preset.Max = _snapPriceRangePresetMax;
        ragfairConfig.Dynamic.PriceRanges.Pack.Min = _snapPriceRangePackMin;
        ragfairConfig.Dynamic.PriceRanges.Pack.Max = _snapPriceRangePackMax;
        ragfairConfig.Dynamic.EndTimeSeconds.Min = _snapEndTimeSecondsMin;
        ragfairConfig.Dynamic.EndTimeSeconds.Max = _snapEndTimeSecondsMax;
        ragfairConfig.Dynamic.NonStackableCount.Min = _snapNonStackableCountMin;
        ragfairConfig.Dynamic.NonStackableCount.Max = _snapNonStackableCountMax;
        ragfairConfig.Dynamic.StackablePercent.Min = _snapStackablePercentMin;
        ragfairConfig.Dynamic.StackablePercent.Max = _snapStackablePercentMax;

        // Currency ratios
        ragfairConfig.Dynamic.OfferCurrencyChangePercent[RubTpl] = _snapCurrencyRub;
        ragfairConfig.Dynamic.OfferCurrencyChangePercent[UsdTpl] = _snapCurrencyUsd;
        ragfairConfig.Dynamic.OfferCurrencyChangePercent[EurTpl] = _snapCurrencyEur;

        // ── F. Restore Per-Category Conditions ──
        foreach (var (parentId, (min, max)) in _categoryConditionSnapshots)
        {
            var mongoId = new MongoId(parentId);
            if (ragfairConfig.Dynamic.Condition.TryGetValue(mongoId, out var condition))
            {
                condition.Max.Min = min;
                condition.Max.Max = max;
            }
        }

        // ── G. Restore Blacklist Control ──
        ragfairConfig.Dynamic.Blacklist.EnableBsgList = _snapEnableBsgList;
        ragfairConfig.Dynamic.Blacklist.EnableQuestList = _snapEnableQuestList;
        ragfairConfig.Dynamic.Blacklist.EnableCustomItemCategoryList = _snapEnableCustomItemCategoryList;
        ragfairConfig.Dynamic.Blacklist.TraderItems = _snapTraderItems;
    }

    // ═══════════════════════════════════════════════════════
    // APPLY
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Check an expansion config before it is persisted. Returns null when valid,
    /// otherwise the first problem found.
    /// </summary>
    /// <remarks>
    /// Apply() writes to disk before it touches live state, so anything that throws inside
    /// ApplyInternal leaves a bad config persisted and the market half-changed. Every value
    /// here is optional, so only the fields actually supplied are checked.
    /// </remarks>
    public string? ValidateConfig(FleaExpansionConfig? cfg)
    {
        if (cfg == null) return "Config is missing.";

        var error =
            Check(cfg.SellChanceBase, 0, 100, "sellChanceBase")
            ?? Check(cfg.SellChanceMult, 0.0, 100.0, "sellChanceMult")
            ?? Check(cfg.RepGainPerSale, 0.0, 100.0, "repGainPerSale")
            ?? Check(cfg.RepLossPerCancel, 0.0, 100.0, "repLossPerCancel")
            ?? Check(cfg.BarterChance, 0.0, 100.0, "barterChance")
            ?? Check(cfg.BundleChance, 0.0, 100.0, "bundleChance")
            ?? Check(cfg.ExpiredOfferThreshold, 0, 1_000_000, "expiredOfferThreshold")
            ?? Check(cfg.OfferItemCountMin, 1, 10_000, "offerItemCountMin")
            ?? Check(cfg.OfferItemCountMax, 1, 10_000, "offerItemCountMax")
            ?? Check(cfg.PriceRangeDefaultMin, 0.0, 1_000.0, "priceRangeDefaultMin")
            ?? Check(cfg.PriceRangeDefaultMax, 0.0, 1_000.0, "priceRangeDefaultMax")
            ?? Check(cfg.PriceRangePresetMin, 0.0, 1_000.0, "priceRangePresetMin")
            ?? Check(cfg.PriceRangePresetMax, 0.0, 1_000.0, "priceRangePresetMax")
            ?? Check(cfg.PriceRangePackMin, 0.0, 1_000.0, "priceRangePackMin")
            ?? Check(cfg.PriceRangePackMax, 0.0, 1_000.0, "priceRangePackMax")
            ?? Check(cfg.OfferDurationMin, 1, 100_000, "offerDurationMin")
            ?? Check(cfg.OfferDurationMax, 1, 100_000, "offerDurationMax")
            ?? Check(cfg.NonStackableCountMin, 1, 10_000, "nonStackableCountMin")
            ?? Check(cfg.NonStackableCountMax, 1, 10_000, "nonStackableCountMax")
            ?? Check(cfg.StackablePercentMin, 0.0, 10_000.0, "stackablePercentMin")
            ?? Check(cfg.StackablePercentMax, 0.0, 10_000.0, "stackablePercentMax")
            ?? Check(cfg.CurrencyRatioRub, 0.0, 100.0, "currencyRatioRub")
            ?? Check(cfg.CurrencyRatioUsd, 0.0, 100.0, "currencyRatioUsd")
            ?? Check(cfg.CurrencyRatioEur, 0.0, 100.0, "currencyRatioEur")
            ?? Check(cfg.MinUserLevel, 0, 100, "minUserLevel")
            ?? Check(cfg.MaxActiveOfferCount, 1.0, 100_000.0, "maxActiveOfferCount");
        if (error != null) return error;

        error = CheckPair(cfg.OfferItemCountMin, cfg.OfferItemCountMax, "offerItemCount")
                ?? CheckPair(cfg.OfferDurationMin, cfg.OfferDurationMax, "offerDuration")
                ?? CheckPair(cfg.NonStackableCountMin, cfg.NonStackableCountMax, "nonStackableCount")
                ?? CheckPair(cfg.PriceRangeDefaultMin, cfg.PriceRangeDefaultMax, "priceRangeDefault")
                ?? CheckPair(cfg.PriceRangePresetMin, cfg.PriceRangePresetMax, "priceRangePreset")
                ?? CheckPair(cfg.PriceRangePackMin, cfg.PriceRangePackMax, "priceRangePack")
                ?? CheckPair(cfg.StackablePercentMin, cfg.StackablePercentMax, "stackablePercent");
        if (error != null) return error;

        if (cfg.CategoryConditions is { Count: > 0 })
        {
            foreach (var (parentId, entry) in cfg.CategoryConditions)
            {
                // ApplyInternal builds a MongoId from this key, which throws on a malformed value.
                if (string.IsNullOrWhiteSpace(parentId) || !MongoId.IsValidMongoId(parentId))
                    return $"categoryConditions key '{parentId}' is not a valid template ID.";
                if (entry == null)
                    return $"categoryConditions['{parentId}'] has no condition object.";

                error = Check(entry.ConditionMin, 0.0, 100.0, $"categoryConditions['{parentId}'].conditionMin")
                        ?? Check(entry.ConditionMax, 0.0, 100.0, $"categoryConditions['{parentId}'].conditionMax")
                        ?? CheckPair(entry.ConditionMin, entry.ConditionMax, $"categoryConditions['{parentId}'].condition");
                if (error != null) return error;
            }
        }

        return null;
    }

    private static string? Check(double? value, double min, double max, string field) =>
        value.HasValue ? FleaPriceService.CheckRange(value.Value, min, max, field) : null;

    private static string? Check(int? value, int min, int max, string field) =>
        value.HasValue ? FleaPriceService.CheckRange(value.Value, min, max, field) : null;

    private static string? CheckPair(double? min, double? max, string field) =>
        min.HasValue && max.HasValue && min.Value > max.Value
            ? $"{field}Min ({min.Value}) must not exceed {field}Max ({max.Value})."
            : null;

    private static string? CheckPair(int? min, int? max, string field) =>
        min.HasValue && max.HasValue && min.Value > max.Value
            ? $"{field}Min ({min.Value}) must not exceed {field}Max ({max.Value})."
            : null;

    public FleaRouteResult Apply(FleaExpansionConfig config)
    {
        lock (_lock)
        {
            EnsureSnapshot();

            // Persist config
            configService.GetConfig().FleaExpansion = config;
            configService.SaveConfig();

            try
            {
                // Restore ALL from snapshot first, then apply fresh
                RestoreAll();

                if (config.EnableFleaExpansion)
                {
                    ApplyInternal(config);
                    logger.Info("FleaExpansionService: applied expansion settings");
                }
                else
                {
                    logger.Info("FleaExpansionService: disabled — defaults restored");
                }

                return new FleaRouteResult
                {
                    Persisted = true,
                    Applied = true,
                    MarketState = FleaMarketStates.Ok
                };
            }
            catch (Exception ex)
            {
                // Config is already on disk and ApplyInternal writes to globals and the ragfair
                // config as it goes, so the live server may now sit between two configurations.
                // Report that rather than letting the generic 500 handler imply nothing happened.
                logger.Error($"FleaExpansionService: apply failed — {ex.Message}");
                return new FleaRouteResult
                {
                    Persisted = true,
                    Applied = false,
                    MarketState = FleaMarketStates.PartialOrUnknown,
                    Error = ex.Message,
                    Guidance = "Settings were saved but only partly applied. Retry, or restart the "
                             + "server to bring the live market back in line with the saved config."
                };
            }
        }
    }

    private void ApplyInternal(FleaExpansionConfig cfg)
    {
        var globals = databaseService.GetGlobals();
        var ragfairConfig = configServer.GetConfig<RagfairConfig>();

        // ── Flea Globals ──
        if (cfg.MinUserLevel.HasValue)
            globals.Configuration.RagFair.MinUserLevel = cfg.MinUserLevel.Value;

        if (cfg.MaxActiveOfferCount.HasValue && globals.Configuration.RagFair.MaxActiveOfferCount != null)
        {
            foreach (var entry in globals.Configuration.RagFair.MaxActiveOfferCount)
                entry.Count = cfg.MaxActiveOfferCount.Value;
        }

        // ── D. FIR & Sell Controls ──
        if (cfg.AllowNonFirSales)
            globals.Configuration.RagFair.IsOnlyFoundInRaidAllowed = false;

        if (cfg.PurchasesAreFir.HasValue)
            ragfairConfig.Dynamic.PurchasesAreFoundInRaid = cfg.PurchasesAreFir.Value;

        if (cfg.EnableFees.HasValue)
            ragfairConfig.Sell.Fees = cfg.EnableFees.Value;

        if (cfg.SellChanceBase.HasValue)
            ragfairConfig.Sell.Chance.Base = cfg.SellChanceBase.Value;

        if (cfg.SellChanceMult.HasValue)
            ragfairConfig.Sell.Chance.SellMultiplier = cfg.SellChanceMult.Value;

        if (cfg.RepGainPerSale.HasValue)
            globals.Configuration.RagFair.RatingIncreaseCount = cfg.RepGainPerSale.Value;

        if (cfg.RepLossPerCancel.HasValue)
            globals.Configuration.RagFair.RatingDecreaseCount = cfg.RepLossPerCancel.Value;

        if (cfg.TieredFleaEnabled.HasValue)
            ragfairConfig.TieredFlea.Enabled = cfg.TieredFleaEnabled.Value;

        if (cfg.RemoveSeasonalItems.HasValue)
            ragfairConfig.Dynamic.RemoveSeasonalItemsWhenNotInEvent = cfg.RemoveSeasonalItems.Value;

        // ── E. Dynamic Offer Configuration ──
        if (cfg.BarterChance.HasValue)
            ragfairConfig.Dynamic.Barter.ChancePercent = cfg.BarterChance.Value;

        if (cfg.BundleChance.HasValue)
            ragfairConfig.Dynamic.Pack.ChancePercent = cfg.BundleChance.Value;

        if (cfg.ExpiredOfferThreshold.HasValue)
            ragfairConfig.Dynamic.ExpiredOfferThreshold = cfg.ExpiredOfferThreshold.Value;

        if (cfg.OfferItemCountMin.HasValue)
            ragfairConfig.Dynamic.OfferItemCount["default"].Min = cfg.OfferItemCountMin.Value;
        if (cfg.OfferItemCountMax.HasValue)
            ragfairConfig.Dynamic.OfferItemCount["default"].Max = cfg.OfferItemCountMax.Value;

        if (cfg.PriceRangeDefaultMin.HasValue)
            ragfairConfig.Dynamic.PriceRanges.Default.Min = cfg.PriceRangeDefaultMin.Value;
        if (cfg.PriceRangeDefaultMax.HasValue)
            ragfairConfig.Dynamic.PriceRanges.Default.Max = cfg.PriceRangeDefaultMax.Value;

        if (cfg.PriceRangePresetMin.HasValue)
            ragfairConfig.Dynamic.PriceRanges.Preset.Min = cfg.PriceRangePresetMin.Value;
        if (cfg.PriceRangePresetMax.HasValue)
            ragfairConfig.Dynamic.PriceRanges.Preset.Max = cfg.PriceRangePresetMax.Value;

        if (cfg.PriceRangePackMin.HasValue)
            ragfairConfig.Dynamic.PriceRanges.Pack.Min = cfg.PriceRangePackMin.Value;
        if (cfg.PriceRangePackMax.HasValue)
            ragfairConfig.Dynamic.PriceRanges.Pack.Max = cfg.PriceRangePackMax.Value;

        if (cfg.OfferDurationMin.HasValue)
            ragfairConfig.Dynamic.EndTimeSeconds.Min = cfg.OfferDurationMin.Value;
        if (cfg.OfferDurationMax.HasValue)
            ragfairConfig.Dynamic.EndTimeSeconds.Max = cfg.OfferDurationMax.Value;

        if (cfg.NonStackableCountMin.HasValue)
            ragfairConfig.Dynamic.NonStackableCount.Min = cfg.NonStackableCountMin.Value;
        if (cfg.NonStackableCountMax.HasValue)
            ragfairConfig.Dynamic.NonStackableCount.Max = cfg.NonStackableCountMax.Value;

        if (cfg.StackablePercentMin.HasValue)
            ragfairConfig.Dynamic.StackablePercent.Min = cfg.StackablePercentMin.Value;
        if (cfg.StackablePercentMax.HasValue)
            ragfairConfig.Dynamic.StackablePercent.Max = cfg.StackablePercentMax.Value;

        if (cfg.CurrencyRatioRub.HasValue)
            ragfairConfig.Dynamic.OfferCurrencyChangePercent[RubTpl] = cfg.CurrencyRatioRub.Value;
        if (cfg.CurrencyRatioUsd.HasValue)
            ragfairConfig.Dynamic.OfferCurrencyChangePercent[UsdTpl] = cfg.CurrencyRatioUsd.Value;
        if (cfg.CurrencyRatioEur.HasValue)
            ragfairConfig.Dynamic.OfferCurrencyChangePercent[EurTpl] = cfg.CurrencyRatioEur.Value;

        // ── F. Per-Category Conditions ──
        if (cfg.CategoryConditions is { Count: > 0 })
        {
            foreach (var (parentId, entry) in cfg.CategoryConditions)
            {
                var mongoId = new MongoId(parentId);
                if (!ragfairConfig.Dynamic.Condition.TryGetValue(mongoId, out var condition))
                    continue;

                if (entry.ConditionMin.HasValue)
                    condition.Max.Min = entry.ConditionMin.Value;
                if (entry.ConditionMax.HasValue)
                    condition.Max.Max = entry.ConditionMax.Value;
            }
        }

        // ── G. Blacklist Control ──
        if (cfg.DisableFleaBlacklist == true)
        {
            ragfairConfig.Dynamic.Blacklist.EnableBsgList = false;
            ragfairConfig.Dynamic.Blacklist.EnableQuestList = false;
            ragfairConfig.Dynamic.Blacklist.EnableCustomItemCategoryList = false;
            ragfairConfig.Dynamic.Blacklist.TraderItems = false;
        }
    }

    // ═══════════════════════════════════════════════════════
    // RESET
    // ═══════════════════════════════════════════════════════

    public FleaRouteResult Reset()
    {
        lock (_lock)
        {
            EnsureSnapshot();

            configService.GetConfig().FleaExpansion = new FleaExpansionConfig();
            configService.SaveConfig();

            try
            {
                RestoreAll();
                logger.Info("FleaExpansionService: reset — all values restored to defaults");
                return new FleaRouteResult
                {
                    Persisted = true,
                    Applied = true,
                    MarketState = FleaMarketStates.Ok
                };
            }
            catch (Exception ex)
            {
                logger.Error($"FleaExpansionService: reset failed — {ex.Message}");
                return new FleaRouteResult
                {
                    Persisted = true,
                    Applied = false,
                    MarketState = FleaMarketStates.PartialOrUnknown,
                    Error = ex.Message,
                    Guidance = "Defaults were saved but only partly restored. Retry, or restart the server."
                };
            }
        }
    }

    // ═══════════════════════════════════════════════════════
    // GET CONFIG + DEFAULTS
    // ═══════════════════════════════════════════════════════

    public FleaExpansionConfigResponse GetConfig()
    {
        lock (_lock)
        {
            EnsureSnapshot();

            var ragfairConfig = configServer.GetConfig<RagfairConfig>();

            // Build category defaults from snapshots
            var categoryDefaults = new List<CategoryConditionDefaults>();
            foreach (var (parentId, name) in CategoryNames)
            {
                if (_categoryConditionSnapshots.TryGetValue(parentId, out var snap))
                {
                    categoryDefaults.Add(new CategoryConditionDefaults
                    {
                        ParentId = parentId,
                        Name = name,
                        ConditionMin = snap.Min,
                        ConditionMax = snap.Max
                    });
                }
                else
                {
                    // Category not in ragfair config — include with 0/1 defaults
                    categoryDefaults.Add(new CategoryConditionDefaults
                    {
                        ParentId = parentId,
                        Name = name,
                        ConditionMin = 0,
                        ConditionMax = 1
                    });
                }
            }

            return new FleaExpansionConfigResponse
            {
                Config = configService.GetConfig().FleaExpansion,
                Defaults = new FleaExpansionDefaults
                {
                    // Flea Globals
                    MinUserLevel = _snapMinUserLevel,
                    MaxActiveOfferCount = _snapMaxActiveOfferCounts.Count > 0 ? _snapMaxActiveOfferCounts[0] : 0,

                    // D
                    IsOnlyFirAllowed = _snapIsOnlyFirAllowed,
                    PurchasesAreFir = _snapPurchasesAreFir,
                    FeesEnabled = _snapFeesEnabled,
                    SellChanceBase = _snapSellChanceBase,
                    SellChanceMult = _snapSellChanceMult,
                    RepGainPerSale = _snapRepGainPerSale,
                    RepLossPerCancel = _snapRepLossPerCancel,
                    TieredFleaEnabled = _snapTieredFleaEnabled,
                    RemoveSeasonalItems = _snapRemoveSeasonalItems,

                    // E
                    BarterChance = _snapBarterChance,
                    BundleChance = _snapBundleChance,
                    ExpiredOfferThreshold = _snapExpiredOfferThreshold,
                    OfferItemCountMin = _snapOfferItemCountMin,
                    OfferItemCountMax = _snapOfferItemCountMax,
                    PriceRangeDefaultMin = _snapPriceRangeDefaultMin,
                    PriceRangeDefaultMax = _snapPriceRangeDefaultMax,
                    PriceRangePresetMin = _snapPriceRangePresetMin,
                    PriceRangePresetMax = _snapPriceRangePresetMax,
                    PriceRangePackMin = _snapPriceRangePackMin,
                    PriceRangePackMax = _snapPriceRangePackMax,
                    OfferDurationMin = _snapEndTimeSecondsMin,
                    OfferDurationMax = _snapEndTimeSecondsMax,
                    NonStackableCountMin = _snapNonStackableCountMin,
                    NonStackableCountMax = _snapNonStackableCountMax,
                    StackablePercentMin = _snapStackablePercentMin,
                    StackablePercentMax = _snapStackablePercentMax,
                    CurrencyRatioRub = _snapCurrencyRub,
                    CurrencyRatioUsd = _snapCurrencyUsd,
                    CurrencyRatioEur = _snapCurrencyEur,

                    // F
                    Categories = categoryDefaults,

                    // G
                    EnableBsgList = _snapEnableBsgList,
                    EnableQuestList = _snapEnableQuestList,
                    EnableCustomItemCategoryList = _snapEnableCustomItemCategoryList,
                    TraderItems = _snapTraderItems
                }
            };
        }
    }
}

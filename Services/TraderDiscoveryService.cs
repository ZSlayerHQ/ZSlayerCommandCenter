using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using ZSlayerCommandCenter.Models;

namespace ZSlayerCommandCenter.Services;

[Injectable(InjectionType.Singleton)]
public class TraderDiscoveryService(
    DatabaseService databaseService,
    LocaleService localeService,
    ConfigServer configServer,
    ImageRouter imageRouter,
    ConfigService configService,
    ISptLogger<TraderDiscoveryService> logger)
{
    // Vanilla trader IDs
    private static readonly HashSet<string> VanillaTraderIds =
    [
        "54cb50c76803fa8b248b4571",  // Prapor
        "54cb57776803fa99248b456e",  // Therapist
        "579dc571d53a0658a154fbec",  // Fence
        "5935c25fb3acc3127c3d8cd9",  // Jaeger
        "58330581ace78e27b8b10cee",  // Skier
        "5a7c2eca46aef81a7ca2145d",  // Mechanic
        "5ac3b934156ae10c4430e83c",  // Ragman
        "5c0647fdd443bc2504c2d371",  // Peacekeeper
        "638f541a29ffd1183d187f16",  // Lightkeeper
        "6617beeaa9cfa777ca915b7c",  // Ref
    ];

    // Currency template IDs
    public const string RoublesTpl = "5449016a4bdc2d6f028b456f";
    public const string DollarsTpl = "5696686a4bdc2da3298b456a";
    public const string EurosTpl = "569668774bdc2da2298b4568";

    private readonly Dictionary<string, TraderSnapshot> _snapshots = new();
    private readonly Dictionary<string, (string Nickname, string? AvatarUrl, string? Description)> _originalDisplayInfo = new();
    private readonly Dictionary<string, (string? Nickname, string? Description)> _originalLocaleValues = new();
    private List<TraderSummary>? _discoveredTraders;
    private bool _initialized;

    /// <summary>
    /// Discover all traders and snapshot their original data.
    /// Called once during startup after all mods have loaded.
    /// </summary>
    public void Initialize()
    {
        if (_initialized) return;

        var traders = databaseService.GetTables().Traders;
        if (traders == null || traders.Count == 0)
        {
            logger.Warning("ZSlayerCC Traders: No traders found in database");
            _initialized = true;
            return;
        }

        var discovered = new List<TraderSummary>();
        var traderConfig = configServer.GetConfig<TraderConfig>();

        foreach (var (traderId, trader) in traders)
        {
            if (trader.Base == null || trader.Assort == null) continue;

            var id = traderId.ToString();
            var isModded = !VanillaTraderIds.Contains(id);
            var rootItemCount = CountRootItems(trader.Assort);
            var currency = trader.Base.Currency?.ToString() ?? "RUB";

            // Find restock timing from TraderConfig
            int? restockMin = null, restockMax = null;
            var updateEntry = traderConfig.UpdateTime.FirstOrDefault(u => u.TraderId.ToString() == id);
            if (updateEntry != null)
            {
                restockMin = updateEntry.Seconds.Min;
                restockMax = updateEntry.Seconds.Max;
            }

            // Resolve name: Base fields first, then locale DB fallback (modded traders often use locales)
            var nickname = ResolveTraderName(id, trader.Base.Nickname, "Nickname");
            var fullName = ResolveTraderName(id, trader.Base.Name, "FullName") ?? nickname;
            var description = ResolveLocaleField(id, "Description");
            var location = ResolveLocaleField(id, "Location");

            // Store original display info for restore
            _originalDisplayInfo[id] = (nickname, trader.Base.Avatar, description);

            discovered.Add(new TraderSummary
            {
                Id = id,
                Nickname = nickname,
                FullName = fullName,
                Currency = currency,
                LoyaltyLevelCount = trader.Base.LoyaltyLevels?.Count ?? 0,
                ItemCount = rootItemCount,
                IsModded = isModded,
                RestockMinSeconds = restockMin,
                RestockMaxSeconds = restockMax,
                AvatarUrl = trader.Base.Avatar,
                OriginalNickname = nickname,
                OriginalAvatarUrl = trader.Base.Avatar,
                Location = location,
                Description = description,
            });

            // Snapshot original data
            SnapshotTrader(id, trader);
        }

        _discoveredTraders = discovered;
        _initialized = true;

        // Snapshot original locale values before any modifications
        SnapshotLocaleValues(discovered);

        // Register locale transformers for all languages (handles lazy-loaded locales)
        RegisterDisplayTransformers();

        // Apply in-game overrides for already-loaded locale + avatar routes
        var ccConfig = configService.GetConfig().Traders;
        if (ccConfig.TraderDisplayOverrides.Count > 0)
        {
            ApplyInGameDisplayOverrides(ccConfig);
            logger.Info($"ZSlayerCC Traders: Applied {ccConfig.TraderDisplayOverrides.Count} display override(s) to locale/image routes");
        }

        var vanillaCount = discovered.Count(t => !t.IsModded);
        var moddedCount = discovered.Count(t => t.IsModded);
        var totalItems = discovered.Sum(t => t.ItemCount);
        logger.Success($"ZSlayerCC Traders: Discovered {discovered.Count} traders ({vanillaCount} vanilla, {moddedCount} modded), {totalItems} total items");
    }

    /// <summary>Get all discovered traders with current config state applied.</summary>
    public List<TraderSummary> GetDiscoveredTraders(TraderControlConfig config)
    {
        if (_discoveredTraders == null) return [];

        // Re-read current restock timers from TraderConfig (may have been modified by ApplyRestockTimers)
        var traderConfig = configServer.GetConfig<TraderConfig>();

        foreach (var info in _discoveredTraders)
        {
            // Restore original display info first (prevents mutation drift)
            if (_originalDisplayInfo.TryGetValue(info.Id, out var orig))
            {
                info.Nickname = orig.Nickname;
                info.AvatarUrl = orig.AvatarUrl;
                info.Description = orig.Description;
                info.OriginalNickname = orig.Nickname;
                info.OriginalAvatarUrl = orig.AvatarUrl;
                info.OriginalDescription = orig.Description;
            }

            // Apply display overrides from config
            info.HasDisplayOverride = false;
            if (config.TraderDisplayOverrides.TryGetValue(info.Id, out var displayOv))
            {
                if (!string.IsNullOrWhiteSpace(displayOv.DisplayName))
                {
                    info.Nickname = displayOv.DisplayName;
                    info.HasDisplayOverride = true;
                }
                if (!string.IsNullOrWhiteSpace(displayOv.CustomDescription))
                {
                    info.Description = displayOv.CustomDescription;
                    info.HasDisplayOverride = true;
                }
                if (!string.IsNullOrWhiteSpace(displayOv.CustomAvatar))
                {
                    info.AvatarUrl = "/zslayer/cc/Trader%20Icons/" + Uri.EscapeDataString(displayOv.CustomAvatar);
                    info.HasDisplayOverride = true;
                }
            }

            var hasOverride = config.TraderOverrides.ContainsKey(info.Id);
            info.HasOverride = hasOverride;

            var globalBuy = config.GlobalBuyMultiplier;
            var globalSell = config.GlobalSellMultiplier;
            var globalStock = config.GlobalStockMultiplier;

            if (hasOverride && config.TraderOverrides.TryGetValue(info.Id, out var ov) && ov.Enabled)
            {
                info.CurrentBuyMultiplier = ov.BuyMultiplier;
                info.CurrentSellMultiplier = ov.SellMultiplier;
                info.CurrentStockMultiplier = ov.StockMultiplier;
            }
            else
            {
                info.CurrentBuyMultiplier = globalBuy;
                info.CurrentSellMultiplier = globalSell;
                info.CurrentStockMultiplier = globalStock;
            }

            // Refresh restock timers from live TraderConfig
            var updateEntry = traderConfig.UpdateTime.FirstOrDefault(u => u.TraderId.ToString() == info.Id);
            if (updateEntry != null)
            {
                info.RestockMinSeconds = updateEntry.Seconds.Min;
                info.RestockMaxSeconds = updateEntry.Seconds.Max;
            }
        }

        return _discoveredTraders;
    }

    /// <summary>Get the snapshot for a specific trader (for restore).</summary>
    public TraderSnapshot? GetSnapshot(string traderId) =>
        _snapshots.GetValueOrDefault(traderId);

    /// <summary>Check if a trader ID is vanilla.</summary>
    public static bool IsVanilla(string traderId) => VanillaTraderIds.Contains(traderId);

    /// <summary>Get all trader snapshots.</summary>
    public Dictionary<string, TraderSnapshot> GetAllSnapshots() => _snapshots;

    /// <summary>Map a currency string to its template ID.</summary>
    public static string CurrencyToTemplateId(string currency) => currency.ToUpperInvariant() switch
    {
        "RUB" => RoublesTpl,
        "USD" => DollarsTpl,
        "EUR" => EurosTpl,
        _ => RoublesTpl
    };

    /// <summary>Map a template ID to its currency string.</summary>
    public static string TemplateIdToCurrency(string templateId) => templateId switch
    {
        RoublesTpl => "RUB",
        DollarsTpl => "USD",
        EurosTpl => "EUR",
        _ => "RUB"
    };

    /// <summary>Check if a template ID is a currency.</summary>
    public static bool IsCurrencyTemplate(string templateId) =>
        templateId is RoublesTpl or DollarsTpl or EurosTpl;

    /// <summary>Get exchange rate from source currency to roubles.</summary>
    public static double GetExchangeRateToRoubles(string currencyTemplateId) => currencyTemplateId switch
    {
        DollarsTpl => 130.0,
        EurosTpl => 150.0,
        RoublesTpl => 1.0,
        _ => 1.0
    };

    // ── In-game display override application ──

    /// <summary>
    /// Apply display overrides to SPT locale DB and image routes so changes appear in-game.
    /// Called at startup and after any display override API change.
    /// </summary>
    public void ApplyInGameDisplayOverrides(TraderControlConfig config)
    {
        // Modify the default locale directly (handles already-loaded locale)
        try
        {
            var locale = localeService.GetLocaleDb();
            ApplyOverridesToLocale(locale, config);
        }
        catch (Exception ex)
        {
            logger.Warning($"ZSlayerCC Traders: Failed to apply display overrides to locale: {ex.Message}");
        }

        // Apply avatar routes via ImageRouter
        ApplyAvatarRoutes(config);
    }

    private void SnapshotLocaleValues(List<TraderSummary> traders)
    {
        try
        {
            var locale = localeService.GetLocaleDb();
            foreach (var t in traders)
            {
                locale.TryGetValue($"{t.Id} Nickname", out var nick);
                locale.TryGetValue($"{t.Id} Description", out var desc);
                _originalLocaleValues[t.Id] = (nick, desc);
            }
        }
        catch (Exception ex)
        {
            logger.Warning($"ZSlayerCC Traders: Failed to snapshot locale values: {ex.Message}");
        }
    }

    private void RegisterDisplayTransformers()
    {
        try
        {
            // Register transformer on all locale databases — reads latest config when locale loads
            foreach (var lazyLoc in databaseService.GetLocales().Global.Values)
            {
                lazyLoc.AddTransformer(loc =>
                {
                    var overrides = configService.GetConfig().Traders.TraderDisplayOverrides;
                    foreach (var (traderId, displayOv) in overrides)
                    {
                        if (!string.IsNullOrWhiteSpace(displayOv.DisplayName))
                            loc[$"{traderId} Nickname"] = displayOv.DisplayName;
                        if (!string.IsNullOrWhiteSpace(displayOv.CustomDescription))
                            loc[$"{traderId} Description"] = displayOv.CustomDescription;
                    }
                    return loc;
                });
            }
        }
        catch (Exception ex)
        {
            logger.Warning($"ZSlayerCC Traders: Failed to register locale transformers: {ex.Message}");
        }
    }

    private void ApplyOverridesToLocale(Dictionary<string, string> locale, TraderControlConfig config)
    {
        // Restore originals first
        foreach (var (traderId, orig) in _originalLocaleValues)
        {
            if (orig.Nickname != null) locale[$"{traderId} Nickname"] = orig.Nickname;
            if (orig.Description != null) locale[$"{traderId} Description"] = orig.Description;
        }

        // Apply current overrides
        foreach (var (traderId, displayOv) in config.TraderDisplayOverrides)
        {
            if (!string.IsNullOrWhiteSpace(displayOv.DisplayName))
                locale[$"{traderId} Nickname"] = displayOv.DisplayName;
            if (!string.IsNullOrWhiteSpace(displayOv.CustomDescription))
                locale[$"{traderId} Description"] = displayOv.CustomDescription;
        }
    }

    private void ApplyAvatarRoutes(TraderControlConfig config)
    {
        foreach (var (traderId, displayOv) in config.TraderDisplayOverrides)
        {
            if (string.IsNullOrWhiteSpace(displayOv.CustomAvatar)) continue;
            if (!_originalDisplayInfo.TryGetValue(traderId, out var origInfo) || origInfo.AvatarUrl == null) continue;

            var avatar = origInfo.AvatarUrl;
            var extIdx = avatar.LastIndexOf('.');
            if (extIdx <= 0) continue;

            var route = avatar[..extIdx];
            var filePath = System.IO.Path.Combine(configService.ModPath, "res", "Trader Icons", displayOv.CustomAvatar);
            if (File.Exists(filePath))
                imageRouter.AddRoute(route, filePath);
        }
    }

    // ── Private helpers ──

    /// <summary>Resolve a trader name field, falling back to locale DB if Base field is empty.</summary>
    private string ResolveTraderName(string traderId, string? baseValue, string localeKey)
    {
        if (!string.IsNullOrWhiteSpace(baseValue)) return baseValue;

        // Modded traders often set names via locale DB: "{traderId} Nickname", "{traderId} FullName"
        var localeDb = localeService.GetLocaleDb();
        if (localeDb.TryGetValue($"{traderId} {localeKey}", out var localeName) &&
            !string.IsNullOrWhiteSpace(localeName))
            return localeName;

        return traderId;
    }

    /// <summary>Look up a field from the locale DB, return null if not found.</summary>
    private string? ResolveLocaleField(string traderId, string localeKey)
    {
        var localeDb = localeService.GetLocaleDb();
        if (localeDb.TryGetValue($"{traderId} {localeKey}", out var value) &&
            !string.IsNullOrWhiteSpace(value))
            return value;
        return null;
    }

    private void SnapshotTrader(string traderId, Trader trader)
    {
        var snapshot = new TraderSnapshot
        {
            OriginalCurrency = trader.Base.Currency?.ToString() ?? "RUB"
        };

        // Snapshot stock counts for root items
        if (trader.Assort.Items != null)
        {
            foreach (var item in trader.Assort.Items)
            {
                if (item.ParentId == "hideout" && item.Upd?.StackObjectsCount != null)
                    snapshot.StockCounts[item.Id.ToString()] = item.Upd.StackObjectsCount.Value;
            }
        }

        // Snapshot barter costs in-place (template + count for each cost entry)
        if (trader.Assort.BarterScheme != null)
        {
            foreach (var (itemId, paymentOptions) in trader.Assort.BarterScheme)
            {
                var optionSnapshots = new List<List<BarterCostSnapshot>>();
                foreach (var option in paymentOptions)
                {
                    var costSnapshots = new List<BarterCostSnapshot>();
                    foreach (var cost in option)
                        costSnapshots.Add(new BarterCostSnapshot(cost.Template.ToString(), cost.Count));
                    optionSnapshots.Add(costSnapshots);
                }
                snapshot.BarterCosts[itemId.ToString()] = optionSnapshots;
            }
        }

        // Snapshot loyalty level items
        if (trader.Assort.LoyalLevelItems != null)
        {
            foreach (var (itemId, level) in trader.Assort.LoyalLevelItems)
                snapshot.LoyaltyLevels[itemId.ToString()] = level;
        }

        // Snapshot buy_price_coef for each loyalty level
        if (trader.Base.LoyaltyLevels != null)
        {
            foreach (var ll in trader.Base.LoyaltyLevels)
                snapshot.BuyPriceCoefs.Add(ll.BuyPriceCoefficient ?? 0);
        }

        _snapshots[traderId] = snapshot;
    }

    private static int CountRootItems(TraderAssort assort)
    {
        if (assort.Items == null) return 0;
        return assort.Items.Count(item => item.ParentId == "hideout");
    }
}

using System.Text.Json;
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
public class TraderDiscoveryService(
    DatabaseService databaseService,
    LocaleService localeService,
    ConfigServer configServer,
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

    private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly Dictionary<string, TraderSnapshot> _snapshots = new();
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
                Location = location,
                Description = description,
            });

            // Snapshot original data
            SnapshotTrader(id, trader);
        }

        _discoveredTraders = discovered;
        _initialized = true;

        var vanillaCount = discovered.Count(t => !t.IsModded);
        var moddedCount = discovered.Count(t => t.IsModded);
        var totalItems = discovered.Sum(t => t.ItemCount);
        logger.Success($"ZSlayerCC Traders: Discovered {discovered.Count} traders ({vanillaCount} vanilla, {moddedCount} modded), {totalItems} total items");
    }

    /// <summary>Get all discovered traders with current config state applied.</summary>
    public List<TraderSummary> GetDiscoveredTraders(TraderControlConfig config)
    {
        if (_discoveredTraders == null) return [];

        foreach (var info in _discoveredTraders)
        {
            var hasOverride = config.TraderOverrides.ContainsKey(info.Id);
            info.HasOverride = hasOverride;

            var globalBuy = config.GlobalBuyMultiplier;
            var globalSell = config.GlobalSellMultiplier;
            var globalStock = config.GlobalStockMultiplier;

            if (hasOverride && config.TraderOverrides.TryGetValue(info.Id, out var ov) && ov.Enabled)
            {
                info.CurrentBuyMultiplier = globalBuy * ov.BuyMultiplier;
                info.CurrentSellMultiplier = globalSell * ov.SellMultiplier;
                info.CurrentStockMultiplier = globalStock * ov.StockMultiplier;
            }
            else
            {
                info.CurrentBuyMultiplier = globalBuy;
                info.CurrentSellMultiplier = globalSell;
                info.CurrentStockMultiplier = globalStock;
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

    /// <summary>
    /// Restore a single trader's assort data from snapshot.
    /// Returns the deserialized items, barter scheme, and loyal level items.
    /// </summary>
    public (List<Item> items, Dictionary<MongoId, List<List<BarterScheme>>> barterScheme, Dictionary<MongoId, int> loyalLevelItems)?
        RestoreFromSnapshot(string traderId)
    {
        if (!_snapshots.TryGetValue(traderId, out var snapshot)) return null;

        var items = JsonSerializer.Deserialize<List<Item>>(snapshot.ItemsJson, SnapshotJsonOptions);

        // Deserialize with string keys, then convert back to MongoId keys
        var strBarter = JsonSerializer.Deserialize<Dictionary<string, List<List<BarterScheme>>>>(
            snapshot.BarterSchemeJson, SnapshotJsonOptions);
        var barterScheme = strBarter?.ToDictionary(
            kvp => (MongoId)kvp.Key,
            kvp => kvp.Value);

        var strLl = JsonSerializer.Deserialize<Dictionary<string, int>>(
            snapshot.LoyalLevelItemsJson, SnapshotJsonOptions);
        var loyalLevelItems = strLl?.ToDictionary(
            kvp => (MongoId)kvp.Key,
            kvp => kvp.Value);

        if (items == null || barterScheme == null || loyalLevelItems == null) return null;
        return (items, barterScheme, loyalLevelItems);
    }

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

        // Deep-copy assort data via JSON round-trip
        // MongoId can't be used as dictionary key with System.Text.Json — convert to string keys
        if (trader.Assort.Items != null)
            snapshot.ItemsJson = JsonSerializer.Serialize(trader.Assort.Items, SnapshotJsonOptions);

        if (trader.Assort.BarterScheme != null)
        {
            var strKeyBarter = trader.Assort.BarterScheme.ToDictionary(
                kvp => kvp.Key.ToString(),
                kvp => kvp.Value);
            snapshot.BarterSchemeJson = JsonSerializer.Serialize(strKeyBarter, SnapshotJsonOptions);
        }

        if (trader.Assort.LoyalLevelItems != null)
        {
            var strKeyLl = trader.Assort.LoyalLevelItems.ToDictionary(
                kvp => kvp.Key.ToString(),
                kvp => kvp.Value);
            snapshot.LoyalLevelItemsJson = JsonSerializer.Serialize(strKeyLl, SnapshotJsonOptions);
        }

        // Snapshot stock counts for root items
        if (trader.Assort.Items != null)
        {
            foreach (var item in trader.Assort.Items)
            {
                if (item.ParentId == "hideout" && item.Upd?.StackObjectsCount != null)
                    snapshot.StockCounts[item.Id.ToString()] = item.Upd.StackObjectsCount.Value;
            }
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

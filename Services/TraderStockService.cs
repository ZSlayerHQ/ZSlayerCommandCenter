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
public class TraderStockService(
    DatabaseService databaseService,
    ConfigServer configServer,
    TraderDiscoveryService discoveryService,
    ISptLogger<TraderStockService> logger)
{
    /// <summary>Track injected item IDs per trader so we can remove them before restore.</summary>
    private readonly Dictionary<string, HashSet<string>> _injectedItemIds = new();

    /// <summary>
    /// Remove all previously injected items from trader assorts.
    /// Must be called BEFORE RestoreAllTraders() to avoid leftover items.
    /// </summary>
    public void RemoveInjectedItems()
    {
        if (_injectedItemIds.Count == 0) return;

        var traders = databaseService.GetTables().Traders;
        if (traders == null) return;

        foreach (var (traderId, itemIds) in _injectedItemIds)
        {
            if (!traders.TryGetValue(traderId, out var trader)) continue;
            if (trader.Assort == null) continue;

            // Remove from Items list
            trader.Assort.Items?.RemoveAll(item => itemIds.Contains(item.Id.ToString()));

            // Remove from BarterScheme and LoyalLevelItems
            foreach (var itemId in itemIds)
            {
                MongoId mongoId = itemId;
                trader.Assort.BarterScheme?.Remove(mongoId);
                trader.Assort.LoyalLevelItems?.Remove(mongoId);
            }
        }

        _injectedItemIds.Clear();
    }

    /// <summary>
    /// Inject added items into trader assorts (Step 9).
    /// Returns total number of items added.
    /// </summary>
    public int ApplyAddedItems(TraderControlConfig config)
    {
        var traders = databaseService.GetTables().Traders;
        if (traders == null) return 0;

        var totalAdded = 0;

        foreach (var (traderId, ov) in config.TraderOverrides)
        {
            if (!ov.Enabled || ov.AddedItems.Count == 0) continue;
            if (!traders.TryGetValue(traderId, out var trader)) continue;
            if (trader.Assort?.Items == null) continue;

            if (!_injectedItemIds.TryGetValue(traderId, out var trackingSet))
            {
                trackingSet = new HashSet<string>();
                _injectedItemIds[traderId] = trackingSet;
            }

            foreach (var addedItem in ov.AddedItems)
            {
                var itemId = new MongoId();
                var itemIdStr = itemId.ToString();

                // Create the assort item
                var item = new Item
                {
                    Id = itemId,
                    Template = addedItem.TemplateId,
                    ParentId = "hideout",
                    SlotId = "hideout",
                    Upd = new Upd
                    {
                        StackObjectsCount = addedItem.Stock,
                        UnlimitedCount = addedItem.UnlimitedStock ? true : null,
                        BuyRestrictionMax = addedItem.UnlimitedStock ? null : addedItem.Stock,
                        BuyRestrictionCurrent = 0
                    }
                };

                trader.Assort.Items.Add(item);

                // Add barter scheme (exact price, not affected by multipliers)
                var currencyTpl = TraderDiscoveryService.CurrencyToTemplateId(addedItem.Currency);
                trader.Assort.BarterScheme ??= new Dictionary<MongoId, List<List<BarterScheme>>>();
                trader.Assort.BarterScheme[itemId] = new List<List<BarterScheme>>
                {
                    new() { new BarterScheme { Count = addedItem.Price, Template = currencyTpl } }
                };

                // Add loyalty level
                trader.Assort.LoyalLevelItems ??= new Dictionary<MongoId, int>();
                trader.Assort.LoyalLevelItems[itemId] = Math.Clamp(addedItem.LoyaltyLevel, 1, 4);

                trackingSet.Add(itemIdStr);
                totalAdded++;
            }
        }

        return totalAdded;
    }

    /// <summary>Get the set of injected item IDs for a trader (used by item browser).</summary>
    public HashSet<string>? GetInjectedItemIds(string traderId)
    {
        return _injectedItemIds.TryGetValue(traderId, out var ids) ? ids : null;
    }

    /// <summary>
    /// Apply stock cap + stock multipliers to all traders' root item StackObjectsCount.
    /// Stock cap is applied first (clamps original stock down to cap), then multiplier is applied.
    /// Assumes items have been restored from snapshot.
    /// </summary>
    public void ApplyStockMultipliers(TraderControlConfig config)
    {
        var traders = databaseService.GetTables().Traders;
        if (traders == null) return;

        var stockCap = config.GlobalStockCap;

        foreach (var (traderId, trader) in traders)
        {
            if (trader.Assort?.Items == null) continue;
            var id = traderId.ToString();
            var snapshot = discoveryService.GetSnapshot(id);
            if (snapshot == null) continue;

            var effectiveMult = config.GlobalStockMultiplier;
            if (config.TraderOverrides.TryGetValue(id, out var ov) && ov.Enabled)
                effectiveMult = ov.StockMultiplier; // Override replaces global

            var hasCap = stockCap is > 0;
            var hasMultiplier = Math.Abs(effectiveMult - 1.0) >= 0.001;
            if (!hasCap && !hasMultiplier) continue;

            foreach (var item in trader.Assort.Items)
            {
                if (item.ParentId != "hideout") continue;
                var itemIdStr = item.Id.ToString();

                if (snapshot.StockCounts.TryGetValue(itemIdStr, out var originalStock) && item.Upd != null)
                {
                    // Cap first, then multiply
                    var stock = originalStock;
                    if (hasCap && stock > stockCap!.Value)
                        stock = stockCap.Value;
                    item.Upd.StackObjectsCount = Math.Max(1.0, Math.Round(stock * effectiveMult));
                }
            }
        }
    }

    /// <summary>
    /// Apply restock timer overrides to TraderConfig.UpdateTime.
    /// Per-trader override > global override > leave at default.
    /// </summary>
    public void ApplyRestockTimers(TraderControlConfig config, Dictionary<string, (int min, int max)> originalRestockTimers)
    {
        var traderConfig = configServer.GetConfig<TraderConfig>();

        foreach (var updateEntry in traderConfig.UpdateTime)
        {
            var id = updateEntry.TraderId.ToString();

            // Determine restock timing: per-trader > global > original
            int? minSec = null, maxSec = null;

            if (config.TraderOverrides.TryGetValue(id, out var ov) && ov.Enabled)
            {
                minSec = ov.RestockMinSeconds;
                maxSec = ov.RestockMaxSeconds;
            }

            minSec ??= config.GlobalRestockMinSeconds;
            maxSec ??= config.GlobalRestockMaxSeconds;

            if (minSec != null)
                updateEntry.Seconds = new MinMax<int> { Min = minSec.Value, Max = maxSec ?? minSec.Value };
            else if (maxSec != null)
            {
                var origMin = originalRestockTimers.TryGetValue(id, out var origFallback) ? origFallback.min : 3600;
                updateEntry.Seconds = new MinMax<int> { Min = origMin, Max = maxSec.Value };
            }
            else if (originalRestockTimers.TryGetValue(id, out var orig))
            {
                // Restore original
                updateEntry.Seconds = new MinMax<int> { Min = orig.min, Max = orig.max };
            }
        }
    }

    /// <summary>
    /// Apply loyalty level shifts to all traders' LoyalLevelItems.
    /// Assumes LoyalLevelItems have been restored from snapshot.
    /// </summary>
    public void ApplyLoyaltyLevelShifts(TraderControlConfig config)
    {
        var traders = databaseService.GetTables().Traders;
        if (traders == null) return;

        foreach (var (traderId, trader) in traders)
        {
            if (trader.Assort?.LoyalLevelItems == null) continue;
            var id = traderId.ToString();

            var totalShift = config.GlobalLoyaltyLevelShift;
            if (config.TraderOverrides.TryGetValue(id, out var ov) && ov.Enabled)
                totalShift = ov.LoyaltyLevelShift; // Override replaces global
            if (totalShift == 0) continue;

            var keys = trader.Assort.LoyalLevelItems.Keys.ToList();
            foreach (var itemId in keys)
            {
                var originalLevel = trader.Assort.LoyalLevelItems[itemId];
                var newLevel = Math.Clamp(originalLevel + totalShift, 1, 4);
                trader.Assort.LoyalLevelItems[itemId] = newLevel;
            }
        }
    }

    /// <summary>
    /// Remove disabled items from trader assorts.
    /// Uses BFS to find and remove root items + all descendants (attachments).
    /// </summary>
    public int ApplyDisabledItems(TraderControlConfig config)
    {
        var traders = databaseService.GetTables().Traders;
        if (traders == null) return 0;

        var totalRemoved = 0;

        foreach (var (traderId, trader) in traders)
        {
            if (trader.Assort?.Items == null) continue;
            var id = traderId.ToString();

            if (!config.TraderOverrides.TryGetValue(id, out var ov) || !ov.Enabled) continue;
            if (ov.DisabledItems.Count == 0) continue;

            var disabledTemplates = new HashSet<string>(ov.DisabledItems);

            // Find root items matching disabled templates
            var rootItemsToRemove = trader.Assort.Items
                .Where(item => item.ParentId == "hideout" && disabledTemplates.Contains(item.Template.ToString()))
                .Select(item => item.Id.ToString())
                .ToList();

            if (rootItemsToRemove.Count == 0) continue;

            // BFS to find all descendants of each root item
            var allItemsToRemove = new HashSet<string>();
            var queue = new Queue<string>(rootItemsToRemove);

            while (queue.Count > 0)
            {
                var parentId = queue.Dequeue();
                allItemsToRemove.Add(parentId);

                // Find children
                foreach (var child in trader.Assort.Items)
                {
                    if (child.ParentId == parentId)
                        queue.Enqueue(child.Id.ToString());
                }
            }

            // Remove from Items list
            trader.Assort.Items.RemoveAll(item => allItemsToRemove.Contains(item.Id.ToString()));

            // Remove from BarterScheme and LoyalLevelItems (root items only)
            foreach (var rootId in rootItemsToRemove)
            {
                MongoId mongoId = rootId;
                trader.Assort.BarterScheme?.Remove(mongoId);
                trader.Assort.LoyalLevelItems?.Remove(mongoId);
            }

            totalRemoved += rootItemsToRemove.Count;
        }

        return totalRemoved;
    }
}

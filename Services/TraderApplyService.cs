using System.Diagnostics;
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
public class TraderApplyService(
    DatabaseService databaseService,
    ConfigServer configServer,
    ConfigService configService,
    TraderDiscoveryService discoveryService,
    TraderPriceService priceService,
    TraderStockService stockService,
    LocaleService localeService,
    ISptLogger<TraderApplyService> logger)
{
    private readonly object _lock = new();
    private bool _initialized;
    private long? _lastApplyTimeMs;

    // Snapshot of original restock timers (traderId → min, max)
    private readonly Dictionary<string, (int min, int max)> _originalRestockTimers = new();

    /// <summary>
    /// Initialize: discover traders, snapshot originals, apply config.
    /// Called once during CommandCenterMod.OnLoad().
    /// </summary>
    public void Initialize()
    {
        if (_initialized) return;

        discoveryService.Initialize();

        // Snapshot original restock timers
        var traderConfig = configServer.GetConfig<TraderConfig>();
        foreach (var entry in traderConfig.UpdateTime)
        {
            _originalRestockTimers[entry.TraderId.ToString()] = (entry.Seconds.Min, entry.Seconds.Max);
        }

        // Apply config on startup
        var config = configService.GetConfig().Traders;
        var hasAnyOverrides = config.GlobalBuyMultiplier != 1.0 ||
                              config.GlobalSellMultiplier != 1.0 ||
                              config.GlobalStockMultiplier != 1.0 ||
                              config.GlobalLoyaltyLevelShift != 0 ||
                              config.ForceCurrency != null ||
                              config.GlobalRestockMinSeconds != null ||
                              config.GlobalRestockMaxSeconds != null ||
                              config.TraderOverrides.Count > 0;

        if (hasAnyOverrides)
        {
            var result = ApplyConfig();
            if (result.Success)
                logger.Success($"ZSlayerCC Traders: Applied config on startup — {result.ItemsModified} items across {result.TradersAffected} traders in {result.ApplyTimeMs}ms");
        }
        else
        {
            logger.Info("ZSlayerCC Traders: No trader overrides configured — using defaults");
        }

        _initialized = true;
    }

    /// <summary>
    /// Apply the full trader config: restore all → apply all modifications.
    /// Thread-safe via lock.
    /// </summary>
    public TraderApplyResult ApplyConfig()
    {
        lock (_lock)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var config = configService.GetConfig().Traders;
                var traders = databaseService.GetTables().Traders;
                if (traders == null)
                    return new TraderApplyResult { Success = false, Error = "No traders found" };

                // Step 1: Restore ALL traders from snapshots
                var tradersAffected = RestoreAllTraders();

                // Step 2: Apply buy price multipliers
                var itemsModified = priceService.ApplyBuyMultipliers(config);

                // Step 3: Apply sell multipliers (modify buy_price_coef)
                priceService.ApplySellMultipliers(config);

                // Step 4: Apply currency override
                priceService.ApplyCurrencyOverride(config);

                // Step 5: Apply stock multipliers
                stockService.ApplyStockMultipliers(config);

                // Step 6: Apply restock timers
                stockService.ApplyRestockTimers(config, _originalRestockTimers);

                // Step 7: Apply loyalty level shifts
                stockService.ApplyLoyaltyLevelShifts(config);

                // Step 8: Apply disabled items
                var disabledCount = stockService.ApplyDisabledItems(config);

                sw.Stop();
                _lastApplyTimeMs = sw.ElapsedMilliseconds;

                return new TraderApplyResult
                {
                    Success = true,
                    TradersAffected = tradersAffected,
                    ItemsModified = itemsModified + disabledCount,
                    ApplyTimeMs = sw.ElapsedMilliseconds
                };
            }
            catch (Exception ex)
            {
                sw.Stop();
                logger.Error($"ZSlayerCC Traders: Apply failed — {ex.Message}");
                return new TraderApplyResult
                {
                    Success = false,
                    Error = ex.Message,
                    ApplyTimeMs = sw.ElapsedMilliseconds
                };
            }
        }
    }

    /// <summary>
    /// Get the full trader config.
    /// </summary>
    public TraderControlConfig GetConfig() => configService.GetConfig().Traders;

    /// <summary>
    /// Replace the full trader config, save, and apply.
    /// </summary>
    public TraderApplyResult UpdateFullConfig(TraderControlConfig newConfig)
    {
        lock (_lock)
        {
            configService.GetConfig().Traders = newConfig;
            configService.SaveConfig();
            return ApplyConfig();
        }
    }

    /// <summary>
    /// Update just the global multipliers, save, and apply.
    /// </summary>
    public TraderApplyResult UpdateGlobalConfig(TraderGlobalUpdateRequest req)
    {
        lock (_lock)
        {
            var config = configService.GetConfig().Traders;
            config.GlobalBuyMultiplier = Math.Clamp(req.GlobalBuyMultiplier, 0.01, 100.0);
            config.GlobalSellMultiplier = Math.Clamp(req.GlobalSellMultiplier, 0.01, 100.0);
            config.GlobalStockMultiplier = Math.Clamp(req.GlobalStockMultiplier, 0.1, 100.0);
            config.GlobalStockCap = req.GlobalStockCap is > 0 ? req.GlobalStockCap : null;
            config.GlobalRestockMinSeconds = req.GlobalRestockMinSeconds;
            config.GlobalRestockMaxSeconds = req.GlobalRestockMaxSeconds;
            config.GlobalLoyaltyLevelShift = Math.Clamp(req.GlobalLoyaltyLevelShift, -3, 0);
            config.ForceCurrency = req.ForceCurrency;
            config.MinPriceRoubles = Math.Clamp(req.MinPriceRoubles, 1, 999_999);
            config.MaxPriceRoubles = Math.Clamp(req.MaxPriceRoubles, 1, 999_999_999);
            configService.SaveConfig();
            return ApplyConfig();
        }
    }

    /// <summary>
    /// Update a single trader's overrides, save, and apply.
    /// </summary>
    public TraderApplyResult UpdateTraderOverride(TraderOverrideUpdateRequest req)
    {
        lock (_lock)
        {
            var config = configService.GetConfig().Traders;
            if (!config.TraderOverrides.TryGetValue(req.TraderId, out var existing))
            {
                existing = new TraderOverride();
                config.TraderOverrides[req.TraderId] = existing;
            }

            // Get trader name for display
            var traders = databaseService.GetTables().Traders;
            if (traders != null && traders.TryGetValue(req.TraderId, out var trader))
                existing.TraderName = trader.Base?.Nickname ?? req.TraderId;

            existing.Enabled = req.Enabled;
            existing.BuyMultiplier = Math.Clamp(req.BuyMultiplier, 0.01, 100.0);
            existing.SellMultiplier = Math.Clamp(req.SellMultiplier, 0.01, 100.0);
            existing.StockMultiplier = Math.Clamp(req.StockMultiplier, 0.1, 100.0);
            existing.RestockMinSeconds = req.RestockMinSeconds;
            existing.RestockMaxSeconds = req.RestockMaxSeconds;
            existing.LoyaltyLevelShift = Math.Clamp(req.LoyaltyLevelShift, -3, 0);
            existing.ForceCurrency = req.ForceCurrency;
            if (req.DisabledItems != null)
                existing.DisabledItems = req.DisabledItems;

            configService.SaveConfig();
            return ApplyConfig();
        }
    }

    /// <summary>
    /// Add or update a per-item override for a specific trader.
    /// </summary>
    public TraderApplyResult SetItemOverride(TraderItemOverrideRequest req)
    {
        lock (_lock)
        {
            var config = configService.GetConfig().Traders;
            if (!config.TraderOverrides.TryGetValue(req.TraderId, out var existing))
            {
                existing = new TraderOverride();
                config.TraderOverrides[req.TraderId] = existing;
            }

            existing.ItemOverrides[req.TemplateId] = new TraderItemOverride
            {
                Name = req.Name,
                BuyMultiplier = Math.Clamp(req.BuyMultiplier, 0.01, 100.0),
                SellMultiplier = Math.Clamp(req.SellMultiplier, 0.01, 100.0),
            };

            configService.SaveConfig();
            return ApplyConfig();
        }
    }

    /// <summary>
    /// Remove a per-item override for a specific trader.
    /// </summary>
    public TraderApplyResult RemoveItemOverride(string traderId, string templateId)
    {
        lock (_lock)
        {
            var config = configService.GetConfig().Traders;
            if (config.TraderOverrides.TryGetValue(traderId, out var existing))
            {
                existing.ItemOverrides.Remove(templateId);
                configService.SaveConfig();
            }
            return ApplyConfig();
        }
    }

    /// <summary>
    /// Reset ALL traders to original values and clear config.
    /// </summary>
    public TraderApplyResult ResetAll()
    {
        lock (_lock)
        {
            var config = configService.GetConfig().Traders;
            config.GlobalBuyMultiplier = 1.0;
            config.GlobalSellMultiplier = 1.0;
            config.GlobalStockMultiplier = 1.0;
            config.GlobalStockCap = null;
            config.MinPriceRoubles = 1;
            config.MaxPriceRoubles = 50_000_000;
            config.GlobalRestockMinSeconds = null;
            config.GlobalRestockMaxSeconds = null;
            config.GlobalLoyaltyLevelShift = 0;
            config.ForceCurrency = null;
            config.TraderOverrides.Clear();
            configService.SaveConfig();

            RestoreAllTraders();
            RestoreAllRestockTimers();
            RestoreAllLoyaltyCoefs();

            _lastApplyTimeMs = null;
            logger.Info("ZSlayerCC Traders: Reset all to defaults");

            return new TraderApplyResult { Success = true, TradersAffected = 0, ItemsModified = 0, ApplyTimeMs = 0 };
        }
    }

    /// <summary>
    /// Reset a single trader to original values and remove its override.
    /// </summary>
    public TraderApplyResult ResetTrader(string traderId)
    {
        lock (_lock)
        {
            var config = configService.GetConfig().Traders;
            config.TraderOverrides.Remove(traderId);
            configService.SaveConfig();

            // Re-apply everything (restore all then apply remaining config)
            return ApplyConfig();
        }
    }

    /// <summary>Get trader items for the item browser.</summary>
    public TraderItemListResponse GetTraderItems(string traderId, string? search, int? loyaltyLevel, int limit, int offset)
    {
        var traders = databaseService.GetTables().Traders;
        if (traders == null || !traders.TryGetValue(traderId, out var trader))
            return new TraderItemListResponse();

        var config = configService.GetConfig().Traders;
        var snapshot = discoveryService.GetSnapshot(traderId);
        var locales = localeService.GetLocaleDb("en");
        var searchLower = search?.ToLowerInvariant();

        var allItems = new List<TraderItemInfo>();

        if (trader.Assort?.Items == null) return new TraderItemListResponse();

        // Get disabled items set for this trader
        var disabledTemplates = new HashSet<string>();
        if (config.TraderOverrides.TryGetValue(traderId, out var ov))
            disabledTemplates = new HashSet<string>(ov.DisabledItems);

        foreach (var item in trader.Assort.Items)
        {
            if (item.ParentId != "hideout") continue;

            var itemId = item.Id.ToString();
            var templateId = item.Template.ToString();

            // Get names from locale
            locales.TryGetValue($"{templateId} Name", out var fullName);
            locales.TryGetValue($"{templateId} ShortName", out var shortName);
            if (string.IsNullOrEmpty(fullName)) fullName = templateId;
            if (string.IsNullOrEmpty(shortName)) shortName = fullName;

            // Search filter
            if (searchLower != null &&
                !fullName.ToLowerInvariant().Contains(searchLower) &&
                !shortName.ToLowerInvariant().Contains(searchLower) &&
                !templateId.Contains(searchLower))
                continue;

            // Get loyalty level
            var ll = trader.Assort.LoyalLevelItems?.TryGetValue(itemId, out var level) == true ? level : 1;
            var originalLl = ll;
            if (snapshot?.LoyaltyLevels.TryGetValue(itemId, out var snapLevel) == true)
                originalLl = snapLevel;

            // Loyalty level filter
            if (loyaltyLevel.HasValue && ll != loyaltyLevel.Value)
                continue;

            // Get price info from barter scheme
            double basePrice = 0, modifiedPrice = 0;
            var isBarter = false;
            var currency = "RUB";

            if (trader.Assort.BarterScheme?.TryGetValue(itemId, out var paymentOptions) == true &&
                paymentOptions.Count > 0 && paymentOptions[0].Count > 0)
            {
                var firstOption = paymentOptions[0];
                if (firstOption.Count == 1 && TraderDiscoveryService.IsCurrencyTemplate(firstOption[0].Template.ToString()))
                {
                    modifiedPrice = firstOption[0].Count ?? 0;
                    currency = TraderDiscoveryService.TemplateIdToCurrency(firstOption[0].Template.ToString());

                    // Get original price from snapshot
                    if (snapshot?.BarterCosts.TryGetValue(itemId, out var snapOptions) == true &&
                        snapOptions.Count > 0 && snapOptions[0].Count > 0)
                    {
                        basePrice = snapOptions[0][0].Count ?? 0;
                    }
                    if (basePrice == 0) basePrice = modifiedPrice;
                }
                else
                {
                    isBarter = true;
                }
            }

            // Stock info
            var stock = item.Upd?.StackObjectsCount ?? 1;
            var originalStock = snapshot?.StockCounts.GetValueOrDefault(itemId, stock) ?? stock;

            // Override check
            var hasItemOverride = false;
            if (config.TraderOverrides.TryGetValue(traderId, out var traderOv))
                hasItemOverride = traderOv.ItemOverrides.ContainsKey(templateId);

            // Effective multiplier (override replaces, not multiplies)
            var effectiveMult = config.GlobalBuyMultiplier;
            if (traderOv is { Enabled: true })
                effectiveMult = traderOv.BuyMultiplier;
            if (hasItemOverride && traderOv?.ItemOverrides.TryGetValue(templateId, out var itemOv) == true)
                effectiveMult = itemOv.BuyMultiplier;

            allItems.Add(new TraderItemInfo
            {
                ItemId = itemId,
                TemplateId = templateId,
                ShortName = shortName,
                FullName = fullName,
                BasePrice = basePrice,
                ModifiedPrice = modifiedPrice,
                Currency = currency,
                LoyaltyLevel = ll,
                OriginalLoyaltyLevel = originalLl,
                Stock = stock,
                OriginalStock = originalStock,
                IsBarter = isBarter,
                IsDisabled = disabledTemplates.Contains(templateId),
                HasOverride = hasItemOverride,
                EffectiveMultiplier = effectiveMult,
            });
        }

        // Also add disabled items that were removed from assort (from snapshot)
        foreach (var disabledTpl in disabledTemplates)
        {
            if (allItems.Any(i => i.TemplateId == disabledTpl)) continue;

            locales.TryGetValue($"{disabledTpl} Name", out var dName);
            locales.TryGetValue($"{disabledTpl} ShortName", out var dShort);
            if (string.IsNullOrEmpty(dName)) dName = disabledTpl;
            if (string.IsNullOrEmpty(dShort)) dShort = dName;

            if (searchLower != null &&
                !dName.ToLowerInvariant().Contains(searchLower) &&
                !dShort.ToLowerInvariant().Contains(searchLower))
                continue;

            allItems.Add(new TraderItemInfo
            {
                TemplateId = disabledTpl,
                ShortName = dShort,
                FullName = dName,
                IsDisabled = true,
            });
        }

        allItems.Sort((a, b) => string.Compare(a.FullName, b.FullName, StringComparison.OrdinalIgnoreCase));

        var total = allItems.Count;
        var paged = allItems.Skip(offset).Take(limit).ToList();

        return new TraderItemListResponse
        {
            Items = paged,
            Total = total,
            Limit = limit,
            Offset = offset,
        };
    }

    /// <summary>Get trader status info.</summary>
    public TraderStatusResponse GetStatus()
    {
        var discovered = discoveryService.GetDiscoveredTraders(configService.GetConfig().Traders);
        return new TraderStatusResponse
        {
            ModVersion = ModMetadata.StaticVersion,
            TraderCount = discovered.Count,
            VanillaTraders = discovered.Count(t => !t.IsModded),
            ModdedTraders = discovered.Count(t => t.IsModded),
            TotalItems = discovered.Sum(t => t.ItemCount),
            ConfigApplied = _lastApplyTimeMs.HasValue,
            LastApplyTimeMs = _lastApplyTimeMs,
        };
    }

    // ── Presets ──

    private static readonly JsonSerializerOptions PresetJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private string GetPresetsDir()
    {
        var dir = System.IO.Path.Combine(configService.ModPath, "config", "trader-presets");
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        return dir;
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        var clean = new string(name.Where(c => !invalid.Contains(c)).ToArray()).Trim();
        if (clean.Length > 50) clean = clean[..50];
        return string.IsNullOrEmpty(clean) ? "preset" : clean;
    }

    /// <summary>Snapshot current gameplay config (excludes display overrides).</summary>
    private TraderGameplayConfig SnapshotGameplayConfig()
    {
        var config = configService.GetConfig().Traders;
        return new TraderGameplayConfig
        {
            GlobalBuyMultiplier = config.GlobalBuyMultiplier,
            GlobalSellMultiplier = config.GlobalSellMultiplier,
            MinPriceRoubles = config.MinPriceRoubles,
            MaxPriceRoubles = config.MaxPriceRoubles,
            GlobalStockMultiplier = config.GlobalStockMultiplier,
            GlobalStockCap = config.GlobalStockCap,
            GlobalRestockMinSeconds = config.GlobalRestockMinSeconds,
            GlobalRestockMaxSeconds = config.GlobalRestockMaxSeconds,
            GlobalLoyaltyLevelShift = config.GlobalLoyaltyLevelShift,
            ForceCurrency = config.ForceCurrency,
            TraderOverrides = config.TraderOverrides.ToDictionary(
                kv => kv.Key,
                kv => kv.Value with { ItemOverrides = new Dictionary<string, TraderItemOverride>(kv.Value.ItemOverrides), DisabledItems = [.. kv.Value.DisabledItems] })
        };
    }

    /// <summary>Apply gameplay config from a preset onto the live config (preserves display overrides).</summary>
    private void ApplyGameplayConfig(TraderGameplayConfig src)
    {
        var config = configService.GetConfig().Traders;
        config.GlobalBuyMultiplier = src.GlobalBuyMultiplier;
        config.GlobalSellMultiplier = src.GlobalSellMultiplier;
        config.MinPriceRoubles = src.MinPriceRoubles;
        config.MaxPriceRoubles = src.MaxPriceRoubles;
        config.GlobalStockMultiplier = src.GlobalStockMultiplier;
        config.GlobalStockCap = src.GlobalStockCap;
        config.GlobalRestockMinSeconds = src.GlobalRestockMinSeconds;
        config.GlobalRestockMaxSeconds = src.GlobalRestockMaxSeconds;
        config.GlobalLoyaltyLevelShift = src.GlobalLoyaltyLevelShift;
        config.ForceCurrency = src.ForceCurrency;
        config.TraderOverrides = src.TraderOverrides.ToDictionary(
            kv => kv.Key,
            kv => kv.Value with { ItemOverrides = new Dictionary<string, TraderItemOverride>(kv.Value.ItemOverrides), DisabledItems = [.. kv.Value.DisabledItems] });
        // TraderDisplayOverrides left untouched
    }

    public TraderPresetListResponse ListTraderPresets()
    {
        var dir = GetPresetsDir();
        var presets = new List<TraderPresetSummary>();
        foreach (var file in Directory.GetFiles(dir, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var preset = JsonSerializer.Deserialize<TraderPreset>(json, PresetJsonOptions);
                if (preset != null)
                {
                    presets.Add(new TraderPresetSummary
                    {
                        Name = preset.Name,
                        Description = preset.Description,
                        CreatedUtc = preset.CreatedUtc
                    });
                }
            }
            catch (Exception ex)
            {
                logger.Debug($"ZSlayerCC Traders: Skipping invalid preset file '{System.IO.Path.GetFileName(file)}': {ex.Message}");
            }
        }
        presets.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return new TraderPresetListResponse { Presets = presets };
    }

    public TraderPreset SaveTraderPreset(string name, string description)
    {
        var preset = new TraderPreset
        {
            Name = name,
            Description = description,
            CreatedUtc = DateTime.UtcNow,
            Config = SnapshotGameplayConfig()
        };
        var dir = GetPresetsDir();
        var filePath = System.IO.Path.Combine(dir, SanitizeFileName(name) + ".json");
        var json = JsonSerializer.Serialize(preset, PresetJsonOptions);
        File.WriteAllText(filePath, json);
        logger.Info($"ZSlayerCC Traders: Saved preset '{name}' to {System.IO.Path.GetFileName(filePath)}");
        return preset;
    }

    public TraderPreset? LoadTraderPreset(string name)
    {
        var dir = GetPresetsDir();
        var filePath = System.IO.Path.Combine(dir, SanitizeFileName(name) + ".json");
        if (!File.Exists(filePath))
            return null;
        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<TraderPreset>(json, PresetJsonOptions);
    }

    public TraderApplyResult LoadAndApplyTraderPreset(string name)
    {
        lock (_lock)
        {
            var preset = LoadTraderPreset(name);
            if (preset == null)
                return new TraderApplyResult { Success = false, Error = "Preset not found" };

            ApplyGameplayConfig(preset.Config);
            configService.SaveConfig();
            return ApplyConfig();
        }
    }

    public bool DeleteTraderPreset(string name)
    {
        var dir = GetPresetsDir();
        var filePath = System.IO.Path.Combine(dir, SanitizeFileName(name) + ".json");
        if (!File.Exists(filePath))
            return false;
        File.Delete(filePath);
        logger.Info($"ZSlayerCC Traders: Deleted preset '{name}'");
        return true;
    }

    public TraderPreset UploadTraderPreset(string name, string presetJson)
    {
        var preset = JsonSerializer.Deserialize<TraderPreset>(presetJson, PresetJsonOptions)
                     ?? throw new InvalidOperationException("Invalid preset JSON");
        if (!string.IsNullOrWhiteSpace(name))
            preset.Name = name;
        preset.CreatedUtc = DateTime.UtcNow;
        var dir = GetPresetsDir();
        var filePath = System.IO.Path.Combine(dir, SanitizeFileName(preset.Name) + ".json");
        var json = JsonSerializer.Serialize(preset, PresetJsonOptions);
        File.WriteAllText(filePath, json);
        logger.Info($"ZSlayerCC Traders: Imported preset '{preset.Name}'");
        return preset;
    }

    public string? DownloadTraderPreset(string name)
    {
        var dir = GetPresetsDir();
        var filePath = System.IO.Path.Combine(dir, SanitizeFileName(name) + ".json");
        return File.Exists(filePath) ? File.ReadAllText(filePath) : null;
    }

    // ── Private helpers ──

    /// <summary>Restore all traders' values in-place from snapshots (no collection replacement).</summary>
    private int RestoreAllTraders()
    {
        var traders = databaseService.GetTables().Traders;
        if (traders == null) return 0;

        var count = 0;
        foreach (var (traderId, trader) in traders)
        {
            var id = traderId.ToString();
            var snapshot = discoveryService.GetSnapshot(id);
            if (snapshot == null) continue;

            // Restore stock counts in-place
            if (trader.Assort?.Items != null)
            {
                foreach (var item in trader.Assort.Items)
                {
                    if (item.ParentId != "hideout" || item.Upd == null) continue;
                    if (snapshot.StockCounts.TryGetValue(item.Id.ToString(), out var origStock))
                        item.Upd.StackObjectsCount = origStock;
                }
            }

            // Restore barter scheme costs in-place (count + template)
            if (trader.Assort?.BarterScheme != null)
            {
                foreach (var (itemId, paymentOptions) in trader.Assort.BarterScheme)
                {
                    if (!snapshot.BarterCosts.TryGetValue(itemId.ToString(), out var origOptions)) continue;
                    for (var oi = 0; oi < paymentOptions.Count && oi < origOptions.Count; oi++)
                    {
                        var option = paymentOptions[oi];
                        var origOption = origOptions[oi];
                        for (var ci = 0; ci < option.Count && ci < origOption.Count; ci++)
                        {
                            option[ci].Count = origOption[ci].Count;
                            option[ci].Template = origOption[ci].Template;
                        }
                    }
                }
            }

            // Restore loyalty level items in-place
            if (trader.Assort?.LoyalLevelItems != null)
            {
                foreach (var (itemId, _) in trader.Assort.LoyalLevelItems)
                {
                    if (snapshot.LoyaltyLevels.TryGetValue(itemId.ToString(), out var origLevel))
                        trader.Assort.LoyalLevelItems[itemId] = origLevel;
                }
            }

            // Restore loyalty level coefs
            if (trader.Base?.LoyaltyLevels != null)
            {
                for (var i = 0; i < trader.Base.LoyaltyLevels.Count && i < snapshot.BuyPriceCoefs.Count; i++)
                    trader.Base.LoyaltyLevels[i].BuyPriceCoefficient = snapshot.BuyPriceCoefs[i];
            }

            // Restore original currency
            if (Enum.TryParse<CurrencyType>(snapshot.OriginalCurrency, out var origCurrency))
                trader.Base!.Currency = origCurrency;

            count++;
        }

        return count;
    }

    /// <summary>Restore all restock timers to original values.</summary>
    private void RestoreAllRestockTimers()
    {
        var traderConfig = configServer.GetConfig<TraderConfig>();
        foreach (var entry in traderConfig.UpdateTime)
        {
            var id = entry.TraderId.ToString();
            if (_originalRestockTimers.TryGetValue(id, out var orig))
                entry.Seconds = new MinMax<int> { Min = orig.min, Max = orig.max };
        }
    }

    /// <summary>Restore all loyalty level coefs from snapshots.</summary>
    private void RestoreAllLoyaltyCoefs()
    {
        var traders = databaseService.GetTables().Traders;
        if (traders == null) return;

        foreach (var (traderId, trader) in traders)
        {
            var snapshot = discoveryService.GetSnapshot(traderId.ToString());
            if (snapshot == null || trader.Base?.LoyaltyLevels == null) continue;

            for (var i = 0; i < trader.Base.LoyaltyLevels.Count && i < snapshot.BuyPriceCoefs.Count; i++)
                trader.Base.LoyaltyLevels[i].BuyPriceCoefficient = snapshot.BuyPriceCoefs[i];
        }
    }
}

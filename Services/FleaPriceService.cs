using System.Reflection;
using System.Text.Json;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using ZSlayerCommandCenter.Models;

namespace ZSlayerCommandCenter.Services;

[Injectable(InjectionType.Singleton)]
public class FleaPriceService(
    ConfigService configService,
    DatabaseService databaseService,
    ItemHelper itemHelper,
    LocaleService localeService,
    HandbookHelper handbookHelper,
    ModHelper modHelper,
    ISptLogger<FleaPriceService> logger)
{
    private readonly object _lock = new();

    // Category name → list of base class IDs for that category
    private static readonly Dictionary<string, List<string>> CategoryBaseClasses = new()
    {
        ["weapons"] = [BaseClasses.WEAPON],
        ["ammo"] = [BaseClasses.AMMO],
        ["armor"] = [BaseClasses.ARMORED_EQUIPMENT, BaseClasses.HEADWEAR],
        ["medical"] = [BaseClasses.MEDICAL_SUPPLIES],
        ["provisions"] = [BaseClasses.FOOD_DRINK],
        ["barter"] = [BaseClasses.BARTER_ITEM],
        ["keys"] = [BaseClasses.KEY],
        ["containers"] = [BaseClasses.MOB_CONTAINER],
        ["mods"] = [BaseClasses.MOD],
        ["specialEquipment"] = [BaseClasses.ARMOR]
    };

    // Display names for categories
    private static readonly Dictionary<string, string> CategoryDisplayNames = new()
    {
        ["weapons"] = "Weapons",
        ["ammo"] = "Ammo",
        ["armor"] = "Armor & Headwear",
        ["medical"] = "Medical",
        ["provisions"] = "Provisions",
        ["barter"] = "Barter Items",
        ["keys"] = "Keys",
        ["containers"] = "Containers",
        ["mods"] = "Weapon Mods",
        ["specialEquipment"] = "Special Equipment"
    };

    /// <summary>
    /// Get a thread-safe copy of the current flea config.
    /// </summary>
    public FleaConfig GetConfig()
    {
        lock (_lock)
        {
            return configService.GetConfig().Flea;
        }
    }

    /// <summary>
    /// Replace the full flea config, persist, and return the new config.
    /// </summary>
    public FleaConfig UpdateFullConfig(FleaConfig newConfig)
    {
        lock (_lock)
        {
            var config = configService.GetConfig();
            config.Flea = newConfig;
            configService.SaveConfig();
            logger.Info($"ZSlayerCC Flea: Full config updated — globalBuy={config.Flea.GlobalBuyMultiplier:F2} tax={config.Flea.FleaTaxMultiplier:F2} categories={config.Flea.CategoryMultipliers.Count}");
            return config.Flea;
        }
    }

    /// <summary>
    /// Update the global buy multiplier.
    /// </summary>
    public FleaConfig UpdateGlobalMultiplier(double buyMult)
    {
        lock (_lock)
        {
            var flea = configService.GetConfig().Flea;
            flea.GlobalBuyMultiplier = ClampMultiplier(buyMult);
            configService.SaveConfig();
            logger.Info($"ZSlayerCC Flea: Global multiplier set to buy={flea.GlobalBuyMultiplier:F2}");
            return flea;
        }
    }

    /// <summary>
    /// Update market settings (tax, offers, duration, restock, barter).
    /// </summary>
    public FleaConfig UpdateMarketSettings(FleaMarketSettingsRequest req)
    {
        lock (_lock)
        {
            var flea = configService.GetConfig().Flea;
            flea.FleaTaxMultiplier = Math.Clamp(req.FleaTaxMultiplier, 0.0, 5.0);
            flea.PlayerMaxOffers = Math.Clamp(req.PlayerMaxOffers, 1, 100);
            flea.OfferDurationHours = Math.Clamp(req.OfferDurationHours, 1, 168);
            flea.RestockIntervalMinutes = Math.Clamp(req.RestockIntervalMinutes, 1, 360);
            flea.BarterOffersEnabled = req.BarterOffersEnabled;
            flea.BarterOfferFrequency = Math.Clamp(req.BarterOfferFrequency, 0, 100);
            flea.DollarOffersEnabled = req.DollarOffersEnabled;
            flea.EuroOffersEnabled = req.EuroOffersEnabled;
            configService.SaveConfig();
            logger.Info($"ZSlayerCC Flea: Market settings updated — tax={flea.FleaTaxMultiplier:F1} maxOffers={flea.PlayerMaxOffers} duration={flea.OfferDurationHours}h");
            return flea;
        }
    }

    /// <summary>
    /// Update a single category's buy multiplier.
    /// </summary>
    public FleaConfig UpdateCategoryMultiplier(string category, double buyMult)
    {
        lock (_lock)
        {
            var flea = configService.GetConfig().Flea;
            flea.CategoryMultipliers[category] = new CategoryMultiplier
            {
                BuyMultiplier = ClampMultiplier(buyMult)
            };
            configService.SaveConfig();
            logger.Info($"ZSlayerCC Flea: Category '{category}' set to buy={buyMult:F2}");
            return flea;
        }
    }

    /// <summary>
    /// Add or update a per-item override.
    /// </summary>
    public FleaConfig SetItemOverride(string templateId, string name, double buyMult)
    {
        lock (_lock)
        {
            var flea = configService.GetConfig().Flea;
            flea.ItemOverrides[templateId] = new ItemOverride
            {
                Name = name,
                BuyMultiplier = ClampMultiplier(buyMult)
            };
            configService.SaveConfig();
            logger.Info($"ZSlayerCC Flea: Item override set for '{name}' ({templateId}) buy={buyMult:F2}");
            return flea;
        }
    }

    /// <summary>
    /// Remove a per-item override.
    /// </summary>
    public bool RemoveItemOverride(string templateId)
    {
        lock (_lock)
        {
            var flea = configService.GetConfig().Flea;
            var removed = flea.ItemOverrides.Remove(templateId);
            if (removed) configService.SaveConfig();
            return removed;
        }
    }

    /// <summary>
    /// Resolve the effective buy multiplier for a template using the hierarchy:
    /// per-item > global × category > global.
    /// </summary>
    public (double multiplier, string level) GetEffectiveBuyMultiplier(string templateId)
    {
        FleaConfig flea;
        lock (_lock)
        {
            flea = configService.GetConfig().Flea;
        }
        return ResolveMultiplier(templateId, flea);
    }

    /// <summary>
    /// Apply the buy multiplier to a base price, including clamping.
    /// </summary>
    public double ApplyBuyMultiplier(double basePrice, string templateId)
    {
        if (basePrice <= 0) return basePrice;

        var (mult, _) = GetEffectiveBuyMultiplier(templateId);

        FleaConfig flea;
        lock (_lock)
        {
            flea = configService.GetConfig().Flea;
        }

        var result = Math.Round(basePrice * mult);
        result = Math.Clamp(result, flea.MinPriceRoubles, flea.MaxPriceRoubles);

        // Apply variance
        if (flea.DynamicPriceVariance > 0)
        {
            var variance = flea.DynamicPriceVariance;
            var offset = (Random.Shared.NextDouble() * 2.0 - 1.0) * variance;
            result = Math.Round(result * (1.0 + offset));
            result = Math.Max(result, flea.MinPriceRoubles);
        }

        return result;
    }

    /// <summary>
    /// Get a price preview for a specific item.
    /// </summary>
    public FleaPricePreview? GetPreview(string templateId)
    {
        var items = databaseService.GetItems();
        if (!items.ContainsKey(templateId))
            return null;

        var locales = localeService.GetLocaleDb("en");
        locales.TryGetValue($"{templateId} Name", out var name);
        if (string.IsNullOrEmpty(name)) name = templateId;

        var basePrice = (int)handbookHelper.GetTemplatePrice(templateId);
        var (buyMult, buyLevel) = GetEffectiveBuyMultiplier(templateId);

        FleaConfig flea;
        lock (_lock)
        {
            flea = configService.GetConfig().Flea;
        }

        var effectiveBuy = (int)Math.Round(basePrice * buyMult);
        effectiveBuy = Math.Clamp(effectiveBuy, flea.MinPriceRoubles, flea.MaxPriceRoubles);

        return new FleaPricePreview
        {
            TemplateId = templateId,
            Name = name,
            BasePrice = basePrice,
            EffectiveBuyPrice = effectiveBuy,
            AppliedLevel = buyLevel
        };
    }

    /// <summary>
    /// Get all flea categories with item counts and current multipliers.
    /// </summary>
    public FleaCategoryListResponse GetCategories()
    {
        var items = databaseService.GetItems();
        FleaConfig flea;
        lock (_lock)
        {
            flea = configService.GetConfig().Flea;
        }

        var result = new List<FleaCategoryInfo>();

        foreach (var (key, baseClassIds) in CategoryBaseClasses)
        {
            var count = 0;
            foreach (var (id, template) in items)
            {
                if (template.Type != "Item") continue;
                var tpl = id.ToString();
                foreach (var baseClassId in baseClassIds)
                {
                    if (itemHelper.IsOfBaseclass(tpl, baseClassId))
                    {
                        count++;
                        break;
                    }
                }
            }

            var buyMult = flea.GlobalBuyMultiplier;
            if (flea.CategoryMultipliers.TryGetValue(key, out var catMult))
            {
                buyMult = catMult.BuyMultiplier;
            }

            result.Add(new FleaCategoryInfo
            {
                Name = CategoryDisplayNames.GetValueOrDefault(key, key),
                Key = key,
                ItemCount = count,
                BaseClassIds = baseClassIds,
                CurrentBuyMultiplier = buyMult
            });
        }

        return new FleaCategoryListResponse { Categories = result };
    }

    /// <summary>
    /// Search items by name for the per-item override picker.
    /// </summary>
    public FleaItemSearchResponse SearchItems(string query)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            return new FleaItemSearchResponse();

        var items = databaseService.GetItems();
        var locales = localeService.GetLocaleDb("en");
        var queryLower = query.ToLowerInvariant();
        var results = new List<FleaItemSearchResult>();

        foreach (var (id, template) in items)
        {
            if (template.Type != "Item") continue;
            if (template.Properties?.QuestItem == true) continue;

            var tpl = id.ToString();
            locales.TryGetValue($"{tpl} Name", out var fullName);
            locales.TryGetValue($"{tpl} ShortName", out var shortName);
            if (string.IsNullOrEmpty(fullName)) fullName = template.Name ?? tpl;
            if (string.IsNullOrEmpty(shortName)) shortName = fullName;

            if (!fullName.ToLowerInvariant().Contains(queryLower) &&
                !shortName.ToLowerInvariant().Contains(queryLower) &&
                !tpl.Contains(queryLower))
                continue;

            var parentId = template.Parent.ToString();
            locales.TryGetValue($"{parentId} Name", out var catName);
            if (string.IsNullOrEmpty(catName)) catName = "";

            var basePrice = (int)handbookHelper.GetTemplatePrice(id);

            results.Add(new FleaItemSearchResult
            {
                TemplateId = tpl,
                Name = fullName,
                ShortName = shortName,
                BasePrice = basePrice,
                Category = catName
            });

            if (results.Count >= 50) break;
        }

        results.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return new FleaItemSearchResponse { Items = results };
    }

    /// <summary>
    /// Find which flea category an item belongs to (if any).
    /// Returns the category key or null.
    /// </summary>
    public string? GetItemCategory(string templateId)
    {
        foreach (var (key, baseClassIds) in CategoryBaseClasses)
        {
            foreach (var baseClassId in baseClassIds)
            {
                if (itemHelper.IsOfBaseclass(templateId, baseClassId))
                    return key;
            }
        }
        return null;
    }

    // ── Private helpers ──

    private (double multiplier, string level) ResolveMultiplier(string templateId, FleaConfig flea)
    {
        // Per-item override replaces everything
        if (flea.ItemOverrides.TryGetValue(templateId, out var itemOverride))
        {
            return (itemOverride.BuyMultiplier, "item");
        }

        // Stacking: global × category
        var globalMult = flea.GlobalBuyMultiplier;
        var category = GetItemCategory(templateId);
        if (category != null && flea.CategoryMultipliers.TryGetValue(category, out var catMult))
        {
            return (globalMult * catMult.BuyMultiplier, "global×category");
        }

        return (globalMult, "global");
    }

    private static double ClampMultiplier(double value) => Math.Clamp(value, 0.01, 100.0);

    // ── Preset Management ──

    private static readonly JsonSerializerOptions PresetJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private string GetPresetsDir()
    {
        var modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        var dir = Path.Combine(modPath, "config", "flea-presets");
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        return dir;
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string(name.Where(c => !invalid.Contains(c)).ToArray()).Trim();
        return string.IsNullOrEmpty(clean) ? "preset" : clean;
    }

    public FleaPresetListResponse ListPresets()
    {
        var dir = GetPresetsDir();
        var presets = new List<FleaPresetSummary>();
        foreach (var file in Directory.GetFiles(dir, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var preset = JsonSerializer.Deserialize<FleaPreset>(json, PresetJsonOptions);
                if (preset != null)
                {
                    presets.Add(new FleaPresetSummary
                    {
                        Name = preset.Name,
                        Description = preset.Description,
                        CreatedUtc = preset.CreatedUtc
                    });
                }
            }
            catch { /* skip corrupt files */ }
        }
        presets.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return new FleaPresetListResponse { Presets = presets };
    }

    public FleaPreset SavePreset(string name, string description)
    {
        var preset = new FleaPreset
        {
            Name = name,
            Description = description,
            CreatedUtc = DateTime.UtcNow,
            Config = GetConfig()
        };
        var dir = GetPresetsDir();
        var filePath = Path.Combine(dir, SanitizeFileName(name) + ".json");
        var json = JsonSerializer.Serialize(preset, PresetJsonOptions);
        File.WriteAllText(filePath, json);
        logger.Info($"ZSlayerCC Flea: Saved preset '{name}' to {Path.GetFileName(filePath)}");
        return preset;
    }

    public FleaPreset? LoadPreset(string name)
    {
        var dir = GetPresetsDir();
        var filePath = Path.Combine(dir, SanitizeFileName(name) + ".json");
        if (!File.Exists(filePath))
            return null;
        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<FleaPreset>(json, PresetJsonOptions);
    }

    public bool DeletePreset(string name)
    {
        var dir = GetPresetsDir();
        var filePath = Path.Combine(dir, SanitizeFileName(name) + ".json");
        if (!File.Exists(filePath))
            return false;
        File.Delete(filePath);
        logger.Info($"ZSlayerCC Flea: Deleted preset '{name}'");
        return true;
    }

    public FleaPreset ImportPreset(FleaPreset preset)
    {
        preset.CreatedUtc = DateTime.UtcNow;
        var dir = GetPresetsDir();
        var filePath = Path.Combine(dir, SanitizeFileName(preset.Name) + ".json");
        var json = JsonSerializer.Serialize(preset, PresetJsonOptions);
        File.WriteAllText(filePath, json);
        logger.Info($"ZSlayerCC Flea: Imported preset '{preset.Name}'");
        return preset;
    }
}

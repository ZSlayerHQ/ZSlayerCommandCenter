using System.Diagnostics;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Generators;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using ZSlayerCommandCenter.Models;

namespace ZSlayerCommandCenter.Services;

[Injectable(InjectionType.Singleton)]
public class OfferRegenerationService(
    DatabaseService databaseService,
    RagfairConfig ragfairConfig,
    RagfairOfferService ragfairOfferService,
    RagfairOfferGenerator ragfairOfferGenerator,
    FleaPriceService fleaPriceService,
    LocaleService localeService,
    ISptLogger<OfferRegenerationService> logger)
{
    private readonly object _lock = new();

    // Snapshot storage for original values
    private List<double>? _snapshotOfferCounts;
    private double? _snapshotOfferDuration;
    private double? _snapshotBarterChance;
    private Dictionary<MongoId, double>? _snapshotCurrencyPercents;
    private Dictionary<MongoId, double>? _snapshotPrices;
    private float? _snapshotCommunityItemTax;
    private double? _snapshotCommunityRequirementTax;
    private bool _snapshotTaken;

    private DateTime? _lastRegeneratedUtc;
    private int _lastModifiedPriceCount;

    /// <summary>Structured flea display info for the startup banner.</summary>
    public FleaStartupDisplay? StartupDisplay { get; private set; }

    // Well-known items for debug sampling
    private static readonly (string Id, string Name)[] DebugSampleItems =
    [
        ("5447a9cd4bdc2dbd208b4567", "M4A1"),
        ("5644bd2b4bdc2d3b4c8b4572", "AK-74N"),
        ("544fb45d4bdc2dee738b4568", "Salewa"),
        ("59faff1d86f7746c51718c9c", "Bitcoin"),
        ("5c94bbff86f7747ee735c08f", "TerraGroup Labs keycard"),
    ];

    // Preferred example items for startup display (in priority order)
    private static readonly (string Id, string Name)[] StartupExampleItems =
    [
        ("63a3a93f8a56922e82001f5d", "Dorms 314 marked key"),
        ("5c94bbff86f7747ee735c08f", "Labs keycard"),
        ("59faff1d86f7746c51718c9c", "Bitcoin"),
        ("5447a9cd4bdc2dbd208b4567", "M4A1"),
    ];

    /// <summary>
    /// Take a snapshot of original globals/config values on first call.
    /// On every call: restore from snapshot, then apply CC config.
    /// </summary>
    public void ApplyGlobalsAndConfig()
    {
        lock (_lock)
        {
            var globals = databaseService.GetGlobals();
            var flea = fleaPriceService.GetConfig();

            // Snapshot on first call
            if (!_snapshotTaken)
            {
                _snapshotOfferCounts = globals.Configuration.RagFair.MaxActiveOfferCount
                    .Select(o => o.Count).ToList();
                _snapshotOfferDuration = globals.Configuration.RagFair.OfferDurationTimeInHour;
                _snapshotBarterChance = ragfairConfig.Dynamic.Barter.ChancePercent;
                _snapshotCurrencyPercents = new Dictionary<MongoId, double>(ragfairConfig.Dynamic.OfferCurrencyChangePercent);
                _snapshotPrices = new Dictionary<MongoId, double>(databaseService.GetPrices());
                _snapshotCommunityItemTax = globals.Configuration.RagFair.CommunityItemTax;
                _snapshotCommunityRequirementTax = globals.Configuration.RagFair.CommunityRequirementTax;
                _snapshotTaken = true;
            }

            // ── Restore from snapshot ──
            var maxOfferEntries = globals.Configuration.RagFair.MaxActiveOfferCount.ToList();
            for (var i = 0; i < maxOfferEntries.Count && i < _snapshotOfferCounts!.Count; i++)
            {
                maxOfferEntries[i].Count = _snapshotOfferCounts[i];
            }
            globals.Configuration.RagFair.OfferDurationTimeInHour = _snapshotOfferDuration!.Value;
            ragfairConfig.Dynamic.Barter.ChancePercent = _snapshotBarterChance!.Value;
            foreach (var (key, val) in _snapshotCurrencyPercents!)
            {
                ragfairConfig.Dynamic.OfferCurrencyChangePercent[key] = val;
            }
            globals.Configuration.RagFair.CommunityItemTax = _snapshotCommunityItemTax!.Value;
            globals.Configuration.RagFair.CommunityRequirementTax = _snapshotCommunityRequirementTax!.Value;

            // ── Apply CC config values ──
            foreach (var entry in maxOfferEntries)
            {
                entry.Count = flea.PlayerMaxOffers;
            }
            globals.Configuration.RagFair.OfferDurationTimeInHour = flea.OfferDurationHours;
            ragfairConfig.Dynamic.Barter.ChancePercent = flea.BarterOffersEnabled
                ? flea.BarterOfferFrequency
                : 0;

            // Apply tax multiplier to globals (client reads these values)
            globals.Configuration.RagFair.CommunityItemTax = (float)(_snapshotCommunityItemTax!.Value * flea.FleaTaxMultiplier);
            globals.Configuration.RagFair.CommunityRequirementTax = _snapshotCommunityRequirementTax!.Value * flea.FleaTaxMultiplier;

            // Apply currency toggles — disabled currencies get 0%, redistributed to roubles
            MongoId roublesTpl = "5449016a4bdc2d6f028b456f";
            MongoId dollarsTpl = "5696686a4bdc2da3298b456a";
            MongoId eurosTpl = "569668774bdc2da2298b4568";
            var currencyPercents = ragfairConfig.Dynamic.OfferCurrencyChangePercent;
            var redistributed = 0.0;
            if (!flea.DollarOffersEnabled && currencyPercents.ContainsKey(dollarsTpl))
            {
                redistributed += currencyPercents[dollarsTpl];
                currencyPercents[dollarsTpl] = 0;
            }
            if (!flea.EuroOffersEnabled && currencyPercents.ContainsKey(eurosTpl))
            {
                redistributed += currencyPercents[eurosTpl];
                currencyPercents[eurosTpl] = 0;
            }
            if (redistributed > 0 && currencyPercents.ContainsKey(roublesTpl))
            {
                currencyPercents[roublesTpl] += redistributed;
            }

            // ── Apply price multipliers directly to prices dictionary ──
            var prices = databaseService.GetPrices();
            var modifiedCount = 0;

            foreach (var (tplId, originalPrice) in _snapshotPrices!)
            {
                var (mult, _) = fleaPriceService.GetEffectiveBuyMultiplier(tplId.ToString());
                var newPrice = Math.Max(1.0, Math.Round(originalPrice * mult));
                prices[tplId] = newPrice;

                if (Math.Abs(mult - 1.0) > 0.001)
                    modifiedCount++;
            }

            _lastModifiedPriceCount = modifiedCount;

            // ── Build startup display info ──
            var exName = "";
            double exBasePrice = 0, exModPrice = 0, exEffMult = 0, exBaseTax = 0, exModTax = 0;
            var exMultSource = "";
            var baseTaxRate = (_snapshotCommunityItemTax!.Value + (float)_snapshotCommunityRequirementTax!.Value) / 100.0;

            var locales = localeService.GetLocaleDb("en");
            foreach (var (candidateId, fallbackName) in StartupExampleItems)
            {
                MongoId mongoId = candidateId;
                if (_snapshotPrices!.TryGetValue(mongoId, out var origPrice) && origPrice > 0)
                {
                    locales.TryGetValue($"{candidateId} Name", out var localeName);
                    exName = !string.IsNullOrEmpty(localeName) ? localeName : fallbackName;
                    exBasePrice = origPrice;
                    var (mult, multSource) = fleaPriceService.GetEffectiveBuyMultiplier(candidateId);
                    exModPrice = Math.Round(origPrice * mult);
                    exEffMult = mult;
                    exMultSource = multSource;
                    exBaseTax = Math.Round(exBasePrice * baseTaxRate);
                    exModTax = Math.Round(exModPrice * baseTaxRate * flea.FleaTaxMultiplier);
                    break;
                }
            }

            var hasModifiers = Math.Abs(flea.GlobalBuyMultiplier - 1.0) > 0.001 ||
                               Math.Abs(flea.FleaTaxMultiplier - 1.0) > 0.001 ||
                               flea.CategoryMultipliers.Count > 0 ||
                               modifiedCount > 0;

            StartupDisplay = new FleaStartupDisplay
            {
                HasModifiers = hasModifiers,
                BuyMultiplier = flea.GlobalBuyMultiplier,
                TaxMultiplier = flea.FleaTaxMultiplier,
                MaxOffers = flea.PlayerMaxOffers,
                DurationHours = flea.OfferDurationHours,
                BarterPercent = ragfairConfig.Dynamic.Barter.ChancePercent,
                CategoryCount = flea.CategoryMultipliers.Count,
                ModifiedPrices = modifiedCount,
                TotalPrices = _snapshotPrices!.Count,
                ExampleName = exName,
                ExampleBasePrice = exBasePrice,
                ExampleModifiedPrice = exModPrice,
                ExampleEffectiveMult = exEffMult,
                ExampleMultSource = exMultSource,
                ExampleBaseTax = exBaseTax,
                ExampleModifiedTax = exModTax,
            };
        }
    }

    /// <summary>
    /// Apply config, clear NPC offers, regenerate.
    /// Returns timing and offer count info.
    /// </summary>
    public FleaRegenerateResponse RegenerateOffers()
    {
        var sw = Stopwatch.StartNew();

        try
        {
            // Step 1: Apply globals/config + price multipliers from snapshot
            ApplyGlobalsAndConfig();

            // Step 2: Clear ALL existing offers (trader + dynamic/player)
            var allOffers = ragfairOfferService.GetOffers().ToList();
            var clearedCount = allOffers.Count;
            foreach (var offer in allOffers)
            {
                ragfairOfferService.RemoveOfferById(offer.Id);
            }
            logger.Info($"ZSlayerCC Flea: Cleared {clearedCount} total offers");

            // Step 3: Generate fresh dynamic offers (reads from modified prices dictionary)
            ragfairOfferGenerator.GenerateDynamicOffers(null);

            sw.Stop();
            var offerCount = ragfairOfferService.GetOffers().Count;
            _lastRegeneratedUtc = DateTime.UtcNow;

            logger.Success($"ZSlayerCC Flea: Regenerated {offerCount} offers in {sw.ElapsedMilliseconds}ms");

            return new FleaRegenerateResponse
            {
                Success = true,
                OfferCount = offerCount,
                DurationMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.Error($"ZSlayerCC Flea: Regeneration failed — {ex.Message}");
            return new FleaRegenerateResponse
            {
                Success = false,
                Error = ex.Message,
                DurationMs = sw.ElapsedMilliseconds
            };
        }
    }

    /// <summary>
    /// Get current flea market status.
    /// </summary>
    public FleaMarketStatus GetStatus()
    {
        var offers = ragfairOfferService.GetOffers();
        return new FleaMarketStatus
        {
            ActiveOfferCount = offers.Count,
            LastRegeneratedUtc = _lastRegeneratedUtc,
            ConfigLoaded = true
        };
    }

    /// <summary>
    /// Get debug/diagnostic info for the flea debug panel.
    /// </summary>
    public FleaDebugResponse GetDebugInfo()
    {
        var flea = fleaPriceService.GetConfig();
        var prices = databaseService.GetPrices();
        var locales = localeService.GetLocaleDb("en");

        var samples = new List<FleaDebugPriceSample>();
        foreach (var (tplId, fallbackName) in DebugSampleItems)
        {
            MongoId mongoId = tplId;
            locales.TryGetValue($"{tplId} Name", out var localeName);
            var name = !string.IsNullOrEmpty(localeName) ? localeName : fallbackName;

            var originalPrice = _snapshotPrices?.TryGetValue(mongoId, out var origVal) == true ? origVal : 0;
            var currentPrice = prices.TryGetValue(mongoId, out var curVal) ? curVal : 0;
            var (mult, source) = fleaPriceService.GetEffectiveBuyMultiplier(tplId);

            // Check actual live offers for this item
            var liveOffers = ragfairOfferService.GetOffersOfType(tplId)?
                .Where(o => o.Requirements?.Any() == true)
                .Select(o => o.RequirementsCost ?? 0)
                .Where(p => p > 0)
                .ToList() ?? [];

            samples.Add(new FleaDebugPriceSample
            {
                TemplateId = tplId,
                Name = name,
                OriginalPrice = originalPrice,
                CurrentPrice = currentPrice,
                EffectiveMultiplier = mult,
                MultiplierSource = source,
                LiveOfferCount = liveOffers.Count,
                LiveOfferMinPrice = liveOffers.Count > 0 ? liveOffers.Min() : 0,
                LiveOfferMaxPrice = liveOffers.Count > 0 ? liveOffers.Max() : 0
            });
        }

        return new FleaDebugResponse
        {
            PriceModificationActive = _snapshotTaken,
            TotalPricesInTable = prices.Count,
            ModifiedPriceCount = _lastModifiedPriceCount,
            CurrentGlobalMultiplier = flea.GlobalBuyMultiplier,
            Samples = samples
        };
    }
}

public record FleaStartupDisplay
{
    public bool HasModifiers { get; init; }
    public double BuyMultiplier { get; init; }
    public double TaxMultiplier { get; init; }
    public int MaxOffers { get; init; }
    public int DurationHours { get; init; }
    public double BarterPercent { get; init; }
    public int CategoryCount { get; init; }
    public int ModifiedPrices { get; init; }
    public int TotalPrices { get; init; }
    public string ExampleName { get; init; } = "";
    public double ExampleBasePrice { get; init; }
    public double ExampleModifiedPrice { get; init; }
    public double ExampleEffectiveMult { get; init; }
    public string ExampleMultSource { get; init; } = "";
    public double ExampleBaseTax { get; init; }
    public double ExampleModifiedTax { get; init; }
}

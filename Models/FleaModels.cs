using System.Text.Json.Serialization;

namespace ZSlayerCommandCenter.Models;

// ── Config records (persisted to config.json) ──

public record FleaConfig
{
    [JsonPropertyName("globalBuyMultiplier")]
    public double GlobalBuyMultiplier { get; set; } = 1.0;

    [JsonPropertyName("minPriceRoubles")]
    public int MinPriceRoubles { get; set; } = 1;

    [JsonPropertyName("maxPriceRoubles")]
    public int MaxPriceRoubles { get; set; } = 50_000_000;

    [JsonPropertyName("dynamicPriceVariance")]
    public double DynamicPriceVariance { get; set; } = 0.0;

    [JsonPropertyName("fleaTaxMultiplier")]
    public double FleaTaxMultiplier { get; set; } = 1.0;

    [JsonPropertyName("playerMaxOffers")]
    public int PlayerMaxOffers { get; set; } = 2;

    [JsonPropertyName("offerDurationHours")]
    public int OfferDurationHours { get; set; } = 24;

    [JsonPropertyName("restockIntervalMinutes")]
    public int RestockIntervalMinutes { get; set; } = 60;

    [JsonPropertyName("barterOffersEnabled")]
    public bool BarterOffersEnabled { get; set; } = true;

    [JsonPropertyName("barterOfferFrequency")]
    public int BarterOfferFrequency { get; set; } = 15;

    [JsonPropertyName("dollarOffersEnabled")]
    public bool DollarOffersEnabled { get; set; } = true;

    [JsonPropertyName("euroOffersEnabled")]
    public bool EuroOffersEnabled { get; set; } = true;

    [JsonPropertyName("categoryMultipliers")]
    public Dictionary<string, CategoryMultiplier> CategoryMultipliers { get; set; } = new();

    [JsonPropertyName("itemOverrides")]
    public Dictionary<string, ItemOverride> ItemOverrides { get; set; } = new();
}

public record CategoryMultiplier
{
    [JsonPropertyName("buyMultiplier")]
    public double BuyMultiplier { get; set; } = 1.0;
}

public record ItemOverride
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("buyMultiplier")]
    public double BuyMultiplier { get; set; } = 1.0;
}

// ── API request/response DTOs ──

public record FleaGlobalUpdateRequest
{
    [JsonPropertyName("buyMultiplier")]
    public double BuyMultiplier { get; set; }
}

public record FleaMarketSettingsRequest
{
    [JsonPropertyName("fleaTaxMultiplier")]
    public double FleaTaxMultiplier { get; set; }

    [JsonPropertyName("playerMaxOffers")]
    public int PlayerMaxOffers { get; set; }

    [JsonPropertyName("offerDurationHours")]
    public int OfferDurationHours { get; set; }

    [JsonPropertyName("restockIntervalMinutes")]
    public int RestockIntervalMinutes { get; set; }

    [JsonPropertyName("barterOffersEnabled")]
    public bool BarterOffersEnabled { get; set; }

    [JsonPropertyName("barterOfferFrequency")]
    public int BarterOfferFrequency { get; set; }

    [JsonPropertyName("dollarOffersEnabled")]
    public bool DollarOffersEnabled { get; set; } = true;

    [JsonPropertyName("euroOffersEnabled")]
    public bool EuroOffersEnabled { get; set; } = true;
}

public record FleaCategoryUpdateRequest
{
    [JsonPropertyName("category")]
    public string Category { get; set; } = "";

    [JsonPropertyName("buyMultiplier")]
    public double BuyMultiplier { get; set; }
}

public record FleaItemOverrideRequest
{
    [JsonPropertyName("templateId")]
    public string TemplateId { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("buyMultiplier")]
    public double BuyMultiplier { get; set; }
}

public record FleaPricePreview
{
    [JsonPropertyName("templateId")]
    public string TemplateId { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("basePrice")]
    public int BasePrice { get; set; }

    [JsonPropertyName("effectiveBuyPrice")]
    public int EffectiveBuyPrice { get; set; }

    [JsonPropertyName("appliedLevel")]
    public string AppliedLevel { get; set; } = "global";
}

public record FleaMarketStatus
{
    [JsonPropertyName("activeOfferCount")]
    public int ActiveOfferCount { get; set; }

    [JsonPropertyName("lastRegeneratedUtc")]
    public DateTime? LastRegeneratedUtc { get; set; }

    [JsonPropertyName("configLoaded")]
    public bool ConfigLoaded { get; set; }
}

public record FleaCategoryInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("key")]
    public string Key { get; set; } = "";

    [JsonPropertyName("itemCount")]
    public int ItemCount { get; set; }

    [JsonPropertyName("baseClassIds")]
    public List<string> BaseClassIds { get; set; } = [];

    [JsonPropertyName("currentBuyMultiplier")]
    public double CurrentBuyMultiplier { get; set; } = 1.0;
}

public record FleaRegenerateResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("offerCount")]
    public int OfferCount { get; set; }

    [JsonPropertyName("durationMs")]
    public long DurationMs { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    /// <summary>Fake-player offers removed and rebuilt by this regeneration.</summary>
    [JsonPropertyName("removedFakeOffers")]
    public int RemovedFakeOffers { get; set; }

    /// <summary>Player, trader and unattributed offers left untouched by this regeneration.</summary>
    [JsonPropertyName("preservedOffers")]
    public int PreservedOffers { get; set; }

    /// <summary>Did any accompanying config change reach disk. True when there was nothing to persist.</summary>
    [JsonPropertyName("persisted")]
    public bool Persisted { get; set; } = true;

    /// <summary>Did the live market finish rebuilding. Same value as <see cref="Success"/>, which is kept as the legacy alias.</summary>
    [JsonPropertyName("applied")]
    public bool Applied { get; set; }

    /// <summary>One of <see cref="FleaMarketStates"/>.</summary>
    [JsonPropertyName("marketState")]
    public string MarketState { get; set; } = FleaMarketStates.Ok;

    /// <summary>What the operator should do next, when anything other than "ok" happened.</summary>
    [JsonPropertyName("guidance")]
    public string? Guidance { get; set; }
}

/// <summary>
/// What the live flea market looks like after a route ran.
/// Deliberately not an enum: it is a wire value read by the dashboard and third-party callers.
/// </summary>
public static class FleaMarketStates
{
    /// <summary>The market matches the persisted config.</summary>
    public const string Ok = "ok";

    /// <summary>Nothing touched the market — a rejected or skipped regeneration.</summary>
    public const string Unchanged = "unchanged";

    /// <summary>
    /// A regeneration failed partway. Globals, prices and the offer holder may each be
    /// half-updated. There is no honest rollback: RegenerateOffers destroys holder state
    /// before a failure can be returned, so reverting the config alone would misreport.
    /// </summary>
    public const string PartialOrUnknown = "partial-or-unknown";
}

/// <summary>
/// The uniform result contract for flea routes that persist config, mutate live state, or both.
/// </summary>
public record FleaRouteResult
{
    [JsonPropertyName("persisted")]
    public bool Persisted { get; set; }

    [JsonPropertyName("applied")]
    public bool Applied { get; set; }

    [JsonPropertyName("marketState")]
    public string MarketState { get; set; } = FleaMarketStates.Ok;

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("guidance")]
    public string? Guidance { get; set; }

    /// <summary>The resulting flea config, on routes that return one.</summary>
    [JsonPropertyName("config")]
    public FleaConfig? Config { get; set; }

    /// <summary>Regeneration detail, on routes that regenerate.</summary>
    [JsonPropertyName("regeneration")]
    public FleaRegenerateResponse? Regeneration { get; set; }

    /// <summary>Whether the requested change actually altered anything, on delete-style routes.</summary>
    [JsonPropertyName("changed")]
    public bool? Changed { get; set; }
}

public record FleaItemSearchResult
{
    [JsonPropertyName("templateId")]
    public string TemplateId { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("shortName")]
    public string ShortName { get; set; } = "";

    [JsonPropertyName("basePrice")]
    public int BasePrice { get; set; }

    [JsonPropertyName("category")]
    public string Category { get; set; } = "";
}

public record FleaItemSearchResponse
{
    [JsonPropertyName("items")]
    public List<FleaItemSearchResult> Items { get; set; } = [];
}

public record FleaCategoryListResponse
{
    [JsonPropertyName("categories")]
    public List<FleaCategoryInfo> Categories { get; set; } = [];
}

// ── Debug DTOs ──

public record FleaDebugResponse
{
    [JsonPropertyName("priceModificationActive")]
    public bool PriceModificationActive { get; set; }

    [JsonPropertyName("totalPricesInTable")]
    public int TotalPricesInTable { get; set; }

    [JsonPropertyName("modifiedPriceCount")]
    public int ModifiedPriceCount { get; set; }

    [JsonPropertyName("currentGlobalMultiplier")]
    public double CurrentGlobalMultiplier { get; set; }

    [JsonPropertyName("samples")]
    public List<FleaDebugPriceSample> Samples { get; set; } = [];
}

public record FleaDebugPriceSample
{
    [JsonPropertyName("templateId")]
    public string TemplateId { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("originalPrice")]
    public double OriginalPrice { get; set; }

    [JsonPropertyName("currentPrice")]
    public double CurrentPrice { get; set; }

    [JsonPropertyName("effectiveMultiplier")]
    public double EffectiveMultiplier { get; set; }

    [JsonPropertyName("multiplierSource")]
    public string MultiplierSource { get; set; } = "global";

    [JsonPropertyName("liveOfferCount")]
    public int LiveOfferCount { get; set; }

    [JsonPropertyName("liveOfferMinPrice")]
    public double LiveOfferMinPrice { get; set; }

    [JsonPropertyName("liveOfferMaxPrice")]
    public double LiveOfferMaxPrice { get; set; }
}

// ── Preset DTOs ──

public record FleaPreset
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("createdUtc")]
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("config")]
    public FleaConfig Config { get; set; } = new();
}

public record FleaPresetSummary
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("createdUtc")]
    public DateTime CreatedUtc { get; set; }
}

public record FleaSavePresetRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";
}

public record FleaLoadPresetRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}

public record FleaPresetListResponse
{
    [JsonPropertyName("presets")]
    public List<FleaPresetSummary> Presets { get; set; } = [];

    [JsonPropertyName("activePreset")]
    public string? ActivePreset { get; set; }
}

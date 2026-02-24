using System.Text.Json.Serialization;

namespace ZSlayerCommandCenter.Models;

// ── Config records (persisted to config.json) ──

public record TraderControlConfig
{
    [JsonPropertyName("globalBuyMultiplier")]
    public double GlobalBuyMultiplier { get; set; } = 1.0;

    [JsonPropertyName("globalSellMultiplier")]
    public double GlobalSellMultiplier { get; set; } = 1.0;

    [JsonPropertyName("minPriceRoubles")]
    public int MinPriceRoubles { get; set; } = 1;

    [JsonPropertyName("maxPriceRoubles")]
    public int MaxPriceRoubles { get; set; } = 50_000_000;

    [JsonPropertyName("globalStockMultiplier")]
    public double GlobalStockMultiplier { get; set; } = 1.0;

    [JsonPropertyName("globalRestockMinSeconds")]
    public int? GlobalRestockMinSeconds { get; set; }

    [JsonPropertyName("globalRestockMaxSeconds")]
    public int? GlobalRestockMaxSeconds { get; set; }

    [JsonPropertyName("globalLoyaltyLevelShift")]
    public int GlobalLoyaltyLevelShift { get; set; } = 0;

    [JsonPropertyName("forceCurrency")]
    public string? ForceCurrency { get; set; }

    [JsonPropertyName("traderOverrides")]
    public Dictionary<string, TraderOverride> TraderOverrides { get; set; } = new();
}

public record TraderOverride
{
    [JsonPropertyName("traderName")]
    public string TraderName { get; set; } = "";

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("buyMultiplier")]
    public double BuyMultiplier { get; set; } = 1.0;

    [JsonPropertyName("sellMultiplier")]
    public double SellMultiplier { get; set; } = 1.0;

    [JsonPropertyName("stockMultiplier")]
    public double StockMultiplier { get; set; } = 1.0;

    [JsonPropertyName("restockMinSeconds")]
    public int? RestockMinSeconds { get; set; }

    [JsonPropertyName("restockMaxSeconds")]
    public int? RestockMaxSeconds { get; set; }

    [JsonPropertyName("loyaltyLevelShift")]
    public int LoyaltyLevelShift { get; set; } = 0;

    [JsonPropertyName("forceCurrency")]
    public string? ForceCurrency { get; set; }

    [JsonPropertyName("disabledItems")]
    public List<string> DisabledItems { get; set; } = [];

    [JsonPropertyName("itemOverrides")]
    public Dictionary<string, TraderItemOverride> ItemOverrides { get; set; } = new();
}

public record TraderItemOverride
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("buyMultiplier")]
    public double BuyMultiplier { get; set; } = 1.0;

    [JsonPropertyName("sellMultiplier")]
    public double SellMultiplier { get; set; } = 1.0;
}

// ── Discovery & snapshot DTOs ──

public record TraderSummary
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("nickname")]
    public string Nickname { get; set; } = "";

    [JsonPropertyName("fullName")]
    public string FullName { get; set; } = "";

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "RUB";

    [JsonPropertyName("loyaltyLevelCount")]
    public int LoyaltyLevelCount { get; set; }

    [JsonPropertyName("itemCount")]
    public int ItemCount { get; set; }

    [JsonPropertyName("isModded")]
    public bool IsModded { get; set; }

    [JsonPropertyName("hasOverride")]
    public bool HasOverride { get; set; }

    [JsonPropertyName("currentBuyMultiplier")]
    public double CurrentBuyMultiplier { get; set; } = 1.0;

    [JsonPropertyName("currentSellMultiplier")]
    public double CurrentSellMultiplier { get; set; } = 1.0;

    [JsonPropertyName("currentStockMultiplier")]
    public double CurrentStockMultiplier { get; set; } = 1.0;

    [JsonPropertyName("restockMinSeconds")]
    public int? RestockMinSeconds { get; set; }

    [JsonPropertyName("restockMaxSeconds")]
    public int? RestockMaxSeconds { get; set; }

    [JsonPropertyName("avatarUrl")]
    public string? AvatarUrl { get; set; }

    [JsonPropertyName("location")]
    public string? Location { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

/// <summary>Deep-copy snapshot of trader data for restore-from-snapshot pattern.</summary>
public class TraderSnapshot
{
    /// <summary>Serialized JSON of the original BarterScheme dict.</summary>
    public string BarterSchemeJson { get; set; } = "";

    /// <summary>Serialized JSON of the original LoyalLevelItems dict.</summary>
    public string LoyalLevelItemsJson { get; set; } = "";

    /// <summary>Original stock counts: item ID → StackObjectsCount.</summary>
    public Dictionary<string, double> StockCounts { get; set; } = new();

    /// <summary>Original buy_price_coef per loyalty level index.</summary>
    public List<double> BuyPriceCoefs { get; set; } = [];

    /// <summary>Serialized JSON of the original Items list (for disabled item restore).</summary>
    public string ItemsJson { get; set; } = "";

    /// <summary>Original currency type.</summary>
    public string OriginalCurrency { get; set; } = "RUB";
}

// ── API request/response DTOs ──

public record TraderItemInfo
{
    [JsonPropertyName("itemId")]
    public string ItemId { get; set; } = "";

    [JsonPropertyName("templateId")]
    public string TemplateId { get; set; } = "";

    [JsonPropertyName("shortName")]
    public string ShortName { get; set; } = "";

    [JsonPropertyName("fullName")]
    public string FullName { get; set; } = "";

    [JsonPropertyName("basePrice")]
    public double BasePrice { get; set; }

    [JsonPropertyName("modifiedPrice")]
    public double ModifiedPrice { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "RUB";

    [JsonPropertyName("loyaltyLevel")]
    public int LoyaltyLevel { get; set; }

    [JsonPropertyName("originalLoyaltyLevel")]
    public int OriginalLoyaltyLevel { get; set; }

    [JsonPropertyName("stock")]
    public double Stock { get; set; }

    [JsonPropertyName("originalStock")]
    public double OriginalStock { get; set; }

    [JsonPropertyName("isBarter")]
    public bool IsBarter { get; set; }

    [JsonPropertyName("isDisabled")]
    public bool IsDisabled { get; set; }

    [JsonPropertyName("hasOverride")]
    public bool HasOverride { get; set; }

    [JsonPropertyName("effectiveMultiplier")]
    public double EffectiveMultiplier { get; set; } = 1.0;
}

public record TraderApplyResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("tradersAffected")]
    public int TradersAffected { get; set; }

    [JsonPropertyName("itemsModified")]
    public int ItemsModified { get; set; }

    [JsonPropertyName("applyTimeMs")]
    public long ApplyTimeMs { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

public record TraderStatusResponse
{
    [JsonPropertyName("modVersion")]
    public string ModVersion { get; set; } = "";

    [JsonPropertyName("traderCount")]
    public int TraderCount { get; set; }

    [JsonPropertyName("vanillaTraders")]
    public int VanillaTraders { get; set; }

    [JsonPropertyName("moddedTraders")]
    public int ModdedTraders { get; set; }

    [JsonPropertyName("totalItems")]
    public int TotalItems { get; set; }

    [JsonPropertyName("configApplied")]
    public bool ConfigApplied { get; set; }

    [JsonPropertyName("lastApplyTimeMs")]
    public long? LastApplyTimeMs { get; set; }
}

public record TraderItemListResponse
{
    [JsonPropertyName("items")]
    public List<TraderItemInfo> Items { get; set; } = [];

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("limit")]
    public int Limit { get; set; }

    [JsonPropertyName("offset")]
    public int Offset { get; set; }
}

// ── API request DTOs ──

public record TraderGlobalUpdateRequest
{
    [JsonPropertyName("globalBuyMultiplier")]
    public double GlobalBuyMultiplier { get; set; } = 1.0;

    [JsonPropertyName("globalSellMultiplier")]
    public double GlobalSellMultiplier { get; set; } = 1.0;

    [JsonPropertyName("globalStockMultiplier")]
    public double GlobalStockMultiplier { get; set; } = 1.0;

    [JsonPropertyName("globalRestockMinSeconds")]
    public int? GlobalRestockMinSeconds { get; set; }

    [JsonPropertyName("globalRestockMaxSeconds")]
    public int? GlobalRestockMaxSeconds { get; set; }

    [JsonPropertyName("globalLoyaltyLevelShift")]
    public int GlobalLoyaltyLevelShift { get; set; }

    [JsonPropertyName("forceCurrency")]
    public string? ForceCurrency { get; set; }

    [JsonPropertyName("minPriceRoubles")]
    public int MinPriceRoubles { get; set; } = 1;

    [JsonPropertyName("maxPriceRoubles")]
    public int MaxPriceRoubles { get; set; } = 50_000_000;
}

public record TraderOverrideUpdateRequest
{
    [JsonPropertyName("traderId")]
    public string TraderId { get; set; } = "";

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("buyMultiplier")]
    public double BuyMultiplier { get; set; } = 1.0;

    [JsonPropertyName("sellMultiplier")]
    public double SellMultiplier { get; set; } = 1.0;

    [JsonPropertyName("stockMultiplier")]
    public double StockMultiplier { get; set; } = 1.0;

    [JsonPropertyName("restockMinSeconds")]
    public int? RestockMinSeconds { get; set; }

    [JsonPropertyName("restockMaxSeconds")]
    public int? RestockMaxSeconds { get; set; }

    [JsonPropertyName("loyaltyLevelShift")]
    public int LoyaltyLevelShift { get; set; }

    [JsonPropertyName("forceCurrency")]
    public string? ForceCurrency { get; set; }

    [JsonPropertyName("disabledItems")]
    public List<string>? DisabledItems { get; set; }
}

public record TraderItemOverrideRequest
{
    [JsonPropertyName("traderId")]
    public string TraderId { get; set; } = "";

    [JsonPropertyName("templateId")]
    public string TemplateId { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("buyMultiplier")]
    public double BuyMultiplier { get; set; } = 1.0;

    [JsonPropertyName("sellMultiplier")]
    public double SellMultiplier { get; set; } = 1.0;
}

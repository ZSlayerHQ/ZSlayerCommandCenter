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

    [JsonPropertyName("globalStockCap")]
    public int? GlobalStockCap { get; set; }

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

    [JsonPropertyName("traderDisplayOverrides")]
    public Dictionary<string, TraderDisplayOverride> TraderDisplayOverrides { get; set; } = new();
}

public record TraderDisplayOverride
{
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("customDescription")]
    public string? CustomDescription { get; set; }

    [JsonPropertyName("customAvatar")]
    public string? CustomAvatar { get; set; }  // filename in res/Trader Icons/
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

    [JsonPropertyName("originalNickname")]
    public string? OriginalNickname { get; set; }

    [JsonPropertyName("originalAvatarUrl")]
    public string? OriginalAvatarUrl { get; set; }

    [JsonPropertyName("hasDisplayOverride")]
    public bool HasDisplayOverride { get; set; }

    [JsonPropertyName("location")]
    public string? Location { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("originalDescription")]
    public string? OriginalDescription { get; set; }
}

/// <summary>In-place snapshot of trader data for restore-from-snapshot pattern.</summary>
public class TraderSnapshot
{
    /// <summary>Original stock counts: item ID → StackObjectsCount.</summary>
    public Dictionary<string, double> StockCounts { get; set; } = new();

    /// <summary>Original buy_price_coef per loyalty level index.</summary>
    public List<double> BuyPriceCoefs { get; set; } = [];

    /// <summary>Original currency type.</summary>
    public string OriginalCurrency { get; set; } = "RUB";

    /// <summary>Original barter costs: itemId → list of payment options → list of (templateId, count).</summary>
    public Dictionary<string, List<List<BarterCostSnapshot>>> BarterCosts { get; set; } = new();

    /// <summary>Original loyalty level items: itemId → loyalty level.</summary>
    public Dictionary<string, int> LoyaltyLevels { get; set; } = new();
}

/// <summary>Snapshot of a single barter cost entry (template + count).</summary>
public record BarterCostSnapshot(string Template, double? Count);

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

    [JsonPropertyName("globalStockCap")]
    public int? GlobalStockCap { get; set; }

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

public record TraderDisplayUpdateRequest
{
    [JsonPropertyName("traderId")]
    public string TraderId { get; set; } = "";

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

public record TraderAvatarUploadRequest
{
    [JsonPropertyName("traderId")]
    public string TraderId { get; set; } = "";

    [JsonPropertyName("imageBase64")]
    public string ImageBase64 { get; set; } = "";
}

// ── Preset DTOs ──

/// <summary>Gameplay-only config snapshot (excludes display overrides).</summary>
public record TraderGameplayConfig
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

    [JsonPropertyName("globalStockCap")]
    public int? GlobalStockCap { get; set; }

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

public record TraderPreset
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("createdUtc")]
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("config")]
    public TraderGameplayConfig Config { get; set; } = new();
}

public record TraderPresetSummary
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("createdUtc")]
    public DateTime CreatedUtc { get; set; }
}

public record TraderPresetListResponse
{
    [JsonPropertyName("presets")]
    public List<TraderPresetSummary> Presets { get; set; } = [];
}

public record TraderPresetSaveRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";
}

public record TraderPresetLoadRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}

public record TraderPresetUploadRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("presetJson")]
    public string PresetJson { get; set; } = "";
}

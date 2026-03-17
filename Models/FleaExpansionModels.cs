using System.Text.Json.Serialization;

namespace ZSlayerCommandCenter.Models;

// ═══════════════════════════════════════════════════════
// FLEA MARKET EXPANSION CONFIG (persisted to config.json)
// ═══════════════════════════════════════════════════════

public record FleaExpansionConfig
{
    // ── D. FIR & Sell Controls ──
    [JsonPropertyName("enableFleaExpansion")] public bool EnableFleaExpansion { get; set; }
    [JsonPropertyName("allowNonFirSales")] public bool AllowNonFirSales { get; set; }
    [JsonPropertyName("purchasesAreFir")] public bool? PurchasesAreFir { get; set; }
    [JsonPropertyName("enableFees")] public bool? EnableFees { get; set; }
    [JsonPropertyName("sellChanceBase")] public int? SellChanceBase { get; set; }
    [JsonPropertyName("sellChanceMult")] public double? SellChanceMult { get; set; }
    [JsonPropertyName("repGainPerSale")] public double? RepGainPerSale { get; set; }
    [JsonPropertyName("repLossPerCancel")] public double? RepLossPerCancel { get; set; }
    [JsonPropertyName("tieredFleaEnabled")] public bool? TieredFleaEnabled { get; set; }
    [JsonPropertyName("removeSeasonalItems")] public bool? RemoveSeasonalItems { get; set; }

    // ── E. Dynamic Offer Configuration ──
    [JsonPropertyName("barterChance")] public double? BarterChance { get; set; }
    [JsonPropertyName("bundleChance")] public double? BundleChance { get; set; }
    [JsonPropertyName("expiredOfferThreshold")] public int? ExpiredOfferThreshold { get; set; }
    [JsonPropertyName("offerItemCountMin")] public int? OfferItemCountMin { get; set; }
    [JsonPropertyName("offerItemCountMax")] public int? OfferItemCountMax { get; set; }
    [JsonPropertyName("priceRangeDefaultMin")] public double? PriceRangeDefaultMin { get; set; }
    [JsonPropertyName("priceRangeDefaultMax")] public double? PriceRangeDefaultMax { get; set; }
    [JsonPropertyName("priceRangePresetMin")] public double? PriceRangePresetMin { get; set; }
    [JsonPropertyName("priceRangePresetMax")] public double? PriceRangePresetMax { get; set; }
    [JsonPropertyName("priceRangePackMin")] public double? PriceRangePackMin { get; set; }
    [JsonPropertyName("priceRangePackMax")] public double? PriceRangePackMax { get; set; }
    [JsonPropertyName("offerDurationMin")] public int? OfferDurationMin { get; set; }
    [JsonPropertyName("offerDurationMax")] public int? OfferDurationMax { get; set; }
    [JsonPropertyName("nonStackableCountMin")] public int? NonStackableCountMin { get; set; }
    [JsonPropertyName("nonStackableCountMax")] public int? NonStackableCountMax { get; set; }
    [JsonPropertyName("stackablePercentMin")] public double? StackablePercentMin { get; set; }
    [JsonPropertyName("stackablePercentMax")] public double? StackablePercentMax { get; set; }
    [JsonPropertyName("currencyRatioRub")] public double? CurrencyRatioRub { get; set; }
    [JsonPropertyName("currencyRatioUsd")] public double? CurrencyRatioUsd { get; set; }
    [JsonPropertyName("currencyRatioEur")] public double? CurrencyRatioEur { get; set; }

    // ── Flea Globals ──
    [JsonPropertyName("minUserLevel")] public int? MinUserLevel { get; set; }
    [JsonPropertyName("maxActiveOfferCount")] public double? MaxActiveOfferCount { get; set; }

    // ── G. Blacklist Control ──
    [JsonPropertyName("disableFleaBlacklist")] public bool? DisableFleaBlacklist { get; set; }

    // ── F. Per-Category Conditions ──
    [JsonPropertyName("categoryConditions")] public Dictionary<string, CategoryConditionEntry>? CategoryConditions { get; set; }
}

public record CategoryConditionEntry
{
    [JsonPropertyName("conditionMin")] public double? ConditionMin { get; set; }
    [JsonPropertyName("conditionMax")] public double? ConditionMax { get; set; }
}

// ═══════════════════════════════════════════════════════
// RESPONSE DTOs
// ═══════════════════════════════════════════════════════

public record FleaExpansionConfigResponse
{
    [JsonPropertyName("config")] public FleaExpansionConfig Config { get; set; } = new();
    [JsonPropertyName("defaults")] public FleaExpansionDefaults Defaults { get; set; } = new();

    /// <summary>Fields that modify globals cached by the client — players must restart client to see changes.</summary>
    [JsonPropertyName("clientRestartFields")] public List<string> ClientRestartFields { get; set; } =
    [
        "minUserLevel",
        "maxActiveOfferCount",
        "allowNonFirSales",
        "repGainPerSale",
        "repLossPerCancel"
    ];
}

public record FleaExpansionDefaults
{
    // D
    [JsonPropertyName("isOnlyFirAllowed")] public bool IsOnlyFirAllowed { get; set; }
    [JsonPropertyName("purchasesAreFir")] public bool PurchasesAreFir { get; set; }
    [JsonPropertyName("feesEnabled")] public bool FeesEnabled { get; set; }
    [JsonPropertyName("sellChanceBase")] public int SellChanceBase { get; set; }
    [JsonPropertyName("sellChanceMult")] public double SellChanceMult { get; set; }
    [JsonPropertyName("repGainPerSale")] public double RepGainPerSale { get; set; }
    [JsonPropertyName("repLossPerCancel")] public double RepLossPerCancel { get; set; }
    [JsonPropertyName("tieredFleaEnabled")] public bool TieredFleaEnabled { get; set; }
    [JsonPropertyName("removeSeasonalItems")] public bool RemoveSeasonalItems { get; set; }

    // Flea Globals
    [JsonPropertyName("minUserLevel")] public int MinUserLevel { get; set; }
    [JsonPropertyName("maxActiveOfferCount")] public double MaxActiveOfferCount { get; set; }

    // E
    [JsonPropertyName("barterChance")] public double BarterChance { get; set; }
    [JsonPropertyName("bundleChance")] public double BundleChance { get; set; }
    [JsonPropertyName("expiredOfferThreshold")] public int ExpiredOfferThreshold { get; set; }
    [JsonPropertyName("offerItemCountMin")] public int OfferItemCountMin { get; set; }
    [JsonPropertyName("offerItemCountMax")] public int OfferItemCountMax { get; set; }
    [JsonPropertyName("priceRangeDefaultMin")] public double PriceRangeDefaultMin { get; set; }
    [JsonPropertyName("priceRangeDefaultMax")] public double PriceRangeDefaultMax { get; set; }
    [JsonPropertyName("priceRangePresetMin")] public double PriceRangePresetMin { get; set; }
    [JsonPropertyName("priceRangePresetMax")] public double PriceRangePresetMax { get; set; }
    [JsonPropertyName("priceRangePackMin")] public double PriceRangePackMin { get; set; }
    [JsonPropertyName("priceRangePackMax")] public double PriceRangePackMax { get; set; }
    [JsonPropertyName("offerDurationMin")] public int OfferDurationMin { get; set; }
    [JsonPropertyName("offerDurationMax")] public int OfferDurationMax { get; set; }
    [JsonPropertyName("nonStackableCountMin")] public int NonStackableCountMin { get; set; }
    [JsonPropertyName("nonStackableCountMax")] public int NonStackableCountMax { get; set; }
    [JsonPropertyName("stackablePercentMin")] public double StackablePercentMin { get; set; }
    [JsonPropertyName("stackablePercentMax")] public double StackablePercentMax { get; set; }
    [JsonPropertyName("currencyRatioRub")] public double CurrencyRatioRub { get; set; }
    [JsonPropertyName("currencyRatioUsd")] public double CurrencyRatioUsd { get; set; }
    [JsonPropertyName("currencyRatioEur")] public double CurrencyRatioEur { get; set; }

    // F
    [JsonPropertyName("categories")] public List<CategoryConditionDefaults> Categories { get; set; } = [];

    // G
    [JsonPropertyName("enableBsgList")] public bool EnableBsgList { get; set; }
    [JsonPropertyName("enableQuestList")] public bool EnableQuestList { get; set; }
    [JsonPropertyName("enableCustomItemCategoryList")] public bool EnableCustomItemCategoryList { get; set; }
    [JsonPropertyName("traderItems")] public bool TraderItems { get; set; }
}

public record CategoryConditionDefaults
{
    [JsonPropertyName("parentId")] public string ParentId { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("conditionMin")] public double ConditionMin { get; set; }
    [JsonPropertyName("conditionMax")] public double ConditionMax { get; set; }
}

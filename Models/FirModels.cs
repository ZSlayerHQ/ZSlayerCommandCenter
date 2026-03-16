using System.Text.Json.Serialization;

namespace ZSlayerCommandCenter.Models;

// ═══════════════════════════════════════════════════════════════
//  CONFIG (persisted to config.json under "fir")
// ═══════════════════════════════════════════════════════════════

public record FirConfig
{
    /// <summary>Flea market purchases arrive marked as Found in Raid.</summary>
    [JsonPropertyName("fleaPurchasesFir")]
    public bool FleaPurchasesFir { get; set; } = false;

    /// <summary>Trader purchases arrive marked as Found in Raid.</summary>
    [JsonPropertyName("traderPurchasesFir")]
    public bool TraderPurchasesFir { get; set; } = false;

    /// <summary>Non-currency barter items require functional (undamaged) condition.</summary>
    [JsonPropertyName("bartersRequireFunctional")]
    public bool BartersRequireFunctional { get; set; } = false;

    /// <summary>Hideout construction item requirements must be FIR.</summary>
    [JsonPropertyName("hideoutConstructionFir")]
    public bool HideoutConstructionFir { get; set; } = false;

    /// <summary>Non-currency items used in trader barters must be FIR.</summary>
    [JsonPropertyName("barterItemsMustBeFir")]
    public bool BarterItemsMustBeFir { get; set; } = false;

    /// <summary>Items sold to traders must be FIR.</summary>
    [JsonPropertyName("sellToTraderRequiresFir")]
    public bool SellToTraderRequiresFir { get; set; } = false;

    /// <summary>Percentage price reduction when selling non-FIR items to traders (0 = disabled, 100 = worthless).</summary>
    [JsonPropertyName("nonFirSellPenaltyPercent")]
    public int NonFirSellPenaltyPercent { get; set; } = 0;

    /// <summary>Enable global purchase quantity limit per item per trader reset cycle.</summary>
    [JsonPropertyName("purchaseLimitEnabled")]
    public bool PurchaseLimitEnabled { get; set; } = false;

    /// <summary>Max units of any single item purchasable per trader reset (0 = unlimited).</summary>
    [JsonPropertyName("purchaseLimitPerReset")]
    public int PurchaseLimitPerReset { get; set; } = 5;

    /// <summary>Percentage of stash rubles lost on death (0 = disabled, 100 = lose all).</summary>
    [JsonPropertyName("deathTaxPercent")]
    public int DeathTaxPercent { get; set; } = 0;
}

// ═══════════════════════════════════════════════════════════════
//  API RESPONSE MODELS
// ═══════════════════════════════════════════════════════════════

public record FirStatusResponse
{
    [JsonPropertyName("config")]
    public FirConfig Config { get; set; } = new();
}

public record FirApplyResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("changes")]
    public List<string> Changes { get; set; } = [];
}

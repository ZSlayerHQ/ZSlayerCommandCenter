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

    /// <summary>On death, lose all items in secure container except keys and cases.</summary>
    [JsonPropertyName("secureContainerWipeOnDeath")]
    public bool SecureContainerWipeOnDeath { get; set; } = false;

    // ── Global Gameplay Toggles ──

    /// <summary>All traders have unlimited stock.</summary>
    [JsonPropertyName("tradingUnlimitedItems")]
    public bool? TradingUnlimitedItems { get; set; }

    /// <summary>Unlock all trader loyalty levels for all players.</summary>
    [JsonPropertyName("maxLoyaltyLevelForAll")]
    public bool? MaxLoyaltyLevelForAll { get; set; }

    /// <summary>Disable in-raid item discard limits.</summary>
    [JsonPropertyName("discardLimitsDisabled")]
    public bool? DiscardLimitsDisabled { get; set; }

    /// <summary>Disable skill decay over time.</summary>
    [JsonPropertyName("skillAtrophyDisabled")]
    public bool? SkillAtrophyDisabled { get; set; }

    /// <summary>Enable level-gated flea market (items unlock at certain player levels).</summary>
    [JsonPropertyName("tieredFleaEnabled")]
    public bool? TieredFleaEnabled { get; set; }

    // ── Lost on Death ──

    /// <summary>Override lost-on-death settings. Null = use server default.</summary>
    [JsonPropertyName("lostOnDeath")]
    public LostOnDeathOverride? LostOnDeath { get; set; }
}

public record LostOnDeathOverride
{
    [JsonPropertyName("enabled")] public bool Enabled { get; set; } = false;
    [JsonPropertyName("headwear")] public bool Headwear { get; set; } = true;
    [JsonPropertyName("earpiece")] public bool Earpiece { get; set; } = true;
    [JsonPropertyName("faceCover")] public bool FaceCover { get; set; } = true;
    [JsonPropertyName("armorVest")] public bool ArmorVest { get; set; } = true;
    [JsonPropertyName("eyewear")] public bool Eyewear { get; set; } = true;
    [JsonPropertyName("tacticalVest")] public bool TacticalVest { get; set; } = true;
    [JsonPropertyName("pocketItems")] public bool PocketItems { get; set; } = true;
    [JsonPropertyName("backpack")] public bool Backpack { get; set; } = true;
    [JsonPropertyName("holster")] public bool Holster { get; set; } = true;
    [JsonPropertyName("firstPrimaryWeapon")] public bool FirstPrimaryWeapon { get; set; } = true;
    [JsonPropertyName("secondPrimaryWeapon")] public bool SecondPrimaryWeapon { get; set; } = true;
    [JsonPropertyName("scabbard")] public bool Scabbard { get; set; } = false;
    [JsonPropertyName("armBand")] public bool ArmBand { get; set; } = false;
    [JsonPropertyName("compass")] public bool Compass { get; set; } = false;
    [JsonPropertyName("securedContainer")] public bool SecuredContainer { get; set; } = false;
    [JsonPropertyName("questItems")] public bool QuestItems { get; set; } = true;
    [JsonPropertyName("specialSlotItems")] public bool SpecialSlotItems { get; set; } = false;
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

using System.Text.Json.Serialization;

namespace ZSlayerCommandCenter.Models;

// ═══════════════════════════════════════════════════════
// MISC TOGGLES CONFIG (persisted to config.json)
// ═══════════════════════════════════════════════════════

public record MiscToggleConfig
{
    // ── J. Chatbot & Message Controls ──
    [JsonPropertyName("disableCommando")] public bool DisableCommando { get; set; }
    [JsonPropertyName("disableSptFriend")] public bool DisableSptFriend { get; set; }
    [JsonPropertyName("disablePmcKillMessages")] public bool DisablePmcKillMessages { get; set; }

    // ── K. Trader Shortcuts ──
    [JsonPropertyName("allTradersLl4")] public bool AllTradersLl4 { get; set; }
    [JsonPropertyName("unlockJaeger")] public bool UnlockJaeger { get; set; }
    [JsonPropertyName("unlockRef")] public bool UnlockRef { get; set; }
    [JsonPropertyName("unlockQuestAssorts")] public bool UnlockQuestAssorts { get; set; }
    [JsonPropertyName("traderPurchasesFir")] public bool? TraderPurchasesFir { get; set; }
    [JsonPropertyName("questRedeemTimeDefault")] public double? QuestRedeemTimeDefault { get; set; }
    [JsonPropertyName("questRedeemTimeUnheard")] public double? QuestRedeemTimeUnheard { get; set; }
    [JsonPropertyName("questPlantTimeMult")] public double? QuestPlantTimeMult { get; set; }

    // ── L. Quest Availability Shortcuts ──
    [JsonPropertyName("allQuestsAvailable")] public bool AllQuestsAvailable { get; set; }
    [JsonPropertyName("removeQuestTimeConditions")] public bool RemoveQuestTimeConditions { get; set; }
    [JsonPropertyName("removeQuestFirReqs")] public bool RemoveQuestFirReqs { get; set; }

    // ── M. Fun Mode Toggles ──
    [JsonPropertyName("noWeaponMalfunctions")] public bool NoWeaponMalfunctions { get; set; }
    [JsonPropertyName("unlimitedStamina")] public bool UnlimitedStamina { get; set; }
    [JsonPropertyName("noFallDamage")] public bool NoFallDamage { get; set; }
    [JsonPropertyName("noSkillFatigue")] public bool NoSkillFatigue { get; set; }

    // ── N. Trader & Economy Tweaks ──
    [JsonPropertyName("minDurabilityToSell")] public double? MinDurabilityToSell { get; set; }
    [JsonPropertyName("lightKeeperAccessTime")] public double? LightKeeperAccessTime { get; set; }
    [JsonPropertyName("lightKeeperKickNotifTime")] public double? LightKeeperKickNotifTime { get; set; }

    // ── O. Fence Controls ──
    [JsonPropertyName("fenceAssortSize")] public int? FenceAssortSize { get; set; }
    [JsonPropertyName("fenceWeaponPresetMin")] public int? FenceWeaponPresetMin { get; set; }
    [JsonPropertyName("fenceWeaponPresetMax")] public int? FenceWeaponPresetMax { get; set; }
    [JsonPropertyName("fenceItemPriceMult")] public double? FenceItemPriceMult { get; set; }
    [JsonPropertyName("fencePresetPriceMult")] public double? FencePresetPriceMult { get; set; }
    [JsonPropertyName("fenceWeaponDurabilityMin")] public double? FenceWeaponDurabilityMin { get; set; }
    [JsonPropertyName("fenceWeaponDurabilityMax")] public double? FenceWeaponDurabilityMax { get; set; }
    [JsonPropertyName("fenceArmorDurabilityMin")] public double? FenceArmorDurabilityMin { get; set; }
    [JsonPropertyName("fenceArmorDurabilityMax")] public double? FenceArmorDurabilityMax { get; set; }
    [JsonPropertyName("fenceModdedItemFilter")] public string? FenceModdedItemFilter { get; set; } // "all", "vanilla_only", "modded_only"
    [JsonPropertyName("fenceCategoryBlacklist")] public List<string>? FenceCategoryBlacklist { get; set; }
}

// ═══════════════════════════════════════════════════════
// RESPONSE DTOs
// ═══════════════════════════════════════════════════════

public record MiscToggleConfigResponse
{
    [JsonPropertyName("config")] public MiscToggleConfig Config { get; set; } = new();
    [JsonPropertyName("defaults")] public MiscToggleDefaults Defaults { get; set; } = new();

    /// <summary>Fields that modify globals cached by the client — players must restart client to see changes.</summary>
    [JsonPropertyName("clientRestartFields")] public List<string> ClientRestartFields { get; set; } =
    [
        "questPlantTimeMult",
        "noWeaponMalfunctions",
        "unlimitedStamina",
        "noFallDamage",
        "noSkillFatigue",
        "allQuestsAvailable",
        "removeQuestTimeConditions",
        "removeQuestFirReqs",
        "minDurabilityToSell",
        "lightKeeperAccessTime",
        "lightKeeperKickNotifTime"
    ];
}

public record MiscToggleDefaults
{
    // J
    [JsonPropertyName("commandoEnabled")] public bool CommandoEnabled { get; set; }
    [JsonPropertyName("sptFriendEnabled")] public bool SptFriendEnabled { get; set; }
    [JsonPropertyName("victimResponseChance")] public double VictimResponseChance { get; set; }
    [JsonPropertyName("killerResponseChance")] public double KillerResponseChance { get; set; }

    // K
    [JsonPropertyName("traderPurchasesFir")] public bool TraderPurchasesFir { get; set; }
    [JsonPropertyName("questRedeemTimeDefault")] public double QuestRedeemTimeDefault { get; set; }
    [JsonPropertyName("questRedeemTimeUnheard")] public double QuestRedeemTimeUnheard { get; set; }
    [JsonPropertyName("traderCount")] public int TraderCount { get; set; }
    [JsonPropertyName("questAssortCount")] public int QuestAssortCount { get; set; }

    // L
    [JsonPropertyName("totalQuests")] public int TotalQuests { get; set; }
    [JsonPropertyName("questsWithTimeConditions")] public int QuestsWithTimeConditions { get; set; }
    [JsonPropertyName("questsWithFirReqs")] public int QuestsWithFirReqs { get; set; }

    // M — Fun Mode
    [JsonPropertyName("ammoMalfChanceMult")] public double AmmoMalfChanceMult { get; set; }
    [JsonPropertyName("magazineMalfChanceMult")] public double MagazineMalfChanceMult { get; set; }
    [JsonPropertyName("staminaCapacity")] public double StaminaCapacity { get; set; }
    [JsonPropertyName("sprintDrainRate")] public double SprintDrainRate { get; set; }
    [JsonPropertyName("fallDamageMultiplier")] public double FallDamageMultiplier { get; set; }
    [JsonPropertyName("skillMinEffectiveness")] public double SkillMinEffectiveness { get; set; }
    [JsonPropertyName("skillFatiguePerPoint")] public double SkillFatiguePerPoint { get; set; }

    // N — Trader/Economy
    [JsonPropertyName("minDurabilityToSell")] public double MinDurabilityToSell { get; set; }
    [JsonPropertyName("lightKeeperAccessTime")] public double LightKeeperAccessTime { get; set; }
    [JsonPropertyName("lightKeeperKickNotifTime")] public double LightKeeperKickNotifTime { get; set; }

    // O — Fence
    [JsonPropertyName("fenceAssortSize")] public int FenceAssortSize { get; set; }
    [JsonPropertyName("fenceWeaponPresetMin")] public int FenceWeaponPresetMin { get; set; }
    [JsonPropertyName("fenceWeaponPresetMax")] public int FenceWeaponPresetMax { get; set; }
    [JsonPropertyName("fenceItemPriceMult")] public double FenceItemPriceMult { get; set; }
    [JsonPropertyName("fencePresetPriceMult")] public double FencePresetPriceMult { get; set; }
    [JsonPropertyName("fenceWeaponDurabilityMin")] public double FenceWeaponDurabilityMin { get; set; }
    [JsonPropertyName("fenceWeaponDurabilityMax")] public double FenceWeaponDurabilityMax { get; set; }
    [JsonPropertyName("fenceArmorDurabilityMin")] public double FenceArmorDurabilityMin { get; set; }
    [JsonPropertyName("fenceArmorDurabilityMax")] public double FenceArmorDurabilityMax { get; set; }
    [JsonPropertyName("fenceBlacklistCount")] public int FenceBlacklistCount { get; set; }
    [JsonPropertyName("fenceItemTypeLimitCount")] public int FenceItemTypeLimitCount { get; set; }
}

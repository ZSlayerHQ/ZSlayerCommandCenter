using System.Text.Json.Serialization;

namespace ZSlayerCommandCenter.Models;

// ═══════════════════════════════════════════════════════
// REPEATABLE QUEST CONFIG (persisted to config.json)
// ═══════════════════════════════════════════════════════

public record RepeatableQuestEditorConfig
{
    [JsonPropertyName("enableRepeatableQuests")] public bool EnableRepeatableQuests { get; set; }

    // Per-type settings (daily=0, weekly=1, scav=2)
    [JsonPropertyName("types")] public Dictionary<int, RepeatableTypeConfig>? Types { get; set; }
}

public record RepeatableTypeConfig
{
    // ── G. Basic Settings ──
    [JsonPropertyName("numQuests")] public int? NumQuests { get; set; }
    [JsonPropertyName("resetTimeSec")] public long? ResetTimeSec { get; set; }
    [JsonPropertyName("minPlayerLevel")] public int? MinPlayerLevel { get; set; }

    // ── H. Tier Overrides ──
    [JsonPropertyName("eliminationTiers")] public List<EliminationTierOverride>? EliminationTiers { get; set; }
    [JsonPropertyName("completionTiers")] public List<CompletionTierOverride>? CompletionTiers { get; set; }
    [JsonPropertyName("explorationTiers")] public List<ExplorationTierOverride>? ExplorationTiers { get; set; }

    // ── I. Reward Scaling ──
    [JsonPropertyName("rewardScaling")] public RewardScalingOverride? RewardScaling { get; set; }
}

public record EliminationTierOverride
{
    [JsonPropertyName("tierIndex")] public int TierIndex { get; set; }
    [JsonPropertyName("killCountMin")] public int? KillCountMin { get; set; }
    [JsonPropertyName("killCountMax")] public int? KillCountMax { get; set; }
}

public record CompletionTierOverride
{
    [JsonPropertyName("tierIndex")] public int TierIndex { get; set; }
    [JsonPropertyName("itemCountMin")] public int? ItemCountMin { get; set; }
    [JsonPropertyName("itemCountMax")] public int? ItemCountMax { get; set; }
}

public record ExplorationTierOverride
{
    [JsonPropertyName("tierIndex")] public int TierIndex { get; set; }
    [JsonPropertyName("extractMin")] public int? ExtractMin { get; set; }
    [JsonPropertyName("extractMax")] public int? ExtractMax { get; set; }
    [JsonPropertyName("specificExtractMin")] public int? SpecificExtractMin { get; set; }
    [JsonPropertyName("specificExtractMax")] public int? SpecificExtractMax { get; set; }
}

public record RewardScalingOverride
{
    [JsonPropertyName("experience")] public List<double>? Experience { get; set; }
    [JsonPropertyName("roubles")] public List<double>? Roubles { get; set; }
    [JsonPropertyName("gpCoins")] public List<double>? GpCoins { get; set; }
    [JsonPropertyName("items")] public List<double>? Items { get; set; }
    [JsonPropertyName("reputation")] public List<double>? Reputation { get; set; }
}

// ═══════════════════════════════════════════════════════
// RESPONSE DTOs
// ═══════════════════════════════════════════════════════

public record RepeatableQuestConfigResponse
{
    [JsonPropertyName("config")] public RepeatableQuestEditorConfig Config { get; set; } = new();
    [JsonPropertyName("defaults")] public RepeatableQuestDefaults Defaults { get; set; } = new();
}

public record RepeatableQuestDefaults
{
    [JsonPropertyName("types")] public List<RepeatableTypeDefaults> Types { get; set; } = [];
}

public record RepeatableTypeDefaults
{
    [JsonPropertyName("index")] public int Index { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("numQuests")] public int NumQuests { get; set; }
    [JsonPropertyName("resetTimeSec")] public long ResetTimeSec { get; set; }
    [JsonPropertyName("minPlayerLevel")] public int MinPlayerLevel { get; set; }

    // Tier ranges
    [JsonPropertyName("eliminationTiers")] public List<EliminationTierDefaults> EliminationTiers { get; set; } = [];
    [JsonPropertyName("completionTiers")] public List<CompletionTierDefaults> CompletionTiers { get; set; } = [];
    [JsonPropertyName("explorationTiers")] public List<ExplorationTierDefaults> ExplorationTiers { get; set; } = [];

    // Reward scaling
    [JsonPropertyName("rewardLevels")] public List<double> RewardLevels { get; set; } = [];
    [JsonPropertyName("rewardExperience")] public List<double> RewardExperience { get; set; } = [];
    [JsonPropertyName("rewardRoubles")] public List<double> RewardRoubles { get; set; } = [];
    [JsonPropertyName("rewardGpCoins")] public List<double> RewardGpCoins { get; set; } = [];
    [JsonPropertyName("rewardItems")] public List<double> RewardItems { get; set; } = [];
    [JsonPropertyName("rewardReputation")] public List<double> RewardReputation { get; set; } = [];
}

public record EliminationTierDefaults
{
    [JsonPropertyName("tierIndex")] public int TierIndex { get; set; }
    [JsonPropertyName("levelMin")] public int LevelMin { get; set; }
    [JsonPropertyName("levelMax")] public int LevelMax { get; set; }
    [JsonPropertyName("killCountMin")] public int KillCountMin { get; set; }
    [JsonPropertyName("killCountMax")] public int KillCountMax { get; set; }
}

public record CompletionTierDefaults
{
    [JsonPropertyName("tierIndex")] public int TierIndex { get; set; }
    [JsonPropertyName("levelMin")] public int LevelMin { get; set; }
    [JsonPropertyName("levelMax")] public int LevelMax { get; set; }
    [JsonPropertyName("itemCountMin")] public int ItemCountMin { get; set; }
    [JsonPropertyName("itemCountMax")] public int ItemCountMax { get; set; }
}

public record ExplorationTierDefaults
{
    [JsonPropertyName("tierIndex")] public int TierIndex { get; set; }
    [JsonPropertyName("levelMin")] public int LevelMin { get; set; }
    [JsonPropertyName("levelMax")] public int LevelMax { get; set; }
    [JsonPropertyName("extractMin")] public int ExtractMin { get; set; }
    [JsonPropertyName("extractMax")] public int ExtractMax { get; set; }
    [JsonPropertyName("specificExtractMin")] public int SpecificExtractMin { get; set; }
    [JsonPropertyName("specificExtractMax")] public int SpecificExtractMax { get; set; }
}

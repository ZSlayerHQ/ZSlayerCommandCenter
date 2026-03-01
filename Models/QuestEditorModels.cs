using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace ZSlayerCommandCenter.Models;

// ═══════════════════════════════════════════════════════════════════
//  CONFIG RECORDS (persisted to config.json under "quests")
// ═══════════════════════════════════════════════════════════════════

public record QuestEditorConfig
{
    [JsonPropertyName("globalXpMultiplier")]
    public double GlobalXpMultiplier { get; set; } = 1.0;

    [JsonPropertyName("globalStandingMultiplier")]
    public double GlobalStandingMultiplier { get; set; } = 1.0;

    [JsonPropertyName("globalItemRewardMultiplier")]
    public double GlobalItemRewardMultiplier { get; set; } = 1.0;

    [JsonPropertyName("globalKillCountMultiplier")]
    public double GlobalKillCountMultiplier { get; set; } = 1.0;

    [JsonPropertyName("globalHandoverCountMultiplier")]
    public double GlobalHandoverCountMultiplier { get; set; } = 1.0;

    [JsonPropertyName("removeFIRRequirements")]
    public bool RemoveFIRRequirements { get; set; } = false;

    [JsonPropertyName("globalLevelRequirementShift")]
    public int GlobalLevelRequirementShift { get; set; } = 0;

    [JsonPropertyName("disabledQuests")]
    public List<string> DisabledQuests { get; set; } = [];

    [JsonPropertyName("questOverrides")]
    public Dictionary<string, QuestOverrideConfig> QuestOverrides { get; set; } = new();
}

public record QuestOverrideConfig
{
    [JsonPropertyName("questName")]
    public string QuestName { get; set; } = "";

    [JsonPropertyName("traderIdOverride")]
    public string? TraderIdOverride { get; set; }

    [JsonPropertyName("levelRequirementOverride")]
    public int? LevelRequirementOverride { get; set; }

    [JsonPropertyName("removedPrerequisites")]
    public List<string> RemovedPrerequisites { get; set; } = [];

    [JsonPropertyName("conditionOverrides")]
    public Dictionary<string, ConditionOverrideConfig> ConditionOverrides { get; set; } = new();

    [JsonPropertyName("rewardOverrides")]
    public Dictionary<string, RewardOverrideConfig> RewardOverrides { get; set; } = new();

    [JsonPropertyName("removedRewards")]
    public List<string> RemovedRewards { get; set; } = [];

    [JsonPropertyName("localeOverrides")]
    public Dictionary<string, string> LocaleOverrides { get; set; } = new();
}

public record ConditionOverrideConfig
{
    [JsonPropertyName("conditionType")]
    public string ConditionType { get; set; } = "";

    [JsonPropertyName("valueOverride")]
    public double? ValueOverride { get; set; }

    [JsonPropertyName("locationOverride")]
    public string? LocationOverride { get; set; }

    [JsonPropertyName("onlyFoundInRaidOverride")]
    public bool? OnlyFoundInRaidOverride { get; set; }
}

public record RewardOverrideConfig
{
    [JsonPropertyName("rewardType")]
    public string RewardType { get; set; } = "";

    [JsonPropertyName("valueOverride")]
    public double? ValueOverride { get; set; }
}

// ═══════════════════════════════════════════════════════════════════
//  SNAPSHOTS (in-memory only, for restore-before-apply)
// ═══════════════════════════════════════════════════════════════════

public class QuestSnapshot
{
    public string TraderId { get; set; } = "";

    /// <summary>Per-condition snapshots keyed by condition ID string.</summary>
    public Dictionary<string, ConditionSnapshot> Conditions { get; set; } = new();

    /// <summary>Per-reward snapshots keyed by reward ID string.</summary>
    public Dictionary<string, RewardSnapshot> Rewards { get; set; } = new();

    /// <summary>Original locale entries (key → original text) for this quest.</summary>
    public Dictionary<string, string> LocaleEntries { get; set; } = new();

    /// <summary>Original AvailableForStart condition objects (for restoring removed prerequisites).</summary>
    public List<QuestCondition>? OriginalStartConditions { get; set; }
}

public class ConditionSnapshot
{
    public string ConditionType { get; set; } = "";
    public double? Value { get; set; }
    public bool? OnlyFoundInRaid { get; set; }
}

public class RewardSnapshot
{
    public string RewardType { get; set; } = "";
    public double? Value { get; set; }
}

// ═══════════════════════════════════════════════════════════════════
//  DTOs — Quest Browser (list view)
// ═══════════════════════════════════════════════════════════════════

public record QuestBrowserResponse
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("quests")]
    public List<QuestSummaryDto> Quests { get; set; } = [];

    [JsonPropertyName("maps")]
    public List<QuestMapGroup> Maps { get; set; } = [];
}

public record QuestSummaryDto
{
    [JsonPropertyName("questId")]
    public string QuestId { get; set; } = "";

    [JsonPropertyName("questName")]
    public string QuestName { get; set; } = "";

    [JsonPropertyName("traderName")]
    public string TraderName { get; set; } = "";

    [JsonPropertyName("traderId")]
    public string TraderId { get; set; } = "";

    [JsonPropertyName("location")]
    public string Location { get; set; } = "";

    [JsonPropertyName("locationName")]
    public string LocationName { get; set; } = "";

    [JsonPropertyName("levelRequired")]
    public int LevelRequired { get; set; }

    [JsonPropertyName("objectiveCount")]
    public int ObjectiveCount { get; set; }

    [JsonPropertyName("rewardCount")]
    public int RewardCount { get; set; }

    [JsonPropertyName("prerequisiteCount")]
    public int PrerequisiteCount { get; set; }

    [JsonPropertyName("side")]
    public string Side { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("isDisabled")]
    public bool IsDisabled { get; set; }

    [JsonPropertyName("hasOverrides")]
    public bool HasOverrides { get; set; }

    [JsonPropertyName("playerStatus")]
    public string PlayerStatus { get; set; } = "";
}

public record QuestMapGroup
{
    [JsonPropertyName("location")]
    public string Location { get; set; } = "";

    [JsonPropertyName("locationName")]
    public string LocationName { get; set; } = "";

    [JsonPropertyName("count")]
    public int Count { get; set; }
}

// ═══════════════════════════════════════════════════════════════════
//  DTOs — Quest Detail (edit view)
// ═══════════════════════════════════════════════════════════════════

public record QuestDetailResponse
{
    [JsonPropertyName("questId")]
    public string QuestId { get; set; } = "";

    [JsonPropertyName("questName")]
    public string QuestName { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("traderName")]
    public string TraderName { get; set; } = "";

    [JsonPropertyName("traderId")]
    public string TraderId { get; set; } = "";

    [JsonPropertyName("location")]
    public string Location { get; set; } = "";

    [JsonPropertyName("locationName")]
    public string LocationName { get; set; } = "";

    [JsonPropertyName("levelRequired")]
    public int LevelRequired { get; set; }

    [JsonPropertyName("side")]
    public string Side { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("isDisabled")]
    public bool IsDisabled { get; set; }

    [JsonPropertyName("hasOverrides")]
    public bool HasOverrides { get; set; }

    [JsonPropertyName("prerequisites")]
    public List<PrerequisiteInfo> Prerequisites { get; set; } = [];

    [JsonPropertyName("objectives")]
    public List<ObjectiveInfo> Objectives { get; set; } = [];

    [JsonPropertyName("successRewards")]
    public List<RewardInfo> SuccessRewards { get; set; } = [];

    [JsonPropertyName("failRewards")]
    public List<RewardInfo> FailRewards { get; set; } = [];
}

public record PrerequisiteInfo
{
    [JsonPropertyName("conditionId")]
    public string ConditionId { get; set; } = "";

    [JsonPropertyName("questId")]
    public string QuestId { get; set; } = "";

    [JsonPropertyName("questName")]
    public string QuestName { get; set; } = "";

    [JsonPropertyName("requiredStatuses")]
    public List<string> RequiredStatuses { get; set; } = [];
}

public record ObjectiveInfo
{
    [JsonPropertyName("conditionId")]
    public string ConditionId { get; set; } = "";

    [JsonPropertyName("conditionType")]
    public string ConditionType { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("value")]
    public double Value { get; set; }

    [JsonPropertyName("originalValue")]
    public double OriginalValue { get; set; }

    [JsonPropertyName("onlyFoundInRaid")]
    public bool? OnlyFoundInRaid { get; set; }

    [JsonPropertyName("target")]
    public List<string> Target { get; set; } = [];

    [JsonPropertyName("targetNames")]
    public List<string> TargetNames { get; set; } = [];

    [JsonPropertyName("hasOverride")]
    public bool HasOverride { get; set; }
}

public record RewardInfo
{
    [JsonPropertyName("rewardId")]
    public string RewardId { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("value")]
    public double Value { get; set; }

    [JsonPropertyName("originalValue")]
    public double OriginalValue { get; set; }

    [JsonPropertyName("target")]
    public string Target { get; set; } = "";

    [JsonPropertyName("targetName")]
    public string TargetName { get; set; } = "";

    [JsonPropertyName("items")]
    public List<RewardItemInfo> Items { get; set; } = [];

    [JsonPropertyName("hasOverride")]
    public bool HasOverride { get; set; }
}

public record RewardItemInfo
{
    [JsonPropertyName("templateId")]
    public string TemplateId { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("count")]
    public int Count { get; set; }
}

// ═══════════════════════════════════════════════════════════════════
//  DTOs — Quest Tree (prerequisite visualization)
// ═══════════════════════════════════════════════════════════════════

public record QuestTreeResponse
{
    [JsonPropertyName("nodes")]
    public List<QuestTreeNode> Nodes { get; set; } = [];

    [JsonPropertyName("edges")]
    public List<QuestTreeEdge> Edges { get; set; } = [];
}

public record QuestTreeNode
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("trader")]
    public string Trader { get; set; } = "";

    [JsonPropertyName("traderId")]
    public string TraderId { get; set; } = "";

    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("isDisabled")]
    public bool IsDisabled { get; set; }

    [JsonPropertyName("hasOverrides")]
    public bool HasOverrides { get; set; }
}

public record QuestTreeEdge
{
    [JsonPropertyName("from")]
    public string From { get; set; } = "";

    [JsonPropertyName("to")]
    public string To { get; set; } = "";
}

// ═══════════════════════════════════════════════════════════════════
//  DTOs — API Request/Response
// ═══════════════════════════════════════════════════════════════════

public record QuestGlobalConfigRequest
{
    [JsonPropertyName("globalXpMultiplier")]
    public double? GlobalXpMultiplier { get; set; }

    [JsonPropertyName("globalStandingMultiplier")]
    public double? GlobalStandingMultiplier { get; set; }

    [JsonPropertyName("globalItemRewardMultiplier")]
    public double? GlobalItemRewardMultiplier { get; set; }

    [JsonPropertyName("globalKillCountMultiplier")]
    public double? GlobalKillCountMultiplier { get; set; }

    [JsonPropertyName("globalHandoverCountMultiplier")]
    public double? GlobalHandoverCountMultiplier { get; set; }

    [JsonPropertyName("removeFIRRequirements")]
    public bool? RemoveFIRRequirements { get; set; }

    [JsonPropertyName("globalLevelRequirementShift")]
    public int? GlobalLevelRequirementShift { get; set; }
}

public record QuestOverrideRequest
{
    [JsonPropertyName("traderIdOverride")]
    public string? TraderIdOverride { get; set; }

    [JsonPropertyName("levelRequirementOverride")]
    public int? LevelRequirementOverride { get; set; }

    [JsonPropertyName("removedPrerequisites")]
    public List<string>? RemovedPrerequisites { get; set; }

    [JsonPropertyName("conditionOverrides")]
    public Dictionary<string, ConditionOverrideConfig>? ConditionOverrides { get; set; }

    [JsonPropertyName("rewardOverrides")]
    public Dictionary<string, RewardOverrideConfig>? RewardOverrides { get; set; }

    [JsonPropertyName("removedRewards")]
    public List<string>? RemovedRewards { get; set; }

    [JsonPropertyName("localeOverrides")]
    public Dictionary<string, string>? LocaleOverrides { get; set; }
}

public record SetQuestStateRequest
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";
}

public record QuestApplyResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("questsModified")]
    public int QuestsModified { get; set; }

    [JsonPropertyName("objectivesModified")]
    public int ObjectivesModified { get; set; }

    [JsonPropertyName("rewardsModified")]
    public int RewardsModified { get; set; }

    [JsonPropertyName("applyTimeMs")]
    public long ApplyTimeMs { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

public record QuestStatusResponse
{
    [JsonPropertyName("totalQuests")]
    public int TotalQuests { get; set; }

    [JsonPropertyName("overrideCount")]
    public int OverrideCount { get; set; }

    [JsonPropertyName("disabledCount")]
    public int DisabledCount { get; set; }

    [JsonPropertyName("globalsActive")]
    public bool GlobalsActive { get; set; }
}

public record QuestPlayerStatus
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = "";

    [JsonPropertyName("playerName")]
    public string PlayerName { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";
}

public record QuestTraderInfo
{
    [JsonPropertyName("traderId")]
    public string TraderId { get; set; } = "";

    [JsonPropertyName("traderName")]
    public string TraderName { get; set; } = "";

    [JsonPropertyName("questCount")]
    public int QuestCount { get; set; }
}

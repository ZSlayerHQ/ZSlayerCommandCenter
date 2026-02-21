using System.Text.Json.Serialization;

namespace ZSlayerCommandCenter.Models;

// ── Quest Browser ──

public record QuestBrowserDto
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

    [JsonPropertyName("side")]
    public string Side { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";
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

// ── Quest Detail ──

public record QuestDetailDto
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

    [JsonPropertyName("side")]
    public string Side { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("objectives")]
    public List<QuestObjectiveDto> Objectives { get; set; } = [];
}

public record QuestObjectiveDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("value")]
    public double Value { get; set; }
}

// ── Quest State Setter ──

public record SetQuestStateRequest
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";
}

public record SetQuestStateResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

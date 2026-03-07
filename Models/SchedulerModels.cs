using System.Text.Json.Serialization;

namespace ZSlayerCommandCenter.Models;

// ═══════════════════════════════════════════════════════════════
//  ENUMS
// ═══════════════════════════════════════════════════════════════

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EventType
{
    DoubleXP,
    TraderSale,
    LootBoost
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EventStatus
{
    Draft,
    Scheduled,
    Active,
    Expired,
    Cancelled
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TaskType
{
    Broadcast,
    Backup,
    ServerRestart,
    HeadlessRestart
}

// ═══════════════════════════════════════════════════════════════
//  EVENTS
// ═══════════════════════════════════════════════════════════════

public record ServerEvent
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("type")]
    public EventType Type { get; set; }

    [JsonPropertyName("status")]
    public EventStatus Status { get; set; } = EventStatus.Draft;

    // Scheduling — one-time
    [JsonPropertyName("startTime")]
    public DateTime? StartTime { get; set; }

    [JsonPropertyName("endTime")]
    public DateTime? EndTime { get; set; }

    // Scheduling — recurring
    [JsonPropertyName("cronExpression")]
    public string? CronExpression { get; set; }

    [JsonPropertyName("durationMinutes")]
    public int DurationMinutes { get; set; } = 60;

    // Active instance tracking
    [JsonPropertyName("activatedAt")]
    public DateTime? ActivatedAt { get; set; }

    [JsonPropertyName("expiresAt")]
    public DateTime? ExpiresAt { get; set; }

    // Parameters
    [JsonPropertyName("multiplier")]
    public double Multiplier { get; set; } = 2.0;

    [JsonPropertyName("targetIds")]
    public List<string> TargetIds { get; set; } = [];

    // Notifications
    [JsonPropertyName("broadcastOnStart")]
    public bool BroadcastOnStart { get; set; } = true;

    [JsonPropertyName("broadcastOnEnd")]
    public bool BroadcastOnEnd { get; set; } = true;

    [JsonPropertyName("customStartMessage")]
    public string? CustomStartMessage { get; set; }

    [JsonPropertyName("customEndMessage")]
    public string? CustomEndMessage { get; set; }

    // Metadata
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("createdBy")]
    public string CreatedBy { get; set; } = "";
}

// ═══════════════════════════════════════════════════════════════
//  SCHEDULED TASKS
// ═══════════════════════════════════════════════════════════════

public record ScheduledTask
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("type")]
    public TaskType Type { get; set; }

    [JsonPropertyName("cronExpression")]
    public string? CronExpression { get; set; }

    [JsonPropertyName("runAt")]
    public DateTime? RunAt { get; set; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    // Parameters
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    // Tracking
    [JsonPropertyName("lastRun")]
    public DateTime? LastRun { get; set; }

    [JsonPropertyName("nextRun")]
    public DateTime? NextRun { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// ═══════════════════════════════════════════════════════════════
//  SCHEDULER CONFIG (persisted alongside events/tasks)
// ═══════════════════════════════════════════════════════════════

public record SchedulerState
{
    [JsonPropertyName("events")]
    public List<ServerEvent> Events { get; set; } = [];

    [JsonPropertyName("tasks")]
    public List<ScheduledTask> Tasks { get; set; } = [];
}

// ═══════════════════════════════════════════════════════════════
//  API DTOs
// ═══════════════════════════════════════════════════════════════

public record SchedulerOverviewResponse
{
    [JsonPropertyName("events")]
    public List<ServerEvent> Events { get; set; } = [];

    [JsonPropertyName("tasks")]
    public List<ScheduledTask> Tasks { get; set; } = [];

    [JsonPropertyName("activeEvents")]
    public List<ServerEvent> ActiveEvents { get; set; } = [];

    [JsonPropertyName("templates")]
    public List<EventTemplateDto> Templates { get; set; } = [];
}

public record EventTemplateDto
{
    [JsonPropertyName("type")]
    public EventType Type { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("defaultMultiplier")]
    public double DefaultMultiplier { get; set; } = 2.0;

    [JsonPropertyName("icon")]
    public string Icon { get; set; } = "";

    [JsonPropertyName("supportsTargets")]
    public bool SupportsTargets { get; set; }

    [JsonPropertyName("targetLabel")]
    public string TargetLabel { get; set; } = "";

    [JsonPropertyName("multiplierLabel")]
    public string MultiplierLabel { get; set; } = "";
}

public record CreateEventRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("type")]
    public EventType Type { get; set; }

    [JsonPropertyName("startTime")]
    public DateTime? StartTime { get; set; }

    [JsonPropertyName("endTime")]
    public DateTime? EndTime { get; set; }

    [JsonPropertyName("cronExpression")]
    public string? CronExpression { get; set; }

    [JsonPropertyName("durationMinutes")]
    public int DurationMinutes { get; set; } = 60;

    [JsonPropertyName("multiplier")]
    public double Multiplier { get; set; } = 2.0;

    [JsonPropertyName("targetIds")]
    public List<string> TargetIds { get; set; } = [];

    [JsonPropertyName("broadcastOnStart")]
    public bool BroadcastOnStart { get; set; } = true;

    [JsonPropertyName("broadcastOnEnd")]
    public bool BroadcastOnEnd { get; set; } = true;

    [JsonPropertyName("customStartMessage")]
    public string? CustomStartMessage { get; set; }

    [JsonPropertyName("customEndMessage")]
    public string? CustomEndMessage { get; set; }

    [JsonPropertyName("activateNow")]
    public bool ActivateNow { get; set; }
}

public record CreateTaskRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("type")]
    public TaskType Type { get; set; }

    [JsonPropertyName("cronExpression")]
    public string? CronExpression { get; set; }

    [JsonPropertyName("runAt")]
    public DateTime? RunAt { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

public record CronValidateRequest
{
    [JsonPropertyName("expression")]
    public string? Expression { get; set; }
}

public record TaskToggleRequest
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }
}

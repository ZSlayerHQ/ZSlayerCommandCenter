using System.Text.Json.Serialization;

namespace ZSlayerCommandCenter.Models;

public record CcBackupConfig
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("intervalHours")]
    public int IntervalHours { get; set; } = 24;

    [JsonPropertyName("retentionDays")]
    public int RetentionDays { get; set; } = 30;

    [JsonPropertyName("maxBackupCount")]
    public int MaxBackupCount { get; set; } = 50;

    [JsonPropertyName("backupOnServerStart")]
    public bool BackupOnServerStart { get; set; } = true;

    [JsonPropertyName("includeConfigs")]
    public bool IncludeConfigs { get; set; } = true;
}

public record BackupEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "profile"; // "profile" or "config"

    [JsonPropertyName("profileCount")]
    public int ProfileCount { get; set; }

    [JsonPropertyName("totalSizeBytes")]
    public long TotalSizeBytes { get; set; }

    [JsonPropertyName("sptVersion")]
    public string SptVersion { get; set; } = "";

    [JsonPropertyName("ccVersion")]
    public string CcVersion { get; set; } = "";

    [JsonPropertyName("notes")]
    public string Notes { get; set; } = "";

    [JsonPropertyName("isPreRestore")]
    public bool IsPreRestore { get; set; }
}

public record BackupManifest
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "profile";

    [JsonPropertyName("profileCount")]
    public int ProfileCount { get; set; }

    [JsonPropertyName("sptVersion")]
    public string SptVersion { get; set; } = "";

    [JsonPropertyName("ccVersion")]
    public string CcVersion { get; set; } = "";

    [JsonPropertyName("notes")]
    public string Notes { get; set; } = "";

    [JsonPropertyName("isPreRestore")]
    public bool IsPreRestore { get; set; }

    [JsonPropertyName("profiles")]
    public List<string> Profiles { get; set; } = [];
}

public record BackupListResponse
{
    [JsonPropertyName("entries")]
    public List<BackupEntry> Entries { get; set; } = [];

    [JsonPropertyName("storage")]
    public StorageUsage Storage { get; set; } = new();
}

public record StorageUsage
{
    [JsonPropertyName("totalSizeBytes")]
    public long TotalSizeBytes { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("oldest")]
    public DateTime? Oldest { get; set; }

    [JsonPropertyName("newest")]
    public DateTime? Newest { get; set; }
}

public record BackupDiffResponse
{
    [JsonPropertyName("backupId")]
    public string BackupId { get; set; } = "";

    [JsonPropertyName("backupTimestamp")]
    public DateTime BackupTimestamp { get; set; }

    [JsonPropertyName("profiles")]
    public List<ProfileDiff> Profiles { get; set; } = [];
}

public record ProfileDiff
{
    [JsonPropertyName("profileId")]
    public string ProfileId { get; set; } = "";

    [JsonPropertyName("nickname")]
    public string Nickname { get; set; } = "";

    [JsonPropertyName("backupLevel")]
    public int BackupLevel { get; set; }

    [JsonPropertyName("currentLevel")]
    public int CurrentLevel { get; set; }

    [JsonPropertyName("backupRoubles")]
    public long BackupRoubles { get; set; }

    [JsonPropertyName("currentRoubles")]
    public long CurrentRoubles { get; set; }

    [JsonPropertyName("backupDollars")]
    public long BackupDollars { get; set; }

    [JsonPropertyName("currentDollars")]
    public long CurrentDollars { get; set; }

    [JsonPropertyName("backupEuros")]
    public long BackupEuros { get; set; }

    [JsonPropertyName("currentEuros")]
    public long CurrentEuros { get; set; }

    [JsonPropertyName("backupQuestsCompleted")]
    public int BackupQuestsCompleted { get; set; }

    [JsonPropertyName("currentQuestsCompleted")]
    public int CurrentQuestsCompleted { get; set; }

    [JsonPropertyName("backupStashItems")]
    public int BackupStashItems { get; set; }

    [JsonPropertyName("currentStashItems")]
    public int CurrentStashItems { get; set; }

    [JsonPropertyName("existsInCurrent")]
    public bool ExistsInCurrent { get; set; } = true;

    [JsonPropertyName("existsInBackup")]
    public bool ExistsInBackup { get; set; } = true;
}

public record BackupCreateRequest
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "profile"; // "profile", "config", "both"

    [JsonPropertyName("notes")]
    public string Notes { get; set; } = "";
}

public record RestoreRequest
{
    [JsonPropertyName("backupId")]
    public string BackupId { get; set; } = "";
}

public record WipeRequest
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "full"; // "full" or "selective"

    [JsonPropertyName("categories")]
    public List<string> Categories { get; set; } = [];

    [JsonPropertyName("confirmText")]
    public string ConfirmText { get; set; } = "";
}

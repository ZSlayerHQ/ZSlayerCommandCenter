using System.Text.Json.Serialization;

namespace ZSlayerCommandCenter.Models;

public record CommandCenterConfig
{
    [JsonPropertyName("access")]
    public AccessControlConfig Access { get; set; } = new();

    [JsonPropertyName("logging")]
    public LoggingConfig Logging { get; set; } = new();

    [JsonPropertyName("items")]
    public ItemsConfig Items { get; set; } = new();

    [JsonPropertyName("dashboard")]
    public DashboardConfig Dashboard { get; set; } = new();

    [JsonPropertyName("flea")]
    public FleaConfig Flea { get; set; } = new();

    [JsonPropertyName("headless")]
    public HeadlessConfig Headless { get; set; } = new();

    [JsonPropertyName("watchdog")]
    public WatchdogConfig Watchdog { get; set; } = new();
}

public record HeadlessConfig
{
    [JsonPropertyName("autoStart")]
    public bool AutoStart { get; set; } = false;

    [JsonPropertyName("autoStartDelaySec")]
    public int AutoStartDelaySec { get; set; } = 30;

    [JsonPropertyName("autoRestart")]
    public bool AutoRestart { get; set; } = true;

    [JsonPropertyName("profileId")]
    public string ProfileId { get; set; } = "";

    [JsonPropertyName("exePath")]
    public string ExePath { get; set; } = "";

    [JsonPropertyName("restartAfterRaids")]
    public int RestartAfterRaids { get; set; } = 0;
}

public record WatchdogConfig
{
    [JsonPropertyName("port")]
    public int Port { get; set; } = 6971;

    [JsonPropertyName("sptServerExe")]
    public string SptServerExe { get; set; } = "auto";

    [JsonPropertyName("autoStartServer")]
    public bool AutoStartServer { get; set; } = true;

    [JsonPropertyName("autoStartDelaySec")]
    public int AutoStartDelaySec { get; set; } = 3;

    [JsonPropertyName("autoRestartOnCrash")]
    public bool AutoRestartOnCrash { get; set; } = true;

    [JsonPropertyName("restartDelaySec")]
    public int RestartDelaySec { get; set; } = 5;

    [JsonPropertyName("sessionTimeoutMin")]
    public int SessionTimeoutMin { get; set; } = 5;
}

public record AccessControlConfig
{
    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "whitelist";

    [JsonPropertyName("whitelist")]
    public List<string> Whitelist { get; set; } = [];

    [JsonPropertyName("blacklist")]
    public List<string> Blacklist { get; set; } = [];

    [JsonPropertyName("allowAllWhenEmpty")]
    public bool AllowAllWhenEmpty { get; set; } = true;

    [JsonPropertyName("password")]
    public string Password { get; set; } = "";

    [JsonPropertyName("banList")]
    public List<BanEntryConfig> BanList { get; set; } = [];
}

public record BanEntryConfig
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = "";

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = "";

    [JsonPropertyName("bannedAt")]
    public DateTime BannedAt { get; set; }
}

public record ItemsConfig
{
    [JsonPropertyName("presets")]
    public List<PresetConfig> Presets { get; set; } = [];
}

public record PresetConfig
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("items")]
    public List<PresetItemConfig> Items { get; set; } = [];
}

public record PresetItemConfig
{
    [JsonPropertyName("tpl")]
    public string Tpl { get; set; } = "";

    [JsonPropertyName("count")]
    public int Count { get; set; } = 1;

    [JsonPropertyName("note")]
    public string Note { get; set; } = "";
}

public record LoggingConfig
{
    [JsonPropertyName("logGiveEvents")]
    public bool LogGiveEvents { get; set; } = true;
}

public record DashboardConfig
{
    [JsonPropertyName("refreshIntervalSeconds")]
    public int RefreshIntervalSeconds { get; set; } = 30;

    [JsonPropertyName("consoleBufferSize")]
    public int ConsoleBufferSize { get; set; } = 500;

    [JsonPropertyName("consolePollingMs")]
    public int ConsolePollingMs { get; set; } = 3000;

    [JsonPropertyName("activityRetentionDays")]
    public int ActivityRetentionDays { get; set; } = 30;

    [JsonPropertyName("headlessLogPath")]
    public string HeadlessLogPath { get; set; } = "";
}

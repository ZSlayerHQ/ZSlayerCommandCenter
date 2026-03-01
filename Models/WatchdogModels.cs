using System.Net.WebSockets;
using System.Text.Json.Serialization;

namespace ZSlayerCommandCenter.Models;

// ── Internal State ──

public class ConnectedWatchdog
{
    public string WatchdogId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Hostname { get; set; } = "";
    public string Ip { get; set; } = "";
    public WatchdogManages Manages { get; set; } = new();
    public WatchdogProcessStatus? SptServer { get; set; }
    public WatchdogProcessStatus? HeadlessClient { get; set; }
    public WatchdogSystemStats? System { get; set; }
    public WebSocket Socket { get; set; } = null!;
    public string SessionIdContext { get; set; } = "";
    public DateTime ConnectedAt { get; set; }
    public DateTime LastStatusAt { get; set; }
}

public record WatchdogManages
{
    [JsonPropertyName("sptServer")]
    public bool SptServer { get; set; }

    [JsonPropertyName("headlessClient")]
    public bool HeadlessClient { get; set; }
}

public record WatchdogProcessStatus
{
    [JsonPropertyName("running")]
    public bool Running { get; set; }

    [JsonPropertyName("pid")]
    public int? Pid { get; set; }

    [JsonPropertyName("uptime")]
    public string? Uptime { get; set; }

    [JsonPropertyName("crashes")]
    public int Crashes { get; set; }

    [JsonPropertyName("autoRestart")]
    public bool AutoRestart { get; set; }

    [JsonPropertyName("autoStart")]
    public bool AutoStart { get; set; }

    [JsonPropertyName("profile")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Profile { get; set; }

    [JsonPropertyName("restartAfterRaids")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int RestartAfterRaids { get; set; }

    [JsonPropertyName("startDelay")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StartDelay { get; set; }
}

public record WatchdogSystemStats
{
    [JsonPropertyName("cpuPercent")]
    public double CpuPercent { get; set; }

    [JsonPropertyName("ramUsedGB")]
    public double RamUsedGB { get; set; }

    [JsonPropertyName("ramTotalGB")]
    public double RamTotalGB { get; set; }
}

// ── Inbound Messages (from Watchdog → CC Server) ──

public record WatchdogMessage
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";
}

public record WatchdogRegisterMessage : WatchdogMessage
{
    [JsonPropertyName("watchdogId")]
    public string WatchdogId { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("hostname")]
    public string Hostname { get; set; } = "";

    [JsonPropertyName("ip")]
    public string Ip { get; set; } = "";

    [JsonPropertyName("manages")]
    public WatchdogManages Manages { get; set; } = new();
}

public record WatchdogStatusMessage : WatchdogMessage
{
    [JsonPropertyName("watchdogId")]
    public string WatchdogId { get; set; } = "";

    [JsonPropertyName("sptServer")]
    public WatchdogProcessStatus? SptServer { get; set; }

    [JsonPropertyName("headlessClient")]
    public WatchdogProcessStatus? HeadlessClient { get; set; }

    [JsonPropertyName("system")]
    public WatchdogSystemStats? System { get; set; }
}

public record WatchdogCommandResultMessage : WatchdogMessage
{
    [JsonPropertyName("watchdogId")]
    public string WatchdogId { get; set; } = "";

    [JsonPropertyName("target")]
    public string Target { get; set; } = "";

    [JsonPropertyName("action")]
    public string Action { get; set; } = "";

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";
}

// ── Outbound Messages (CC Server → Watchdog) ──

public record WatchdogCommandMessage
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "command";

    [JsonPropertyName("target")]
    public string Target { get; set; } = "";

    [JsonPropertyName("action")]
    public string Action { get; set; } = "";
}

public record WatchdogRaidEndMessage
{
    [JsonPropertyName("type")] public string Type { get; set; } = "raidEnd";
    [JsonPropertyName("map")] public string Map { get; set; } = "";
}

// ── API Response Models ──

public record WatchdogStatusResponse
{
    [JsonPropertyName("watchdogs")]
    public List<WatchdogStatusEntry> Watchdogs { get; set; } = [];
}

public record WatchdogStatusEntry
{
    [JsonPropertyName("watchdogId")]
    public string WatchdogId { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("hostname")]
    public string Hostname { get; set; } = "";

    [JsonPropertyName("ip")]
    public string Ip { get; set; } = "";

    [JsonPropertyName("connected")]
    public bool Connected { get; set; }

    [JsonPropertyName("manages")]
    public WatchdogManages Manages { get; set; } = new();

    [JsonPropertyName("sptServer")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WatchdogProcessStatus? SptServer { get; set; }

    [JsonPropertyName("headlessClient")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WatchdogProcessStatus? HeadlessClient { get; set; }

    [JsonPropertyName("system")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WatchdogSystemStats? System { get; set; }
}

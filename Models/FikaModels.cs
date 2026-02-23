using System.Text.Json.Serialization;

namespace ZSlayerCommandCenter.Models;

public record FikaConfigDto
{
    // Headless card
    [JsonPropertyName("restartAfterAmountOfRaids")]
    public int RestartAfterAmountOfRaids { get; set; }

    [JsonPropertyName("setLevelToAverageOfLobby")]
    public bool SetLevelToAverageOfLobby { get; set; }

    // FIKA card — Gameplay
    [JsonPropertyName("friendlyFire")]
    public bool FriendlyFire { get; set; }

    [JsonPropertyName("useInertia")]
    public bool UseInertia { get; set; }

    [JsonPropertyName("sharedQuestProgression")]
    public bool SharedQuestProgression { get; set; }

    [JsonPropertyName("dynamicVExfils")]
    public bool DynamicVExfils { get; set; }

    [JsonPropertyName("enableTransits")]
    public bool EnableTransits { get; set; }

    [JsonPropertyName("anyoneCanStartRaid")]
    public bool AnyoneCanStartRaid { get; set; }

    // FIKA card — Server
    [JsonPropertyName("sessionTimeout")]
    public int SessionTimeout { get; set; }

    [JsonPropertyName("allowItemSending")]
    public bool AllowItemSending { get; set; }

    [JsonPropertyName("sentItemsLoseFIR")]
    public bool SentItemsLoseFIR { get; set; }

    [JsonPropertyName("launcherListAllProfiles")]
    public bool LauncherListAllProfiles { get; set; }

    // Response only
    [JsonPropertyName("available")]
    public bool Available { get; set; }
}

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

    [JsonPropertyName("canEditRaidSettings")]
    public bool CanEditRaidSettings { get; set; } = true;

    // FIKA card — Server
    [JsonPropertyName("sessionTimeout")]
    public int SessionTimeout { get; set; }

    [JsonPropertyName("allowItemSending")]
    public bool AllowItemSending { get; set; }

    [JsonPropertyName("sentItemsLoseFIR")]
    public bool SentItemsLoseFIR { get; set; }

    [JsonPropertyName("launcherListAllProfiles")]
    public bool LauncherListAllProfiles { get; set; }

    // Mod validation
    [JsonPropertyName("requiredMods")]
    public List<string> RequiredMods { get; set; } = [];

    [JsonPropertyName("optionalMods")]
    public List<string> OptionalMods { get; set; } = [];

    [JsonPropertyName("blacklistedMods")]
    public List<string> BlacklistedMods { get; set; } = [];

    // Response only
    [JsonPropertyName("available")]
    public bool Available { get; set; }
}

using System.Text.Json.Serialization;

namespace ZSlayerCommandCenter.Models;

public record AuthResponse
{
    [JsonPropertyName("authorized")]
    public bool Authorized { get; set; }

    [JsonPropertyName("profileName")]
    public string ProfileName { get; set; } = "";

    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = "";

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}

public record ItemSearchResponse
{
    [JsonPropertyName("items")]
    public List<ItemDto> Items { get; set; } = [];

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("limit")]
    public int Limit { get; set; }

    [JsonPropertyName("offset")]
    public int Offset { get; set; }
}

public record ItemDto
{
    [JsonPropertyName("tpl")]
    public string Tpl { get; set; } = "";

    [JsonPropertyName("shortName")]
    public string ShortName { get; set; } = "";

    [JsonPropertyName("fullName")]
    public string FullName { get; set; } = "";

    [JsonPropertyName("category")]
    public string Category { get; set; } = "";

    [JsonPropertyName("categoryId")]
    public string CategoryId { get; set; } = "";

    [JsonPropertyName("weight")]
    public float Weight { get; set; }

    [JsonPropertyName("stackMaxSize")]
    public int StackMaxSize { get; set; }

    [JsonPropertyName("handbookPrice")]
    public int HandbookPrice { get; set; }

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }
}

public record CategoryResponse
{
    [JsonPropertyName("categories")]
    public List<CategoryDto> Categories { get; set; } = [];
}

public record CategoryDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("children")]
    public List<CategoryDto> Children { get; set; } = [];

    [JsonPropertyName("itemCount")]
    public int ItemCount { get; set; }
}

public record GiveResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("itemsGiven")]
    public int ItemsGiven { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("failedItems")]
    public List<string> FailedItems { get; set; } = [];
}

public record PresetListResponse
{
    [JsonPropertyName("presets")]
    public List<PresetInfo> Presets { get; set; } = [];
}

public record PresetInfo
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

public record ProfileListResponse
{
    [JsonPropertyName("profiles")]
    public List<ProfileEntry> Profiles { get; set; } = [];

    [JsonPropertyName("hasPassword")]
    public bool HasPassword { get; set; }

    [JsonPropertyName("modVersion")]
    public string ModVersion { get; set; } = "";
}

public record ProfileEntry
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = "";

    [JsonPropertyName("nickname")]
    public string Nickname { get; set; } = "";

    [JsonPropertyName("side")]
    public string Side { get; set; } = "";

    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("avatarIcon")]
    public string? AvatarIcon { get; set; }

    [JsonPropertyName("totalRaids")]
    public int TotalRaids { get; set; }

    [JsonPropertyName("survivalRate")]
    public int SurvivalRate { get; set; }

    [JsonPropertyName("isOnline")]
    public bool IsOnline { get; set; }
}

public record ProfileIconSetRequest
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = "";

    [JsonPropertyName("icon")]
    public string Icon { get; set; } = "";
}

public record PresetGiveResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("presetName")]
    public string PresetName { get; set; } = "";

    [JsonPropertyName("itemsGiven")]
    public int ItemsGiven { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

// ── Player Build DTOs ──

public record PlayerBuildListResponse
{
    [JsonPropertyName("weaponBuilds")]
    public List<PlayerBuildDto> WeaponBuilds { get; set; } = [];

    [JsonPropertyName("gearBuilds")]
    public List<PlayerBuildDto> GearBuilds { get; set; } = [];
}

public record PlayerBuildDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("ownerName")]
    public string OwnerName { get; set; } = "";

    [JsonPropertyName("ownerId")]
    public string OwnerId { get; set; } = "";

    [JsonPropertyName("rootTpl")]
    public string RootTpl { get; set; } = "";

    [JsonPropertyName("rootName")]
    public string RootName { get; set; } = "";

    [JsonPropertyName("itemCount")]
    public int ItemCount { get; set; }

    [JsonPropertyName("parts")]
    public List<BuildPartDto> Parts { get; set; } = [];
}

public record BuildPartDto
{
    [JsonPropertyName("tpl")]
    public string Tpl { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("slotId")]
    public string SlotId { get; set; } = "";
}

public record PlayerBuildGiveRequest
{
    [JsonPropertyName("buildId")]
    public string BuildId { get; set; } = "";

    [JsonPropertyName("buildType")]
    public string BuildType { get; set; } = "";
}

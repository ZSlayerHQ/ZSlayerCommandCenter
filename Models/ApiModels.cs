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

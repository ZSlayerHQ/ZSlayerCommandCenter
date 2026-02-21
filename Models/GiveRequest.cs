using System.Text.Json.Serialization;

namespace ZSlayerCommandCenter.Models;

public record GiveRequest
{
    [JsonPropertyName("items")]
    public List<GiveRequestItem> Items { get; set; } = [];
}

public record GiveRequestItem
{
    [JsonPropertyName("tpl")]
    public string Tpl { get; set; } = "";

    [JsonPropertyName("count")]
    public int Count { get; set; } = 1;
}

public record PresetGiveRequest
{
    [JsonPropertyName("presetId")]
    public string PresetId { get; set; } = "";
}

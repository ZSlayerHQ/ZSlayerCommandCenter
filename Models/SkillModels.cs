using System.Text.Json.Serialization;

namespace ZSlayerCommandCenter.Models;

// ═══════════════════════════════════════════════════════════════════
//  SKILL LIST / MULTIPLIERS
// ═══════════════════════════════════════════════════════════════════

public record SkillInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("internalName")]
    public string InternalName { get; set; } = "";

    [JsonPropertyName("effectiveMultiplier")]
    public double EffectiveMultiplier { get; set; } = 1.0;

    [JsonPropertyName("hasOverride")]
    public bool HasOverride { get; set; }

    [JsonPropertyName("isWeapon")]
    public bool IsWeapon { get; set; }

    [JsonPropertyName("isModded")]
    public bool IsModded { get; set; }
}

public record SkillListResponse
{
    [JsonPropertyName("skills")]
    public List<SkillInfo> Skills { get; set; } = [];

    [JsonPropertyName("globalMultiplier")]
    public double GlobalMultiplier { get; set; } = 1.0;

    [JsonPropertyName("weaponMultiplier")]
    public double WeaponMultiplier { get; set; } = 1.0;

    [JsonPropertyName("fatigueMultiplier")]
    public double FatigueMultiplier { get; set; } = 1.0;
}

// ═══════════════════════════════════════════════════════════════════
//  SKILL BONUS EDITING (globals SkillsSettings fields)
// ═══════════════════════════════════════════════════════════════════

public record SkillBonusField
{
    [JsonPropertyName("fieldName")]
    public string FieldName { get; set; } = "";

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = "";

    [JsonPropertyName("defaultValue")]
    public double DefaultValue { get; set; }

    [JsonPropertyName("currentValue")]
    public double CurrentValue { get; set; }
}

public record SkillBonusConfig
{
    [JsonPropertyName("skillName")]
    public string SkillName { get; set; } = "";

    [JsonPropertyName("fields")]
    public List<SkillBonusField> Fields { get; set; } = [];
}

public record SkillBonusUpdateRequest
{
    [JsonPropertyName("skillName")]
    public string SkillName { get; set; } = "";

    [JsonPropertyName("fields")]
    public Dictionary<string, double> Fields { get; set; } = new();
}

public record SkillBonusResponse
{
    [JsonPropertyName("skills")]
    public List<SkillBonusConfig> Skills { get; set; } = [];
}

// ═══════════════════════════════════════════════════════════════════
//  PER-PLAYER SKILL EDITING (Phase 6C)
// ═══════════════════════════════════════════════════════════════════

public record PlayerSkillEntry
{
    [JsonPropertyName("skillId")]
    public string SkillId { get; set; } = "";

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = "";

    [JsonPropertyName("progress")]
    public double Progress { get; set; }

    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("isElite")]
    public bool IsElite { get; set; }

    [JsonPropertyName("isModded")]
    public bool IsModded { get; set; }
}

public record PlayerSkillsResponse
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = "";

    [JsonPropertyName("nickname")]
    public string Nickname { get; set; } = "";

    [JsonPropertyName("skills")]
    public List<PlayerSkillEntry> Skills { get; set; } = [];
}

public record PlayerSkillUpdateRequest
{
    [JsonPropertyName("skills")]
    public List<PlayerSkillUpdate> Skills { get; set; } = [];
}

public record PlayerSkillUpdate
{
    [JsonPropertyName("skillId")]
    public string SkillId { get; set; } = "";

    [JsonPropertyName("level")]
    public int Level { get; set; }
}

public record PlayerSkillBulkRequest
{
    [JsonPropertyName("level")]
    public int Level { get; set; }
}

public record SkillIconUploadRequest
{
    [JsonPropertyName("skillName")]
    public string SkillName { get; set; } = "";

    [JsonPropertyName("imageBase64")]
    public string ImageBase64 { get; set; } = "";
}

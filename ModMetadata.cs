using SPTarkov.Server.Core.Models.Spt.Mod;

namespace ZSlayerCommandCenter;

public record ModMetadata : AbstractModMetadata
{
    public const string StaticVersion = "2.4.0";

    public override string ModGuid { get; init; } = "com.zslayerhq.commandcenter";
    public override string Name { get; init; } = "ZSlayer Command Center";
    public override string Author { get; init; } = "ZSlayer";
    public override List<string>? Contributors { get; init; }
    public override SemanticVersioning.Version Version { get; init; } = new(StaticVersion);
    public override SemanticVersioning.Range SptVersion { get; init; } = new("~4.0.0");
    public override List<string>? Incompatibilities { get; init; }
    public override Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public override string? Url { get; init; }
    public override bool? IsBundleMod { get; init; }
    public override string License { get; init; } = "MIT";
}

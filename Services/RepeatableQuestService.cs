using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using ZSlayerCommandCenter.Models;

namespace ZSlayerCommandCenter.Services;

[Injectable(InjectionType.Singleton)]
public class RepeatableQuestService(
    ConfigServer configServer,
    ConfigService configService,
    ISptLogger<RepeatableQuestService> logger)
{
    private readonly object _lock = new();
    private bool _snapshotTaken;

    // ═══════════════════════════════════════════════════════
    // SNAPSHOT RECORDS
    // ═══════════════════════════════════════════════════════

    private record TypeSnapshot(
        int NumQuests, long ResetTime, int MinPlayerLevel);

    private record EliminationTierSnapshot(int MinKills, int MaxKills);
    private record CompletionTierSnapshot(int ItemCountMin, int ItemCountMax);
    private record ExplorationTierSnapshot(
        int MinExtracts, int MaxExtracts,
        int MinSpecificExtracts, int MaxSpecificExtracts);

    private record RewardScalingSnapshot(
        List<double> Levels, List<double> Experience, List<double> Roubles,
        List<double> GpCoins, List<double> Items, List<double> Reputation);

    // ═══════════════════════════════════════════════════════
    // SNAPSHOT STORAGE — per type index (0=Daily, 1=Weekly, 2=Scav)
    // ═══════════════════════════════════════════════════════

    private readonly Dictionary<int, TypeSnapshot> _typeSnapshots = new();
    private readonly Dictionary<int, List<EliminationTierSnapshot>> _elimSnapshots = new();
    private readonly Dictionary<int, List<CompletionTierSnapshot>> _compSnapshots = new();
    private readonly Dictionary<int, List<ExplorationTierSnapshot>> _exploSnapshots = new();
    private readonly Dictionary<int, RewardScalingSnapshot> _rewardSnapshots = new();

    private static readonly string[] TypeNames = ["Daily", "Weekly", "Scav Daily"];

    // ═══════════════════════════════════════════════════════
    // INITIALIZE
    // ═══════════════════════════════════════════════════════

    public void Initialize()
    {
        lock (_lock)
        {
            TakeSnapshot();
            ApplyAll();
        }
    }

    // ═══════════════════════════════════════════════════════
    // SNAPSHOT
    // ═══════════════════════════════════════════════════════

    private void TakeSnapshot()
    {
        if (_snapshotTaken) return;

        try
        {
            var questConfig = configServer.GetConfig<QuestConfig>();
            var repeatables = questConfig.RepeatableQuests;
            if (repeatables == null || repeatables.Count == 0)
            {
                logger.Warning("[ZSlayerHQ] RepeatableQuests list is null/empty — skipping snapshot");
                _snapshotTaken = true;
                return;
            }

            for (var i = 0; i < repeatables.Count && i < 3; i++)
            {
                var rq = repeatables[i];

                // Basic settings
                _typeSnapshots[i] = new TypeSnapshot(rq.NumQuests, rq.ResetTime, rq.MinPlayerLevel);

                // Elimination tiers
                var elimList = new List<EliminationTierSnapshot>();
                if (rq.QuestConfig?.Elimination != null)
                {
                    foreach (var elim in rq.QuestConfig.Elimination)
                        elimList.Add(new EliminationTierSnapshot(elim.MinKills, elim.MaxKills));
                }
                _elimSnapshots[i] = elimList;

                // Completion tiers
                var compList = new List<CompletionTierSnapshot>();
                if (rq.QuestConfig?.CompletionConfig != null)
                {
                    foreach (var comp in rq.QuestConfig.CompletionConfig)
                        compList.Add(new CompletionTierSnapshot(
                            comp.RequestedItemCount?.Min ?? 0,
                            comp.RequestedItemCount?.Max ?? 0));
                }
                _compSnapshots[i] = compList;

                // Exploration tiers
                var exploList = new List<ExplorationTierSnapshot>();
                if (rq.QuestConfig?.ExplorationConfig != null)
                {
                    foreach (var explo in rq.QuestConfig.ExplorationConfig)
                        exploList.Add(new ExplorationTierSnapshot(
                            explo.MinimumExtracts, explo.MaximumExtracts,
                            explo.MinimumExtractsWithSpecificExit, explo.MaximumExtractsWithSpecificExit));
                }
                _exploSnapshots[i] = exploList;

                // Reward scaling (deep copy each list)
                var rs = rq.RewardScaling;
                _rewardSnapshots[i] = new RewardScalingSnapshot(
                    rs?.Levels?.ToList() ?? [],
                    rs?.Experience?.ToList() ?? [],
                    rs?.Roubles?.ToList() ?? [],
                    rs?.GpCoins?.ToList() ?? [],
                    rs?.Items?.ToList() ?? [],
                    rs?.Reputation?.ToList() ?? []);
            }

            _snapshotTaken = true;
            logger.Info("[ZSlayerHQ] Repeatable quest snapshots taken");
        }
        catch (Exception ex)
        {
            logger.Error($"[ZSlayerHQ] Repeatable quest snapshot failed: {ex.Message}");
            _snapshotTaken = true;
        }
    }

    // ═══════════════════════════════════════════════════════
    // APPLY ALL
    // ═══════════════════════════════════════════════════════

    public void ApplyAll()
    {
        lock (_lock)
        {
            TakeSnapshot();
            var config = configService.GetConfig().RepeatableQuests;
            ApplyConfig(config);
        }
    }

    private void ApplyConfig(RepeatableQuestEditorConfig c)
    {
        if (!c.EnableRepeatableQuests) return;
        if (c.Types == null || c.Types.Count == 0) return;

        try
        {
            var questConfig = configServer.GetConfig<QuestConfig>();
            var repeatables = questConfig.RepeatableQuests;
            if (repeatables == null) return;

            foreach (var (typeIndex, typeOverride) in c.Types)
            {
                if (typeIndex < 0 || typeIndex >= repeatables.Count) continue;
                var rq = repeatables[typeIndex];

                // ── Step 1: Restore all snapshots for this type ──
                RestoreType(rq, typeIndex);

                // ── Step 2: Apply basic settings ──
                if (typeOverride.NumQuests.HasValue)
                    rq.NumQuests = typeOverride.NumQuests.Value;

                if (typeOverride.ResetTimeSec.HasValue)
                    rq.ResetTime = typeOverride.ResetTimeSec.Value;

                if (typeOverride.MinPlayerLevel.HasValue)
                    rq.MinPlayerLevel = typeOverride.MinPlayerLevel.Value;

                // ── Step 3: Elimination tiers ──
                if (typeOverride.EliminationTiers != null && rq.QuestConfig?.Elimination != null)
                {
                    foreach (var tier in typeOverride.EliminationTiers)
                    {
                        if (tier.TierIndex < 0 || tier.TierIndex >= rq.QuestConfig.Elimination.Count) continue;
                        var elim = rq.QuestConfig.Elimination[tier.TierIndex];
                        if (tier.KillCountMin.HasValue) elim.MinKills = tier.KillCountMin.Value;
                        if (tier.KillCountMax.HasValue) elim.MaxKills = tier.KillCountMax.Value;
                    }
                }

                // ── Step 4: Completion tiers ──
                if (typeOverride.CompletionTiers != null && rq.QuestConfig?.CompletionConfig != null)
                {
                    foreach (var tier in typeOverride.CompletionTiers)
                    {
                        if (tier.TierIndex < 0 || tier.TierIndex >= rq.QuestConfig.CompletionConfig.Count) continue;
                        var comp = rq.QuestConfig.CompletionConfig[tier.TierIndex];
                        if (comp.RequestedItemCount != null)
                        {
                            if (tier.ItemCountMin.HasValue) comp.RequestedItemCount.Min = tier.ItemCountMin.Value;
                            if (tier.ItemCountMax.HasValue) comp.RequestedItemCount.Max = tier.ItemCountMax.Value;
                        }
                    }
                }

                // ── Step 5: Exploration tiers ──
                if (typeOverride.ExplorationTiers != null && rq.QuestConfig?.ExplorationConfig != null)
                {
                    foreach (var tier in typeOverride.ExplorationTiers)
                    {
                        if (tier.TierIndex < 0 || tier.TierIndex >= rq.QuestConfig.ExplorationConfig.Count) continue;
                        var explo = rq.QuestConfig.ExplorationConfig[tier.TierIndex];
                        if (tier.ExtractMin.HasValue) explo.MinimumExtracts = tier.ExtractMin.Value;
                        if (tier.ExtractMax.HasValue) explo.MaximumExtracts = tier.ExtractMax.Value;
                        if (tier.SpecificExtractMin.HasValue) explo.MinimumExtractsWithSpecificExit = tier.SpecificExtractMin.Value;
                        if (tier.SpecificExtractMax.HasValue) explo.MaximumExtractsWithSpecificExit = tier.SpecificExtractMax.Value;
                    }
                }

                // ── Step 6: Reward scaling ──
                if (typeOverride.RewardScaling != null && rq.RewardScaling != null)
                {
                    var rs = typeOverride.RewardScaling;
                    if (rs.Experience != null) rq.RewardScaling.Experience = new List<double>(rs.Experience);
                    if (rs.Roubles != null) rq.RewardScaling.Roubles = new List<double>(rs.Roubles);
                    if (rs.GpCoins != null) rq.RewardScaling.GpCoins = new List<double>(rs.GpCoins);
                    if (rs.Items != null) rq.RewardScaling.Items = new List<double>(rs.Items);
                    if (rs.Reputation != null) rq.RewardScaling.Reputation = new List<double>(rs.Reputation);
                }
            }

            logger.Info("[ZSlayerHQ] Repeatable quest settings applied");
        }
        catch (Exception ex)
        {
            logger.Error($"[ZSlayerHQ] Repeatable quest apply failed: {ex.Message}");
        }
    }

    private void RestoreType(RepeatableQuestConfig rq, int typeIndex)
    {
        // Basic settings
        if (_typeSnapshots.TryGetValue(typeIndex, out var ts))
        {
            rq.NumQuests = ts.NumQuests;
            rq.ResetTime = ts.ResetTime;
            rq.MinPlayerLevel = ts.MinPlayerLevel;
        }

        // Elimination tiers
        if (_elimSnapshots.TryGetValue(typeIndex, out var elimSnaps) && rq.QuestConfig?.Elimination != null)
        {
            for (var i = 0; i < elimSnaps.Count && i < rq.QuestConfig.Elimination.Count; i++)
            {
                rq.QuestConfig.Elimination[i].MinKills = elimSnaps[i].MinKills;
                rq.QuestConfig.Elimination[i].MaxKills = elimSnaps[i].MaxKills;
            }
        }

        // Completion tiers
        if (_compSnapshots.TryGetValue(typeIndex, out var compSnaps) && rq.QuestConfig?.CompletionConfig != null)
        {
            for (var i = 0; i < compSnaps.Count && i < rq.QuestConfig.CompletionConfig.Count; i++)
            {
                if (rq.QuestConfig.CompletionConfig[i].RequestedItemCount != null)
                {
                    rq.QuestConfig.CompletionConfig[i].RequestedItemCount.Min = compSnaps[i].ItemCountMin;
                    rq.QuestConfig.CompletionConfig[i].RequestedItemCount.Max = compSnaps[i].ItemCountMax;
                }
            }
        }

        // Exploration tiers
        if (_exploSnapshots.TryGetValue(typeIndex, out var exploSnaps) && rq.QuestConfig?.ExplorationConfig != null)
        {
            for (var i = 0; i < exploSnaps.Count && i < rq.QuestConfig.ExplorationConfig.Count; i++)
            {
                rq.QuestConfig.ExplorationConfig[i].MinimumExtracts = exploSnaps[i].MinExtracts;
                rq.QuestConfig.ExplorationConfig[i].MaximumExtracts = exploSnaps[i].MaxExtracts;
                rq.QuestConfig.ExplorationConfig[i].MinimumExtractsWithSpecificExit = exploSnaps[i].MinSpecificExtracts;
                rq.QuestConfig.ExplorationConfig[i].MaximumExtractsWithSpecificExit = exploSnaps[i].MaxSpecificExtracts;
            }
        }

        // Reward scaling (deep copy from snapshot)
        if (_rewardSnapshots.TryGetValue(typeIndex, out var rwSnap) && rq.RewardScaling != null)
        {
            rq.RewardScaling.Levels = new List<double>(rwSnap.Levels);
            rq.RewardScaling.Experience = new List<double>(rwSnap.Experience);
            rq.RewardScaling.Roubles = new List<double>(rwSnap.Roubles);
            rq.RewardScaling.GpCoins = new List<double>(rwSnap.GpCoins);
            rq.RewardScaling.Items = new List<double>(rwSnap.Items);
            rq.RewardScaling.Reputation = new List<double>(rwSnap.Reputation);
        }
    }

    // ═══════════════════════════════════════════════════════
    // GET CONFIG (for frontend)
    // ═══════════════════════════════════════════════════════

    public RepeatableQuestConfigResponse GetConfig()
    {
        lock (_lock)
        {
            TakeSnapshot();
            var config = configService.GetConfig().RepeatableQuests;

            var defaults = new RepeatableQuestDefaults();

            for (var i = 0; i < 3; i++)
            {
                if (!_typeSnapshots.TryGetValue(i, out var ts)) continue;

                var typeDef = new RepeatableTypeDefaults
                {
                    Index = i,
                    Name = i < TypeNames.Length ? TypeNames[i] : $"Type {i}",
                    NumQuests = ts.NumQuests,
                    ResetTimeSec = ts.ResetTime,
                    MinPlayerLevel = ts.MinPlayerLevel
                };

                // Elimination tier defaults
                if (_elimSnapshots.TryGetValue(i, out var elimSnaps))
                {
                    var questConfig = configServer.GetConfig<QuestConfig>();
                    var rq = questConfig.RepeatableQuests[i];
                    for (var t = 0; t < elimSnaps.Count; t++)
                    {
                        var tierDef = new EliminationTierDefaults
                        {
                            TierIndex = t,
                            KillCountMin = elimSnaps[t].MinKills,
                            KillCountMax = elimSnaps[t].MaxKills
                        };
                        // Get level range from SPT data
                        if (rq.QuestConfig?.Elimination != null && t < rq.QuestConfig.Elimination.Count)
                        {
                            var lr = rq.QuestConfig.Elimination[t].LevelRange;
                            if (lr != null) { tierDef.LevelMin = lr.Min; tierDef.LevelMax = lr.Max; }
                        }
                        typeDef.EliminationTiers.Add(tierDef);
                    }
                }

                // Completion tier defaults
                if (_compSnapshots.TryGetValue(i, out var compSnaps))
                {
                    var questConfig = configServer.GetConfig<QuestConfig>();
                    var rq = questConfig.RepeatableQuests[i];
                    for (var t = 0; t < compSnaps.Count; t++)
                    {
                        var tierDef = new CompletionTierDefaults
                        {
                            TierIndex = t,
                            ItemCountMin = compSnaps[t].ItemCountMin,
                            ItemCountMax = compSnaps[t].ItemCountMax
                        };
                        if (rq.QuestConfig?.CompletionConfig != null && t < rq.QuestConfig.CompletionConfig.Count)
                        {
                            var lr = rq.QuestConfig.CompletionConfig[t].LevelRange;
                            if (lr != null) { tierDef.LevelMin = lr.Min; tierDef.LevelMax = lr.Max; }
                        }
                        typeDef.CompletionTiers.Add(tierDef);
                    }
                }

                // Exploration tier defaults
                if (_exploSnapshots.TryGetValue(i, out var exploSnaps))
                {
                    var questConfig = configServer.GetConfig<QuestConfig>();
                    var rq = questConfig.RepeatableQuests[i];
                    for (var t = 0; t < exploSnaps.Count; t++)
                    {
                        var tierDef = new ExplorationTierDefaults
                        {
                            TierIndex = t,
                            ExtractMin = exploSnaps[t].MinExtracts,
                            ExtractMax = exploSnaps[t].MaxExtracts,
                            SpecificExtractMin = exploSnaps[t].MinSpecificExtracts,
                            SpecificExtractMax = exploSnaps[t].MaxSpecificExtracts
                        };
                        if (rq.QuestConfig?.ExplorationConfig != null && t < rq.QuestConfig.ExplorationConfig.Count)
                        {
                            var lr = rq.QuestConfig.ExplorationConfig[t].LevelRange;
                            if (lr != null) { tierDef.LevelMin = lr.Min; tierDef.LevelMax = lr.Max; }
                        }
                        typeDef.ExplorationTiers.Add(tierDef);
                    }
                }

                // Reward scaling defaults
                if (_rewardSnapshots.TryGetValue(i, out var rwSnap))
                {
                    typeDef.RewardLevels = new List<double>(rwSnap.Levels);
                    typeDef.RewardExperience = new List<double>(rwSnap.Experience);
                    typeDef.RewardRoubles = new List<double>(rwSnap.Roubles);
                    typeDef.RewardGpCoins = new List<double>(rwSnap.GpCoins);
                    typeDef.RewardItems = new List<double>(rwSnap.Items);
                    typeDef.RewardReputation = new List<double>(rwSnap.Reputation);
                }

                defaults.Types.Add(typeDef);
            }

            return new RepeatableQuestConfigResponse { Config = config, Defaults = defaults };
        }
    }

    // ═══════════════════════════════════════════════════════
    // APPLY FROM REQUEST
    // ═══════════════════════════════════════════════════════

    public void Apply(RepeatableQuestEditorConfig incoming)
    {
        lock (_lock)
        {
            var ccConfig = configService.GetConfig();
            ccConfig.RepeatableQuests = incoming;
            configService.SaveConfig();
            ApplyConfig(incoming);
        }
    }

    // ═══════════════════════════════════════════════════════
    // RESET
    // ═══════════════════════════════════════════════════════

    public void Reset()
    {
        lock (_lock)
        {
            var ccConfig = configService.GetConfig();
            ccConfig.RepeatableQuests = new RepeatableQuestEditorConfig();
            configService.SaveConfig();
            ApplyConfig(ccConfig.RepeatableQuests);
        }
    }
}
